// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/barrieroption/mcdoublebarrierengine.hpp, .cpp, which the wheel doesn't expose.
// "MCDoubleBarrierEngine" takes (referenceDate, spot, dividendYield, riskFreeRate, vol, barrierType, barrierLo,
// barrierHi, rebate, optionType, strike, expiryDays, steps, antithetic, samples, seed): a Black-Scholes-Merton process
// on flat forwards of the two rates and a constant volatility from the reference date on Act/365; the double barrier
// option on the plain vanilla payoff, European that many days after it; MakeMCDoubleBarrierEngine<PseudoRandom> with
// that many steps, the antithetic choice, that many samples and the seed; answered as {"NPV", "errorEstimate"}.
// "DoubleBarrierPathPricer" takes (barrierType, barrierLo, barrierHi, rebate, optionType, strike, discounts, times,
// values): the pricer on the path with those values at those times (the first 0).

#include "convert.hpp"

#include <ql/exercise.hpp>
#include <ql/experimental/barrieroption/mcdoublebarrierengine.hpp>
#include <ql/instruments/doublebarrieroption.hpp>
#include <ql/methods/montecarlo/path.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"MCDoubleBarrierEngine",
             [](const Args& a) -> Call {
                 arity(a, 16);
                 DateSpec reference = date(a[0]);
                 Real spot = real(a[1]);
                 Real q = real(a[2]);
                 Real r = real(a[3]);
                 Real vol = real(a[4]);
                 auto barrierType = enumeration<DoubleBarrier::Type>(a[5]);
                 Real lo = real(a[6]);
                 Real hi = real(a[7]);
                 Real rebate = real(a[8]);
                 auto optionType = enumeration<Option::Type>(a[9]);
                 Real strike = real(a[10]);
                 Integer expiryDays = integer(a[11]);
                 Integer steps = integer(a[12]);
                 bool antithetic = boolean(a[13]);
                 Integer samples = integer(a[14]);
                 Integer seed = integer(a[15]);
                 return [=] {
                     Date d = makeDate(reference);
                     DayCounter c = Actual365Fixed();
                     auto process = ext::make_shared<BlackScholesMertonProcess>(
                         Handle<Quote>(ext::make_shared<SimpleQuote>(spot)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, q, c)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, r, c)),
                         Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(d, NullCalendar(), vol, c)));
                     DoubleBarrierOption option(barrierType, lo, hi, rebate,
                                                ext::make_shared<PlainVanillaPayoff>(optionType, strike),
                                                ext::make_shared<EuropeanExercise>(d + expiryDays));
                     ext::shared_ptr<PricingEngine> engine = MakeMCDoubleBarrierEngine<PseudoRandom>(process)
                                                                 .withSteps(static_cast<Size>(steps))
                                                                 .withAntitheticVariate(antithetic)
                                                                 .withSamples(static_cast<Size>(samples))
                                                                 .withSeed(static_cast<BigNatural>(seed));
                     option.setPricingEngine(engine);
                     json::object o;
                     o["NPV"] = json::value(option.NPV());
                     o["errorEstimate"] = json::value(option.errorEstimate());
                     return json::value(o);
                 };
             }},
            {"DoubleBarrierPathPricer",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 auto barrierType = enumeration<DoubleBarrier::Type>(a[0]);
                 Real lo = real(a[1]);
                 Real hi = real(a[2]);
                 Real rebate = real(a[3]);
                 auto optionType = enumeration<Option::Type>(a[4]);
                 Real strike = real(a[5]);
                 std::vector<Real> discounts = reals(a[6]);
                 std::vector<Real> times = reals(a[7]);
                 std::vector<Real> values = reals(a[8]);
                 if (times.size() != values.size() || times.empty())
                     throw InvalidParams("expected as many values as times");
                 return [=] {
                     DoubleBarrierPathPricer pricer(barrierType, lo, hi, rebate, optionType, strike, discounts);
                     TimeGrid grid(times.begin(), times.end());
                     return json::value(pricer(Path(grid, Array(values.begin(), values.end()))));
                 };
             }},
        });

    }

}
