// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/finitedifferences/utilities/fdmshoutloginnervaluecalculator.hpp, .cpp: the shout option's exercise value,
// which the wheel doesn't expose. "FdmShoutLogInnerValueCalculator::innerValue" takes (today, spot, r, q, vol,
// dividends, maturity, payoff, xGrid, times): flat Act/365 (Fixed) curves and a constant volatility from today (a
// serial); the dividends [[serial, amount], ...], fixed; the maturity a time; the payoff [optionType, strike], plain
// vanilla; the mesher FdBlackScholesShoutEngine builds on them (fdblackscholesshoutengine.cpp:64-90), the escrowed
// adjustment's; and returns {"innerValue", "avgInnerValue"}, at each time the values at every point of the layout.

#include "convert.hpp"

#include <ql/cashflows/dividend.hpp>
#include <ql/instruments/payoffs.hpp>
#include <ql/methods/finitedifferences/meshers/fdmblackscholesmesher.hpp>
#include <ql/methods/finitedifferences/meshers/fdmmeshercomposite.hpp>
#include <ql/methods/finitedifferences/operators/fdmlinearoplayout.hpp>
#include <ql/methods/finitedifferences/utilities/escroweddividendadjustment.hpp>
#include <ql/methods/finitedifferences/utilities/fdmquantohelper.hpp>
#include <ql/methods/finitedifferences/utilities/fdmshoutloginnervaluecalculator.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"FdmShoutLogInnerValueCalculator::innerValue",
             [](const Args& a) -> Call {
                 arity(a, 10);
                 const Date today = makeDate(date(a[0]));
                 const Real spot = real(a[1]), r = real(a[2]), q = real(a[3]), vol = real(a[4]);
                 std::vector<Date> dividendDates;
                 std::vector<Real> dividendAmounts;
                 if (!a[5].is_array())
                     throw InvalidParams("expected dividends, got " + json::serialize(a[5]));
                 for (const json::value& d : a[5].get_array()) {
                     if (!d.is_array() || d.get_array().size() != 2)
                         throw InvalidParams("a dividend is [serial, amount]");
                     dividendDates.push_back(makeDate(date(d.get_array()[0])));
                     dividendAmounts.push_back(real(d.get_array()[1]));
                 }
                 const Time maturity = real(a[6]);
                 const std::vector<Real> payoffArgs = reals(a[7]);
                 if (payoffArgs.size() != 2)
                     throw InvalidParams("the payoff is [optionType, strike]");
                 const Integer xGrid = integer(a[8]);
                 const std::vector<Real> times = reals(a[9]);
                 return [=] {
                     const DayCounter dc = Actual365Fixed();
                     const auto process = ext::make_shared<BlackScholesMertonProcess>(
                         Handle<Quote>(ext::make_shared<SimpleQuote>(spot)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, q, dc)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, r, dc)),
                         Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(today, NullCalendar(), vol, dc)));
                     const auto payoff =
                         ext::make_shared<PlainVanillaPayoff>(Option::Type(Integer(payoffArgs[0])), payoffArgs[1]);
                     const DividendSchedule dividends = DividendVector(dividendDates, dividendAmounts);
                     const auto adjustment = ext::make_shared<EscrowedDividendAdjustment>(
                         dividends, process->riskFreeRate(), process->dividendYield(),
                         [&](Date d) { return process->time(d); }, maturity);
                     const Real divAdj = adjustment->dividendAdjustment(process->time(today));
                     const auto mesher = ext::make_shared<FdmMesherComposite>(ext::make_shared<FdmBlackScholesMesher>(
                         xGrid, process, maturity, payoff->strike(), Null<Real>(), Null<Real>(), 0.0001, 1.5,
                         std::pair<Real, Real>(payoff->strike(), 0.1), DividendSchedule(),
                         ext::shared_ptr<FdmQuantoHelper>(), divAdj));
                     FdmShoutLogInnerValueCalculator calculator(process->blackVolatility(), adjustment, maturity,
                                                                payoff, mesher, 0);
                     json::array inner, avg;
                     for (Time t : times) {
                         json::array i1, a1;
                         for (const auto& iter : *mesher->layout()) {
                             i1.emplace_back(calculator.innerValue(iter, t));
                             a1.emplace_back(calculator.avgInnerValue(iter, t));
                         }
                         inner.emplace_back(i1);
                         avg.emplace_back(a1);
                     }
                     return json::value(json::object{{"innerValue", inner}, {"avgInnerValue", avg}});
                 };
             }},
        });

    }

}
