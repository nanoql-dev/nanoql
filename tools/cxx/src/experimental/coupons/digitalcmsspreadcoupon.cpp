// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/coupons/digitalcmsspreadcoupon.hpp: the digital CMS spread leg, which the wheel doesn't bind, built
// inside each call, priced by the lognormal CMS spread pricer (ql/experimental/coupons/lognormalcmsspreadpricer.hpp):
//
//     DigitalCmsSpreadLeg   (leg): per coupon, digitalspec.hpp's leg outputs, with rates when the leg has a pricer
//
// A leg is {"schedule" (cashflows/couponspec.hpp's schedule spec), "index1" and "index2" (cashflows/cmsspec.hpp's swap
// indexes, combined as SwapSpreadIndex("CMS10Y2Y", index1, index2)), "notionals", "paymentDayCounter",
// "paymentAdjustment", "fixingDays", "gearings", "spreads", "inArrears", digitalspec.hpp's leg settings, "pricer"}, the
// pricer null for none or {"cms" (cashflows/cmsspec.hpp's pricer spec), "correlation"}: a LognormalCmsSpreadPricer on
// that CMS pricer, at that correlation, with no coupon discount curve and its default 16 points.

#include "cashflows/cmsspec.hpp"
#include "cashflows/couponspec.hpp"
#include "cashflows/digitalspec.hpp"
#include "convert.hpp"

#include <ql/cashflows/couponpricer.hpp>
#include <ql/experimental/coupons/digitalcmsspreadcoupon.hpp>
#include <ql/experimental/coupons/lognormalcmsspreadpricer.hpp>
#include <ql/experimental/coupons/swapspreadindex.hpp>
#include <ql/quotes/simplequote.hpp>

#include <optional>

#include <initializer_list>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " needs " + name);
            return *v;
        }

        struct LegSpec {
            cashflows::ScheduleSpec schedule;
            cms::SwapIndexSpec index1, index2;
            std::vector<Real> notionals, gearings, spreads;
            DayCounterSpec paymentDayCounter;
            BusinessDayConvention paymentAdjustment = Following;
            std::vector<Natural> fixingDays;
            bool inArrears = false;
            digital::LegTermsSpec terms;
            std::optional<cms::PricerSpec> pricer;
            Real correlation = 0.0;
        };

        LegSpec leg(const json::value& v) {
            const std::string what = "a digital CMS spread leg";
            if (!v.is_object())
                throw InvalidParams("expected " + what + ", got " + json::serialize(v));
            const json::object& o = v.get_object();
            LegSpec s;
            s.schedule = cashflows::schedule(key(o, "schedule", what));
            s.index1 = cms::swapIndex(key(o, "index1", what));
            s.index2 = cms::swapIndex(key(o, "index2", what));
            s.notionals = reals(key(o, "notionals", what));
            s.paymentDayCounter = dayCounter(key(o, "paymentDayCounter", what));
            s.paymentAdjustment = enumeration<BusinessDayConvention>(key(o, "paymentAdjustment", what));
            for (Integer d : integers(key(o, "fixingDays", what)))
                s.fixingDays.push_back(static_cast<Natural>(d));
            s.gearings = reals(key(o, "gearings", what));
            s.spreads = reals(key(o, "spreads", what));
            s.inArrears = boolean(key(o, "inArrears", what));
            s.terms = digital::legTerms(o, what);
            const json::value& pricer = key(o, "pricer", what);
            if (!pricer.is_null()) {
                if (!pricer.is_object())
                    throw InvalidParams("a spread pricer is {\"cms\", \"correlation\"}");
                const json::object& po = pricer.get_object();
                s.pricer = cms::pricer(key(po, "cms", "a spread pricer"));
                s.correlation = real(key(po, "correlation", "a spread pricer"));
            }
            return s;
        }

        const Register unit({
            {"DigitalCmsSpreadLeg",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 LegSpec s = leg(a[0]);
                 return [=] {
                     auto index = ext::make_shared<SwapSpreadIndex>("CMS10Y2Y", cms::makeSwapIndex(s.index1),
                                                                    cms::makeSwapIndex(s.index2));
                     Leg l = DigitalCmsSpreadLeg(cashflows::makeSchedule(s.schedule), index)
                                 .withNotionals(s.notionals)
                                 .withPaymentDayCounter(makeDayCounter(s.paymentDayCounter))
                                 .withPaymentAdjustment(s.paymentAdjustment)
                                 .withFixingDays(s.fixingDays)
                                 .withGearings(s.gearings)
                                 .withSpreads(s.spreads)
                                 .inArrears(s.inArrears)
                                 .withCallStrikes(s.terms.callStrikes)
                                 .withLongCallOption(s.terms.longCallOption)
                                 .withCallATM(s.terms.callATM)
                                 .withCallPayoffs(s.terms.callPayoffs)
                                 .withPutStrikes(s.terms.putStrikes)
                                 .withLongPutOption(s.terms.longPutOption)
                                 .withPutATM(s.terms.putATM)
                                 .withPutPayoffs(s.terms.putPayoffs)
                                 .withReplication(digital::makeReplication(s.terms.replication))
                                 .withNakedOption(s.terms.nakedOption);
                     if (!s.pricer)
                         return digital::legOutputs(l, false);
                     setCouponPricer(l, ext::make_shared<LognormalCmsSpreadPricer>(
                                            cms::makePricer(*s.pricer),
                                            Handle<Quote>(ext::make_shared<SimpleQuote>(s.correlation))));
                     return digital::legOutputs(l, true);
                 };
             }},
        });

    }

}
