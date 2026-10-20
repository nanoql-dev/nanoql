// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/cashflows/inflationcoupon.hpp: InflationCoupon's price on a discount curve and its setPricer, which the wheel
// doesn't bind (it sets a pricer only through setCouponPricer, of its own type). "CPICoupon::price" and
// "YoYInflationCoupon::price" take the index, the coupon and a pricer (cashflows/inflationspec.hpp's specs) and a
// discount curve (indexes/indexspec.hpp's); each sets the pricer on the coupon built inside the call and gives its
// price. "CPICoupon::setPricer" and "YoYInflationCoupon::setPricer" take the index, the coupon and a pricer, or null
// for none, and set it, which QuantLib refuses for a pricer of the other coupon's type or none; the result is true.

#include "cashflows/inflationspec.hpp"
#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/cashflows/inflationcoupon.hpp>

#include <optional>

using namespace QuantLib;

namespace harness {

    namespace {

        std::optional<cashflows::InflationPricerSpec> optionalPricer(const json::value& v) {
            if (v.is_null())
                return std::nullopt;
            return cashflows::inflationPricer(v);
        }

        ext::shared_ptr<InflationCouponPricer> makeOptionalPricer(
            const std::optional<cashflows::InflationPricerSpec>& s) {
            if (!s)
                return {};
            return cashflows::makeInflationPricer(*s);
        }

        const Register unit({
            // (index, coupon, pricer, discount curve)
            {"CPICoupon::price",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 cashflows::InflationIndexSpec i = cashflows::inflationIndex(a[0]);
                 cashflows::CPICouponSpec c = cashflows::cpiCoupon(a[1]);
                 cashflows::InflationPricerSpec p = cashflows::inflationPricer(a[2]);
                 indexes::CurveSpec d = indexes::curve(a[3]);
                 return [=] {
                     auto coupon = cashflows::makeCPICoupon(c, cashflows::makeZeroIndex(i));
                     coupon->setPricer(cashflows::makeInflationPricer(p));
                     return json::value(coupon->price(indexes::makeCurve(d)));
                 };
             }},
            {"YoYInflationCoupon::price",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 cashflows::InflationIndexSpec i = cashflows::inflationIndex(a[0]);
                 cashflows::YoYCouponSpec c = cashflows::yoyCoupon(a[1]);
                 cashflows::InflationPricerSpec p = cashflows::inflationPricer(a[2]);
                 indexes::CurveSpec d = indexes::curve(a[3]);
                 return [=] {
                     auto coupon = cashflows::makeYoYCoupon(c, cashflows::makeYoYIndex(i));
                     coupon->setPricer(cashflows::makeInflationPricer(p));
                     return json::value(coupon->price(indexes::makeCurve(d)));
                 };
             }},
            // (index, coupon, pricer or null)
            {"CPICoupon::setPricer",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 cashflows::InflationIndexSpec i = cashflows::inflationIndex(a[0]);
                 cashflows::CPICouponSpec c = cashflows::cpiCoupon(a[1]);
                 std::optional<cashflows::InflationPricerSpec> p = optionalPricer(a[2]);
                 return [=] {
                     auto coupon = cashflows::makeCPICoupon(c, cashflows::makeZeroIndex(i));
                     coupon->setPricer(makeOptionalPricer(p));
                     return json::value(true);
                 };
             }},
            {"YoYInflationCoupon::setPricer",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 cashflows::InflationIndexSpec i = cashflows::inflationIndex(a[0]);
                 cashflows::YoYCouponSpec c = cashflows::yoyCoupon(a[1]);
                 std::optional<cashflows::InflationPricerSpec> p = optionalPricer(a[2]);
                 return [=] {
                     auto coupon = cashflows::makeYoYCoupon(c, cashflows::makeYoYIndex(i));
                     coupon->setPricer(makeOptionalPricer(p));
                     return json::value(true);
                 };
             }},
        });

    }

}
