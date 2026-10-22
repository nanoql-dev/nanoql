// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/volatility/sviinterpolation.hpp: SviInterpolation and the Svi factory, which no wheel class exposes,
// and the section the interpolation prices with, ql/experimental/volatility/svismilesection.hpp's SviSmileSection,
// built fresh at given parameters.
//
// A fit is {"strikes", "volatilities", "expiry", "forward", "a", "b", "sigma", "rho", "m" (each null for Null<Real>(),
// the default), "aIsFixed", "bIsFixed", "sigmaIsFixed", "rhoIsFixed", "mIsFixed", "vegaWeighted", "endCriteria"
// (null for the default, or math/optimization/specs.hpp's end criteria), "optMethod" (null for the default,
// {"type": "LevenbergMarquardt", "epsfcn", "xtol", "gtol"} or {"type": "Simplex", "lambda"}), "errorAccept",
// "useMaxError", "maxGuesses"}.
//
//     SviInterpolation::SviInterpolation     (fit, points)   the interpolation after its update(): {"expiry",
//                                                            "forward", "a", "b", "sigma", "rho", "m", "rmsError",
//                                                            "maxError", "endCriteria", "interpolationWeights",
//                                                            "values"}, the values at the points, extrapolating
//     Svi::interpolate                       (fit, points)   the factory's interpolation, its update() run, through
//                                                            its values at the points; the fit's vegaWeighted and
//                                                            the rest given to the factory
//     Svi::global                            ()              the factory's trait
//     SviSmileSection::volatility            (expiry, forward, parameters, strikes)
//                                                            a fresh section's volatilities: the values a fit's
//                                                            reported parameters give
//
// The fit's own checks are QuantLib's; the harness refuses only sizes it would read past (fewer volatilities than
// strikes, not five parameters).

#include "math/optimization/specs.hpp"

#include <ql/experimental/volatility/sviinterpolation.hpp>
#include <ql/experimental/volatility/svismilesection.hpp>
#include <ql/math/optimization/levenbergmarquardt.hpp>
#include <ql/math/optimization/simplex.hpp>
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
            const std::string what = "an SVI fit spec";
            if (!v.is_object())
                throw InvalidParams("expected " + what + ", got " + json::serialize(v));
            const json::object& o = v.get_object();
            static const std::vector<const char*> keys{
                "strikes", "volatilities", "expiry", "forward", "a", "b", "sigma", "rho", "m", "aIsFixed",
                "bIsFixed", "sigmaIsFixed", "rhoIsFixed", "mIsFixed", "vegaWeighted", "endCriteria", "optMethod",
                "errorAccept", "useMaxError", "maxGuesses"};
            if (o.size() != keys.size())
                throw InvalidParams(what + " takes exactly its " + std::to_string(keys.size()) + " keys");
            FitSpec s;
            s.strikes = reals(key(o, "strikes", what));
            s.volatilities = reals(key(o, "volatilities", what));
            if (s.volatilities.size() < s.strikes.size())
                throw InvalidParams("a fit reads one volatility per strike");
            s.expiry = real(key(o, "expiry", what));
            s.forward = real(key(o, "forward", what));
            for (const char* p : {"a", "b", "sigma", "rho", "m"})
                s.parameters.push_back(real(key(o, p, what)));
            for (const char* f : {"aIsFixed", "bIsFixed", "sigmaIsFixed", "rhoIsFixed", "mIsFixed"})
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

        json::value fit(const FitSpec& s, const std::vector<Real>& points) {
            SviInterpolation f(s.strikes.begin(), s.strikes.end(), s.volatilities.begin(), s.expiry, s.forward,
                               s.parameters[0], s.parameters[1], s.parameters[2], s.parameters[3], s.parameters[4],
                               s.fixed[0], s.fixed[1], s.fixed[2], s.fixed[3], s.fixed[4], s.vegaWeighted,
                               makeEnd(s), makeMethod(s.method), s.errorAccept, s.useMaxError, s.maxGuesses);
            f.update();
            json::array values;
            for (Real x : points)
                values.emplace_back(f(x, true));
            return json::value(json::object{{"expiry", f.expiry()},
                                            {"forward", f.forward()},
                                            {"a", f.a()},
                                            {"b", f.b()},
                                            {"sigma", f.sigma()},
                                            {"rho", f.rho()},
                                            {"m", f.m()},
                                            {"rmsError", f.rmsError()},
                                            {"maxError", f.maxError()},
                                            {"endCriteria", static_cast<std::int64_t>(f.endCriteria())},
                                            {"interpolationWeights", realArray(f.interpolationWeights())},
                                            {"values", std::move(values)}});
        }

        json::value factory(const FitSpec& s, const std::vector<Real>& points) {
            const Svi z(s.expiry, s.forward, s.parameters[0], s.parameters[1], s.parameters[2], s.parameters[3],
                        s.parameters[4], s.fixed[0], s.fixed[1], s.fixed[2], s.fixed[3], s.fixed[4], s.vegaWeighted,
                        makeEnd(s), makeMethod(s.method), s.errorAccept, s.useMaxError, s.maxGuesses);
            Interpolation f = z.interpolate(s.strikes.begin(), s.strikes.end(), s.volatilities.begin());
            f.update();
            json::array values;
            for (Real x : points)
                values.emplace_back(f(x, true));
            return json::value(std::move(values));
        }

        const Register unit({
            {"SviInterpolation::SviInterpolation",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 FitSpec s = fitSpec(a[0]);
                 std::vector<Real> points = reals(a[1]);
                 return [=] { return fit(s, points); };
             }},
            {"Svi::interpolate",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 FitSpec s = fitSpec(a[0]);
                 std::vector<Real> points = reals(a[1]);
                 return [=] { return factory(s, points); };
             }},
            {"Svi::global",
             [](const Args& a) -> Call {
                 arity(a, 0);
                 return [] { return json::value(Svi::global); };
             }},
            {"SviSmileSection::volatility",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Real expiry = real(a[0]), forward = real(a[1]);
                 std::vector<Real> parameters = reals(a[2]), strikes = reals(a[3]);
                 if (parameters.size() != 5)
                     throw InvalidParams("an SVI section reads five parameters");
                 return [=] {
                     SviSmileSection z(expiry, forward, parameters);
                     json::array out;
                     for (Real k : strikes)
                         out.emplace_back(z.volatility(k));
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
