// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/vanilla/analyticeuropeanvasicekengine.hpp, .cpp: the Black-Vasicek engine, which the wheel doesn't
// expose. "AnalyticBlackVasicekEngine::NPV" takes (spot, riskFreeRate, volatility, referenceDate, dayCounter,
// exerciseType, optionType, strike, exerciseDate, r0, a, b, sigma, lambda, correlation) and prices a plain vanilla
// option, European or American ("European" or "American" from the reference date), on a Black-Scholes-Merton process
// over a flat risk-free forward, no dividends and a constant volatility from the reference date, with a Vasicek model
// (r0, a, b, sigma, lambda), and returns its NPV. The evaluation date is the case's Settings.

#include "convert.hpp"

#include <ql/exercise.hpp>
#include <ql/instruments/vanillaoption.hpp>
#include <ql/models/shortrate/onefactormodels/vasicek.hpp>
#include <ql/pricingengines/vanilla/analyticeuropeanvasicekengine.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"AnalyticBlackVasicekEngine::NPV",
             [](const Args& a) -> Call {
                 arity(a, 15);
                 Real spot = real(a[0]), r = real(a[1]), vol = real(a[2]);
                 DateSpec reference = date(a[3]);
                 DayCounterSpec dc = dayCounter(a[4]);
                 std::string exerciseType = text(a[5]);
                 if (exerciseType != "European" && exerciseType != "American")
                     throw InvalidParams("an exercise type is European or American");
                 auto type = enumeration<Option::Type>(a[6]);
                 Real strike = real(a[7]);
                 DateSpec exercise = date(a[8]);
                 Real r0 = real(a[9]), kappa = real(a[10]), b = real(a[11]), sigma = real(a[12]),
                      lambda = real(a[13]), correlation = real(a[14]);
                 return [=] {
                     Date d = makeDate(reference);
                     DayCounter c = makeDayCounter(dc);
                     auto p = ext::make_shared<BlackScholesMertonProcess>(
                         Handle<Quote>(ext::make_shared<SimpleQuote>(spot)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, 0.0, c)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, r, c)),
                         Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(d, NullCalendar(), vol, c)));
                     ext::shared_ptr<Exercise> ex;
                     if (exerciseType == "European")
                         ex = ext::make_shared<EuropeanExercise>(makeDate(exercise));
                     else
                         ex = ext::make_shared<AmericanExercise>(d, makeDate(exercise));
                     VanillaOption option(ext::make_shared<PlainVanillaPayoff>(type, strike), ex);
                     option.setPricingEngine(ext::make_shared<AnalyticBlackVasicekEngine>(
                         p, ext::make_shared<Vasicek>(r0, kappa, b, sigma, lambda), correlation));
                     return json::value(option.NPV());
                 };
             }},
        });

    }

}
