// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/interpolations/sabrinterpolation.hpp: the members of SABRInterpolation the wheel's shim doesn't bind
// (expiry, forward, endCriteria, interpolationWeights, rmsError and maxError, and the primitive and derivatives XABR
// refuses), and the SABR factory, which the wheel doesn't bind at all. Each call builds the interpolation and runs its
// update(), the fit QuantLib's callers run at once, over copies of the strikes, volatilities and forward that outlive
// it, as QuantLib requires.
//
// An interpolation is {"x", "y", "t", "forward", "alpha", "beta", "nu", "rho" (null for Null<Real>()),
// "alphaIsFixed", "betaIsFixed", "nuIsFixed", "rhoIsFixed", "vegaWeighted", "endCriteria" (null for QuantLib's
// default, else math/optimization/specs.hpp's end criteria), "method" (null for QuantLib's default, else {"type":
// "Simplex", "lambda"} or {"type": "LevenbergMarquardt", "epsfcn", "xtol", "gtol", "useCostFunctionsJacobian"}),
// "errorAccept", "useMaxError", "maxGuesses", "shift", "volatilityType"}, tools/vectors/gen/math/interpolations/
// sabrinterpolation.py's inputs. A factory is the same without "x", "y" and "volatilityType", which SABR doesn't take.
//
//     SABRInterpolation::<member>          (interpolation)        expiry, forward, endCriteria (EndCriteria::Type's
//                                                                  integer), interpolationWeights, rmsError, maxError
//     SABRInterpolation::<derivative>      (interpolation, x)     primitive, derivative, secondDerivative
//     SABR::interpolate                    (factory, x, y, points) the values at the points, allowExtrapolation set
//     SABR::global                         ()

#include "math/optimization/specs.hpp"

#include <ql/math/interpolations/sabrinterpolation.hpp>
#include <ql/math/optimization/levenbergmarquardt.hpp>
#include <ql/math/optimization/simplex.hpp>

