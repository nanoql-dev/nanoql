// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/finitedifferences/utilities/fdmescrowedloginnervaluecalculator.hpp, .cpp, which the wheel doesn't expose:
// innerValue and avgInnerValue at every point of the mesher. "FdmEscrowedLogInnerValueCalculator::values" takes
// (riskFreeRate, dividendYield, dividends, maturity, optionType, strike, xMin, xMax, size, times): the escrowed
// dividend adjustment of fixed dividends ([date serial, amount] pairs) over flat forwards from the evaluation date on
// Act/365, the dates' times their Act/365 year fractions from it, to the maturity; a plain vanilla payoff; a uniform
// log mesher of size points over [xMin, xMax]; direction 0. It returns {"innerValue", "avgInnerValue"}, at each time
// the values at every point.

#include "convert.hpp"

#include <ql/cashflows/dividend.hpp>
#include <ql/instruments/payoffs.hpp>
#include <ql/methods/finitedifferences/meshers/fdmmeshercomposite.hpp>
#include <ql/methods/finitedifferences/meshers/uniform1dmesher.hpp>
#include <ql/methods/finitedifferences/operators/fdmlinearoplayout.hpp>
#include <ql/methods/finitedifferences/utilities/escroweddividendadjustment.hpp>
#include <ql/methods/finitedifferences/utilities/fdmescrowedloginnervaluecalculator.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <utility>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"FdmEscrowedLogInnerValueCalculator::values",
             [](const Args& a) -> Call {
                 arity(a, 10);
                 Real r = real(a[0]), q = real(a[1]);
                 if (!a[2].is_array())
                     throw InvalidParams("expected [date, amount] pairs, got " + json::serialize(a[2]));
                 std::vector<std::pair<DateSpec, Real>> dividends;
                 for (const json::value& d : a[2].get_array()) {
                     if (!d.is_array() || d.get_array().size() != 2)
                         throw InvalidParams("expected a [date, amount] pair, got " + json::serialize(d));
                     dividends.emplace_back(date(d.get_array()[0]), real(d.get_array()[1]));
                 }
                 Time maturity = real(a[3]);
                 auto type = enumeration<Option::Type>(a[4]);
                 Real strike = real(a[5]), xMin = real(a[6]), xMax = real(a[7]);
                 Integer size = integer(a[8]);
                 std::vector<Real> times = reals(a[9]);
                 if (size < 2)
                     throw InvalidParams("a mesher of at least 2 points");
                 return [=] {
                     Date today = Settings::instance().evaluationDate();
                     DayCounter dc = Actual365Fixed();
                     DividendSchedule schedule;
                     for (const auto& d : dividends)
                         schedule.push_back(ext::make_shared<FixedDividend>(d.second, makeDate(d.first)));
                     auto adjustment = ext::make_shared<EscrowedDividendAdjustment>(
                         schedule, Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, r, dc)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, q, dc)),
                         [today, dc](Date d) { return dc.yearFraction(today, d); }, maturity);
                     auto mesher = ext::make_shared<FdmMesherComposite>(
                         ext::make_shared<Uniform1dMesher>(xMin, xMax, static_cast<Size>(size)));
                     FdmEscrowedLogInnerValueCalculator calculator(
                         adjustment, ext::make_shared<PlainVanillaPayoff>(type, strike), mesher, 0);
                     json::array inner, average;
                     for (Time t : times) {
                         json::array i, v;
                         for (const auto& iter : *mesher->layout()) {
                             i.emplace_back(calculator.innerValue(iter, t));
                             v.emplace_back(calculator.avgInnerValue(iter, t));
                         }
                         inner.emplace_back(i);
                         average.emplace_back(v);
                     }
                     return json::value(json::object{{"innerValue", inner}, {"avgInnerValue", average}});
                 };
             }},
        });

    }

}
