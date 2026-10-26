// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/forward/mcforwardeuropeanbsengine.hpp, .cpp: ForwardEuropeanBSPathPricer on paths given by the
// caller, and MakeMCForwardEuropeanBSEngine's checks, which the wheel can't reach.
//
// "ForwardEuropeanBSPathPricer::value" takes (optionType, moneyness, resetIndex, discount, values): the pricer's
// arguments and the path's values on a uniform grid to a year (a step between each two values). It returns the
// pricer's value; the constructor's refusal is QuantLib's, its message the error's.
//
// "MakeMCForwardEuropeanBSEngine::check" takes (setting): "noSteps" (samples only), "overspecified" (steps and steps
// per year), "toleranceThenSamples", "samplesThenTolerance" or "lowDiscrepancyTolerance" (a tolerance on
// low-discrepancy numbers), each converted to an engine on a Black-Scholes-Merton process; its refusal is QuantLib's,
// its message the error's, and "built" where none comes.

#include "convert.hpp"

#include <ql/methods/montecarlo/path.hpp>
#include <ql/pricingengines/forward/mcforwardeuropeanbsengine.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
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

        ext::shared_ptr<GeneralizedBlackScholesProcess> flatProcess() {
            const Date today = Settings::instance().evaluationDate();
            const DayCounter dc = Actual365Fixed();
            return ext::make_shared<BlackScholesMertonProcess>(
                Handle<Quote>(ext::make_shared<SimpleQuote>(100.0)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, 0.02, dc)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, 0.05, dc)),
                Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(today, NullCalendar(), 0.25, dc)));
        }

        const Register unit({
            {"ForwardEuropeanBSPathPricer::value",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 const auto type = enumeration<Option::Type>(a[0]);
                 const Real moneyness = real(a[1]);
                 const Size resetIndex = index(a[2]);
                 const Real discount = real(a[3]);
                 const std::vector<Real> values = reals(a[4]);
                 if (values.size() < 2)
                     throw InvalidParams("a path of two values at least");
                 return [=] {
                     ForwardEuropeanBSPathPricer pricer(type, moneyness, resetIndex, discount);
                     Path path(TimeGrid(1.0, values.size() - 1), Array(values.begin(), values.end()));
                     return json::value(pricer(path));
                 };
             }},
            {"MakeMCForwardEuropeanBSEngine::check",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 const std::string setting = text(a[0]);
                 if (setting != "noSteps" && setting != "overspecified" && setting != "toleranceThenSamples" &&
                     setting != "samplesThenTolerance" && setting != "lowDiscrepancyTolerance")
                     throw InvalidParams("unknown setting " + setting);
                 return [=] {
                     ext::shared_ptr<PricingEngine> engine;
                     if (setting == "noSteps")
                         engine = MakeMCForwardEuropeanBSEngine<PseudoRandom>(flatProcess()).withSamples(10);
                     else if (setting == "overspecified")
                         engine = MakeMCForwardEuropeanBSEngine<PseudoRandom>(flatProcess())
                                      .withSteps(2)
                                      .withStepsPerYear(4)
                                      .withSamples(10);
                     else if (setting == "toleranceThenSamples")
                         engine = MakeMCForwardEuropeanBSEngine<PseudoRandom>(flatProcess())
                                      .withSteps(2)
                                      .withAbsoluteTolerance(0.1)
                                      .withSamples(10);
                     else if (setting == "samplesThenTolerance")
                         engine = MakeMCForwardEuropeanBSEngine<PseudoRandom>(flatProcess())
                                      .withSteps(2)
                                      .withSamples(10)
                                      .withAbsoluteTolerance(0.1);
                     else
                         engine = MakeMCForwardEuropeanBSEngine<LowDiscrepancy>(flatProcess())
                                      .withSteps(2)
                                      .withAbsoluteTolerance(0.1);
                     return json::value("built");
                 };
             }},
        });

    }

}
