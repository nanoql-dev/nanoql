// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/capfloor/constantcapfloortermvol.hpp: ConstantCapFloorTermVolatility, which the wheel
// doesn't bind, built inside each call from the spec {"referenceDate" or "settlementDays", "calendar", "convention",
// "volatility", "dayCounter"} on its constructors on a volatility value (the quote ones equal them once a quote is a
// value): the reference date fixed, or the given business days after the evaluation date, which the case's settings
// set; the calendar as in indexes/indexspec.hpp. A settlement-days count is a Natural, so a negative one is the
// harness's refusal.
//
//     ConstantCapFloorTermVolatility::referenceDate, maxDate, minStrike, maxStrike, businessDayConvention (structure)
//     ConstantCapFloorTermVolatility::settlementDays          (structure): or QuantLib's refusal for a fixed date
//     ConstantCapFloorTermVolatility::volatility(Period)      (structure, [length, units], strike, extrapolate)
//     ConstantCapFloorTermVolatility::volatility(Date)        (structure, date, strike, extrapolate)
//     ConstantCapFloorTermVolatility::volatility(Time)        (structure, time, strike, extrapolate)

#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "termstructures/volatility/capfloor/capfloorvolspec.hpp"

#include <ql/termstructures/volatility/capfloor/constantcapfloortermvol.hpp>
#include <ql/time/period.hpp>

#include <cstdint>
#include <initializer_list>
#include <string>
#include <utility>

using namespace QuantLib;

namespace harness {

    namespace {

        struct ConstantSpec {
            bool fixed = true;
            DateSpec referenceDate{true, 0};
            Natural settlementDays = 0;
            indexes::CalendarSpec calendar;
            BusinessDayConvention convention = Following;
            Volatility volatility = 0.0;
            DayCounterSpec dayCounter;
        };

        const json::value& key(const json::object& o, const char* name) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string("a constant cap/floor term volatility spec needs ") + name);
            return *v;
        }

        ConstantSpec constant(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a constant cap/floor term volatility spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            ConstantSpec s;
            bool hasDate = o.if_contains("referenceDate") != nullptr;
            bool hasDays = o.if_contains("settlementDays") != nullptr;
            if (hasDate == hasDays)
                throw InvalidParams("a constant cap/floor term volatility spec needs one of referenceDate and settlementDays");
            for (const auto& kv : o) {
                bool known = false;
                for (const char* k : {hasDate ? "referenceDate" : "settlementDays", "calendar", "convention",
                                      "volatility", "dayCounter"})
                    known = known || kv.key() == k;
                if (!known)
                    throw InvalidParams("a constant cap/floor term volatility takes no key " + std::string(kv.key()));
            }
            s.fixed = hasDate;
            if (s.fixed) {
                s.referenceDate = date(key(o, "referenceDate"));
            } else {
                Integer n = integer(key(o, "settlementDays"));
                if (n == Null<Integer>() || n < 0)
                    throw InvalidParams("expected a Natural, got " + json::serialize(key(o, "settlementDays")));
                s.settlementDays = static_cast<Natural>(n);
            }
            s.calendar = indexes::calendar(key(o, "calendar"));
            s.convention = enumeration<BusinessDayConvention>(key(o, "convention"));
            s.volatility = real(key(o, "volatility"));
            s.dayCounter = dayCounter(key(o, "dayCounter"));
            return s;
        }

        ext::shared_ptr<ConstantCapFloorTermVolatility> make(const ConstantSpec& s) {
            Calendar cal = indexes::makeCalendar(s.calendar);
            DayCounter dc = makeDayCounter(s.dayCounter);
            return s.fixed ? ext::make_shared<ConstantCapFloorTermVolatility>(makeDate(s.referenceDate), cal,
                                                                              s.convention, s.volatility, dc)
                           : ext::make_shared<ConstantCapFloorTermVolatility>(s.settlementDays, cal, s.convention,
                                                                              s.volatility, dc);
        }

        Call whole(const Args& a, json::value (*f)(const ConstantCapFloorTermVolatility&)) {
            arity(a, 1);
            ConstantSpec s = constant(a[0]);
            return [=] { return f(*make(s)); };
        }

        template <class P, class F>
        Call atPoint(const Args& a, P (*point)(const json::value&), F f) {
            arity(a, 4);
            ConstantSpec s = constant(a[0]);
            P p = point(a[1]);
            Rate strike = real(a[2]);
            bool extrapolate = boolean(a[3]);
            return [=] { return json::value(f(*make(s), p, strike, extrapolate)); };
        }

        DateSpec dateOf(const json::value& v) { return date(v); }
        Real timeOf(const json::value& v) { return real(v); }

        const Register unit({
            {"ConstantCapFloorTermVolatility::referenceDate",
             [](const Args& a) {
                 return whole(a, [](const ConstantCapFloorTermVolatility& c) { return result(c.referenceDate()); });
             }},
            {"ConstantCapFloorTermVolatility::maxDate",
             [](const Args& a) {
                 return whole(a, [](const ConstantCapFloorTermVolatility& c) { return result(c.maxDate()); });
             }},
            {"ConstantCapFloorTermVolatility::minStrike",
             [](const Args& a) {
                 return whole(a, [](const ConstantCapFloorTermVolatility& c) { return json::value(c.minStrike()); });
             }},
            {"ConstantCapFloorTermVolatility::maxStrike",
             [](const Args& a) {
                 return whole(a, [](const ConstantCapFloorTermVolatility& c) { return json::value(c.maxStrike()); });
             }},
            {"ConstantCapFloorTermVolatility::businessDayConvention",
             [](const Args& a) {
                 return whole(a, [](const ConstantCapFloorTermVolatility& c) {
                     return json::value(static_cast<std::int64_t>(c.businessDayConvention()));
                 });
             }},
            {"ConstantCapFloorTermVolatility::settlementDays",
             [](const Args& a) {
                 return whole(a, [](const ConstantCapFloorTermVolatility& c) {
                     return json::value(static_cast<std::int64_t>(c.settlementDays()));
                 });
             }},
            {"ConstantCapFloorTermVolatility::volatility(Period)",
             [](const Args& a) {
                 return atPoint(a, capfloorvols::period,
                                [](const ConstantCapFloorTermVolatility& c, const std::pair<Integer, TimeUnit>& t,
                                   Rate k, bool e) { return c.volatility(Period(t.first, t.second), k, e); });
             }},
            {"ConstantCapFloorTermVolatility::volatility(Date)",
             [](const Args& a) {
                 return atPoint(a, dateOf, [](const ConstantCapFloorTermVolatility& c, const DateSpec& d, Rate k,
                                              bool e) { return c.volatility(makeDate(d), k, e); });
             }},
            {"ConstantCapFloorTermVolatility::volatility(Time)",
             [](const Args& a) {
                 return atPoint(a, timeOf, [](const ConstantCapFloorTermVolatility& c, Real t, Rate k, bool e) {
                     return c.volatility(t, k, e);
                 });
             }},
        });

    }

}
