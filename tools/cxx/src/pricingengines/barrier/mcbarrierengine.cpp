// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/barrier/mcbarrierengine.hpp, .cpp, whose path pricers and builder the wheel doesn't expose.
// "BarrierPathPricer" takes (barrierType, barrier, rebate, optionType, strike, discounts, seed, times, values): the
// Brownian-bridge pricer with its uniforms from a MersenneTwisterUniformRsg of the path's steps at the seed, its
// diffusion a Black-Scholes-Merton process at 100 on flat forwards of 3% and 1% and a constant volatility of 20% from
// the evaluation date on Act/365, priced on the path with those values at those times (the first 0).
// "BiasedBarrierPathPricer" takes the same less the seed. "MakeMCBarrierEngine" takes (traits, steps, stepsPerYear,
// samples, tolerance, order): the builder on that process, "pseudorandom" or "lowdiscrepancy", each argument null for
// none, the samples and tolerance set in the order given ("samples" first or "tolerance" first), and the engine built;
// true when QuantLib's checks pass.

#include "convert.hpp"

#include <ql/methods/montecarlo/path.hpp>
#include <ql/pricingengines/barrier/mcbarrierengine.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        ext::shared_ptr<GeneralizedBlackScholesProcess> process() {
            Date d = Settings::instance().evaluationDate();
            DayCounter c = Actual365Fixed();
            return ext::make_shared<BlackScholesMertonProcess>(
                Handle<Quote>(ext::make_shared<SimpleQuote>(100.0)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, 0.01, c)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, 0.03, c)),
                Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(d, NullCalendar(), 0.2, c)));
        }

        struct PricerSpec {
            Barrier::Type barrierType;
            Real barrier, rebate;
            Option::Type optionType;
            Real strike;
            std::vector<Real> discounts, times, values;
        };

        PricerSpec pricerSpec(const Args& a, std::size_t times) {
            PricerSpec s;
            s.barrierType = enumeration<Barrier::Type>(a[0]);
            s.barrier = real(a[1]);
            s.rebate = real(a[2]);
            s.optionType = enumeration<Option::Type>(a[3]);
            s.strike = real(a[4]);
            s.discounts = reals(a[5]);
            s.times = reals(a[times]);
            s.values = reals(a[times + 1]);
            if (s.times.size() != s.values.size() || s.times.empty())
                throw InvalidParams("expected as many values as times");
            return s;
        }

        Path path(const PricerSpec& s) {
            TimeGrid grid(s.times.begin(), s.times.end());
            return Path(grid, Array(s.values.begin(), s.values.end()));
        }

        const Register unit({
            {"BarrierPathPricer",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 PricerSpec s = pricerSpec(a, 7);
                 Integer seed = integer(a[6]);
                 return [=] {
                     Path p = path(s);
                     PseudoRandom::ursg_type generator(p.length() - 1, static_cast<BigNatural>(seed));
                     BarrierPathPricer pricer(s.barrierType, s.barrier, s.rebate, s.optionType, s.strike, s.discounts,
                                              process(), generator);
                     return json::value(pricer(p));
                 };
             }},
            {"BiasedBarrierPathPricer",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 PricerSpec s = pricerSpec(a, 6);
                 return [=] {
                     BiasedBarrierPathPricer pricer(s.barrierType, s.barrier, s.rebate, s.optionType, s.strike,
                                                    s.discounts);
                     return json::value(pricer(path(s)));
                 };
             }},
            {"MakeMCBarrierEngine",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 std::string traits = text(a[0]);
                 if (traits != "pseudorandom" && traits != "lowdiscrepancy")
                     throw InvalidParams("unknown traits " + traits);
                 Integer steps = integer(a[1]);
                 Integer stepsPerYear = integer(a[2]);
                 Integer samples = integer(a[3]);
                 Real tolerance = real(a[4]);
                 std::string order = text(a[5]);
                 return [=] {
                     auto build = [&](auto builder) {
                         if (steps != Null<Integer>())
                             builder.withSteps(static_cast<Size>(steps));
                         if (stepsPerYear != Null<Integer>())
                             builder.withStepsPerYear(static_cast<Size>(stepsPerYear));
                         auto setSamples = [&] {
                             if (samples != Null<Integer>())
                                 builder.withSamples(static_cast<Size>(samples));
                         };
                         auto setTolerance = [&] {
                             if (tolerance != Null<Real>())
                                 builder.withAbsoluteTolerance(tolerance);
                         };
                         if (order == "samples") {
                             setSamples();
                             setTolerance();
                         } else {
                             setTolerance();
                             setSamples();
                         }
                         builder.withSeed(42);
                         ext::shared_ptr<PricingEngine> engine = builder;
                         return engine != nullptr;
                     };
                     bool built = traits == "pseudorandom"
                                      ? build(MakeMCBarrierEngine<PseudoRandom>(process()))
                                      : build(MakeMCBarrierEngine<LowDiscrepancy>(process()));
                     return json::value(built);
                 };
             }},
        });

    }

}
