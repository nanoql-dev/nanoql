// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/yieldtermstructure.hpp, .cpp: the jumps, which the wheel's curves take but whose jumpDates() and
// jumpTimes() it doesn't bind, read on QuantLib's InterpolatedDiscountCurve<LogLinear> built inside the call: its
// jumpDates() and jumpTimes(), and discount(Date, bool), which applies the jumps. A curve is {"dates", "discounts",
// "dayCounter", "jumps", "jumpDates"}, on QuantLib's empty calendar: the jumps are values, each quoted by a
// SimpleQuote, null for none; null jump dates are none, which leaves the jumps on QuantLib's default dates.

#include "convert.hpp"

#include <ql/math/interpolations/loginterpolation.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/yield/discountcurve.hpp>

#include <initializer_list>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct JumpCurveSpec {
            std::vector<DateSpec> dates;
            std::vector<Real> discounts;
            DayCounterSpec dayCounter;
            std::vector<Real> jumps;
            std::vector<DateSpec> jumpDates;
        };

        std::vector<DateSpec> dateList(const json::value& v) {
            std::vector<DateSpec> out;
            if (v.is_null())
                return out;
            if (!v.is_array())
                throw InvalidParams("expected an array of dates, got " + json::serialize(v));
            for (const json::value& d : v.get_array())
                out.push_back(date(d));
            return out;
        }

        JumpCurveSpec curve(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a curve spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            const std::initializer_list<const char*> keys = {"dates", "discounts", "dayCounter", "jumps",
                                                             "jumpDates"};
            for (const auto& kv : o) {
                bool known = false;
                for (const char* k : keys)
                    known = known || kv.key() == k;
                if (!known)
                    throw InvalidParams("a curve takes no key " + std::string(kv.key()));
            }
            auto get = [&](const char* name) -> const json::value& {
                const json::value* x = o.if_contains(name);
                if (x == nullptr)
                    throw InvalidParams(std::string("a curve needs ") + name);
                return *x;
            };
            JumpCurveSpec s;
            s.dates = dateList(get("dates"));
            s.discounts = reals(get("discounts"));
            s.dayCounter = dayCounter(get("dayCounter"));
            if (!get("jumps").is_null())
                s.jumps = reals(get("jumps"));
            s.jumpDates = dateList(get("jumpDates"));
            return s;
        }

        ext::shared_ptr<YieldTermStructure> makeCurve(const JumpCurveSpec& s) {
            std::vector<Date> dates, jumpDates;
            for (const DateSpec& d : s.dates)
                dates.push_back(makeDate(d));
            for (const DateSpec& d : s.jumpDates)
                jumpDates.push_back(makeDate(d));
            std::vector<Handle<Quote>> jumps;
            for (Real j : s.jumps)
                jumps.emplace_back(ext::make_shared<SimpleQuote>(j));
            return ext::make_shared<InterpolatedDiscountCurve<LogLinear>>(
                dates, s.discounts, makeDayCounter(s.dayCounter), Calendar(), jumps, jumpDates);
        }

        const Register unit({
            {"YieldTermStructure::jumpDates",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 JumpCurveSpec s = curve(a[0]);
                 return [=] {
                     // the curve outlives the read of its jump dates
                     ext::shared_ptr<YieldTermStructure> c = makeCurve(s);
                     json::array out;
                     for (const Date& d : c->jumpDates())
                         out.push_back(result(d));
                     return json::value(std::move(out));
                 };
             }},
            {"YieldTermStructure::jumpTimes",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 JumpCurveSpec s = curve(a[0]);
                 return [=] {
                     ext::shared_ptr<YieldTermStructure> c = makeCurve(s);
                     json::array out;
                     for (Time t : c->jumpTimes())
                         out.emplace_back(t);
                     return json::value(std::move(out));
                 };
             }},
            {"YieldTermStructure::discount(Date,bool)",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 JumpCurveSpec s = curve(a[0]);
                 DateSpec d = date(a[1]);
                 bool extrapolate = boolean(a[2]);
                 return [=] { return json::value(makeCurve(s)->discount(makeDate(d), extrapolate)); };
             }},
        });

    }

}
