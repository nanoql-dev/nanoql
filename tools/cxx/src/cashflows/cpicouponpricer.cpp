// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/cashflows/cpicouponpricer.hpp: CPICouponPricer's members, which the wheel doesn't bind (it builds the pricer with
// no arguments and reaches it only through a coupon's rate). "CPICouponPricer::member" takes the index, a CPI coupon
// and a pricer (cashflows/inflationspec.hpp's specs), the member's name and its argument; it builds them inside the
// call, initializes the pricer on the coupon and calls the member: swapletPrice and swapletRate (the argument null);
// capletPrice, capletRate, floorletPrice and floorletRate (a strike); accruedRate (a date); and capletVolatility and
// nominalTermStructure, whose result is whether the handle is non-empty. "CPICouponPricer::setCapletVolatility" takes
// a pricer and a ConstantCPIVolatility spec, or null for an empty handle, and gives whether the pricer's handle is
// non-empty afterwards.

#include "cashflows/inflationspec.hpp"
#include "convert.hpp"

#include <ql/cashflows/cpicouponpricer.hpp>
#include <ql/utilities/null.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        struct Member {
            std::string name;
            Real strike = Null<Real>();
            DateSpec date{true, 0};
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
            } else if (m.name == "accruedRate") {
                m.date = date(argument);
            } else {
                throw InvalidParams("unknown CPICouponPricer member " + m.name);
            }
            return m;
        }

        cashflows::InflationPricerSpec cpiPricer(const json::value& v) {
            cashflows::InflationPricerSpec s = cashflows::inflationPricer(v);
            if (s.type != "CPICouponPricer")
                throw InvalidParams("expected a CPICouponPricer spec, got " + s.type);
            return s;
        }

        ext::shared_ptr<CPICouponPricer> makeCPIPricer(const cashflows::InflationPricerSpec& s) {
            return ext::dynamic_pointer_cast<CPICouponPricer>(cashflows::makeInflationPricer(s));
        }

        json::value call(const CPICouponPricer& p, const Member& m) {
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
            if (m.name == "nominalTermStructure")
                return json::value(!p.nominalTermStructure().empty());
            return json::value(p.accruedRate(makeDate(m.date)));
        }

        const Register unit({
            // (index, coupon, pricer, member, argument)
            {"CPICouponPricer::member",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 cashflows::InflationIndexSpec i = cashflows::inflationIndex(a[0]);
                 cashflows::CPICouponSpec c = cashflows::cpiCoupon(a[1]);
                 cashflows::InflationPricerSpec p = cpiPricer(a[2]);
                 Member m = member(a[3], a[4]);
                 return [=] {
                     auto coupon = cashflows::makeCPICoupon(c, cashflows::makeZeroIndex(i));
                     auto pricer = makeCPIPricer(p);
                     pricer->initialize(*coupon);
                     return call(*pricer, m);
                 };
             }},
            // (pricer, volatility or null)
            {"CPICouponPricer::setCapletVolatility",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 cashflows::InflationPricerSpec p = cpiPricer(a[0]);
                 bool empty = a[1].is_null();
                 termstructures::InflationVolSpec vol;
                 if (!empty)
                     vol = termstructures::inflationVolatility(a[1], "ConstantCPIVolatility");
                 return [=] {
                     auto pricer = makeCPIPricer(p);
                     Handle<CPIVolatilitySurface> h;
                     if (!empty)
                         h = Handle<CPIVolatilitySurface>(termstructures::makeCPIVolatility(vol));
                     pricer->setCapletVolatility(h);
                     return json::value(!pricer->capletVolatility().empty());
                 };
             }},
        });

    }

}
