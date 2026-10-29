// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/volatility/noarbsabrinterpolation.hpp, which the wheel doesn't expose (its interpolated section is
// the wheel's file). "NoArbSabrInterpolation::values" takes (interpolation, points): the interpolation built and
// updated, the fit QuantLib's callers run at once, over copies of the strikes, volatilities and forward that outlive it;
// it returns {"expiry", "forward", "alpha", "beta", "nu", "rho", "rmsError", "maxError", "endCriteria",
// "interpolationWeights", "values"}, the values at the points with allowExtrapolation set. "NoArbSabr::interpolate"
// takes (interpolation, points) and returns the factory's interpolation's values at the points, updated.
//
// An interpolation is {"x", "y", "t", "forward", "alpha", "beta", "nu", "rho" (null for Null<Real>()), "alphaIsFixed",
// "betaIsFixed", "nuIsFixed", "rhoIsFixed", "vegaWeighted", "errorAccept", "useMaxError", "maxGuesses"}, with QuantLib's
// default end criteria and optimizer.

#include "convert.hpp"

#include <ql/experimental/volatility/noarbsabrinterpolation.hpp>

#include <memory>
#include <set>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct Spec {
            std::vector<Real> x, y;
            Real t = 0.0, forward = 0.0, alpha = 0.0, beta = 0.0, nu = 0.0, rho = 0.0;
            bool alphaIsFixed = false, betaIsFixed = false, nuIsFixed = false, rhoIsFixed = false;
            bool vegaWeighted = false;
            Real errorAccept = 0.0;
            bool useMaxError = false;
            Integer maxGuesses = 0;
        };

        const json::value& key(const json::object& o, const char* name) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string("a NoArbSabrInterpolation needs ") + name);
            return *v;
        }

        Spec spec(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a NoArbSabrInterpolation spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            const std::set<std::string> keys{"x", "y", "t", "forward", "alpha", "beta", "nu", "rho", "alphaIsFixed",
                                             "betaIsFixed", "nuIsFixed", "rhoIsFixed", "vegaWeighted", "errorAccept",
                                             "useMaxError", "maxGuesses"};
            for (const auto& kv : o) {
                if (keys.count(std::string(kv.key())) == 0)
                    throw InvalidParams("a NoArbSabrInterpolation takes no key " + std::string(kv.key()));
            }
            Spec s;
            s.x = reals(key(o, "x"));
            s.y = reals(key(o, "y"));
            // QuantLib reads one volatility per strike.
            if (s.y.size() != s.x.size())
                throw InvalidParams("a NoArbSabrInterpolation needs one volatility per strike");
            s.t = real(key(o, "t"));
            s.forward = real(key(o, "forward"));
            s.alpha = real(key(o, "alpha"));
            s.beta = real(key(o, "beta"));
            s.nu = real(key(o, "nu"));
            s.rho = real(key(o, "rho"));
            s.alphaIsFixed = boolean(key(o, "alphaIsFixed"));
            s.betaIsFixed = boolean(key(o, "betaIsFixed"));
            s.nuIsFixed = boolean(key(o, "nuIsFixed"));
            s.rhoIsFixed = boolean(key(o, "rhoIsFixed"));
            s.vegaWeighted = boolean(key(o, "vegaWeighted"));
            s.errorAccept = real(key(o, "errorAccept"));
            s.useMaxError = boolean(key(o, "useMaxError"));
            s.maxGuesses = integer(key(o, "maxGuesses"));
            if (s.maxGuesses < 0)
                throw InvalidParams("a NoArbSabrInterpolation's maxGuesses is a Size");
            return s;
        }

        const Register unit({
            {"NoArbSabrInterpolation::values",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 Spec s = spec(a[0]);
                 std::vector<Real> points = reals(a[1]);
                 return [=] {
                     std::vector<Real> x = s.x, y = s.y;
                     Real forward = s.forward;
                     NoArbSabrInterpolation f(x.begin(), x.end(), y.begin(), s.t, forward, s.alpha, s.beta, s.nu,
                                              s.rho, s.alphaIsFixed, s.betaIsFixed, s.nuIsFixed, s.rhoIsFixed,
                                              s.vegaWeighted, ext::shared_ptr<EndCriteria>(),
                                              ext::shared_ptr<OptimizationMethod>(), s.errorAccept, s.useMaxError,
                                              static_cast<Size>(s.maxGuesses));
                     f.update();
                     json::object out;
                     out["expiry"] = json::value(f.expiry());
                     out["forward"] = json::value(f.forward());
                     out["alpha"] = json::value(f.alpha());
                     out["beta"] = json::value(f.beta());
                     out["nu"] = json::value(f.nu());
                     out["rho"] = json::value(f.rho());
                     out["rmsError"] = json::value(f.rmsError());
                     out["maxError"] = json::value(f.maxError());
                     out["endCriteria"] = json::value(static_cast<int>(f.endCriteria()));
                     json::array weights, values;
                     for (Real w : f.interpolationWeights())
                         weights.emplace_back(w);
                     for (Real p : points)
                         values.emplace_back(f(p, true));
                     out["interpolationWeights"] = weights;
                     out["values"] = values;
                     return json::value(out);
                 };
             }},
            {"NoArbSabr::interpolate",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 Spec s = spec(a[0]);
                 std::vector<Real> points = reals(a[1]);
                 return [=] {
                     std::vector<Real> x = s.x, y = s.y;
                     NoArbSabr factory(s.t, s.forward, s.alpha, s.beta, s.nu, s.rho, s.alphaIsFixed, s.betaIsFixed,
                                       s.nuIsFixed, s.rhoIsFixed, s.vegaWeighted, ext::shared_ptr<EndCriteria>(),
                                       ext::shared_ptr<OptimizationMethod>(), s.errorAccept, s.useMaxError,
                                       static_cast<Size>(s.maxGuesses));
                     Interpolation f = factory.interpolate(x.begin(), x.end(), y.begin());
                     f.update();
                     json::array out;
                     for (Real p : points)
                         out.emplace_back(f(p, true));
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
