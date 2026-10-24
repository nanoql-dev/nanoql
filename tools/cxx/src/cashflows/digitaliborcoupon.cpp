// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/cashflows/digitaliborcoupon.hpp: the digital Ibor leg, which the wheel doesn't bind, built inside each call:
//
//     DigitalIborLeg  (leg): per coupon, digitalspec.hpp's leg outputs with rates, the leg priced by its Ibor pricer
//                     through setCouponPricer
//
// A leg is {"schedule" (cashflows/couponspec.hpp's schedule spec), "index" (an Ibor index spec of
// indexes/indexspec.hpp), "notionals", "paymentDayCounter", "paymentAdjustment", "fixingDays", "gearings", "spreads",
// "inArrears", digitalspec.hpp's leg settings, "pricer" (couponspec.hpp's pricer)}.

#include "cashflows/couponspec.hpp"
#include "cashflows/digitalspec.hpp"
#include "convert.hpp"

#include <ql/cashflows/couponpricer.hpp>
#include <ql/cashflows/digitaliborcoupon.hpp>

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
            indexes::IndexSpec index;
            std::vector<Real> notionals, gearings, spreads;
            DayCounterSpec paymentDayCounter;
            BusinessDayConvention paymentAdjustment = Following;
            std::vector<Natural> fixingDays;
            bool inArrears = false;
            digital::LegTermsSpec terms;
            cashflows::PricerSpec pricer;
        };

        LegSpec leg(const json::value& v) {
            const std::string what = "a digital Ibor leg";
            if (!v.is_object())
                throw InvalidParams("expected " + what + ", got " + json::serialize(v));
            const json::object& o = v.get_object();
            LegSpec s;
            s.schedule = cashflows::schedule(key(o, "schedule", what));
            s.index = indexes::iborIndex(key(o, "index", what));
            s.notionals = reals(key(o, "notionals", what));
            s.paymentDayCounter = dayCounter(key(o, "paymentDayCounter", what));
            s.paymentAdjustment = enumeration<BusinessDayConvention>(key(o, "paymentAdjustment", what));
            for (Integer d : integers(key(o, "fixingDays", what)))
                s.fixingDays.push_back(static_cast<Natural>(d));
            s.gearings = reals(key(o, "gearings", what));
            s.spreads = reals(key(o, "spreads", what));
            s.inArrears = boolean(key(o, "inArrears", what));
            s.terms = digital::legTerms(o, what);
            s.pricer = cashflows::pricer(key(o, "pricer", what));
            return s;
        }

        const Register unit({
            {"DigitalIborLeg",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 LegSpec s = leg(a[0]);
                 return [=] {
                     Leg l = DigitalIborLeg(cashflows::makeSchedule(s.schedule), indexes::makeIborIndex(s.index))
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
                     setCouponPricer(l, cashflows::makePricer(s.pricer));
                     return digital::legOutputs(l, true);
                 };
             }},
        });

    }

}
