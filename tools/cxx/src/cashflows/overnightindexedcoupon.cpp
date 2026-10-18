// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/cashflows/overnightindexedcoupon.hpp: OvernightLeg with the setters the wheel doesn't reach, withLastRecentPeriod,
// withLastRecentPeriodCalendar and withCouponPricer, beside those it does. A leg is a spec of
// cashflows/overnightspec.hpp; each call builds it, and reads one cash flow by its position: its description (type,
// dates, nominal, the rate-computation dates and the pricers of an overnight or capped coupon), its rate or its amount.

#include "cashflows/overnightspec.hpp"
#include "convert.hpp"

#include <ql/cashflows/coupon.hpp>
#include <ql/cashflows/fixedratecoupon.hpp>
#include <ql/cashflows/overnightindexedcoupon.hpp>

#include <cstdint>

using namespace QuantLib;

namespace harness {

    namespace {

        std::size_t position(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0)
                throw InvalidParams("expected a position in the leg, got " + json::serialize(v));
            return static_cast<std::size_t>(v.get_int64());
        }

        ext::shared_ptr<CashFlow> at(const Leg& leg, std::size_t i) {
            if (i >= leg.size())
                throw InvalidParams("the leg has no cash flow " + std::to_string(i));
            return leg[i];
        }

        json::value describe(const ext::shared_ptr<CashFlow>& cf) {
            json::object o;
            auto capped = ext::dynamic_pointer_cast<CappedFlooredOvernightIndexedCoupon>(cf);
            auto on = ext::dynamic_pointer_cast<OvernightIndexedCoupon>(cf);
            auto fixed = ext::dynamic_pointer_cast<FixedRateCoupon>(cf);
            auto c = ext::dynamic_pointer_cast<Coupon>(cf);
            if (!c)
                throw InvalidParams("the leg holds a cash flow that isn't a coupon");
            o["type"] = capped ? "CappedFlooredOvernightIndexedCoupon"
                               : on ? "OvernightIndexedCoupon" : fixed ? "FixedRateCoupon" : "?";
            o["paymentDate"] = result(c->date());
            o["accrualStartDate"] = result(c->accrualStartDate());
            o["accrualEndDate"] = result(c->accrualEndDate());
            o["referencePeriodStart"] = result(c->referencePeriodStart());
            o["referencePeriodEnd"] = result(c->referencePeriodEnd());
            o["nominal"] = c->nominal();
            auto underlying = capped ? capped->underlying() : on;
            o["rateComputationStartDate"] = underlying ? result(underlying->rateComputationStartDate()) : nullptr;
            o["rateComputationEndDate"] = underlying ? result(underlying->rateComputationEndDate()) : nullptr;
            auto floating = ext::dynamic_pointer_cast<FloatingRateCoupon>(cf);
            o["pricer"] = floating ? overnight::pricerName(floating->pricer()) : nullptr;
            o["underlyingPricer"] = capped ? overnight::pricerName(capped->underlying()->pricer()) : nullptr;
            return o;
        }

        const Register unit({
            {"OvernightLeg::size",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 overnight::LegSpec s = overnight::leg(a[0]);
                 return [=] { return json::value(static_cast<std::int64_t>(overnight::makeLeg(s).size())); };
             }},
            {"OvernightLeg::describe",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 overnight::LegSpec s = overnight::leg(a[0]);
                 std::size_t i = position(a[1]);
                 return [=] { return describe(at(overnight::makeLeg(s), i)); };
             }},
            {"OvernightLeg::rate",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 overnight::LegSpec s = overnight::leg(a[0]);
                 std::size_t i = position(a[1]);
                 return [=] {
                     auto c = ext::dynamic_pointer_cast<Coupon>(at(overnight::makeLeg(s), i));
                     if (!c)
                         throw InvalidParams("the leg holds a cash flow that isn't a coupon");
                     return json::value(c->rate());
                 };
             }},
            {"OvernightLeg::amount",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 overnight::LegSpec s = overnight::leg(a[0]);
                 std::size_t i = position(a[1]);
                 return [=] { return json::value(at(overnight::makeLeg(s), i)->amount()); };
             }},
        });

    }

}
