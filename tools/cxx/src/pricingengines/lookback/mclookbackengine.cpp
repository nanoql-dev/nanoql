// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/lookback/mclookbackengine.hpp, .cpp, whose builder the wheel doesn't expose. "MakeMCLookbackEngine"
// takes (instrument, traits, steps, stepsPerYear, samples, tolerance, order): the builder for "fixed", "floating",
// "partial fixed" or "partial floating" (ContinuousFixedLookbackOption, ContinuousFloatingLookbackOption and their
// partial-time options) on a Black-Scholes-Merton process at 100 on flat forwards of 3% and 1% and a constant
// volatility of 20% from the evaluation date on Act/365, "pseudorandom" or "lowdiscrepancy", each argument null for
// none, the samples and tolerance set in the order given ("samples" first or "tolerance" first), the seed 42, and the
// engine built; true when QuantLib's checks pass.

#include "convert.hpp"

#include <ql/instruments/lookbackoption.hpp>
#include <ql/pricingengines/lookback/mclookbackengine.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
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
        });

    }

}
