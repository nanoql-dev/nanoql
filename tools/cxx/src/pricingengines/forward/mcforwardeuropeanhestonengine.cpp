// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/forward/mcforwardeuropeanhestonengine.hpp, .cpp: ForwardEuropeanHestonPathPricer on multi-paths
// given by the caller, and MakeMCForwardEuropeanHestonEngine's checks, which the wheel can't reach.
//
// "ForwardEuropeanHestonPathPricer::value" takes (optionType, moneyness, resetIndex, discount, assetValues,
// varianceValues): the pricer's arguments and the multi-path's two paths on a uniform grid to a year (a step between
// each two values). It returns the pricer's value; the constructor's refusal is QuantLib's, its message the error's.
//
// "MakeMCForwardEuropeanHestonEngine::check" takes (setting): "noSteps", "overspecified", "toleranceThenSamples",
// "samplesThenTolerance" or "lowDiscrepancyTolerance", as mcforwardeuropeanbsengine.cpp's check, on a Heston process;
// its refusal is QuantLib's, its message the error's, and "built" where none comes.

#include "convert.hpp"

#include <ql/methods/montecarlo/multipath.hpp>
#include <ql/pricingengines/forward/mcforwardeuropeanhestonengine.hpp>
#include <ql/processes/hestonprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>
#include <ql/timegrid.hpp>

#include <climits>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        Size index(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > INT_MAX)
                throw InvalidParams("expected an index, got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        ext::shared_ptr<HestonProcess> hestonProcess() {
            const Date today = Settings::instance().evaluationDate();
            const DayCounter dc = Actual365Fixed();
            return ext::make_shared<HestonProcess>(
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, 0.05, dc)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, 0.02, dc)),
                Handle<Quote>(ext::make_shared<SimpleQuote>(100.0)), 0.04, 1.5, 0.04, 0.4, -0.6);
        }

        using Builder = MakeMCForwardEuropeanHestonEngine<PseudoRandom, Statistics, HestonProcess>;
        using LdBuilder = MakeMCForwardEuropeanHestonEngine<LowDiscrepancy, Statistics, HestonProcess>;

        const Register unit({
            {"ForwardEuropeanHestonPathPricer::value",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 const auto type = enumeration<Option::Type>(a[0]);
                 const Real moneyness = real(a[1]);
                 const Size resetIndex = index(a[2]);
                 const Real discount = real(a[3]);
                 const std::vector<Real> asset = reals(a[4]), variance = reals(a[5]);
                 if (asset.size() < 2 || variance.size() != asset.size())
                     throw InvalidParams("two paths of one length, two values at least");
                 return [=] {
                     ForwardEuropeanHestonPathPricer pricer(type, moneyness, resetIndex, discount);
                     TimeGrid grid(1.0, asset.size() - 1);
                     MultiPath paths(std::vector<Path>{Path(grid, Array(asset.begin(), asset.end())),
                                                       Path(grid, Array(variance.begin(), variance.end()))});
                     return json::value(pricer(paths));
                 };
             }},
            {"MakeMCForwardEuropeanHestonEngine::check",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 const std::string setting = text(a[0]);
                 if (setting != "noSteps" && setting != "overspecified" && setting != "toleranceThenSamples" &&
                     setting != "samplesThenTolerance" && setting != "lowDiscrepancyTolerance")
                     throw InvalidParams("unknown setting " + setting);
                 return [=] {
                     ext::shared_ptr<PricingEngine> engine;
                     if (setting == "noSteps")
                         engine = Builder(hestonProcess()).withSamples(10);
                     else if (setting == "overspecified")
                         engine = Builder(hestonProcess()).withSteps(2).withStepsPerYear(4).withSamples(10);
                     else if (setting == "toleranceThenSamples")
                         engine = Builder(hestonProcess()).withSteps(2).withAbsoluteTolerance(0.1).withSamples(10);
                     else if (setting == "samplesThenTolerance")
                         engine = Builder(hestonProcess()).withSteps(2).withSamples(10).withAbsoluteTolerance(0.1);
                     else
                         engine = LdBuilder(hestonProcess()).withSteps(2).withAbsoluteTolerance(0.1);
                     return json::value("built");
                 };
             }},
        });

    }

}
