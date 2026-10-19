// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/instruments/bond.hpp and ql/instruments/bonds/fixedratebond.hpp: the bonds the wheel can't build (on QuantLib's
// empty calendar or day counter, a fixed-rate bond's first-period day counter) and the members it can't reach (the
// theoretical yield's guess and price type, FixedRateBond's firstPeriodDayCounter), on bond specs of
// instruments/bondspec.hpp, each bond built inside the call. A member reading the engine's results prices the bond
// with a DiscountingBondEngine on a curve spec of indexes/indexspec.hpp, its includeSettlementDateFlows given (null for
// none).
//
//     Bond::settlementDays, notionals, cashflowDates, redemptionDates, redemptionAmounts, startDate, maturityDate,
//     issueDate, isExpired                         (bond): the inspector's value
//     Bond::cashflowAmount                         (bond, i): the amount of the i-th cash flow
//     Bond::redemption().date, Bond::redemption().amount
//                                                  (bond): the single redemption's date and amount
//     Bond::settlementDate, notional, isTradable, accruedAmount, nextCouponRate, previousCouponRate,
//     nextCashFlowDate, previousCashFlowDate       (bond, date): the member at the date (null for Date())
//     Bond::cleanPrice(Rate,DayCounter,Compounding,Frequency,Date), Bond::dirtyPrice(...)
//                                                  (bond, rate, settlementDate): the price for the rate's yield
//     Bond::yield(Price,DayCounter,Compounding,Frequency,Date,Real,Size,Real)
//                                                  (bond, price, dayCounter, compounding, frequency, settlementDate,
//                                                  accuracy, maxEvaluations, guess)
//     Bond::settlementValue(), cleanPrice(), dirtyPrice()
//                                                  (bond, curve, includeSettlementDateFlows): the priced bond's member
//                                                  (the NPV, error estimate and valuation date are
//                                                  pricingengines/bond/discountingbondengine.cpp's)
//     Bond::settlementValue(Real)                  (bond, curve, includeSettlementDateFlows, cleanPrice)
//     Bond::yield(DayCounter,Compounding,Frequency,Real,Size,Real,Price::Type)
//                                                  (bond, curve, includeSettlementDateFlows, dayCounter, compounding,
//                                                  frequency, accuracy, maxEvaluations, guess, priceType): the
//                                                  theoretical yield
//     FixedRateBond::frequency, dayCounter, firstPeriodDayCounter
//                                                  (bond): the inspector, a day counter by its name (null for
//                                                  the empty one)

#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "instruments/bondspec.hpp"

#include <ql/instruments/bond.hpp>
#include <ql/instruments/bonds/fixedratebond.hpp>

#include <cstdint>
#include <optional>

using namespace QuantLib;

namespace harness {

    namespace {

        using bonds::BondSpec;

        Size size(const json::value& v) {
            Integer n = integer(v);
            if (n == Null<Integer>() || n < 0)
                throw InvalidParams("expected a Size, got " + json::serialize(v));
            return static_cast<Size>(n);
        }

        json::value dates(const Leg& leg) {
            json::array out;
            for (const auto& c : leg)
                out.push_back(result(c->date()));
            return out;
        }

        // A member of the bond: f(bond).
        template <class F>
        Call onBond(const Args& a, F f) {
            arity(a, 1);
            BondSpec s = bonds::bond(a[0]);
            return [=] { return json::value(f(*bonds::makeBond(s))); };
        }

        // A member of the bond at a date: f(bond, date).
        template <class F>
        Call atDate(const Args& a, F f) {
            arity(a, 2);
            BondSpec s = bonds::bond(a[0]);
            DateSpec d = date(a[1]);
            return [=] { return json::value(f(*bonds::makeBond(s), makeDate(d))); };
        }

        // A member of the bond priced on the curve: f(bond, args), its first three arguments the bond, the curve and
        // the engine's includeSettlementDateFlows.
        template <class F>
        Call priced(const Args& a, std::size_t n, F f) {
            arity(a, n);
            BondSpec s = bonds::bond(a[0]);
            indexes::CurveSpec curve = indexes::curve(a[1]);
            std::optional<bool> include = optionalBoolean(a[2]);
            return [=] { return json::value(f(*bonds::makePricedBond(s, curve, include))); };
        }

