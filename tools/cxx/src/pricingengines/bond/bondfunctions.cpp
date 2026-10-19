// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/bond/bondfunctions.hpp: the BondFunctions members the wheel doesn't bind (the reference periods,
// the positions previousCashFlow and nextCashFlow return, and the yield-based dirty price in both forms), on bond specs
// of instruments/bondspec.hpp, each bond built inside the call. A position is an index into the bond's cash flows:
// previousCashFlow's rend() is -1 and nextCashFlow's end() the number of cash flows.
//
//     BondFunctions::referencePeriodStart, referencePeriodEnd
//                                                  (bond, settlementDate): the date (null for Date())
//     BondFunctions::previousCashFlow, nextCashFlow
//                                                  (bond, refDate): the position
//     BondFunctions::dirtyPrice(Bond,InterestRate,Date)
//                                                  (bond, rate, settlementDate)
//     BondFunctions::dirtyPrice(Bond,Rate,DayCounter,Compounding,Frequency,Date)
//                                                  (bond, yield, dayCounter, compounding, frequency, settlementDate)

#include "convert.hpp"
#include "instruments/bondspec.hpp"

#include <ql/pricingengines/bond/bondfunctions.hpp>

#include <cstdint>

using namespace QuantLib;

namespace harness {

    namespace {

        using bonds::BondSpec;

        // A function of the bond at a date: f(bond, date).
        template <class F>
        Call atDate(const Args& a, F f) {
            arity(a, 2);
            BondSpec s = bonds::bond(a[0]);
            DateSpec d = date(a[1]);
            return [=] { return json::value(f(*bonds::makeBond(s), makeDate(d))); };
        }

        const Register unit({
            {"BondFunctions::referencePeriodStart",
             [](const Args& a) {
                 return atDate(a, [](const Bond& b, const Date& d) {
                     return result(BondFunctions::referencePeriodStart(b, d));
                 });
             }},
            {"BondFunctions::referencePeriodEnd",
             [](const Args& a) {
                 return atDate(a, [](const Bond& b, const Date& d) {
                     return result(BondFunctions::referencePeriodEnd(b, d));
                 });
             }},
            {"BondFunctions::previousCashFlow",
             [](const Args& a) {
                 return atDate(a, [](const Bond& b, const Date& d) {
                     const Leg& leg = b.cashflows();
                     auto i = BondFunctions::previousCashFlow(b, d);
                     return static_cast<std::int64_t>(leg.size()) - 1 - static_cast<std::int64_t>(i - leg.rbegin());
                 });
             }},
            {"BondFunctions::nextCashFlow",
             [](const Args& a) {
                 return atDate(a, [](const Bond& b, const Date& d) {
                     const Leg& leg = b.cashflows();
                     return static_cast<std::int64_t>(BondFunctions::nextCashFlow(b, d) - leg.begin());
                 });
             }},
            {"BondFunctions::dirtyPrice(Bond,InterestRate,Date)",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 BondSpec s = bonds::bond(a[0]);
                 bonds::RateSpec r = bonds::rate(a[1]);
                 DateSpec d = date(a[2]);
                 return [=] {
                     return json::value(
                         BondFunctions::dirtyPrice(*bonds::makeBond(s), bonds::makeRate(r), makeDate(d)));
                 };
             }},
            {"BondFunctions::dirtyPrice(Bond,Rate,DayCounter,Compounding,Frequency,Date)",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 BondSpec s = bonds::bond(a[0]);
                 Real yield = real(a[1]);
                 DayCounterSpec dc = dayCounter(a[2]);
                 auto comp = enumeration<Compounding>(a[3]);
                 auto freq = enumeration<Frequency>(a[4]);
                 DateSpec d = date(a[5]);
                 return [=] {
                     return json::value(BondFunctions::dirtyPrice(*bonds::makeBond(s), yield, makeDayCounter(dc), comp,
                                                                  freq, makeDate(d)));
                 };
             }},
        });

    }

}
