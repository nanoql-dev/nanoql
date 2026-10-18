// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/timeseries.hpp: the members of TimeSeries<Real> the wheel doesn't expose (the constructor from a first date,
// iteration, and the const operator[], which doesn't insert), over a series the call builds from its spec. A series
// is the spec {"dates", "values"} for the constructor from two sequences, TimeSeries(dBegin, dEnd, vBegin), or
// {"firstDate", "values"} for the constructor from a first date, TimeSeries(firstDate, begin, end); dates are serials
// and values reals. QuantLib reads one value per date from vBegin without a bound, so the harness refuses a spec with
// fewer values than dates; surplus values are left unread, as QuantLib leaves them.
//
// Iteration's result is the series' [date, value] pairs from begin() to end(); the lookup's, the value, or null for
// Null<Real>(), which the const operator[] returns for a date the series doesn't hold.

#include "convert.hpp"

#include <ql/timeseries.hpp>
#include <ql/utilities/null.hpp>

#include <cstdint>

using namespace QuantLib;

namespace harness {

    namespace {

        struct TimeSeriesSpec {
            bool fromFirstDate;
            std::vector<DateSpec> dates;
            DateSpec firstDate;
            std::vector<Real> values;
        };

        const json::value& key(const json::object& o, const char* name) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string("a TimeSeries spec needs ") + name);
            return *v;
        }

        TimeSeriesSpec timeSeries(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a TimeSeries spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            const bool fromFirstDate = o.contains("firstDate");
            for (const auto& kv : o) {
                if (kv.key() != "values" && kv.key() != (fromFirstDate ? "firstDate" : "dates"))
                    throw InvalidParams("a TimeSeries spec takes values and either dates or firstDate, not " +
                                        std::string(kv.key()));
            }
            TimeSeriesSpec s{fromFirstDate, {}, {true, 0}, reals(key(o, "values"))};
            if (fromFirstDate) {
                s.firstDate = date(key(o, "firstDate"));
            } else {
                const json::value& d = key(o, "dates");
                if (!d.is_array())
                    throw InvalidParams("expected a list of dates, got " + json::serialize(d));
                for (const json::value& x : d.get_array())
                    s.dates.push_back(date(x));
                if (s.values.size() < s.dates.size())
                    throw InvalidParams("fewer values than dates: QuantLib would read past the values");
            }
            return s;
        }

        TimeSeries<Real> make(const TimeSeriesSpec& s) {
            if (s.fromFirstDate)
                return {makeDate(s.firstDate), s.values.begin(), s.values.end()};
            std::vector<Date> dates;
            for (const DateSpec& d : s.dates)
                dates.push_back(makeDate(d));
            return {dates.begin(), dates.end(), s.values.begin()};
        }

        const Register unit({
            {"TimeSeries<Real>::begin",
             [](const Args& a) -> Call {
                 // Iterates from begin() to end().
                 arity(a, 1);
                 TimeSeriesSpec s = timeSeries(a[0]);
                 return [=] {
                     const TimeSeries<Real> ts = make(s);
                     json::array out;
                     for (auto i = ts.begin(); i != ts.end(); ++i)
                         out.emplace_back(json::array{result(i->first), i->second});
                     return json::value(std::move(out));
                 };
             }},
            {"TimeSeries<Real>::operator[](Date) const",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 TimeSeriesSpec s = timeSeries(a[0]);
                 DateSpec d = date(a[1]);
                 return [=] {
                     const TimeSeries<Real> ts = make(s);
                     Real x = ts[makeDate(d)];
                     return x == Null<Real>() ? json::value(nullptr) : json::value(x);
                 };
             }},
        });

    }

}
