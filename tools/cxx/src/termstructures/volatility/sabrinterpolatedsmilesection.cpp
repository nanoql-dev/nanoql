// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/sabrinterpolatedsmilesection.hpp, .cpp: SabrInterpolatedSmileSection, which the wheel
// doesn't expose, built inside the call by either constructor, its reference date the evaluation date the case's
// settings set (QuantLib's section floats with it). A section is {"optionDate", "forward", "strikes",
// "hasFloatingStrikes", "atmVolatility", "vols", "alpha", "beta", "nu", "rho", "isAlphaFixed", "isBetaFixed",
// "isNuFixed", "isRhoFixed", "vegaWeighted", "dayCounter", "shift", "quotes"}: with "quotes" false the constructor
// on values, with it true the constructor on quotes, each volatility a SimpleQuote, a null volatility one holding
// Null<Real>(), which isn't valid, so that its strike is left out; a null parameter is Null<Real>(), SABR's default
// guess; the end criteria and the optimization method are SABR's defaults.
//
//     SabrInterpolatedSmileSection::alpha, beta, nu, rho, rmsError, maxError, endCriteria, minStrike, maxStrike,
//                                   atmLevel, exerciseTime                    (spec)
//     SabrInterpolatedSmileSection::volatility, variance                      (spec, strike)

#include "convert.hpp"

#include <ql/handle.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/sabrinterpolatedsmilesection.hpp>

