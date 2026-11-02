// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/lookback/mclookbackengine.hpp, .cpp, whose builder the wheel doesn't expose. "MakeMCLookbackEngine"
// takes (instrument, traits, steps, stepsPerYear, samples, tolerance, order): the builder for "fixed", "floating",
// "partial fixed" or "partial floating" (ContinuousFixedLookbackOption, ContinuousFloatingLookbackOption and their
// partial-time options) on a Black-Scholes-Merton process at 100 on flat forwards of 3% and 1% and a constant
// volatility of 20% from the evaluation date on Act/365, "pseudorandom" or "lowdiscrepancy", each argument null for
// none, the samples and tolerance set in the order given ("samples" first or "tolerance" first), the seed 42, and the
// engine built; true when QuantLib's checks pass. "MCLookbackEngine" takes (instrument, optionType, traits, steps,
// samples, antithetic, seed): "fixed" or "floating" (ContinuousFixedLookbackOption on a plain vanilla payoff struck at
// 90, or ContinuousFloatingLookbackOption), the extremum so far 100, on testMonteCarloLookback's market
// (test-suite/lookbackoptions.cpp): a Black-Scholes-Merton process at 100 on flat forwards of 6% and 0 and a constant
// volatility of 10% from the evaluation date on Act/360, the exercise 360 days after it; MakeMCLookbackEngine on those
// traits with that many steps and samples, the antithetic choice and the seed; answered as {"NPV"}, and
// "errorEstimate" under the pseudo-random traits.

#include "convert.hpp"

#include <ql/exercise.hpp>
#include <ql/instruments/lookbackoption.hpp>
#include <ql/pricingengines/lookback/mclookbackengine.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual360.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <string>

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

        template <class I, class RNG>
        bool build(Integer steps, Integer stepsPerYear, Integer samples, Real tolerance, const std::string& order) {
            MakeMCLookbackEngine<I, RNG> builder(process());
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
        }

        template <class RNG>
        bool buildFor(const std::string& instrument, Integer steps, Integer stepsPerYear, Integer samples,
                      Real tolerance, const std::string& order) {
            if (instrument == "fixed")
                return build<ContinuousFixedLookbackOption, RNG>(steps, stepsPerYear, samples, tolerance, order);
            if (instrument == "floating")
                return build<ContinuousFloatingLookbackOption, RNG>(steps, stepsPerYear, samples, tolerance, order);
            if (instrument == "partial fixed")
                return build<ContinuousPartialFixedLookbackOption, RNG>(steps, stepsPerYear, samples, tolerance,
                                                                       order);
            return build<ContinuousPartialFloatingLookbackOption, RNG>(steps, stepsPerYear, samples, tolerance, order);
        }

        template <class I, class RNG>
        json::value priced(I& option, Integer steps, Integer samples, bool antithetic, Integer seed, bool errorEstimate) {
            Date d = Settings::instance().evaluationDate();
            DayCounter c = Actual360();
            auto market = ext::make_shared<BlackScholesMertonProcess>(
                Handle<Quote>(ext::make_shared<SimpleQuote>(100.0)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, 0.0, c)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, 0.06, c)),
                Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(d, NullCalendar(), 0.1, c)));
            ext::shared_ptr<PricingEngine> engine = MakeMCLookbackEngine<I, RNG>(market)
                                                        .withSteps(static_cast<Size>(steps))
                                                        .withSamples(static_cast<Size>(samples))
                                                        .withAntitheticVariate(antithetic)
                                                        .withSeed(static_cast<BigNatural>(seed));
            option.setPricingEngine(engine);
            json::object o;
            o["NPV"] = json::value(option.NPV());
            if (errorEstimate)
                o["errorEstimate"] = json::value(option.errorEstimate());
            return json::value(o);
        }

        template <class RNG>
        json::value pricedFor(const std::string& instrument, Option::Type type, Integer steps, Integer samples,
                              bool antithetic, Integer seed, bool errorEstimate) {
            Date today = Settings::instance().evaluationDate();
            auto exercise = ext::make_shared<EuropeanExercise>(today + 360);
            if (instrument == "fixed") {
                ContinuousFixedLookbackOption option(100.0, ext::make_shared<PlainVanillaPayoff>(type, 90.0), exercise);
                return priced<ContinuousFixedLookbackOption, RNG>(option, steps, samples, antithetic, seed,
                                                                  errorEstimate);
            }
            ContinuousFloatingLookbackOption option(100.0, ext::make_shared<FloatingTypePayoff>(type), exercise);
            return priced<ContinuousFloatingLookbackOption, RNG>(option, steps, samples, antithetic, seed,
                                                                 errorEstimate);
        }

        const Register unit({
            {"MakeMCLookbackEngine",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 std::string instrument = text(a[0]);
                 if (instrument != "fixed" && instrument != "floating" && instrument != "partial fixed" &&
                     instrument != "partial floating")
                     throw InvalidParams("unknown instrument " + instrument);
                 std::string traits = text(a[1]);
                 if (traits != "pseudorandom" && traits != "lowdiscrepancy")
                     throw InvalidParams("unknown traits " + traits);
                 Integer steps = integer(a[2]);
                 Integer stepsPerYear = integer(a[3]);
                 Integer samples = integer(a[4]);
                 Real tolerance = real(a[5]);
                 std::string order = text(a[6]);
                 return [=] {
                     bool built = traits == "pseudorandom"
                                      ? buildFor<PseudoRandom>(instrument, steps, stepsPerYear, samples, tolerance,
                                                               order)
                                      : buildFor<LowDiscrepancy>(instrument, steps, stepsPerYear, samples, tolerance,
                                                                 order);
                     return json::value(built);
                 };
             }},
            {"MCLookbackEngine",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 std::string instrument = text(a[0]);
                 if (instrument != "fixed" && instrument != "floating")
                     throw InvalidParams("unknown instrument " + instrument);
                 auto type = enumeration<Option::Type>(a[1]);
                 std::string traits = text(a[2]);
                 if (traits != "pseudorandom" && traits != "lowdiscrepancy")
                     throw InvalidParams("unknown traits " + traits);
                 Integer steps = integer(a[3]);
                 Integer samples = integer(a[4]);
                 bool antithetic = boolean(a[5]);
                 Integer seed = integer(a[6]);
                 return [=] {
                     return traits == "pseudorandom"
                                ? pricedFor<PseudoRandom>(instrument, type, steps, samples, antithetic, seed, true)
                                : pricedFor<LowDiscrepancy>(instrument, type, steps, samples, antithetic, seed, false);
                 };
             }},
        });

    }

}
