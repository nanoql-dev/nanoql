// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/cashflows/iborcoupon.hpp: the five quantities an IborCoupon caches from its pricer (fixingValueDate,
// fixingEndDate, fixingMaturityDate, spanningTime, spanningTimeIndexMaturity), which the wheel doesn't bind, over an
// IborCoupon spec of cashflows/couponspec.hpp built inside each call with its pricer set; and an IborLeg built with the
// setters the wheel doesn't pass (withFixingConvention, withZeroPayments). The leg's result is, per cash flow,
// {"date", "nominal", "accrualStartDate", "accrualEndDate", "referencePeriodStart", "referencePeriodEnd",
// "exCouponDate", "fixingDate" (null for a fixed-rate coupon), "rate", "amount"}.

#include "cashflows/couponspec.hpp"
#include "convert.hpp"

#include <ql/cashflows/coupon.hpp>
#include <ql/cashflows/iborcoupon.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        cashflows::CouponSpec iborCoupon(const json::value& v) {
            cashflows::CouponSpec s = cashflows::coupon(v);
            if (s.type != "IborCoupon")
                throw InvalidParams("expected an IborCoupon spec, got " + s.type);
            return s;
        }

        template <class F>
        Call cached(const Args& a, F f) {
            arity(a, 1);
            cashflows::CouponSpec s = iborCoupon(a[0]);
            return [=] {
                auto c = ext::dynamic_pointer_cast<IborCoupon>(cashflows::makeCoupon(s));
                return f(*c);
            };
        }

        const Register unit({
            {"IborCoupon::fixingValueDate",
             [](const Args& a) { return cached(a, [](const IborCoupon& c) { return result(c.fixingValueDate()); }); }},
            {"IborCoupon::fixingEndDate",
             [](const Args& a) { return cached(a, [](const IborCoupon& c) { return result(c.fixingEndDate()); }); }},
            {"IborCoupon::fixingMaturityDate",
             [](const Args& a) {
                 return cached(a, [](const IborCoupon& c) { return result(c.fixingMaturityDate()); });
             }},
            {"IborCoupon::spanningTime",
             [](const Args& a) {
                 return cached(a, [](const IborCoupon& c) { return json::value(c.spanningTime()); });
             }},
            {"IborCoupon::spanningTimeIndexMaturity",
             [](const Args& a) {
                 return cached(a, [](const IborCoupon& c) { return json::value(c.spanningTimeIndexMaturity()); });
             }},
            {"IborLeg::operator Leg",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 cashflows::LegSpec s = cashflows::leg(a[0]);
                 return [=] {
                     json::array out;
                     for (const auto& cf : cashflows::makeLeg(s)) {
                         auto c = ext::dynamic_pointer_cast<Coupon>(cf);
                         if (!c)
                             throw InvalidParams("an IborLeg's cash flow that isn't a coupon");
                         auto f = ext::dynamic_pointer_cast<FloatingRateCoupon>(cf);
                         out.emplace_back(json::object{
                             {"date", result(c->date())},
                             {"nominal", c->nominal()},
                             {"accrualStartDate", result(c->accrualStartDate())},
                             {"accrualEndDate", result(c->accrualEndDate())},
                             {"referencePeriodStart", result(c->referencePeriodStart())},
                             {"referencePeriodEnd", result(c->referencePeriodEnd())},
                             {"exCouponDate", result(c->exCouponDate())},
                             {"fixingDate", f ? result(f->fixingDate()) : json::value(nullptr)},
                             {"rate", c->rate()},
                             {"amount", c->amount()}});
                     }
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
