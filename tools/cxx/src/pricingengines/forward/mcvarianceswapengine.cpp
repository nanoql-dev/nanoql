// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/forward/mcvarianceswapengine.hpp: VariancePathPricer on paths given by the caller, and the
// engine's and MakeMCVarianceSwapEngine's checks, which the wheel can't reach.
//
// "VariancePathPricer::value" takes (volatility, maturity, values): a Black-Scholes-Merton process on the evaluation
// date (spot 100, dividends 2%, rates 5%, Actual/365 (Fixed)) with a constant 25% volatility ("constant"), a variance
// curve of 20%, 25% and 22% at 91, 365 and 730 days ("curve") or a variance surface over strikes 80, 100 and 120 at
// those dates ("surface"), and the path's values on a uniform grid to the maturity (a step between each two values).
// It returns the pricer's value, the path's integrated local variance over its time.
//
// "MakeMCVarianceSwapEngine::check" takes (setting): "noSteps" (samples only), "overspecified" (steps and steps per
// year), "toleranceThenSamples", "samplesThenTolerance" or "lowDiscrepancyTolerance" (a tolerance on low-discrepancy
// numbers), each converted to an engine; "noTimeSteps", "bothTimeSteps", "zeroTimeSteps" or "zeroTimeStepsPerYear",
// the engine's constructor with those steps; each on the constant process. Its refusal is QuantLib's, its message the
// error's, and "built" where none comes.

#include "convert.hpp"

#include <ql/methods/montecarlo/path.hpp>
#include <ql/pricingengines/forward/mcvarianceswapengine.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/volatility/equityfx/blackvariancecurve.hpp>
#include <ql/termstructures/volatility/equityfx/blackvariancesurface.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>
#include <ql/timegrid.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        ext::shared_ptr<GeneralizedBlackScholesProcess> process(const std::string& volatility) {
            const Date today = Settings::instance().evaluationDate();
            const DayCounter dc = Actual365Fixed();
            const std::vector<Date> dates = {today + 91, today + 365, today + 730};
            ext::shared_ptr<BlackVolTermStructure> vol;
            if (volatility == "constant") {
                vol = ext::make_shared<BlackConstantVol>(today, NullCalendar(), 0.25, dc);
            } else if (volatility == "curve") {
                vol = ext::make_shared<BlackVarianceCurve>(today, dates, std::vector<Volatility>{0.20, 0.25, 0.22},
                                                           dc);
            } else {
                Matrix vols(3, 3);
                const Real v[3][3] = {{0.30, 0.28, 0.26}, {0.24, 0.23, 0.22}, {0.26, 0.24, 0.23}};
                for (Size i = 0; i < 3; ++i)
                    for (Size j = 0; j < 3; ++j)
                        vols[i][j] = v[i][j];
                vol = ext::make_shared<BlackVarianceSurface>(today, NullCalendar(), dates,
                                                             std::vector<Real>{80.0, 100.0, 120.0}, vols, dc);
            }
            return ext::make_shared<BlackScholesMertonProcess>(
                Handle<Quote>(ext::make_shared<SimpleQuote>(100.0)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, 0.02, dc)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, 0.05, dc)),
                Handle<BlackVolTermStructure>(vol));
        }

        const Register unit({
            {"VariancePathPricer::value",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 const std::string volatility = text(a[0]);
                 if (volatility != "constant" && volatility != "curve" && volatility != "surface")
                     throw InvalidParams("unknown volatility " + volatility);
                 const Time maturity = real(a[1]);
                 const std::vector<Real> values = reals(a[2]);
                 if (values.size() < 2)
                     throw InvalidParams("a path of two values at least");
                 return [=] {
                     VariancePathPricer pricer(process(volatility));
                     Path path(TimeGrid(maturity, values.size() - 1), Array(values.begin(), values.end()));
                     return json::value(pricer(path));
                 };
             }},
            {"MakeMCVarianceSwapEngine::check",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 const std::string setting = text(a[0]);
                 if (setting != "noSteps" && setting != "overspecified" && setting != "toleranceThenSamples" &&
                     setting != "samplesThenTolerance" && setting != "lowDiscrepancyTolerance" &&
                     setting != "noTimeSteps" && setting != "bothTimeSteps" && setting != "zeroTimeSteps" &&
                     setting != "zeroTimeStepsPerYear")
                     throw InvalidParams("unknown setting " + setting);
                 return [=] {
                     const auto p = process("constant");
                     ext::shared_ptr<PricingEngine> engine;
                     if (setting == "noSteps")
                         engine = MakeMCVarianceSwapEngine<PseudoRandom>(p).withSamples(10);
                     else if (setting == "overspecified")
                         engine = MakeMCVarianceSwapEngine<PseudoRandom>(p)
                                      .withSteps(2)
                                      .withStepsPerYear(4)
                                      .withSamples(10);
                     else if (setting == "toleranceThenSamples")
                         engine = MakeMCVarianceSwapEngine<PseudoRandom>(p)
                                      .withSteps(2)
                                      .withAbsoluteTolerance(0.1)
                                      .withSamples(10);
                     else if (setting == "samplesThenTolerance")
                         engine = MakeMCVarianceSwapEngine<PseudoRandom>(p)
                                      .withSteps(2)
                                      .withSamples(10)
                                      .withAbsoluteTolerance(0.1);
                     else if (setting == "lowDiscrepancyTolerance")
                         engine = MakeMCVarianceSwapEngine<LowDiscrepancy>(p).withSteps(2).withAbsoluteTolerance(0.1);
                     else if (setting == "noTimeSteps")
                         engine = ext::make_shared<MCVarianceSwapEngine<PseudoRandom>>(
                             p, Null<Size>(), Null<Size>(), false, false, 10, Null<Real>(), Null<Size>(), 42);
                     else if (setting == "bothTimeSteps")
                         engine = ext::make_shared<MCVarianceSwapEngine<PseudoRandom>>(
                             p, 4, 12, false, false, 10, Null<Real>(), Null<Size>(), 42);
                     else if (setting == "zeroTimeSteps")
                         engine = ext::make_shared<MCVarianceSwapEngine<PseudoRandom>>(
                             p, 0, Null<Size>(), false, false, 10, Null<Real>(), Null<Size>(), 42);
                     else
                         engine = ext::make_shared<MCVarianceSwapEngine<PseudoRandom>>(
                             p, Null<Size>(), 0, false, false, 10, Null<Real>(), Null<Size>(), 42);
                     return json::value("built");
                 };
             }},
        });

    }

}
