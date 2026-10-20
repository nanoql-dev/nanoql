// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/interpolations/zabrinterpolation.hpp: ZabrInterpolation and the Zabr factory, which no wheel class exposes,
// on the two short-maturity kernels, and the section the interpolation prices with,
// ql/termstructures/volatility/zabrsmilesection.hpp's ZabrSmileSection, built fresh at given parameters.
//
// A fit is {"kernel" ("ShortMaturityLognormal" or "ShortMaturityNormal"), "strikes", "volatilities", "expiry",
// "forward", "alpha", "beta", "nu", "rho", "gamma" (each null for Null<Real>(), the default), "alphaIsFixed",
// "betaIsFixed", "nuIsFixed", "rhoIsFixed", "gammaIsFixed", "vegaWeighted", "endCriteria" (null for the default, or
// math/optimization/specs.hpp's end criteria), "optMethod" (null for the default, {"type": "LevenbergMarquardt",
// "epsfcn", "xtol", "gtol"} or {"type": "Simplex", "lambda"}), "errorAccept", "useMaxError", "maxGuesses"}.
//
//     ZabrInterpolation::ZabrInterpolation   (fit, points)   the interpolation after its update(): {"expiry",
//                                                            "forward", "alpha", "beta", "nu", "rho", "gamma",
//                                                            "rmsError", "maxError", "endCriteria",
//                                                            "interpolationWeights", "values"}, the values at the
//                                                            points, extrapolating
//     Zabr::interpolate                      (fit, points)   the factory's interpolation, its update() run, through
//                                                            its values at the points; the fit's vegaWeighted and
//                                                            the rest given to the factory
//     Zabr::global                           ()              the factory's trait
//     ZabrSmileSection::volatility           (kernel, expiry, forward, parameters, strikes)
//                                                            a fresh section's volatilities: the values a fit's
//                                                            reported parameters give
//
// The fit's own checks are QuantLib's; the harness refuses only sizes it would read past (fewer volatilities than
// strikes, not five parameters).

#include "math/optimization/specs.hpp"

#include <ql/math/interpolations/zabrinterpolation.hpp>
#include <ql/math/optimization/levenbergmarquardt.hpp>
#include <ql/math/optimization/simplex.hpp>
#include <ql/termstructures/volatility/zabrsmilesection.hpp>
#include <ql/utilities/null.hpp>

