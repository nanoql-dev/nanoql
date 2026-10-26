// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/barrier/discretizedbarrieroption.hpp, .cpp, which the wheel doesn't expose: the two barrier assets'
// rollbacks and the plain asset's checkBarrier. Every call takes an option (referenceDate, barrierType, barrier, rebate,
// optionType, strike, exerciseType, dates) and a step count: a Black-Scholes-Merton process at 100 on flat forwards of
// 3% and 1% and a constant volatility of 20% from the reference date on Act/365, the plain vanilla payoff, the exercise
// "European" (one date), "American" (the earliest and latest dates) or "Bermudan" (its dates), and a Cox-Ross-Rubinstein
// Black-Scholes lattice at 3% over the last date's time in that many steps, its grid regular.
// "DiscretizedBarrierOption::rollback" adds (enhanced): the plain asset (false) or Derman and Kani's (true), initialized
// at the last date's time and rolled back to 0, answered as {"presentValue", "mandatoryTimes"}.
// "DiscretizedBarrierOption::checkBarrier" adds (step, values): the plain asset initialized at the grid's time of that
// step, then checkBarrier on the given values (one per node) and the lattice's grid there, answered as the values.

#include "convert.hpp"

#include <ql/exercise.hpp>
#include <ql/instruments/barrieroption.hpp>
#include <ql/methods/lattices/binomialtree.hpp>
#include <ql/methods/lattices/bsmlattice.hpp>
#include <ql/pricingengines/barrier/discretizedbarrieroption.hpp>
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
            Barrier::Type barrierType;
            Real barrier, rebate;
            Option::Type optionType;
            Real strike;
            std::string exerciseType;
            std::vector<DateSpec> dates;
            Size steps;
        };

        OptionSpec optionSpec(const Args& a) {
            OptionSpec s;
            s.reference = date(a[0]);
            s.barrierType = enumeration<Barrier::Type>(a[1]);
            s.barrier = real(a[2]);
            s.rebate = real(a[3]);
            s.optionType = enumeration<Option::Type>(a[4]);
            s.strike = real(a[5]);
            s.exerciseType = text(a[6]);
            if (!a[7].is_array())
                throw InvalidParams("expected a list of dates, got " + json::serialize(a[7]));
            for (const json::value& d : a[7].get_array())
                s.dates.push_back(date(d));
            const std::size_t needed = s.exerciseType == "European"   ? 1
                                       : s.exerciseType == "American" ? 2
                                                                      : s.dates.size();
            if (s.exerciseType != "European" && s.exerciseType != "American" && s.exerciseType != "Bermudan")
                throw InvalidParams("unknown exercise " + s.exerciseType);
            if (s.dates.size() != needed || s.dates.empty())
                throw InvalidParams("the " + s.exerciseType + " exercise takes " + std::to_string(needed) + " dates");
            Integer steps = integer(a[8]);
            if (steps <= 0)
                throw InvalidParams("expected a positive step count");
            s.steps = static_cast<Size>(steps);
            return s;
        }

        // The process, the option's arguments, the lattice and its maturity.
        struct Setup {
            ext::shared_ptr<BlackScholesMertonProcess> process;
            BarrierOption::arguments args;
            Time maturity;
            TimeGrid grid;
            ext::shared_ptr<BlackScholesLattice<CoxRossRubinstein>> lattice;
        };

        Setup setup(const OptionSpec& s) {
            Setup out;
            Date d = makeDate(s.reference);
            DayCounter c = Actual365Fixed();
            out.process = ext::make_shared<BlackScholesMertonProcess>(
                Handle<Quote>(ext::make_shared<SimpleQuote>(100.0)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, 0.01, c)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, 0.03, c)),
                Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(d, NullCalendar(), 0.2, c)));
            std::vector<Date> ds;
            for (const DateSpec& x : s.dates)
                ds.push_back(makeDate(x));
            out.args.barrierType = s.barrierType;
            out.args.barrier = s.barrier;
            out.args.rebate = s.rebate;
            out.args.payoff = ext::make_shared<PlainVanillaPayoff>(s.optionType, s.strike);
            if (s.exerciseType == "European")
                out.args.exercise = ext::make_shared<EuropeanExercise>(ds[0]);
            else if (s.exerciseType == "American")
                out.args.exercise = ext::make_shared<AmericanExercise>(ds[0], ds[1]);
            else
                out.args.exercise = ext::make_shared<BermudanExercise>(ds);
            out.maturity = out.process->time(ds.back());
            out.grid = TimeGrid(out.maturity, s.steps);
            auto tree = ext::make_shared<CoxRossRubinstein>(out.process, out.maturity, s.steps, s.strike);
            out.lattice = ext::make_shared<BlackScholesLattice<CoxRossRubinstein>>(tree, 0.03, out.maturity, s.steps);
            return out;
        }

        json::value times(const std::vector<Time>& ts) {
            json::array out;
            for (Time t : ts)
                out.emplace_back(t);
            return out;
        }

        const Register unit({
            {"DiscretizedBarrierOption::rollback",
             [](const Args& a) -> Call {
                 arity(a, 10);
                 OptionSpec s = optionSpec(a);
                 bool enhanced = boolean(a[9]);
                 return [=] {
                     Setup u = setup(s);
                     ext::shared_ptr<DiscretizedAsset> asset;
                     if (enhanced)
                         asset = ext::make_shared<DiscretizedDermanKaniBarrierOption>(u.args, *u.process, u.grid);
                     else
                         asset = ext::make_shared<DiscretizedBarrierOption>(u.args, *u.process, u.grid);
                     asset->initialize(u.lattice, u.maturity);
                     asset->rollback(0.0);
                     return json::value(json::object{{"presentValue", asset->presentValue()},
                                                     {"mandatoryTimes", times(asset->mandatoryTimes())}});
                 };
             }},
            {"DiscretizedBarrierOption::checkBarrier",
             [](const Args& a) -> Call {
                 arity(a, 11);
                 OptionSpec s = optionSpec(a);
                 Integer step = integer(a[9]);
                 std::vector<Real> given = reals(a[10]);
                 if (step < 0 || static_cast<Size>(step) > s.steps)
                     throw InvalidParams("the step is outside the grid");
                 return [=] {
                     Setup u = setup(s);
                     DiscretizedBarrierOption asset(u.args, *u.process, u.grid);
                     Time t = u.grid[step];
                     asset.initialize(u.lattice, t);
                     Array grid = u.lattice->grid(t);
                     QL_REQUIRE(given.size() == grid.size(), "expected " << grid.size() << " values");
                     Array values(given.begin(), given.end());
                     asset.checkBarrier(values, grid);
                     json::array out;
                     for (Real x : values)
                         out.emplace_back(x);
                     return json::value(out);
                 };
             }},
        });

    }

}
