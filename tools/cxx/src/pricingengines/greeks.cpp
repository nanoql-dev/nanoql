// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/greeks.hpp: blackScholesTheta and defaultThetaPerDay, which the wheel doesn't expose.
// "blackScholesTheta" takes (spot, riskFreeRate, dividendYield, volatility, referenceDate, dayCounter, value, delta,
// gamma) and builds a Black-Scholes-Merton process on flat forwards and a constant volatility from the reference date,
// Actual/365 (Fixed) or Actual/360; "defaultThetaPerDay" takes a theta.

#include "convert.hpp"

#include <ql/pricingengines/greeks.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            // (spot, r, q, volatility, referenceDate, dayCounter, value, delta, gamma)
            {"blackScholesTheta",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 Real spot = real(a[0]), r = real(a[1]), q = real(a[2]), vol = real(a[3]);
                 DateSpec reference = date(a[4]);
                 DayCounterSpec dc = dayCounter(a[5]);
                 Real value = real(a[6]), delta = real(a[7]), gamma = real(a[8]);
                 return [=] {
                     Date d = makeDate(reference);
                     DayCounter c = makeDayCounter(dc);
                     auto p = ext::make_shared<BlackScholesMertonProcess>(
                         Handle<Quote>(ext::make_shared<SimpleQuote>(spot)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, q, c)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, r, c)),
                         Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(d, NullCalendar(), vol, c)));
                     return json::value(blackScholesTheta(p, value, delta, gamma));
                 };
             }},
            {"defaultThetaPerDay",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Real theta = real(a[0]);
                 return [=] { return json::value(defaultThetaPerDay(theta)); };
             }},
        });

    }

}
