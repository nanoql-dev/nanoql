// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/commodities/dateinterval.hpp, .cpp: an interval's intersections and membership.
// "DateInterval::values" takes (start, end, others, dates): an interval, the intervals [start, end] it is compared and
// intersected with, and the dates tested; it returns {"intersection", "between", "equal", "text"}: each intersection
// [start, end], or null for QuantLib's empty interval; each date's isDateBetween under the four flag pairs (first,
// last) = (true, true), (true, false), (false, true), (false, false); each other interval's [==, !=]; and the interval
// as QuantLib's operator<< writes it. The generator tests dates strictly inside or outside the interval: at an end
// QuantLib's excludes it whatever the flags say.

#include "convert.hpp"

#include <ql/experimental/commodities/dateinterval.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"DateInterval::values",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 DateSpec start = date(a[0]);
                 DateSpec end = date(a[1]);
                 if (!a[2].is_array() || !a[3].is_array())
                     throw InvalidParams("expected (start, end, others, dates)");
                 std::vector<std::pair<DateSpec, DateSpec>> others;
                 for (const auto& o : a[2].as_array()) {
                     if (!o.is_array() || o.as_array().size() != 2)
                         throw InvalidParams("an interval is [start, end]");
                     others.emplace_back(date(o.as_array()[0]), date(o.as_array()[1]));
                 }
                 std::vector<DateSpec> dates;
                 for (const auto& d : a[3].as_array())
                     dates.push_back(date(d));
                 return [=] {
                     DateInterval interval(makeDate(start), makeDate(end));
                     json::array intersections, between, equal;
                     for (const auto& o : others) {
                         const DateInterval other(makeDate(o.first), makeDate(o.second));
                         equal.emplace_back(json::array{interval == other, interval != other});
                         DateInterval x = interval.intersection(other);
                         if (x.startDate() == Date())
                             intersections.emplace_back(nullptr);
                         else
                             intersections.emplace_back(json::array{result(x.startDate()), result(x.endDate())});
                     }
                     for (const auto& d : dates) {
                         const Date day = makeDate(d);
                         between.emplace_back(json::array{interval.isDateBetween(day, true, true),
                                                          interval.isDateBetween(day, true, false),
                                                          interval.isDateBetween(day, false, true),
                                                          interval.isDateBetween(day, false, false)});
                     }
                     json::object out;
                     out["intersection"] = intersections;
                     out["between"] = between;
                     out["equal"] = equal;
                     out["text"] = streamed(interval);
                     return json::value(out);
                 };
             }},
        });

    }

}
