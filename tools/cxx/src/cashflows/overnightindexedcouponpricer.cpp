// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/cashflows/overnightindexedcouponpricer.hpp and ql/cashflows/blackovernightindexedcouponpricer.hpp: the pricer
// members the wheel doesn't reach, each called on a pricer initialized with the coupon, as a coupon initializes its
// pricer before it asks: averageRate(const Date&), the two-argument capletRate and floorletRate, the effective caplet
// and floorlet volatilities after them, and CompoundingOvernightIndexedCouponPricer's effectiveSpread() and
// effectiveIndexFixing(). A pricer and a coupon (plain or capped) are specs of cashflows/overnightspec.hpp, built
// inside the call; the coupon's pricer is left as its constructor set it.

#include "cashflows/overnightspec.hpp"
#include "convert.hpp"

#include <ql/cashflows/overnightindexedcouponpricer.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        // The pricer, initialized with the coupon. The pricer keeps a raw pointer to the coupon (or to a capped
        // coupon's underlying), so the pair keeps the coupon alive while the pricer is queried.
        struct Initialized {
            ext::shared_ptr<FloatingRateCoupon> coupon;
            ext::shared_ptr<OvernightIndexedCouponPricer> pricer;
        };

        Initialized initialized(const overnight::PricerSpec& p, const overnight::AnyCouponSpec& c) {
            Initialized out{overnight::makeAnyCoupon(c), overnight::makePricer(p)};
            out.pricer->initialize(*out.coupon);
            return out;
        }

        ext::shared_ptr<CompoundingOvernightIndexedCouponPricer>
        compounding(const ext::shared_ptr<OvernightIndexedCouponPricer>& p) {
            auto c = ext::dynamic_pointer_cast<CompoundingOvernightIndexedCouponPricer>(p);
            if (!c)
                throw InvalidParams("the pricer is no CompoundingOvernightIndexedCouponPricer");
            return c;
        }

        const Register unit({
            {"OvernightIndexedCouponPricer::averageRate(Date)",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 overnight::PricerSpec p = overnight::pricer(a[0]);
                 overnight::AnyCouponSpec c = overnight::anyCoupon(a[1]);
                 DateSpec d = date(a[2]);
                 return [=] {
                     Initialized i = initialized(p, c);
                     return json::value(i.pricer->averageRate(makeDate(d)));
                 };
             }},
            {"OvernightIndexedCouponPricer::capletRate(Rate,bool)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 overnight::PricerSpec p = overnight::pricer(a[0]);
                 overnight::AnyCouponSpec c = overnight::anyCoupon(a[1]);
                 Real k = real(a[2]);
                 bool daily = boolean(a[3]);
                 return [=] {
                     Initialized i = initialized(p, c);
                     return json::value(i.pricer->capletRate(k, daily));
                 };
             }},
            {"OvernightIndexedCouponPricer::floorletRate(Rate,bool)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 overnight::PricerSpec p = overnight::pricer(a[0]);
                 overnight::AnyCouponSpec c = overnight::anyCoupon(a[1]);
                 Real k = real(a[2]);
                 bool daily = boolean(a[3]);
                 return [=] {
                     Initialized i = initialized(p, c);
                     return json::value(i.pricer->floorletRate(k, daily));
                 };
             }},
            // The effective caplet volatility after capletRate(effectiveCap, dailyCapFloor), or null for none.
            {"OvernightIndexedCouponPricer::effectiveCapletVolatility",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 overnight::PricerSpec p = overnight::pricer(a[0]);
                 overnight::AnyCouponSpec c = overnight::anyCoupon(a[1]);
                 Real k = real(a[2]);
                 bool daily = boolean(a[3]);
                 return [=] {
                     Initialized i = initialized(p, c);
                     i.pricer->capletRate(k, daily);
                     Real vol = i.pricer->effectiveCapletVolatility();
                     return vol == Null<Real>() ? json::value(nullptr) : json::value(vol);
                 };
             }},
            // The effective floorlet volatility after floorletRate(effectiveFloor, dailyCapFloor), or null for none.
            {"OvernightIndexedCouponPricer::effectiveFloorletVolatility",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 overnight::PricerSpec p = overnight::pricer(a[0]);
                 overnight::AnyCouponSpec c = overnight::anyCoupon(a[1]);
                 Real k = real(a[2]);
                 bool daily = boolean(a[3]);
                 return [=] {
                     Initialized i = initialized(p, c);
                     i.pricer->floorletRate(k, daily);
                     Real vol = i.pricer->effectiveFloorletVolatility();
                     return vol == Null<Real>() ? json::value(nullptr) : json::value(vol);
                 };
             }},
            {"CompoundingOvernightIndexedCouponPricer::effectiveSpread",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 overnight::PricerSpec p = overnight::pricer(a[0]);
                 overnight::AnyCouponSpec c = overnight::anyCoupon(a[1]);
                 return [=] {
                     Initialized i = initialized(p, c);
                     return json::value(compounding(i.pricer)->effectiveSpread());
                 };
             }},
            {"CompoundingOvernightIndexedCouponPricer::effectiveIndexFixing",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 overnight::PricerSpec p = overnight::pricer(a[0]);
                 overnight::AnyCouponSpec c = overnight::anyCoupon(a[1]);
                 return [=] {
                     Initialized i = initialized(p, c);
                     return json::value(compounding(i.pricer)->effectiveIndexFixing());
                 };
             }},
        });

    }

}
