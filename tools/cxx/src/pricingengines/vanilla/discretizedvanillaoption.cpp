// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/vanilla/discretizedvanillaoption.hpp, .cpp, which the wheel doesn't expose: mandatoryTimes, the
// stopping times the constructor reads from the exercise dates through the process and moves to the grid's closest.
// "DiscretizedVanillaOption::mandatoryTimes" takes (referenceDate, dayCounter, exerciseType, dates, grid): a
// Black-Scholes-Merton process on flat forwards of 3% and 1% and a constant volatility of 20% from the reference date
// on the day counter, a plain vanilla call struck at 100, the exercise "European" (one date), "American" (the earliest
// and latest dates) or "Bermudan" (its dates), and a grid as timegrid.hpp's spec.

#include "convert.hpp"
#include "timegrid.hpp"

#include <ql/exercise.hpp>
#include <ql/instruments/vanillaoption.hpp>
#include <ql/pricingengines/vanilla/discretizedvanillaoption.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"DiscretizedVanillaOption::mandatoryTimes",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 DateSpec reference = date(a[0]);
                 DayCounterSpec dc = dayCounter(a[1]);
                 std::string type = text(a[2]);
                 if (!a[3].is_array())
                     throw InvalidParams("expected a list of dates, got " + json::serialize(a[3]));
                 std::vector<DateSpec> dates;
                 for (const json::value& d : a[3].get_array())
                     dates.push_back(date(d));
                 const std::size_t needed = type == "European" ? 1 : type == "American" ? 2 : dates.size();
                 if (type != "European" && type != "American" && type != "Bermudan")
                     throw InvalidParams("unknown exercise " + type);
                 if (dates.size() != needed || dates.empty())
                     throw InvalidParams("the " + type + " exercise takes " + std::to_string(needed) + " dates");
                 TimeGridSpec grid = timeGrid(a[4]);
                 return [=] {
                     Date d = makeDate(reference);
                     DayCounter c = makeDayCounter(dc);
                     BlackScholesMertonProcess process(
                         Handle<Quote>(ext::make_shared<SimpleQuote>(100.0)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, 0.01, c)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, 0.03, c)),
                         Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(d, NullCalendar(), 0.2, c)));
                     std::vector<Date> ds;
                     for (const DateSpec& x : dates)
                         ds.push_back(makeDate(x));
                     VanillaOption::arguments args;
                     args.payoff = ext::make_shared<PlainVanillaPayoff>(Option::Call, 100.0);
                     if (type == "European")
                         args.exercise = ext::make_shared<EuropeanExercise>(ds[0]);
                     else if (type == "American")
                         args.exercise = ext::make_shared<AmericanExercise>(ds[0], ds[1]);
                     else
                         args.exercise = ext::make_shared<BermudanExercise>(ds);
                     DiscretizedVanillaOption option(args, process, makeTimeGrid(grid));
                     json::array out;
                     for (Time t : option.mandatoryTimes())
                         out.emplace_back(t);
                     return json::value(out);
                 };
             }},
        });

    }

}
