// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/asian/mc_discr_geom_av_price_heston.hpp, .cpp, whose path pricer and builder the wheel doesn't expose.
// "GeometricAPOHestonPathPricer" takes (optionType, strike, discount, fixingIndices, runningProduct, pastFixings,
// times, spot, variance): the pricer on the two-asset path with those values at those times.
// "MakeMCDiscreteGeometricAPHestonEngine" takes (traits, steps, stepsPerYear, samples, tolerance, order): the builder on
// a Heston process at 100 on flat forwards of 3% and 1% from the evaluation date on Act/365 (v0 0.04, kappa 1.5, theta
// 0.04, sigma 0.3, rho -0.7), each argument null for none, and the engine built; true when QuantLib's checks pass.

#include "convert.hpp"

#include <ql/methods/montecarlo/multipath.hpp>
#include <ql/methods/montecarlo/path.hpp>
#include <ql/pricingengines/asian/mc_discr_geom_av_price_heston.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/processes/hestonprocess.hpp>
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

        ext::shared_ptr<HestonProcess> hestonProcess() {
            Date d = Settings::instance().evaluationDate();
            DayCounter c = Actual365Fixed();
            return ext::make_shared<HestonProcess>(
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, 0.03, c)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, 0.01, c)),
                Handle<Quote>(ext::make_shared<SimpleQuote>(100.0)), 0.04, 1.5, 0.04, 0.3, -0.7);
        }

        // the grid of those times, 0 added where they don't start there, and a value at each of its nodes
        Path path(const std::vector<Real>& times, const std::vector<Real>& values) {
            if (times.empty())
                throw InvalidParams("expected a time");
            TimeGrid grid(times.begin(), times.end());
            if (values.size() != grid.size())
                throw InvalidParams("expected a value at each node of the grid");
            return Path(grid, Array(values.begin(), values.end()));
        }

        MultiPath multiPath(const std::vector<Real>& times, const std::vector<Real>& spot, const std::vector<Real>& variance) {
            if (times.empty())
                throw InvalidParams("expected a time");
            TimeGrid grid(times.begin(), times.end());
            if (spot.size() != grid.size() || variance.size() != grid.size())
                throw InvalidParams("expected a value at each node of the grid");
            MultiPath p(2, grid);
            for (Size i = 0; i < spot.size(); ++i) {
                p[0][i] = spot[i];
                p[1][i] = variance[i];
            }
            return p;
        }

        std::vector<Size> indices(const std::vector<Integer>& v) {
            std::vector<Size> out;
            for (Integer i : v) {
                if (i < 0)
                    throw InvalidParams("negative fixing index");
                out.push_back(static_cast<Size>(i));
            }
            return out;
        }

        // the builder's settings in the order given ("samples" first or "tolerance" first), the seed 42, and the
        // engine built
        template <class B>
        bool built(B builder, Integer samples, Real tolerance, const std::string& order) {
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

        std::string traitsOf(const json::value& v) {
            std::string traits = text(v);
            if (traits != "pseudorandom" && traits != "lowdiscrepancy")
                throw InvalidParams("unknown traits " + traits);
            return traits;
        }

        const Register unit({
            {"GeometricAPOHestonPathPricer",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 auto type = enumeration<Option::Type>(a[0]);
                 Real strike = real(a[1]), discount = real(a[2]);
                 std::vector<Size> fixings = indices(integers(a[3]));
                 Real running = real(a[4]);
                 Integer past = integer(a[5]);
                 std::vector<Real> times = reals(a[6]), spot = reals(a[7]), variance = reals(a[8]);
                 return [=] {
                     GeometricAPOHestonPathPricer pricer(type, strike, discount, fixings, running, static_cast<Size>(past));
                     return json::value(pricer(multiPath(times, spot, variance)));
                 };
             }},
            {"MakeMCDiscreteGeometricAPHestonEngine",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 std::string traits = traitsOf(a[0]);
                 Integer steps = integer(a[1]), stepsPerYear = integer(a[2]), samples = integer(a[3]);
                 Real tolerance = real(a[4]);
                 std::string order = text(a[5]);
                 return [=] {
                     auto build = [&](auto builder) {
                         if (steps != Null<Integer>())
                             builder.withSteps(static_cast<Size>(steps));
                         if (stepsPerYear != Null<Integer>())
                             builder.withStepsPerYear(static_cast<Size>(stepsPerYear));
                         return built(builder, samples, tolerance, order);
                     };
                     bool ok = traits == "pseudorandom"
                                   ? build(MakeMCDiscreteGeometricAPHestonEngine<PseudoRandom>(hestonProcess()))
                                   : build(MakeMCDiscreteGeometricAPHestonEngine<LowDiscrepancy>(hestonProcess()));
                     return json::value(ok);
                 };
             }},
        });

    }

}
