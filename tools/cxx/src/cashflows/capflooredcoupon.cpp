// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/cashflows/capflooredcoupon.hpp: CappedFlooredCoupon::underlying(), which the wheel doesn't bind, over a capped
// coupon spec of cashflows/couponspec.hpp (a CappedFlooredCoupon or a CappedFlooredIborCoupon) built inside each call
// with its pricer set. The underlying's result is its inspectors (couponspec.hpp's inspectors()) and whether it holds
// the capped coupon's own pricer ("pricerIsCoupons"); its rate and index fixing are methods of their own, since they
// throw without a pricer.

#include "cashflows/couponspec.hpp"
#include "convert.hpp"

#include <ql/cashflows/capflooredcoupon.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        cashflows::CouponSpec capped(const json::value& v) {
            cashflows::CouponSpec s = cashflows::coupon(v);
            if (s.type != "CappedFlooredCoupon" && s.type != "CappedFlooredIborCoupon")
                throw InvalidParams("expected a capped or floored coupon spec, got " + s.type);
            return s;
        }

        template <class F>
        Call underlying(const Args& a, F f) {
            arity(a, 1);
            cashflows::CouponSpec s = capped(a[0]);
            return [=] {
                auto c = ext::dynamic_pointer_cast<CappedFlooredCoupon>(cashflows::makeCoupon(s));
                return f(*c);
            };
        }

        const Register unit({
            {"CappedFlooredCoupon::underlying",
             [](const Args& a) {
                 return underlying(a, [](CappedFlooredCoupon& c) {
                     json::value out = cashflows::inspectors(*c.underlying());
                     out.get_object()["pricerIsCoupons"] = c.pricer() != nullptr && c.underlying()->pricer() == c.pricer();
                     return out;
                 });
             }},
            {"CappedFlooredCoupon::underlying()->rate",
             [](const Args& a) {
                 return underlying(a, [](CappedFlooredCoupon& c) { return json::value(c.underlying()->rate()); });
             }},
            {"CappedFlooredCoupon::underlying()->indexFixing",
             [](const Args& a) {
                 return underlying(a, [](CappedFlooredCoupon& c) { return json::value(c.underlying()->indexFixing()); });
             }},
        });

    }

}