#include <functional>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct SabrSpec {
            DateSpec optionDate{true, 0};
            Real forward = 0.0;
            std::vector<Real> strikes, vols;
            bool hasFloatingStrikes = false;
            Real atmVolatility = 0.0;
            Real alpha = 0.0, beta = 0.0, nu = 0.0, rho = 0.0;
            bool isAlphaFixed = false, isBetaFixed = false, isNuFixed = false, isRhoFixed = false;
            bool vegaWeighted = true;
            DayCounterSpec dayCounter;
            Real shift = 0.0;
            bool quotes = false;
        };

        SabrSpec sabrSpec(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a SABR-interpolated smile section spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            const char* keys[] = {"optionDate", "forward", "strikes", "hasFloatingStrikes", "atmVolatility", "vols",
                                  "alpha", "beta", "nu", "rho", "isAlphaFixed", "isBetaFixed", "isNuFixed",
                                  "isRhoFixed", "vegaWeighted", "dayCounter", "shift", "quotes"};
            for (const char* k : keys)
                if (!o.contains(k))
                    throw InvalidParams(std::string("a SABR-interpolated smile section spec needs ") + k);
            if (o.size() != std::size(keys))
                throw InvalidParams("a SABR-interpolated smile section spec takes only its 18 keys: " +
                                    json::serialize(v));
            SabrSpec s;
            s.optionDate = date(o.at("optionDate"));
            s.forward = real(o.at("forward"));
            s.strikes = reals(o.at("strikes"));
            s.hasFloatingStrikes = boolean(o.at("hasFloatingStrikes"));
            s.atmVolatility = real(o.at("atmVolatility"));
            s.vols = reals(o.at("vols"));
            s.alpha = real(o.at("alpha"));
            s.beta = real(o.at("beta"));
            s.nu = real(o.at("nu"));
            s.rho = real(o.at("rho"));
            s.isAlphaFixed = boolean(o.at("isAlphaFixed"));
            s.isBetaFixed = boolean(o.at("isBetaFixed"));
            s.isNuFixed = boolean(o.at("isNuFixed"));
            s.isRhoFixed = boolean(o.at("isRhoFixed"));
            s.vegaWeighted = boolean(o.at("vegaWeighted"));
            s.dayCounter = dayCounter(o.at("dayCounter"));
            s.shift = real(o.at("shift"));
            s.quotes = boolean(o.at("quotes"));
            // QuantLib reads one strike per volatility, past the strikes' end where there are fewer.
            if (s.strikes.size() < s.vols.size())
                throw InvalidParams("a SABR-interpolated smile section needs a strike per volatility");
            return s;
        }

        ext::shared_ptr<SabrInterpolatedSmileSection> make(const SabrSpec& s) {
            if (!s.quotes)
                return ext::make_shared<SabrInterpolatedSmileSection>(
                    makeDate(s.optionDate), s.forward, s.strikes, s.hasFloatingStrikes, s.atmVolatility, s.vols,
                    s.alpha, s.beta, s.nu, s.rho, s.isAlphaFixed, s.isBetaFixed, s.isNuFixed, s.isRhoFixed,
                    s.vegaWeighted, ext::shared_ptr<EndCriteria>(), ext::shared_ptr<OptimizationMethod>(),
                    makeDayCounter(s.dayCounter), s.shift);
            std::vector<Handle<Quote>> vols;
            for (Real x : s.vols)
                vols.emplace_back(ext::make_shared<SimpleQuote>(x));
            return ext::make_shared<SabrInterpolatedSmileSection>(
                makeDate(s.optionDate), Handle<Quote>(ext::make_shared<SimpleQuote>(s.forward)), s.strikes,
                s.hasFloatingStrikes, Handle<Quote>(ext::make_shared<SimpleQuote>(s.atmVolatility)), vols, s.alpha,
                s.beta, s.nu, s.rho, s.isAlphaFixed, s.isBetaFixed, s.isNuFixed, s.isRhoFixed, s.vegaWeighted,
                ext::shared_ptr<EndCriteria>(), ext::shared_ptr<OptimizationMethod>(), makeDayCounter(s.dayCounter),
                s.shift);
        }

        Method whole(std::function<json::value(const SabrInterpolatedSmileSection&)> f) {
            return [f](const Args& a) -> Call {
                arity(a, 1);
                SabrSpec s = sabrSpec(a[0]);
                return [=] { return f(*make(s)); };
            };
        }

        Method atStrike(std::function<Real(const SabrInterpolatedSmileSection&, Real)> f) {
            return [f](const Args& a) -> Call {
                arity(a, 2);
                SabrSpec s = sabrSpec(a[0]);
                Real k = real(a[1]);
                return [=] { return json::value(f(*make(s), k)); };
            };
        }

        const Register unit({
            {"SabrInterpolatedSmileSection::alpha",
             whole([](const SabrInterpolatedSmileSection& s) { return json::value(s.alpha()); })},
            {"SabrInterpolatedSmileSection::beta",
             whole([](const SabrInterpolatedSmileSection& s) { return json::value(s.beta()); })},
            {"SabrInterpolatedSmileSection::nu",
             whole([](const SabrInterpolatedSmileSection& s) { return json::value(s.nu()); })},
            {"SabrInterpolatedSmileSection::rho",
             whole([](const SabrInterpolatedSmileSection& s) { return json::value(s.rho()); })},
            {"SabrInterpolatedSmileSection::rmsError",
             whole([](const SabrInterpolatedSmileSection& s) { return json::value(s.rmsError()); })},
            {"SabrInterpolatedSmileSection::maxError",
             whole([](const SabrInterpolatedSmileSection& s) { return json::value(s.maxError()); })},
            {"SabrInterpolatedSmileSection::endCriteria",
             whole([](const SabrInterpolatedSmileSection& s) {
                 return json::value(static_cast<int>(s.endCriteria()));
             })},
            {"SabrInterpolatedSmileSection::minStrike",
             whole([](const SabrInterpolatedSmileSection& s) { return json::value(s.minStrike()); })},
            {"SabrInterpolatedSmileSection::maxStrike",
             whole([](const SabrInterpolatedSmileSection& s) { return json::value(s.maxStrike()); })},
            {"SabrInterpolatedSmileSection::atmLevel",
             whole([](const SabrInterpolatedSmileSection& s) { return json::value(s.atmLevel()); })},
            {"SabrInterpolatedSmileSection::exerciseTime",
             whole([](const SabrInterpolatedSmileSection& s) { return json::value(s.exerciseTime()); })},
            {"SabrInterpolatedSmileSection::volatility",
             atStrike([](const SabrInterpolatedSmileSection& s, Real k) { return s.volatility(k); })},
            {"SabrInterpolatedSmileSection::variance",
             atStrike([](const SabrInterpolatedSmileSection& s, Real k) { return s.variance(k); })},
        });

    }

}
