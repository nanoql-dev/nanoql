// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/callablebonds/callablebond.hpp, .cpp: the callable bonds the wheel can't build (on QuantLib's empty
// calendar or payment day counter) and the engines it can't name (the zero-coupon engines, the Black engine on a
// volatility structure), on the specs of callablebondspec.hpp, each bond and engine built inside the call.
//
//     CallableBond::callabilityDates, callabilityTypes, callabilityPrices, cashflowDates, cashflowAmounts,
//     settlementDate, maturityDate, accruedAmount  (bond): the inspector's values, a list for the schedule's and the
//                                                  cash flows'
//     CallableBond::NPV, settlementValue, cleanPrice, dirtyPrice
//                                                  (bond, engine): the bond priced by the engine
//     CallableBond::OAS                            (bond, engine, cleanPrice, curve, dayCounter, compounding,
//                                                  frequency, settlementDate, accuracy, maxIterations, guess)
//     CallableBond::cleanPriceOAS                  (bond, engine, oas, curve, dayCounter, compounding, frequency,
//                                                  settlementDate)
//     CallableBond::effectiveDuration, effectiveConvexity
//                                                  (bond, engine, oas, curve, dayCounter, compounding, frequency, bump)
//     CallableBond::impliedVolatility              (bond, price, curve, accuracy, maxEvaluations, minVol, maxVol)

#include "convert.hpp"
#include "experimental/callablebonds/callablebondspec.hpp"
#include "instruments/bondspec.hpp"

#include <ql/experimental/callablebonds/callablebond.hpp>
#include <ql/handle.hpp>
#include <ql/utilities/null.hpp>

#include <cstdint>

using namespace QuantLib;

namespace harness {

    namespace {

        using callablebonds::CallableBondSpec;
        using callablebonds::EngineSpec;

        Size size(const json::value& v) {
            Integer n = integer(v);
            if (n == Null<Integer>() || n < 0)
                throw InvalidParams("expected a Size, got " + json::serialize(v));
            return static_cast<Size>(n);
        }

        // A member of the bond: f(bond).
        template <class F>
        Call onBond(const Args& a, F f) {
            arity(a, 1);
            CallableBondSpec s = callablebonds::bond(a[0]);
            return [=] { return json::value(f(*callablebonds::makeBond(s))); };
        }

        // A member of the bond priced by the engine: f(bond).
        template <class F>
        Call priced(const Args& a, F f) {
            arity(a, 2);
            CallableBondSpec s = callablebonds::bond(a[0]);
            EngineSpec e = callablebonds::engine(a[1]);
            return [=] { return json::value(f(*callablebonds::makePricedBond(s, e))); };
        }

        // The spread conventions: (curve, dayCounter, compounding, frequency) from position i.
        struct Conventions {
            models::CurveSpec curve;
            DayCounterSpec dayCounter;
            Compounding compounding;
            Frequency frequency;
        };

        Conventions conventions(const Args& a, std::size_t i) {
            return {models::curve(a[i]), dayCounter(a[i + 1]), enumeration<Compounding>(a[i + 2]),
                    enumeration<Frequency>(a[i + 3])};
        }

        Handle<YieldTermStructure> handle(const Conventions& c) {
            return Handle<YieldTermStructure>(models::makeCurve(c.curve));
        }

        json::value dates(const std::vector<Date>& ds) {
            json::array out;
            for (const Date& d : ds)
                out.push_back(result(d));
            return json::value(std::move(out));
        }

        json::value reals(const std::vector<Real>& xs) {
            json::array out;
            for (Real x : xs)
                out.emplace_back(x);
            return json::value(std::move(out));
        }

