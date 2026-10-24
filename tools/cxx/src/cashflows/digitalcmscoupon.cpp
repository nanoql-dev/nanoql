// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/cashflows/digitalcmscoupon.hpp: the digital CMS leg, which the wheel doesn't bind, built inside each call:
//
//     DigitalCmsLeg   (leg): per coupon, digitalspec.hpp's leg outputs with rates, the leg priced by its CMS pricer
//                     through setCouponPricer
//
// A leg is {"schedule" (cashflows/couponspec.hpp's schedule spec), "index" (cashflows/cmsspec.hpp's swap index),
// "notionals", "paymentDayCounter", "paymentAdjustment", "fixingDays", "gearings", "spreads", "inArrears",
// digitalspec.hpp's leg settings, "pricer" (cmsspec.hpp's pricer)}.

#include "cashflows/cmsspec.hpp"
#include "cashflows/couponspec.hpp"
#include "cashflows/digitalspec.hpp"
#include "convert.hpp"

#include <ql/cashflows/couponpricer.hpp>
#include <ql/cashflows/digitalcmscoupon.hpp>

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
            cms::SwapIndexSpec index;
            std::vector<Real> notionals, gearings, spreads;
            DayCounterSpec paymentDayCounter;
            BusinessDayConvention paymentAdjustment = Following;
            std::vector<Natural> fixingDays;
            bool inArrears = false;
            digital::LegTermsSpec terms;
            cms::PricerSpec pricer;
        };

        LegSpec leg(const json::value& v) {
            const std::string what = "a digital CMS leg";
            if (!v.is_object())
                throw InvalidParams("expected " + what + ", got " + json::serialize(v));
            const json::object& o = v.get_object();
            LegSpec s;
            s.schedule = cashflows::schedule(key(o, "schedule", what));
            s.index = cms::swapIndex(key(o, "index", what));
            s.notionals = reals(key(o, "notionals", what));
            s.paymentDayCounter = dayCounter(key(o, "paymentDayCounter", what));
            s.paymentAdjustment = enumeration<BusinessDayConvention>(key(o, "paymentAdjustment", what));
            for (Integer d : integers(key(o, "fixingDays", what)))
                s.fixingDays.push_back(static_cast<Natural>(d));
            s.gearings = reals(key(o, "gearings", what));
            s.spreads = reals(key(o, "spreads", what));
            s.inArrears = boolean(key(o, "inArrears", what));
            s.terms = digital::legTerms(o, what);
            s.pricer = cms::pricer(key(o, "pricer", what));
            return s;
        }

        const Register unit({
            {"DigitalCmsLeg",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 LegSpec s = leg(a[0]);
                 return [=] {
                     Leg l = DigitalCmsLeg(cashflows::makeSchedule(s.schedule), cms::makeSwapIndex(s.index))
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
                     setCouponPricer(l, cms::makePricer(s.pricer));
                     return digital::legOutputs(l, true);
                 };
             }},
        });

    }

}