#include <memory>
#include <optional>
#include <set>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using namespace optimization;

        struct MethodSpec {
            std::string type;  // Simplex or LevenbergMarquardt
            Real lambda = 0.0, epsfcn = 0.0, xtol = 0.0, gtol = 0.0;
            bool useCostFunctionsJacobian = false;
        };

        struct SabrSpec {
            bool factory = false;
            std::vector<Real> x, y;
            Real t = 0.0, forward = 0.0, alpha = 0.0, beta = 0.0, nu = 0.0, rho = 0.0;
            bool alphaIsFixed = false, betaIsFixed = false, nuIsFixed = false, rhoIsFixed = false;
            bool vegaWeighted = false;
            std::optional<EndCriteriaSpec> endCriteria;
            std::optional<MethodSpec> method;
            Real errorAccept = 0.0;
            bool useMaxError = false;
            Size maxGuesses = 0;
            Real shift = 0.0;
            VolatilityType volatilityType = ShiftedLognormal;
        };

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " needs " + name);
            return *v;
        }

        void onlyKeys(const json::object& o, const std::set<std::string>& keys, const std::string& what) {
            for (const auto& kv : o) {
                if (keys.count(std::string(kv.key())) == 0)
                    throw InvalidParams(what + " takes no key " + std::string(kv.key()));
            }
        }

        MethodSpec methodSpec(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected an optimizer spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            MethodSpec m;
            m.type = text(key(o, "type", "an optimizer"));
            if (m.type == "Simplex") {
                onlyKeys(o, {"type", "lambda"}, m.type);
                m.lambda = real(key(o, "lambda", m.type));
            } else if (m.type == "LevenbergMarquardt") {
                onlyKeys(o, {"type", "epsfcn", "xtol", "gtol", "useCostFunctionsJacobian"}, m.type);
                m.epsfcn = real(key(o, "epsfcn", m.type));
                m.xtol = real(key(o, "xtol", m.type));
                m.gtol = real(key(o, "gtol", m.type));
                m.useCostFunctionsJacobian = boolean(key(o, "useCostFunctionsJacobian", m.type));
            } else {
                throw InvalidParams("unknown optimizer " + m.type);
            }
            return m;
        }

        SabrSpec sabrSpec(const json::value& v, bool factory) {
            const std::string what = factory ? "a SABR factory" : "a SABRInterpolation";
            if (!v.is_object())
                throw InvalidParams("expected " + what + " spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            std::set<std::string> keys{"t", "forward", "alpha", "beta", "nu", "rho", "alphaIsFixed", "betaIsFixed",
                                       "nuIsFixed", "rhoIsFixed", "vegaWeighted", "endCriteria", "method",
                                       "errorAccept", "useMaxError", "maxGuesses", "shift"};
            if (!factory)
                keys.insert({"x", "y", "volatilityType"});
            onlyKeys(o, keys, what);
            SabrSpec s;
            s.factory = factory;
            if (!factory) {
                s.x = reals(key(o, "x", what));
                s.y = reals(key(o, "y", what));
                // QuantLib reads one volatility per strike.
                if (s.y.size() != s.x.size())
                    throw InvalidParams(what + " needs one volatility per strike");
                s.volatilityType = enumeration<VolatilityType>(key(o, "volatilityType", what));
            }
            s.t = real(key(o, "t", what));
            s.forward = real(key(o, "forward", what));
            s.alpha = real(key(o, "alpha", what));
            s.beta = real(key(o, "beta", what));
            s.nu = real(key(o, "nu", what));
            s.rho = real(key(o, "rho", what));
            s.alphaIsFixed = boolean(key(o, "alphaIsFixed", what));
            s.betaIsFixed = boolean(key(o, "betaIsFixed", what));
            s.nuIsFixed = boolean(key(o, "nuIsFixed", what));
            s.rhoIsFixed = boolean(key(o, "rhoIsFixed", what));
            s.vegaWeighted = boolean(key(o, "vegaWeighted", what));
            const json::value& ec = key(o, "endCriteria", what);
            if (!ec.is_null())
                s.endCriteria = optimization::endCriteria(ec);
            const json::value& m = key(o, "method", what);
            if (!m.is_null())
                s.method = methodSpec(m);
            s.errorAccept = real(key(o, "errorAccept", what));
            s.useMaxError = boolean(key(o, "useMaxError", what));
            s.maxGuesses = size(key(o, "maxGuesses", what));
            s.shift = real(key(o, "shift", what));
            return s;
        }

        ext::shared_ptr<EndCriteria> makeEnd(const SabrSpec& s) {
            return s.endCriteria ? ext::make_shared<EndCriteria>(makeEndCriteria(*s.endCriteria))
                                 : ext::shared_ptr<EndCriteria>();
        }

        ext::shared_ptr<OptimizationMethod> makeMethod(const SabrSpec& s) {
            if (!s.method)
                return {};
            const MethodSpec& m = *s.method;
            if (m.type == "Simplex")
                return ext::make_shared<Simplex>(m.lambda);
            return ext::make_shared<LevenbergMarquardt>(m.epsfcn, m.xtol, m.gtol, m.useCostFunctionsJacobian);
        }

        // The interpolation with its data, which it refers to: built, then updated.
        struct Fitted {
            std::vector<Real> x, y;
            Real forward;
            std::unique_ptr<SABRInterpolation> f;
        };

        std::unique_ptr<Fitted> fit(const SabrSpec& s) {
            auto out = std::make_unique<Fitted>();
            out->x = s.x;
            out->y = s.y;
            out->forward = s.forward;
            out->f = std::make_unique<SABRInterpolation>(
                out->x.begin(), out->x.end(), out->y.begin(), s.t, out->forward, s.alpha, s.beta, s.nu, s.rho,
                s.alphaIsFixed, s.betaIsFixed, s.nuIsFixed, s.rhoIsFixed, s.vegaWeighted, makeEnd(s), makeMethod(s),
                s.errorAccept, s.useMaxError, s.maxGuesses, s.shift, s.volatilityType);
            out->f->update();
            return out;
        }

        // A member of the fitted interpolation: (interpolation).
        Call whole(const Args& a, json::value (*f)(SABRInterpolation&)) {
            arity(a, 1);
            SabrSpec s = sabrSpec(a[0], false);
            return [=] { return f(*fit(s)->f); };
        }

        // A derivative at a point: (interpolation, x), with allowExtrapolation set.
        Call atPoint(const Args& a, Real (*f)(const SABRInterpolation&, Real)) {
            arity(a, 2);
            SabrSpec s = sabrSpec(a[0], false);
            Real x = real(a[1]);
            return [=] { return json::value(f(*fit(s)->f, x)); };
        }

        const Register unit({
            {"SABRInterpolation::expiry",
             [](const Args& a) { return whole(a, [](SABRInterpolation& f) { return json::value(f.expiry()); }); }},
            {"SABRInterpolation::forward",
             [](const Args& a) { return whole(a, [](SABRInterpolation& f) { return json::value(f.forward()); }); }},
            {"SABRInterpolation::endCriteria",
             [](const Args& a) {
                 return whole(a, [](SABRInterpolation& f) {
                     return json::value(static_cast<int>(f.endCriteria()));
                 });
             }},
            {"SABRInterpolation::interpolationWeights",
             [](const Args& a) {
                 return whole(a, [](SABRInterpolation& f) {
                     json::array out;
                     for (Real w : f.interpolationWeights())
                         out.emplace_back(w);
                     return json::value(std::move(out));
                 });
             }},
            {"SABRInterpolation::rmsError",
             [](const Args& a) { return whole(a, [](SABRInterpolation& f) { return json::value(f.rmsError()); }); }},
            {"SABRInterpolation::maxError",
             [](const Args& a) { return whole(a, [](SABRInterpolation& f) { return json::value(f.maxError()); }); }},
            {"SABRInterpolation::primitive",
             [](const Args& a) {
                 return atPoint(a, [](const SABRInterpolation& f, Real x) { return f.primitive(x, true); });
             }},
            {"SABRInterpolation::derivative",
             [](const Args& a) {
                 return atPoint(a, [](const SABRInterpolation& f, Real x) { return f.derivative(x, true); });
             }},
            {"SABRInterpolation::secondDerivative",
             [](const Args& a) {
                 return atPoint(a, [](const SABRInterpolation& f, Real x) { return f.secondDerivative(x, true); });
             }},
            // (factory, x, y, points)
            {"SABR::interpolate",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 SabrSpec s = sabrSpec(a[0], true);
                 std::vector<Real> x = reals(a[1]), y = reals(a[2]), points = reals(a[3]);
                 // QuantLib reads one volatility per strike.
                 if (y.size() != x.size())
                     throw InvalidParams("SABR::interpolate needs one volatility per strike");
                 return [=] {
                     std::vector<Real> xs = x, ys = y;
                     SABR factory(s.t, s.forward, s.alpha, s.beta, s.nu, s.rho, s.alphaIsFixed, s.betaIsFixed,
                                  s.nuIsFixed, s.rhoIsFixed, s.vegaWeighted, makeEnd(s), makeMethod(s),
                                  s.errorAccept, s.useMaxError, s.maxGuesses, s.shift);
                     Interpolation f = factory.interpolate(xs.begin(), xs.end(), ys.begin());
                     f.update();
                     json::array out;
                     for (Real p : points)
                         out.emplace_back(f(p, true));
                     return json::value(std::move(out));
                 };
             }},
            {"SABR::global",
             [](const Args& a) -> Call {
                 arity(a, 0);
                 return [] { return json::value(SABR::global); };
             }},
        });

    }

}
