// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/cashflows/couponpricer.hpp: what the wheel doesn't bind or can't reach. IborCouponPricer::useIndexedCoupon(); a
// pricer's six queries after initialize(coupon), which the wheel binds without initialize; setCouponPricers, in its
// list and first-matching forms; and setCouponPricer on coupons the wheel can't build. Pricers and coupons are specs
// of cashflows/couponspec.hpp, built inside each call; a leg is a list of coupon specs. A setter's result is, per cash
// flow, the position in the call's pricers of the one the cash flow holds afterwards, or null for none.

#include "cashflows/couponspec.hpp"
#include "convert.hpp"

#include <ql/cashflows/couponpricer.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using Pricers = std::vector<ext::shared_ptr<FloatingRateCouponPricer>>;

        Pricers makePricers(const std::vector<cashflows::PricerSpec>& s) {
            Pricers out;
            for (const auto& p : s)
                out.push_back(cashflows::makePricer(p));
            return out;
        }

        template <class F>
        Call query(const Args& a, std::size_t n, F f) {
            arity(a, n);
            cashflows::PricerSpec p = cashflows::pricer(a[0]);
            cashflows::CouponSpec c = cashflows::coupon(a[1]);
            if (p.type.empty())
                throw InvalidParams("a query needs a pricer");
            Real x = n == 3 ? real(a[2]) : 0.0;
            return [=] {
                auto pricer = cashflows::makePricer(p);
                auto coupon = cashflows::makeCoupon(c);
                pricer->initialize(*coupon);
                return json::value(f(*pricer, x));
            };
        }

        Call firstMatching(const Args& a, std::size_t n) {
            arity(a, n + 1);
            std::vector<cashflows::CouponSpec> leg = cashflows::coupons(a[0]);
            std::vector<cashflows::PricerSpec> p;
            for (std::size_t k = 1; k <= n; ++k)
                p.push_back(cashflows::pricer(a[k]));
            return [=] {
                Leg built = cashflows::makeCoupons(leg);
                Pricers pricers = makePricers(p);
                if (n == 2)
                    setCouponPricers(built, pricers[0], pricers[1]);
                else if (n == 3)
                    setCouponPricers(built, pricers[0], pricers[1], pricers[2]);
                else
                    setCouponPricers(built, pricers[0], pricers[1], pricers[2], pricers[3]);
                return cashflows::positions(built, pricers);
            };
        }

        const Register unit({
            {"IborCouponPricer::useIndexedCoupon",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 cashflows::PricerSpec p = cashflows::pricer(a[0]);
                 if (p.type != "BlackIborCouponPricer")
                     throw InvalidParams("expected an Ibor pricer spec");
                 return [=] {
                     auto pricer = ext::dynamic_pointer_cast<IborCouponPricer>(cashflows::makePricer(p));
                     return json::value(pricer->useIndexedCoupon());
                 };
             }},
            {"FloatingRateCouponPricer::swapletPrice",
             [](const Args& a) {
                 return query(a, 2, [](const FloatingRateCouponPricer& p, Real) { return p.swapletPrice(); });
             }},
            {"FloatingRateCouponPricer::swapletRate",
             [](const Args& a) {
                 return query(a, 2, [](const FloatingRateCouponPricer& p, Real) { return p.swapletRate(); });
             }},
            {"FloatingRateCouponPricer::capletPrice",
             [](const Args& a) {
                 return query(a, 3, [](const FloatingRateCouponPricer& p, Real x) { return p.capletPrice(x); });
             }},
            {"FloatingRateCouponPricer::capletRate",
             [](const Args& a) {
                 return query(a, 3, [](const FloatingRateCouponPricer& p, Real x) { return p.capletRate(x); });
             }},
            {"FloatingRateCouponPricer::floorletPrice",
             [](const Args& a) {
                 return query(a, 3, [](const FloatingRateCouponPricer& p, Real x) { return p.floorletPrice(x); });
             }},
            {"FloatingRateCouponPricer::floorletRate",
             [](const Args& a) {
                 return query(a, 3, [](const FloatingRateCouponPricer& p, Real x) { return p.floorletRate(x); });
             }},
            {"setCouponPricer(Leg,FloatingRateCouponPricer)",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 std::vector<cashflows::CouponSpec> leg = cashflows::coupons(a[0]);
                 cashflows::PricerSpec p = cashflows::pricer(a[1]);
                 return [=] {
                     Leg built = cashflows::makeCoupons(leg);
                     Pricers pricers{cashflows::makePricer(p)};
                     setCouponPricer(built, pricers[0]);
                     return cashflows::positions(built, pricers);
                 };
             }},
            {"setCouponPricers(Leg,vector<FloatingRateCouponPricer>)",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 std::vector<cashflows::CouponSpec> leg = cashflows::coupons(a[0]);
                 std::vector<cashflows::PricerSpec> p = cashflows::pricers(a[1]);
                 if (p.empty() && !leg.empty())
                     throw InvalidParams("QuantLib reads the pricer before an empty list's start");
                 return [=] {
                     Leg built = cashflows::makeCoupons(leg);
                     Pricers pricers = makePricers(p);
                     setCouponPricers(built, pricers);
                     return cashflows::positions(built, pricers);
                 };
             }},
            {"setCouponPricers(Leg,p1,p2)", [](const Args& a) { return firstMatching(a, 2); }},
            {"setCouponPricers(Leg,p1,p2,p3)", [](const Args& a) { return firstMatching(a, 3); }},
            {"setCouponPricers(Leg,p1,p2,p3,p4)", [](const Args& a) { return firstMatching(a, 4); }},
        });

    }

}
