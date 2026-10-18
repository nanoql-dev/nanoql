// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/cashflows/floatingratecoupon.hpp: a FloatingRateCoupon built directly, which the wheel can't build, and the
// members of any floating-rate coupon, over a coupon spec of cashflows/couponspec.hpp built inside each call with its
// pricer set. The constructor's result is the coupon's inspectors (couponspec.hpp's inspectors()).

#include "cashflows/couponspec.hpp"
#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/cashflows/floatingratecoupon.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        template <class F>
        Call member(const Args& a, F f) {
            arity(a, 1);
            cashflows::CouponSpec s = cashflows::coupon(a[0]);
            return [=] { return json::value(f(*cashflows::makeCoupon(s))); };
        }

        const Register unit({
            {"FloatingRateCoupon",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 cashflows::CouponSpec s = cashflows::coupon(a[0]);
                 return [=] { return cashflows::inspectors(*cashflows::makeCoupon(s)); };
             }},
            {"FloatingRateCoupon::rate",
             [](const Args& a) { return member(a, [](const FloatingRateCoupon& c) { return c.rate(); }); }},
            {"FloatingRateCoupon::amount",
             [](const Args& a) { return member(a, [](const FloatingRateCoupon& c) { return c.amount(); }); }},
            {"FloatingRateCoupon::indexFixing",
             [](const Args& a) { return member(a, [](const FloatingRateCoupon& c) { return c.indexFixing(); }); }},
            {"FloatingRateCoupon::adjustedFixing",
             [](const Args& a) { return member(a, [](const FloatingRateCoupon& c) { return c.adjustedFixing(); }); }},
            {"FloatingRateCoupon::convexityAdjustment",
             [](const Args& a) {
                 return member(a, [](const FloatingRateCoupon& c) { return c.convexityAdjustment(); });
             }},
            {"FloatingRateCoupon::price(Handle<YieldTermStructure>)",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 cashflows::CouponSpec s = cashflows::coupon(a[0]);
                 indexes::CurveSpec curve = indexes::curve(a[1]);
                 return [=] {
                     return json::value(cashflows::makeCoupon(s)->price(indexes::makeCurve(curve)));
                 };
             }},
            {"FloatingRateCoupon::accruedAmount(Date)",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 cashflows::CouponSpec s = cashflows::coupon(a[0]);
                 DateSpec d = date(a[1]);
                 return [=] { return json::value(cashflows::makeCoupon(s)->accruedAmount(makeDate(d))); };
             }},
        });

    }

}