        const FixedRateBond& fixedRate(const Bond& b) {
            const auto* f = dynamic_cast<const FixedRateBond*>(&b);
            if (f == nullptr)
                throw InvalidParams("the bond isn't a FixedRateBond");
            return *f;
        }

        // A day counter's name, or null for QuantLib's empty one.
        json::value name(const DayCounter& dc) { return dc.empty() ? json::value(nullptr) : json::value(dc.name()); }

        const Register unit({
            {"Bond::settlementDays",
             [](const Args& a) {
                 return onBond(a, [](const Bond& b) { return static_cast<std::int64_t>(b.settlementDays()); });
             }},
            {"Bond::notionals",
             [](const Args& a) { return onBond(a, [](const Bond& b) { return bonds::realArray(b.notionals()); }); }},
            {"Bond::cashflowDates",
             [](const Args& a) { return onBond(a, [](const Bond& b) { return dates(b.cashflows()); }); }},
            {"Bond::cashflowAmount",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 BondSpec s = bonds::bond(a[0]);
                 Size i = size(a[1]);
                 return [=] {
                     auto bond = bonds::makeBond(s);
                     if (i >= bond->cashflows().size())
                         throw InvalidParams("a position outside the bond's cash flows");
                     return json::value(bond->cashflows()[i]->amount());
                 };
             }},
            {"Bond::redemptionDates",
             [](const Args& a) { return onBond(a, [](const Bond& b) { return dates(b.redemptions()); }); }},
            {"Bond::redemptionAmounts",
             [](const Args& a) {
                 return onBond(a, [](const Bond& b) {
                     std::vector<Real> out;
                     for (const auto& c : b.redemptions())
                         out.push_back(c->amount());
                     return bonds::realArray(out);
                 });
             }},
            {"Bond::redemption().date",
             [](const Args& a) { return onBond(a, [](const Bond& b) { return result(b.redemption()->date()); }); }},
            {"Bond::redemption().amount",
             [](const Args& a) { return onBond(a, [](const Bond& b) { return b.redemption()->amount(); }); }},
            {"Bond::startDate",
             [](const Args& a) { return onBond(a, [](const Bond& b) { return result(b.startDate()); }); }},
            {"Bond::maturityDate",
             [](const Args& a) { return onBond(a, [](const Bond& b) { return result(b.maturityDate()); }); }},
            {"Bond::issueDate",
             [](const Args& a) { return onBond(a, [](const Bond& b) { return result(b.issueDate()); }); }},
            {"Bond::isExpired", [](const Args& a) { return onBond(a, [](const Bond& b) { return b.isExpired(); }); }},
            {"Bond::settlementDate",
             [](const Args& a) {
                 return atDate(a, [](const Bond& b, const Date& d) { return result(b.settlementDate(d)); });
             }},
            {"Bond::notional",
             [](const Args& a) { return atDate(a, [](const Bond& b, const Date& d) { return b.notional(d); }); }},
            {"Bond::isTradable",
             [](const Args& a) { return atDate(a, [](const Bond& b, const Date& d) { return b.isTradable(d); }); }},
            {"Bond::accruedAmount",
             [](const Args& a) { return atDate(a, [](const Bond& b, const Date& d) { return b.accruedAmount(d); }); }},
            {"Bond::nextCouponRate",
             [](const Args& a) { return atDate(a, [](const Bond& b, const Date& d) { return b.nextCouponRate(d); }); }},
            {"Bond::previousCouponRate",
             [](const Args& a) {
                 return atDate(a, [](const Bond& b, const Date& d) { return b.previousCouponRate(d); });
             }},
            {"Bond::nextCashFlowDate",
             [](const Args& a) {
                 return atDate(a, [](const Bond& b, const Date& d) { return result(b.nextCashFlowDate(d)); });
             }},
            {"Bond::previousCashFlowDate",
             [](const Args& a) {
                 return atDate(a, [](const Bond& b, const Date& d) { return result(b.previousCashFlowDate(d)); });
             }},
            {"Bond::cleanPrice(Rate,DayCounter,Compounding,Frequency,Date)",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 BondSpec s = bonds::bond(a[0]);
                 bonds::RateSpec r = bonds::rate(a[1]);
                 DateSpec d = date(a[2]);
                 return [=] {
                     InterestRate y = bonds::makeRate(r);
                     return json::value(bonds::makeBond(s)->cleanPrice(y.rate(), y.dayCounter(), y.compounding(),
                                                                         y.frequency(), makeDate(d)));
                 };
             }},
            {"Bond::dirtyPrice(Rate,DayCounter,Compounding,Frequency,Date)",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 BondSpec s = bonds::bond(a[0]);
                 bonds::RateSpec r = bonds::rate(a[1]);
                 DateSpec d = date(a[2]);
                 return [=] {
                     InterestRate y = bonds::makeRate(r);
                     return json::value(bonds::makeBond(s)->dirtyPrice(y.rate(), y.dayCounter(), y.compounding(),
                                                                         y.frequency(), makeDate(d)));
                 };
             }},
            {"Bond::yield(Price,DayCounter,Compounding,Frequency,Date,Real,Size,Real)",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 BondSpec s = bonds::bond(a[0]);
                 bonds::PriceSpec p = bonds::price(a[1]);
                 DayCounterSpec dc = dayCounter(a[2]);
                 auto comp = enumeration<Compounding>(a[3]);
                 auto freq = enumeration<Frequency>(a[4]);
                 DateSpec d = date(a[5]);
                 Real accuracy = real(a[6]);
                 Size maxEvaluations = size(a[7]);
                 Real guess = real(a[8]);
                 return [=] {
                     return json::value(bonds::makeBond(s)->yield(bonds::makePrice(p), makeDayCounter(dc), comp, freq,
                                                                  makeDate(d), accuracy, maxEvaluations, guess));
                 };
             }},
            {"Bond::settlementValue()",
             [](const Args& a) { return priced(a, 3, [](const Bond& b) { return b.settlementValue(); }); }},
            {"Bond::cleanPrice()",
             [](const Args& a) { return priced(a, 3, [](const Bond& b) { return b.cleanPrice(); }); }},
            {"Bond::dirtyPrice()",
             [](const Args& a) { return priced(a, 3, [](const Bond& b) { return b.dirtyPrice(); }); }},
            {"Bond::settlementValue(Real)",
             [](const Args& a) {
                 arity(a, 4);
                 Real clean = real(a[3]);
                 return priced(a, 4, [clean](const Bond& b) { return b.settlementValue(clean); });
             }},
            {"Bond::yield(DayCounter,Compounding,Frequency,Real,Size,Real,Price::Type)",
             [](const Args& a) -> Call {
                 arity(a, 10);
                 DayCounterSpec dc = dayCounter(a[3]);
                 auto comp = enumeration<Compounding>(a[4]);
                 auto freq = enumeration<Frequency>(a[5]);
                 Real accuracy = real(a[6]);
                 Size maxEvaluations = size(a[7]);
                 Real guess = real(a[8]);
                 auto priceType = enumeration<Bond::Price::Type>(a[9]);
                 return priced(a, 10, [=](const Bond& b) {
                     return b.yield(makeDayCounter(dc), comp, freq, accuracy, maxEvaluations, guess, priceType);
                 });
             }},
            {"FixedRateBond::frequency",
             [](const Args& a) {
                 return onBond(a, [](const Bond& b) { return static_cast<std::int64_t>(fixedRate(b).frequency()); });
             }},
            {"FixedRateBond::dayCounter",
             [](const Args& a) { return onBond(a, [](const Bond& b) { return name(fixedRate(b).dayCounter()); }); }},
            {"FixedRateBond::firstPeriodDayCounter",
             [](const Args& a) {
                 return onBond(a, [](const Bond& b) { return name(fixedRate(b).firstPeriodDayCounter()); });
             }},
        });

    }

}
