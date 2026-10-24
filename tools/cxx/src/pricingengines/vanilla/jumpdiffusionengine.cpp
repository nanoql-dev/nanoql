// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/vanilla/jumpdiffusionengine.hpp, .cpp: Merton's jump-diffusion engine, which the wheel doesn't
// expose. "JumpDiffusionEngine::results" takes (spot, dividendYield, riskFreeRate, volatility, referenceDate,
// dayCounter, jumpIntensity, logMeanJump, logJumpVolatility, exerciseType, optionType, strike, exerciseDate,
// relativeAccuracy, maxIterations) and prices a plain vanilla option, European or American ("European" or "American"
// from the reference date), on a Merton-76 process over flat forwards and a constant volatility from the reference
// date, and returns {"NPV", "delta", "gamma", "theta", "vega", "rho", "dividendRho"}. The evaluation date is the
// case's Settings.

#include "convert.hpp"

#include <ql/exercise.hpp>
#include <ql/instruments/vanillaoption.hpp>
#include <ql/pricingengines/vanilla/jumpdiffusionengine.hpp>
#include <ql/processes/merton76process.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        Handle<Quote> quote(Real x) { return Handle<Quote>(ext::make_shared<SimpleQuote>(x)); }

        const Register unit({
            {"JumpDiffusionEngine::results",
             [](const Args& a) -> Call {
                 arity(a, 15);
                 Real spot = real(a[0]), q = real(a[1]), r = real(a[2]), vol = real(a[3]);
                 DateSpec reference = date(a[4]);
                 DayCounterSpec dc = dayCounter(a[5]);
                 Real intensity = real(a[6]), meanJump = real(a[7]), jumpVol = real(a[8]);
                 std::string exerciseType = text(a[9]);
                 if (exerciseType != "European" && exerciseType != "American")
                     throw InvalidParams("an exercise type is European or American");
                 auto type = enumeration<Option::Type>(a[10]);
                 Real strike = real(a[11]);
                 DateSpec exercise = date(a[12]);
                 Real accuracy = real(a[13]);
                 Integer maxIterations = integer(a[14]);
                 if (maxIterations < 0)
                     throw InvalidParams("a maximum must be non-negative");
                 return [=] {
                     Date d = makeDate(reference);
                     DayCounter c = makeDayCounter(dc);
                     auto p = ext::make_shared<Merton76Process>(
                         quote(spot), Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, q, c)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, r, c)),
                         Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(d, NullCalendar(), vol, c)),
                         quote(intensity), quote(meanJump), quote(jumpVol));
                     ext::shared_ptr<Exercise> ex;
                     if (exerciseType == "European")
                         ex = ext::make_shared<EuropeanExercise>(makeDate(exercise));
                     else
                         ex = ext::make_shared<AmericanExercise>(d, makeDate(exercise));
                     VanillaOption option(ext::make_shared<PlainVanillaPayoff>(type, strike), ex);
                     option.setPricingEngine(
                         ext::make_shared<JumpDiffusionEngine>(p, accuracy, static_cast<Size>(maxIterations)));
                     json::object out;
                     out["NPV"] = option.NPV();
                     out["delta"] = option.delta();
                     out["gamma"] = option.gamma();
                     out["theta"] = option.theta();
                     out["vega"] = option.vega();
                     out["rho"] = option.rho();
                     out["dividendRho"] = option.dividendRho();
                     return json::value(out);
                 };
             }},
        });

    }

}
