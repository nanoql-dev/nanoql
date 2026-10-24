// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/cashflows/rangeaccrual.hpp: the coupon's members the wheel doesn't expose, its coupon built inside each call:
//
//     RangeAccrualFloatersCoupon::members  (coupon, discount curve): [startTime, endTime, lowerTrigger, upperTrigger,
//                                          observationsNo, observationDates, observationTimes,
//                                          observationSchedule's dates, priceWithoutOptionality on the curve]
//
// A coupon is {"paymentDate", "nominal", "index" (an Ibor index spec of indexes/indexspec.hpp), "startDate",
// "endDate", "fixingDays", "dayCounter", "gearing", "spread", "observationSchedule" (cashflows/couponspec.hpp's
// schedule spec), "lowerTrigger", "upperTrigger"}, its reference period its accrual period; the curve an
// indexes/indexspec.hpp curve spec.

#include "cashflows/couponspec.hpp"
#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/cashflows/rangeaccrual.hpp>

#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " needs " + name);
            return *v;
        }

        struct CouponSpec {
            DateSpec paymentDate{true, 0}, startDate{true, 0}, endDate{true, 0};
            Real nominal = 0.0, gearing = 1.0, spread = 0.0, lowerTrigger = 0.0, upperTrigger = 0.0;
            indexes::IndexSpec index;
            Natural fixingDays = 0;
            DayCounterSpec dayCounter;
            cashflows::ScheduleSpec observationSchedule;
        };

        CouponSpec coupon(const json::value& v) {
            const std::string what = "a range accrual coupon";
            if (!v.is_object())
                throw InvalidParams("expected " + what + ", got " + json::serialize(v));
            const json::object& o = v.get_object();
            CouponSpec s;
            s.paymentDate = date(key(o, "paymentDate", what));
            s.nominal = real(key(o, "nominal", what));
            s.index = indexes::iborIndex(key(o, "index", what));
            s.startDate = date(key(o, "startDate", what));
            s.endDate = date(key(o, "endDate", what));
            Integer days = integer(key(o, "fixingDays", what));
            if (days < 0)
                throw InvalidParams(what + "'s fixing days are negative");
            s.fixingDays = static_cast<Natural>(days);
            s.dayCounter = dayCounter(key(o, "dayCounter", what));
            s.gearing = real(key(o, "gearing", what));
            s.spread = real(key(o, "spread", what));
            s.observationSchedule = cashflows::schedule(key(o, "observationSchedule", what));
            s.lowerTrigger = real(key(o, "lowerTrigger", what));
            s.upperTrigger = real(key(o, "upperTrigger", what));
            return s;
        }

        json::value dates(const std::vector<Date>& ds) {
            json::array out;
            for (const Date& d : ds)
                out.push_back(result(d));
            return out;
        }

        const Register unit({
            {"RangeAccrualFloatersCoupon::members",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 CouponSpec s = coupon(a[0]);
                 indexes::CurveSpec discount = indexes::curve(a[1]);
                 return [=] {
                     Date start = makeDate(s.startDate), end = makeDate(s.endDate);
                     RangeAccrualFloatersCoupon c(makeDate(s.paymentDate), s.nominal, indexes::makeIborIndex(s.index),
                                                  start, end, s.fixingDays, makeDayCounter(s.dayCounter), s.gearing,
                                                  s.spread, start, end, cashflows::makeSchedule(s.observationSchedule),
                                                  s.lowerTrigger, s.upperTrigger);
                     json::array times;
                     for (Real t : c.observationTimes())
                         times.emplace_back(t);
                     json::array out;
                     out.emplace_back(c.startTime());
                     out.emplace_back(c.endTime());
                     out.emplace_back(c.lowerTrigger());
                     out.emplace_back(c.upperTrigger());
                     out.emplace_back(static_cast<std::int64_t>(c.observationsNo()));
                     out.push_back(dates(c.observationDates()));
                     out.emplace_back(times);
                     out.push_back(dates(c.observationSchedule().dates()));
                     out.emplace_back(c.priceWithoutOptionality(indexes::makeCurve(discount)));
                     return json::value(out);
                 };
             }},
        });

    }

}
