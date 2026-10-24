// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/cashflows/digitalcoupon.hpp: the digital coupon, which the wheel doesn't bind, on an underlying coupon spec of
// cashflows/couponspec.hpp built inside each call with its pricer set, and digital terms of cashflows/digitalspec.hpp:
//
//     DigitalCoupon::inspectors   (underlying, terms): digitalspec.hpp's inspectors
//     DigitalCoupon::rates        (underlying, terms): digitalspec.hpp's rates, the coupon's own pricer the
//                                 underlying's, as QuantLib's coupon reads it

#include "cashflows/couponspec.hpp"
#include "cashflows/digitalspec.hpp"
#include "convert.hpp"

#include <ql/cashflows/digitalcoupon.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        template <class F>
        Call digital(const Args& a, F f) {
            arity(a, 2);
            cashflows::CouponSpec u = cashflows::coupon(a[0]);
            digital::TermsSpec t = digital::terms(a[1]);
            return [=] {
                ext::shared_ptr<FloatingRateCoupon> underlying = cashflows::makeCoupon(u);
                DigitalCoupon c(underlying, t.callStrike, t.callPosition, t.isCallATMIncluded, t.callDigitalPayoff,
                                t.putStrike, t.putPosition, t.isPutATMIncluded, t.putDigitalPayoff,
                                digital::makeReplication(t.replication), t.nakedOption);
                return f(c);
            };
        }

        const Register unit({
            {"DigitalCoupon::inspectors",
             [](const Args& a) { return digital(a, [](DigitalCoupon& c) { return digital::inspectors(c); }); }},
            {"DigitalCoupon::rates",
             [](const Args& a) { return digital(a, [](DigitalCoupon& c) { return digital::rates(c); }); }},
        });

    }

}
