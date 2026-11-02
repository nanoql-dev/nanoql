// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/barrieroption/mcdoublebarrierengine.hpp, .cpp, which the wheel doesn't expose.
// "MCDoubleBarrierEngine" takes (traits, referenceDate, spot, dividendYield, riskFreeRate, vol, barrierType, barrierLo,
// barrierHi, rebate, optionType, strike, expiryDays, steps, antithetic, samples, seed): a Black-Scholes-Merton process
// on flat forwards of the two rates and a constant volatility from the reference date on Act/365; the double barrier
// option on the plain vanilla payoff, European that many days after it; MakeMCDoubleBarrierEngine on PseudoRandom
// ("pr") or LowDiscrepancy ("ld") with that many steps, the antithetic choice, that many samples and the seed; answered
// as {"NPV"}, and "errorEstimate" under the pseudo-random traits.
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

        template <class RNG>
        json::value priced(const DateSpec& reference, Real spot, Real q, Real r, Real vol,
                           DoubleBarrier::Type barrierType, Real lo, Real hi, Real rebate, Option::Type optionType,
                           Real strike, Integer expiryDays, Integer steps, bool antithetic, Integer samples,
                           Integer seed, bool errorEstimate) {
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
            ext::shared_ptr<PricingEngine> engine = MakeMCDoubleBarrierEngine<RNG>(process)
                                                        .withSteps(static_cast<Size>(steps))
                                                        .withAntitheticVariate(antithetic)
                                                        .withSamples(static_cast<Size>(samples))
                                                        .withSeed(static_cast<BigNatural>(seed));
            option.setPricingEngine(engine);
            json::object o;
            o["NPV"] = json::value(option.NPV());
            if (errorEstimate)
                o["errorEstimate"] = json::value(option.errorEstimate());
            return json::value(o);
        }

        const Register unit({
            {"MCDoubleBarrierEngine",
             [](const Args& a) -> Call {
                 arity(a, 17);
                 std::string traits = text(a[0]);
                 if (traits != "pr" && traits != "ld")
                     throw InvalidParams("unknown traits " + traits);
                 DateSpec reference = date(a[1]);
                 Real spot = real(a[2]);
                 Real q = real(a[3]);
                 Real r = real(a[4]);
                 Real vol = real(a[5]);
                 auto barrierType = enumeration<DoubleBarrier::Type>(a[6]);
                 Real lo = real(a[7]);
                 Real hi = real(a[8]);
                 Real rebate = real(a[9]);
                 auto optionType = enumeration<Option::Type>(a[10]);
                 Real strike = real(a[11]);
                 Integer expiryDays = integer(a[12]);
                 Integer steps = integer(a[13]);
                 bool antithetic = boolean(a[14]);
                 Integer samples = integer(a[15]);
                 Integer seed = integer(a[16]);
                 return [=] {
                     return traits == "pr"
                                ? priced<PseudoRandom>(reference, spot, q, r, vol, barrierType, lo, hi, rebate,
                                                       optionType, strike, expiryDays, steps, antithetic, samples,
                                                       seed, true)
                                : priced<LowDiscrepancy>(reference, spot, q, r, vol, barrierType, lo, hi, rebate,
                                                         optionType, strike, expiryDays, steps, antithetic, samples,
                                                         seed, false);
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