        const Register unit({
            {"CallableBond::callabilityDates",
             [](const Args& a) {
                 return onBond(a, [](const CallableBond& b) {
                     std::vector<Date> out;
                     for (const auto& c : b.callability())
                         out.push_back(c->date());
                     return dates(out);
                 });
             }},
            {"CallableBond::callabilityTypes",
             [](const Args& a) {
                 return onBond(a, [](const CallableBond& b) {
                     json::array out;
                     for (const auto& c : b.callability())
                         out.emplace_back(static_cast<std::int64_t>(c->type()));
                     return json::value(std::move(out));
                 });
             }},
            {"CallableBond::callabilityPrices",
             [](const Args& a) {
                 return onBond(a, [](const CallableBond& b) {
                     std::vector<Real> out;
                     for (const auto& c : b.callability())
                         out.push_back(c->price().amount());
                     return reals(out);
                 });
             }},
            {"CallableBond::cashflowDates",
             [](const Args& a) {
                 return onBond(a, [](const CallableBond& b) {
                     std::vector<Date> out;
                     for (const auto& c : b.cashflows())
                         out.push_back(c->date());
                     return dates(out);
                 });
             }},
            {"CallableBond::cashflowAmounts",
             [](const Args& a) {
                 return onBond(a, [](const CallableBond& b) {
                     std::vector<Real> out;
                     for (const auto& c : b.cashflows())
                         out.push_back(c->amount());
                     return reals(out);
                 });
             }},
            {"CallableBond::settlementDate",
             [](const Args& a) { return onBond(a, [](const CallableBond& b) { return result(b.settlementDate()); }); }},
            {"CallableBond::maturityDate",
             [](const Args& a) { return onBond(a, [](const CallableBond& b) { return result(b.maturityDate()); }); }},
            {"CallableBond::accruedAmount",
             [](const Args& a) { return onBond(a, [](const CallableBond& b) { return b.accruedAmount(); }); }},
            {"CallableBond::NPV",
             [](const Args& a) { return priced(a, [](const CallableBond& b) { return b.NPV(); }); }},
            {"CallableBond::settlementValue",
             [](const Args& a) { return priced(a, [](const CallableBond& b) { return b.settlementValue(); }); }},
            {"CallableBond::cleanPrice",
             [](const Args& a) { return priced(a, [](const CallableBond& b) { return b.cleanPrice(); }); }},
            {"CallableBond::dirtyPrice",
             [](const Args& a) { return priced(a, [](const CallableBond& b) { return b.dirtyPrice(); }); }},
            {"CallableBond::OAS",
             [](const Args& a) -> Call {
                 arity(a, 11);
                 CallableBondSpec s = callablebonds::bond(a[0]);
                 EngineSpec e = callablebonds::engine(a[1]);
                 Real cleanPrice = real(a[2]);
                 Conventions c = conventions(a, 3);
                 DateSpec settlement = date(a[7]);
                 Real accuracy = real(a[8]);
                 Size maxIterations = size(a[9]);
                 Real guess = real(a[10]);
                 return [=] {
                     auto bond = callablebonds::makePricedBond(s, e);
                     return json::value(bond->OAS(cleanPrice, handle(c), makeDayCounter(c.dayCounter), c.compounding,
                                                  c.frequency, makeDate(settlement), accuracy, maxIterations, guess));
                 };
             }},
            {"CallableBond::cleanPriceOAS",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 CallableBondSpec s = callablebonds::bond(a[0]);
                 EngineSpec e = callablebonds::engine(a[1]);
                 Real oas = real(a[2]);
                 Conventions c = conventions(a, 3);
                 DateSpec settlement = date(a[7]);
                 return [=] {
                     auto bond = callablebonds::makePricedBond(s, e);
                     return json::value(bond->cleanPriceOAS(oas, handle(c), makeDayCounter(c.dayCounter),
                                                            c.compounding, c.frequency, makeDate(settlement)));
                 };
             }},
            {"CallableBond::effectiveDuration",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 CallableBondSpec s = callablebonds::bond(a[0]);
                 EngineSpec e = callablebonds::engine(a[1]);
                 Real oas = real(a[2]);
                 Conventions c = conventions(a, 3);
                 Real bump = real(a[7]);
                 return [=] {
                     auto bond = callablebonds::makePricedBond(s, e);
                     return json::value(bond->effectiveDuration(oas, handle(c), makeDayCounter(c.dayCounter),
                                                                c.compounding, c.frequency, bump));
                 };
             }},
            {"CallableBond::effectiveConvexity",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 CallableBondSpec s = callablebonds::bond(a[0]);
                 EngineSpec e = callablebonds::engine(a[1]);
                 Real oas = real(a[2]);
                 Conventions c = conventions(a, 3);
                 Real bump = real(a[7]);
                 return [=] {
                     auto bond = callablebonds::makePricedBond(s, e);
                     return json::value(bond->effectiveConvexity(oas, handle(c), makeDayCounter(c.dayCounter),
                                                                 c.compounding, c.frequency, bump));
                 };
             }},
            {"CallableBond::impliedVolatility",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 CallableBondSpec s = callablebonds::bond(a[0]);
                 bonds::PriceSpec p = bonds::price(a[1]);
                 models::CurveSpec curve = models::curve(a[2]);
                 Real accuracy = real(a[3]);
                 Size maxEvaluations = size(a[4]);
                 Real minVol = real(a[5]);
                 Real maxVol = real(a[6]);
                 return [=] {
                     auto bond = callablebonds::makeBond(s);
                     return json::value(bond->impliedVolatility(bonds::makePrice(p),
                                                                Handle<YieldTermStructure>(models::makeCurve(curve)),
                                                                accuracy, maxEvaluations, minVol, maxVol));
                 };
             }},
        });

    }

}