#include <memory>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using namespace optimization;

        struct MethodSpec {
            std::string type;  // empty for the default
            Real epsfcn = 0.0, xtol = 0.0, gtol = 0.0, lambda = 0.0;
        };

        struct FitSpec {
            std::string kernel;
            std::vector<Real> strikes, volatilities;
            Real expiry, forward;
            std::vector<Real> parameters;  // Null<Real>() where not given
            std::vector<bool> fixed;
            bool vegaWeighted;
            std::optional<EndCriteriaSpec> endCriteria;
            MethodSpec method;
            Real errorAccept;
            bool useMaxError;
            Size maxGuesses;
        };

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " needs " + name);
            return *v;
        }

        std::string kernel(const json::value& v) {
            std::string k = text(v);
            if (k != "ShortMaturityLognormal" && k != "ShortMaturityNormal")
                throw InvalidParams("unknown ZABR kernel " + k);
            return k;
        }

        MethodSpec methodSpec(const json::value& v) {
            MethodSpec m;
            if (v.is_null())
                return m;
            const std::string what = "an optimization method spec";
            if (!v.is_object())
                throw InvalidParams("expected " + what + ", got " + json::serialize(v));
            const json::object& o = v.get_object();
            m.type = text(key(o, "type", what));
            if (m.type == "LevenbergMarquardt") {
                if (o.size() != 4)
                    throw InvalidParams("LevenbergMarquardt takes epsfcn, xtol and gtol");
                m.epsfcn = real(key(o, "epsfcn", what));
                m.xtol = real(key(o, "xtol", what));
                m.gtol = real(key(o, "gtol", what));
            } else if (m.type == "Simplex") {
                if (o.size() != 2)
                    throw InvalidParams("Simplex takes lambda");
                m.lambda = real(key(o, "lambda", what));
            } else {
                throw InvalidParams("unknown optimization method " + m.type);
            }
            return m;
        }

        ext::shared_ptr<OptimizationMethod> makeMethod(const MethodSpec& m) {
            if (m.type == "LevenbergMarquardt")
                return ext::make_shared<LevenbergMarquardt>(m.epsfcn, m.xtol, m.gtol);
            if (m.type == "Simplex")
                return ext::make_shared<Simplex>(m.lambda);
            return {};
        }

        FitSpec fitSpec(const json::value& v) {
            const std::string what = "a ZABR fit spec";
            if (!v.is_object())
                throw InvalidParams("expected " + what + ", got " + json::serialize(v));
            const json::object& o = v.get_object();
            static const std::vector<const char*> keys{
                "kernel", "strikes", "volatilities", "expiry", "forward", "alpha", "beta", "nu", "rho", "gamma",
                "alphaIsFixed", "betaIsFixed", "nuIsFixed", "rhoIsFixed", "gammaIsFixed", "vegaWeighted",
                "endCriteria", "optMethod", "errorAccept", "useMaxError", "maxGuesses"};
            if (o.size() != keys.size())
                throw InvalidParams(what + " takes exactly its " + std::to_string(keys.size()) + " keys");
            FitSpec s;
            s.kernel = kernel(key(o, "kernel", what));
            s.strikes = reals(key(o, "strikes", what));
            s.volatilities = reals(key(o, "volatilities", what));
            if (s.volatilities.size() < s.strikes.size())
                throw InvalidParams("a fit reads one volatility per strike");
            s.expiry = real(key(o, "expiry", what));
            s.forward = real(key(o, "forward", what));
            for (const char* p : {"alpha", "beta", "nu", "rho", "gamma"})
                s.parameters.push_back(real(key(o, p, what)));
            for (const char* f : {"alphaIsFixed", "betaIsFixed", "nuIsFixed", "rhoIsFixed", "gammaIsFixed"})
                s.fixed.push_back(boolean(key(o, f, what)));
            s.vegaWeighted = boolean(key(o, "vegaWeighted", what));
            const json::value& e = key(o, "endCriteria", what);
            if (!e.is_null())
                s.endCriteria = endCriteria(e);
            s.method = methodSpec(key(o, "optMethod", what));
            s.errorAccept = real(key(o, "errorAccept", what));
            s.useMaxError = boolean(key(o, "useMaxError", what));
            s.maxGuesses = size(key(o, "maxGuesses", what));
            return s;
        }

        ext::shared_ptr<EndCriteria> makeEnd(const FitSpec& s) {
            if (!s.endCriteria)
                return {};
            return ext::make_shared<EndCriteria>(makeEndCriteria(*s.endCriteria));
        }

        json::value realArray(const std::vector<Real>& v) {
            json::array out;
            for (Real x : v)
                out.emplace_back(x);
            return json::value(std::move(out));
        }

        template <class E>
        json::value fit(const FitSpec& s, const std::vector<Real>& points) {
            ZabrInterpolation<E> f(s.strikes.begin(), s.strikes.end(), s.volatilities.begin(), s.expiry, s.forward,
                                   s.parameters[0], s.parameters[1], s.parameters[2], s.parameters[3],
                                   s.parameters[4], s.fixed[0], s.fixed[1], s.fixed[2], s.fixed[3], s.fixed[4],
                                   s.vegaWeighted, makeEnd(s), makeMethod(s.method), s.errorAccept, s.useMaxError,
                                   s.maxGuesses);
            f.update();
            json::array values;
            for (Real x : points)
                values.emplace_back(f(x, true));
            return json::value(json::object{{"expiry", f.expiry()},
                                            {"forward", f.forward()},
                                            {"alpha", f.alpha()},
                                            {"beta", f.beta()},
                                            {"nu", f.nu()},
                                            {"rho", f.rho()},
                                            {"gamma", f.gamma()},
                                            {"rmsError", f.rmsError()},
                                            {"maxError", f.maxError()},
                                            {"endCriteria", static_cast<std::int64_t>(f.endCriteria())},
                                            {"interpolationWeights", realArray(f.interpolationWeights())},
                                            {"values", std::move(values)}});
        }

        template <class E>
        json::value factory(const FitSpec& s, const std::vector<Real>& points) {
            const Zabr<E> z(s.expiry, s.forward, s.parameters[0], s.parameters[1], s.parameters[2],
                            s.parameters[3], s.parameters[4], s.fixed[0], s.fixed[1], s.fixed[2], s.fixed[3],
                            s.fixed[4], s.vegaWeighted, makeEnd(s), makeMethod(s.method), s.errorAccept,
                            s.useMaxError, s.maxGuesses);
            Interpolation f = z.interpolate(s.strikes.begin(), s.strikes.end(), s.volatilities.begin());
            f.update();
            json::array values;
            for (Real x : points)
                values.emplace_back(f(x, true));
            return json::value(std::move(values));
        }

        template <class E>
        json::value section(Real expiry, Real forward, const std::vector<Real>& parameters,
                            const std::vector<Real>& strikes) {
            ZabrSmileSection<E> z(expiry, forward, parameters);
            json::array out;
            for (Real k : strikes)
                out.emplace_back(z.volatility(k));
            return json::value(std::move(out));
        }

        const Register unit({
            {"ZabrInterpolation::ZabrInterpolation",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 FitSpec s = fitSpec(a[0]);
                 std::vector<Real> points = reals(a[1]);
                 return [=] {
                     return s.kernel == "ShortMaturityLognormal" ? fit<ZabrShortMaturityLognormal>(s, points)
                                                                 : fit<ZabrShortMaturityNormal>(s, points);
                 };
             }},
            {"Zabr::interpolate",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 FitSpec s = fitSpec(a[0]);
                 std::vector<Real> points = reals(a[1]);
                 return [=] {
                     return s.kernel == "ShortMaturityLognormal" ? factory<ZabrShortMaturityLognormal>(s, points)
                                                                 : factory<ZabrShortMaturityNormal>(s, points);
                 };
             }},
            {"Zabr::global",
             [](const Args& a) -> Call {
                 arity(a, 0);
                 return [] {
                     return json::value(json::array{Zabr<ZabrShortMaturityLognormal>::global,
                                                    Zabr<ZabrShortMaturityNormal>::global});
                 };
             }},
            {"ZabrSmileSection::volatility",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 std::string k = kernel(a[0]);
                 Real expiry = real(a[1]), forward = real(a[2]);
                 std::vector<Real> parameters = reals(a[3]), strikes = reals(a[4]);
                 if (parameters.size() != 5)
                     throw InvalidParams("a ZABR section reads five parameters");
                 return [=] {
                     return k == "ShortMaturityLognormal"
                                ? section<ZabrShortMaturityLognormal>(expiry, forward, parameters, strikes)
                                : section<ZabrShortMaturityNormal>(expiry, forward, parameters, strikes);
                 };
             }},
        });

    }

}
