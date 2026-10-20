// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/vanilla/analyticeuropeanengine.hpp: the engine's additional results, which the wheel doesn't
// expose. "AnalyticEuropeanEngine::additionalResults" takes (spot, riskFreeRate, dividendYield, volatility,
// referenceDate, dayCounter, optionType, strike, exerciseDate, discountRate or null) and prices a European plain
// vanilla option on a Black-Scholes-Merton process on flat forwards and a constant volatility from the reference date,
// with a flat discount curve when a discount rate is given, and returns {"spot", "dividendDiscount",
// "riskFreeDiscount", "forward", "strike", "volatility", "timeToExpiry"}. The evaluation date is the case's Settings.

#include "convert.hpp"

#include <ql/exercise.hpp>
#include <ql/instruments/vanillaoption.hpp>
#include <ql/pricingengines/vanilla/analyticeuropeanengine.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            // (spot, r, q, volatility, referenceDate, dayCounter, optionType, strike, exerciseDate, discountRate)
            {"AnalyticEuropeanEngine::additionalResults",
             [](const Args& a) -> Call {
                 arity(a, 10);
                 Real spot = real(a[0]), r = real(a[1]), q = real(a[2]), vol = real(a[3]);
                 DateSpec reference = date(a[4]);
                 DayCounterSpec dc = dayCounter(a[5]);
                 auto type = enumeration<Option::Type>(a[6]);
                 Real strike = real(a[7]);
                 DateSpec exercise = date(a[8]);
                 Real discountRate = real(a[9]);
                 return [=] {
                     Date d = makeDate(reference);
                     DayCounter c = makeDayCounter(dc);
                     auto p = ext::make_shared<BlackScholesMertonProcess>(
                         Handle<Quote>(ext::make_shared<SimpleQuote>(spot)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, q, c)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, r, c)),
                         Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(d, NullCalendar(), vol, c)));
                     VanillaOption option(ext::make_shared<PlainVanillaPayoff>(type, strike),
                                          ext::make_shared<EuropeanExercise>(makeDate(exercise)));
                     if (discountRate == Null<Real>())
                         option.setPricingEngine(ext::make_shared<AnalyticEuropeanEngine>(p));
                     else
                         option.setPricingEngine(ext::make_shared<AnalyticEuropeanEngine>(
                             p, Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, discountRate, c))));
                     option.NPV();
                     const auto& results = option.additionalResults();
                     json::object out;
                     for (const char* key :
                          {"spot", "dividendDiscount", "riskFreeDiscount", "forward", "strike", "volatility",
                           "timeToExpiry"})
                         out[key] = ext::any_cast<Real>(results.at(key));
                     return json::value(out);
                 };
             }},
        });

    }

}
