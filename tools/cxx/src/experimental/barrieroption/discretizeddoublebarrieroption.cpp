// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/barrieroption/discretizeddoublebarrieroption.hpp, .cpp, whose plain asset the wheel doesn't expose
// (QuantLib-SWIG binds the binomial engine on the Derman-Kani asset only). Every call takes an option (referenceDate,
// spot, dividendYield, riskFreeRate, vol, barrierType, barrierLo, barrierHi, rebate, optionType, strike, exerciseType,
// dates) and a step count: a Black-Scholes-Merton process on flat forwards of the two rates and a constant volatility
// from the reference date on Act/365; the double barrier option on the plain vanilla payoff, its exercise "European"
// (one date), "American" (the earliest and latest dates) or "Bermudan" (its dates).
// "BinomialDoubleBarrierEngine": BinomialDoubleBarrierEngine<CoxRossRubinstein, DiscretizedDoubleBarrierOption> on that
// many steps, answered as {"NPV", "delta", "gamma", "theta"}.
// "DiscretizedDoubleBarrierOption::mandatoryTimes" adds (enhanced): the plain asset (false) or Derman and Kani's (true)
// on the option's arguments, the process and a regular grid over the last date's time in that many steps (none at 0
// steps), answered as its mandatory times.

#include "convert.hpp"

#include <ql/exercise.hpp>
#include <ql/experimental/barrieroption/binomialdoublebarrierengine.hpp>
#include <ql/experimental/barrieroption/discretizeddoublebarrieroption.hpp>
#include <ql/instruments/doublebarrieroption.hpp>
#include <ql/methods/lattices/binomialtree.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct OptionSpec {
            DateSpec reference;
            Real spot, q, r, vol;
            DoubleBarrier::Type barrierType;
            Real lo, hi, rebate;
            Option::Type optionType;
            Real strike;
            std::string exerciseType;
            std::vector<Integer> days;
            Integer steps;
        };

        OptionSpec optionSpec(const Args& a) {
            OptionSpec s;
            s.reference = date(a[0]);
            s.spot = real(a[1]);
            s.q = real(a[2]);
            s.r = real(a[3]);
            s.vol = real(a[4]);
            s.barrierType = enumeration<DoubleBarrier::Type>(a[5]);
            s.lo = real(a[6]);
            s.hi = real(a[7]);
            s.rebate = real(a[8]);
            s.optionType = enumeration<Option::Type>(a[9]);
            s.strike = real(a[10]);
            s.exerciseType = text(a[11]);
            s.days = integers(a[12]);
            s.steps = integer(a[13]);
            if (s.days.empty())
                throw InvalidParams("expected at least one exercise day");
            if (s.exerciseType != "European" && s.exerciseType != "American" && s.exerciseType != "Bermudan")
                throw InvalidParams("unknown exercise type " + s.exerciseType);
            return s;
        }

        struct Setup {
            ext::shared_ptr<BlackScholesMertonProcess> process;
            ext::shared_ptr<DoubleBarrierOption> option;
            Date last;
        };

        Setup setup(const OptionSpec& s) {
            Setup out;
            Date d = makeDate(s.reference);
            DayCounter c = Actual365Fixed();
            out.process = ext::make_shared<BlackScholesMertonProcess>(
                Handle<Quote>(ext::make_shared<SimpleQuote>(s.spot)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, s.q, c)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, s.r, c)),
                Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(d, NullCalendar(), s.vol, c)));
            std::vector<Date> dates;
            for (Integer n : s.days)
                dates.push_back(d + n);
            ext::shared_ptr<Exercise> exercise;
            if (s.exerciseType == "European")
                exercise = ext::make_shared<EuropeanExercise>(dates.back());
            else if (s.exerciseType == "American")
                exercise = ext::make_shared<AmericanExercise>(dates.front(), dates.back());
            else
                exercise = ext::make_shared<BermudanExercise>(dates);
            out.option = ext::make_shared<DoubleBarrierOption>(
                s.barrierType, s.lo, s.hi, s.rebate, ext::make_shared<PlainVanillaPayoff>(s.optionType, s.strike),
                exercise);
            out.last = dates.back();
            return out;
        }

        const Register unit({
            {"BinomialDoubleBarrierEngine",
             [](const Args& a) -> Call {
                 arity(a, 14);
                 OptionSpec s = optionSpec(a);
                 return [=] {
                     Setup u = setup(s);
                     u.option->setPricingEngine(ext::make_shared<
                         BinomialDoubleBarrierEngine<CoxRossRubinstein, DiscretizedDoubleBarrierOption>>(
                             u.process, static_cast<Size>(s.steps)));
                     json::object o;
                     o["NPV"] = json::value(u.option->NPV());
                     o["delta"] = json::value(u.option->delta());
                     o["gamma"] = json::value(u.option->gamma());
                     o["theta"] = json::value(u.option->theta());
                     return json::value(o);
                 };
             }},
            {"DiscretizedDoubleBarrierOption::mandatoryTimes",
             [](const Args& a) -> Call {
                 arity(a, 15);
                 OptionSpec s = optionSpec(a);
                 bool enhanced = boolean(a[14]);
                 if (s.steps < 0)
                     throw InvalidParams("expected a step count of at least 0");
                 return [=] {
                     Setup u = setup(s);
                     DoubleBarrierOption::arguments args;
                     u.option->setupArguments(&args);
                     TimeGrid grid = s.steps > 0 ? TimeGrid(u.process->time(u.last), static_cast<Size>(s.steps))
                                                 : TimeGrid();
                     ext::shared_ptr<DiscretizedAsset> asset;
                     if (enhanced)
                         asset = ext::make_shared<DiscretizedDermanKaniDoubleBarrierOption>(args, *u.process, grid);
                     else
                         asset = ext::make_shared<DiscretizedDoubleBarrierOption>(args, *u.process, grid);
                     json::array out;
                     for (Time t : asset->mandatoryTimes())
                         out.emplace_back(t);
                     return json::value(out);
                 };
             }},
        });

    }

}
