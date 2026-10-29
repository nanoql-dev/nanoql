// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/barrieroption/quantodoublebarrieroption.hpp, .cpp, which the wheel binds without an engine.
// "QuantoDoubleBarrierOption" takes (referenceDate, spot, dividendYield, riskFreeRate, vol, foreignRate, fxVol,
// correlation, barrierType, barrierLo, barrierHi, rebate, optionType, strike, expiryDays): a Black-Scholes-Merton
// process on flat forwards of the two rates and a constant volatility, the foreign curve a flat forward and the
// exchange rate's volatility constant, all from the reference date on Act/365; the quanto double barrier option on the
// plain vanilla payoff, European that many days after it; QuantoEngine<DoubleBarrierOption,
// AnalyticDoubleBarrierEngine> at the correlation; its NPV.

#include "convert.hpp"

#include <ql/exercise.hpp>
#include <ql/experimental/barrieroption/quantodoublebarrieroption.hpp>
#include <ql/pricingengines/barrier/analyticdoublebarrierengine.hpp>
#include <ql/pricingengines/quanto/quantoengine.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"QuantoDoubleBarrierOption",
             [](const Args& a) -> Call {
                 arity(a, 15);
                 DateSpec reference = date(a[0]);
                 Real spot = real(a[1]);
                 Real q = real(a[2]);
                 Real r = real(a[3]);
                 Real vol = real(a[4]);
                 Real foreign = real(a[5]);
                 Real fxVol = real(a[6]);
                 Real correlation = real(a[7]);
                 auto barrierType = enumeration<DoubleBarrier::Type>(a[8]);
                 Real lo = real(a[9]);
                 Real hi = real(a[10]);
                 Real rebate = real(a[11]);
                 auto optionType = enumeration<Option::Type>(a[12]);
                 Real strike = real(a[13]);
                 Integer expiryDays = integer(a[14]);
                 return [=] {
                     Date d = makeDate(reference);
                     DayCounter c = Actual365Fixed();
                     auto process = ext::make_shared<BlackScholesMertonProcess>(
                         Handle<Quote>(ext::make_shared<SimpleQuote>(spot)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, q, c)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, r, c)),
                         Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(d, NullCalendar(), vol, c)));
                     QuantoDoubleBarrierOption option(barrierType, lo, hi, rebate,
                                                      ext::make_shared<PlainVanillaPayoff>(optionType, strike),
                                                      ext::make_shared<EuropeanExercise>(d + expiryDays));
                     option.setPricingEngine(
                         ext::make_shared<QuantoEngine<DoubleBarrierOption, AnalyticDoubleBarrierEngine>>(
                             process, Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, foreign, c)),
                             Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(d, NullCalendar(), fxVol, c)),
                             Handle<Quote>(ext::make_shared<SimpleQuote>(correlation))));
                     return json::value(option.NPV());
                 };
             }},
        });

    }

}
