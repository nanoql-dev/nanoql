// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/asian/mcdiscreteasianenginebase.hpp, whose grid the wheel doesn't expose.
// "MCDiscreteAveragingAsianEngineBase.timeGrid" takes (fixingDays, exerciseDays, steps, stepsPerYear,
// includeExerciseDate): an arithmetic option fixing that many days from the evaluation date and exercised on that day,
// its arguments set up on an engine deriving the base on a Black-Scholes-Merton process at 100 on flat forwards of 3%
// and 1% and a constant volatility of 20% from the evaluation date on Act/365, steps and steps per year null for none;
// the grid the engine's timeGrid() builds, its times and mandatory times, or its refusal.

#include "convert.hpp"

#include <ql/instruments/asianoption.hpp>
#include <ql/pricingengines/asian/mcdiscreteasianenginebase.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

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

        // the base with a path pricer it never builds, its protected grid made public
        class GridEngine : public MCDiscreteAveragingAsianEngineBase<SingleVariate, PseudoRandom> {
          public:
            GridEngine(Size steps, Size stepsPerYear, bool includeExerciseDate)
            : MCDiscreteAveragingAsianEngineBase<SingleVariate, PseudoRandom>(
                  process(), false, false, false, 10, Null<Real>(), Null<Size>(), 42, steps, stepsPerYear,
                  includeExerciseDate) {}
            TimeGrid grid() const { return this->timeGrid(); }

          protected:
            ext::shared_ptr<path_pricer_type> pathPricer() const override { return {}; }
        };

        Size size(Integer n) { return n == Null<Integer>() ? Null<Size>() : static_cast<Size>(n); }

        const Register unit({
            {"MCDiscreteAveragingAsianEngineBase.timeGrid",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 std::vector<Integer> fixingDays = integers(a[0]);
                 Integer exerciseDays = integer(a[1]);
                 Integer steps = integer(a[2]);
                 Integer stepsPerYear = integer(a[3]);
                 bool include = boolean(a[4]);
                 return [=] {
                     Date today = Settings::instance().evaluationDate();
                     std::vector<Date> fixings;
                     for (Integer d : fixingDays)
                         fixings.push_back(today + d);
                     DiscreteAveragingAsianOption option(
                         Average::Arithmetic, 0.0, 0, fixings,
                         ext::make_shared<PlainVanillaPayoff>(Option::Call, 100.0),
                         ext::make_shared<EuropeanExercise>(today + exerciseDays));
                     GridEngine engine(size(steps), size(stepsPerYear), include);
                     option.setupArguments(engine.getArguments());
                     TimeGrid grid = engine.grid();
                     json::array times, mandatory;
                     for (Size i = 0; i < grid.size(); ++i)
                         times.emplace_back(grid[i]);
                     for (Real t : grid.mandatoryTimes())
                         mandatory.emplace_back(t);
                     json::object out;
                     out["times"] = times;
                     out["mandatoryTimes"] = mandatory;
                     return json::value(out);
                 };
             }},
        });

    }

}
