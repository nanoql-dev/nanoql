// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/processes/blackscholesprocess.hpp: the generalized process's forceDiscretization argument, which the wheel
// doesn't expose. "GeneralizedBlackScholesProcess::forced" takes (member, spot, riskFreeRate, dividendYield,
// volatility, referenceDate, dayCounter, forceDiscretization, t0, x0, dt, dw), builds the process on flat forwards and
// a constant volatility from the reference date with Euler's discretization, and returns the member's value at the
// point: "expectation", "stdDeviation", "variance" or "evolve".

#include "convert.hpp"

#include <ql/processes/blackscholesprocess.hpp>
#include <ql/processes/eulerdiscretization.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>

#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            // (member, spot, r, q, volatility, referenceDate, dayCounter, forceDiscretization, t0, x0, dt, dw)
            {"GeneralizedBlackScholesProcess::forced",
             [](const Args& a) -> Call {
                 arity(a, 12);
                 std::string member = text(a[0]);
                 if (member != "expectation" && member != "stdDeviation" && member != "variance" &&
                     member != "evolve")
                     throw InvalidParams("unknown member " + member);
                 Real spot = real(a[1]), r = real(a[2]), q = real(a[3]), vol = real(a[4]);
                 DateSpec reference = date(a[5]);
                 DayCounterSpec dc = dayCounter(a[6]);
                 bool force = boolean(a[7]);
                 Real t0 = real(a[8]), x0 = real(a[9]), dt = real(a[10]), dw = real(a[11]);
                 return [=] {
                     Date d = makeDate(reference);
                     DayCounter c = makeDayCounter(dc);
                     GeneralizedBlackScholesProcess p(
                         Handle<Quote>(ext::make_shared<SimpleQuote>(spot)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, q, c)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, r, c)),
                         Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(d, NullCalendar(), vol, c)),
                         ext::make_shared<EulerDiscretization>(), force);
                     if (member == "expectation")
                         return json::value(p.expectation(t0, x0, dt));
                     if (member == "stdDeviation")
                         return json::value(p.stdDeviation(t0, x0, dt));
                     if (member == "variance")
                         return json::value(p.variance(t0, x0, dt));
                     return json::value(p.evolve(t0, x0, dt, dw));
                 };
             }},
        });

    }

}
