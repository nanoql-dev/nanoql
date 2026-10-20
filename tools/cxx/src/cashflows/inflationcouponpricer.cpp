// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/cashflows/inflationcouponpricer.hpp: the year-on-year pricers' members, which the wheel doesn't bind (it reaches
// the pricers only through a capped or floored coupon's rate). "YoYInflationCouponPricer::member" takes the index, a
// year-on-year coupon and a pricer (cashflows/inflationspec.hpp's specs: the base YoYInflationCouponPricer, whose
// optionlets QuantLib refuses, or the Black, unit-displaced Black or Bachelier pricer), the member's name and its
// argument; it builds them inside the call, initializes the pricer on the coupon and calls the member: swapletPrice
// and swapletRate (the argument null); capletPrice, capletRate, floorletPrice and floorletRate (a strike); and
// capletVolatility and nominalTermStructure, whose result is whether the handle is non-empty.
// "YoYInflationCouponPricer::setCapletVolatility" takes a pricer and a ConstantYoYOptionletVolatility spec, or null
// for an empty handle, and gives whether the pricer's handle is non-empty afterwards.
// "YoYInflationCouponPricer::initialize(CPICoupon)" takes a zero-inflation index, a CPI coupon and a pricer, and
// initializes the pricer on the coupon, which QuantLib refuses.

#include "cashflows/inflationspec.hpp"
#include "convert.hpp"

#include <ql/cashflows/inflationcouponpricer.hpp>
#include <ql/utilities/null.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        struct Member {
            std::string name;
            Real strike = Null<Real>();
        };

        Member member(const json::value& name, const json::value& argument) {
            Member m;
            m.name = text(name);
            if (m.name == "swapletPrice" || m.name == "swapletRate" || m.name == "capletVolatility" ||
                m.name == "nominalTermStructure") {
                if (!argument.is_null())
                    throw InvalidParams(m.name + " takes no argument");
            } else if (m.name == "capletPrice" || m.name == "capletRate" || m.name == "floorletPrice" ||
                       m.name == "floorletRate") {
                m.strike = real(argument);
            } else {
                throw InvalidParams("unknown YoYInflationCouponPricer member " + m.name);
            }
            return m;
        }

        cashflows::InflationPricerSpec yoyPricer(const json::value& v) {
            cashflows::InflationPricerSpec s = cashflows::inflationPricer(v);
            if (s.type == "CPICouponPricer")
                throw InvalidParams("expected a year-on-year pricer spec, got " + s.type);
            return s;
        }

        ext::shared_ptr<YoYInflationCouponPricer> makeYoYPricer(const cashflows::InflationPricerSpec& s) {
            return ext::dynamic_pointer_cast<YoYInflationCouponPricer>(cashflows::makeInflationPricer(s));
        }

        json::value call(const YoYInflationCouponPricer& p, const Member& m) {
            if (m.name == "swapletPrice")
                return json::value(p.swapletPrice());
            if (m.name == "swapletRate")
                return json::value(p.swapletRate());
            if (m.name == "capletPrice")
                return json::value(p.capletPrice(m.strike));
            if (m.name == "capletRate")
                return json::value(p.capletRate(m.strike));
            if (m.name == "floorletPrice")
                return json::value(p.floorletPrice(m.strike));
            if (m.name == "floorletRate")
                return json::value(p.floorletRate(m.strike));
            if (m.name == "capletVolatility")
                return json::value(!p.capletVolatility().empty());
            return json::value(!p.nominalTermStructure().empty());
        }

        const Register unit({
            // (index, coupon, pricer, member, argument)
            {"YoYInflationCouponPricer::member",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 cashflows::InflationIndexSpec i = cashflows::inflationIndex(a[0]);
                 cashflows::YoYCouponSpec c = cashflows::yoyCoupon(a[1]);
                 cashflows::InflationPricerSpec p = yoyPricer(a[2]);
                 Member m = member(a[3], a[4]);
                 return [=] {
                     auto coupon = cashflows::makeYoYCoupon(c, cashflows::makeYoYIndex(i));
                     auto pricer = makeYoYPricer(p);
                     pricer->initialize(*coupon);
                     return call(*pricer, m);
                 };
             }},
            // (pricer, volatility or null)
            {"YoYInflationCouponPricer::setCapletVolatility",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 cashflows::InflationPricerSpec p = yoyPricer(a[0]);
                 bool empty = a[1].is_null();
                 termstructures::InflationVolSpec vol;
                 if (!empty)
                     vol = termstructures::inflationVolatility(a[1], "ConstantYoYOptionletVolatility");
                 return [=] {
                     auto pricer = makeYoYPricer(p);
                     Handle<YoYOptionletVolatilitySurface> h;
                     if (!empty)
                         h = Handle<YoYOptionletVolatilitySurface>(termstructures::makeYoYVolatility(vol));
                     pricer->setCapletVolatility(h);
                     return json::value(!pricer->capletVolatility().empty());
                 };
             }},
            // (index, CPI coupon, pricer)
            {"YoYInflationCouponPricer::initialize(CPICoupon)",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 cashflows::InflationIndexSpec i = cashflows::inflationIndex(a[0]);
                 cashflows::CPICouponSpec c = cashflows::cpiCoupon(a[1]);
                 cashflows::InflationPricerSpec p = yoyPricer(a[2]);
                 return [=] {
                     auto coupon = cashflows::makeCPICoupon(c, cashflows::makeZeroIndex(i));
                     makeYoYPricer(p)->initialize(*coupon);
                     return json::value(true);
                 };
             }},
        });

    }

}
