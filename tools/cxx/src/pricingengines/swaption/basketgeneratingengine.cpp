// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/rebatedexercise.hpp, .cpp, the exercise the basket-generating engines read (ql/pricingengines/swaption/
// basketgeneratingengine.cpp): RebatedExercise's rebate(index) and rebatePaymentDate(index), and its constructor from
// one rebate, which the wheel doesn't bind, on an exercise built inside the call, so that a refusal is QuantLib's. A
// rebated exercise is {"exercise", "rebate" or "rebates", "rebateSettlementDays", "rebatePaymentCalendar" (a calendar
// spec of indexes/indexspec.hpp), "rebatePaymentConvention" (BusinessDayConvention's integer)}, its exercise
// {"type": "EuropeanExercise", "date"}, {"type": "BermudanExercise", "dates", "payoffAtExpiry"} or
// {"type": "AmericanExercise", "earliestDate", "latestDate", "payoffAtExpiry"}, dates as serials.
//
//     RebatedExercise::rebates            (spec): [rebate, ...]
//     RebatedExercise::rebate             (spec, index)
//     RebatedExercise::rebatePaymentDate  (spec, index)

#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/rebatedexercise.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct RebatedSpec {
            std::string type;
            DateSpec date, earliestDate, latestDate;
            std::vector<DateSpec> dates;
            bool payoffAtExpiry = false;
            bool vector = false;
            Real rebate = 0.0;
            std::vector<Real> rebates;
            Natural settlementDays = 0;
            indexes::CalendarSpec calendar;
            BusinessDayConvention convention = Following;
        };

        RebatedSpec rebatedSpec(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a rebated exercise spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            RebatedSpec s;
            const json::object& e = o.at("exercise").as_object();
            s.type = text(e.at("type"));
            if (s.type == "EuropeanExercise") {
                s.date = date(e.at("date"));
            } else if (s.type == "BermudanExercise") {
                for (const auto& d : e.at("dates").as_array())
                    s.dates.push_back(date(d));
                s.payoffAtExpiry = boolean(e.at("payoffAtExpiry"));
            } else if (s.type == "AmericanExercise") {
                s.earliestDate = date(e.at("earliestDate"));
                s.latestDate = date(e.at("latestDate"));
                s.payoffAtExpiry = boolean(e.at("payoffAtExpiry"));
            } else {
                throw InvalidParams("unknown exercise " + s.type);
            }
            if (const json::value* r = o.if_contains("rebates")) {
                s.vector = true;
                s.rebates = reals(*r);
            } else {
                s.rebate = real(o.at("rebate"));
            }
            s.settlementDays = static_cast<Natural>(integer(o.at("rebateSettlementDays")));
            s.calendar = indexes::calendar(o.at("rebatePaymentCalendar"));
            s.convention = enumeration<BusinessDayConvention>(o.at("rebatePaymentConvention"));
            return s;
        }

        ext::shared_ptr<RebatedExercise> make(const RebatedSpec& s) {
            ext::shared_ptr<Exercise> e;
            if (s.type == "EuropeanExercise") {
                e = ext::make_shared<EuropeanExercise>(makeDate(s.date));
            } else if (s.type == "BermudanExercise") {
                std::vector<Date> dates;
                for (const DateSpec& d : s.dates)
                    dates.push_back(makeDate(d));
                e = ext::make_shared<BermudanExercise>(dates, s.payoffAtExpiry);
            } else {
                e = ext::make_shared<AmericanExercise>(makeDate(s.earliestDate), makeDate(s.latestDate),
                                                       s.payoffAtExpiry);
            }
            Calendar calendar = indexes::makeCalendar(s.calendar);
            if (s.vector)
                return ext::make_shared<RebatedExercise>(*e, s.rebates, s.settlementDays, calendar, s.convention);
            return ext::make_shared<RebatedExercise>(*e, s.rebate, s.settlementDays, calendar, s.convention);
        }

        Size index(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0)
                throw InvalidParams("expected a Size, got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        const Register unit({
            {"RebatedExercise::rebates",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 RebatedSpec s = rebatedSpec(a[0]);
                 return [=] {
                     ext::shared_ptr<RebatedExercise> e = make(s);
                     json::array out;
                     for (Real r : e->rebates())
                         out.emplace_back(r);
                     return json::value(std::move(out));
                 };
             }},
            {"RebatedExercise::rebate",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 RebatedSpec s = rebatedSpec(a[0]);
                 Size i = index(a[1]);
                 return [=] { return json::value(make(s)->rebate(i)); };
             }},
            {"RebatedExercise::rebatePaymentDate",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 RebatedSpec s = rebatedSpec(a[0]);
                 Size i = index(a[1]);
                 return [=] { return result(make(s)->rebatePaymentDate(i)); };
             }},
        });

    }

}
