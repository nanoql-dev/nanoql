// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/index.hpp: the members the wheel doesn't reach, allowsNativeFixings() and addFixings(const TimeSeries<Real>&,
// bool), and the writers and reads a case needs around them. An index argument is a spec of indexes/indexspec.hpp;
// each call builds its index, whose stored fixings live in QuantLib's IndexManager under its name, so a case's calls
// share them until Settings::clearCaseGlobals clears them. Dates and values come as two lists of one size, since
// QuantLib reads one value per date. A history is returned as [[serial, value], ...] in date order.

#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/index.hpp>
#include <ql/timeseries.hpp>

#include <cstdint>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        std::vector<DateSpec> dates(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected an array of dates, got " + json::serialize(v));
            std::vector<DateSpec> out;
            for (const json::value& d : v.get_array())
                out.push_back(date(d));
            return out;
        }

        std::vector<Date> makeDates(const std::vector<DateSpec>& d) {
            std::vector<Date> out;
            for (const auto& x : d)
                out.push_back(makeDate(x));
            return out;
        }

        // The dates and values of a writer's arguments, of one size.
        void checkSizes(const std::vector<DateSpec>& d, const std::vector<Real>& v) {
            if (d.size() != v.size())
                throw InvalidParams("a writer's dates and values have one size, since QuantLib reads one value per "
                                    "date");
        }

        json::value history(const TimeSeries<Real>& s) {
            json::array out;
            for (const auto& p : s)
                out.emplace_back(json::array{static_cast<std::int64_t>(p.first.serialNumber()), p.second});
            return out;
        }

        const Register unit({
            {"Index::name",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 indexes::IndexSpec s = indexes::index(a[0]);
                 return [=] { return json::value(indexes::makeIndex(s)->name()); };
             }},
            {"Index::allowsNativeFixings",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 indexes::IndexSpec s = indexes::index(a[0]);
                 return [=] { return json::value(indexes::makeIndex(s)->allowsNativeFixings()); };
             }},
            {"Index::addFixing",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 indexes::IndexSpec s = indexes::index(a[0]);
                 DateSpec d = date(a[1]);
                 Real x = real(a[2]);
                 bool force = boolean(a[3]);
                 return [=] {
                     indexes::makeIndex(s)->addFixing(makeDate(d), x, force);
                     return json::value(nullptr);
                 };
             }},
            {"Index::addFixings(DateIterator,DateIterator,ValueIterator,bool)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 indexes::IndexSpec s = indexes::index(a[0]);
                 std::vector<DateSpec> d = dates(a[1]);
                 std::vector<Real> x = reals(a[2]);
                 bool force = boolean(a[3]);
                 checkSizes(d, x);
                 return [=] {
                     std::vector<Date> ds = makeDates(d);
                     indexes::makeIndex(s)->addFixings(ds.begin(), ds.end(), x.begin(), force);
                     return json::value(nullptr);
                 };
             }},
            {"Index::addFixings(TimeSeries,bool)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 indexes::IndexSpec s = indexes::index(a[0]);
                 std::vector<DateSpec> d = dates(a[1]);
                 std::vector<Real> x = reals(a[2]);
                 bool force = boolean(a[3]);
                 checkSizes(d, x);
                 return [=] {
                     std::vector<Date> ds = makeDates(d);
                     indexes::makeIndex(s)->addFixings(TimeSeries<Real>(ds.begin(), ds.end(), x.begin()), force);
                     return json::value(nullptr);
                 };
             }},
            {"Index::clearFixings",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 indexes::IndexSpec s = indexes::index(a[0]);
                 return [=] {
                     indexes::makeIndex(s)->clearFixings();
                     return json::value(nullptr);
                 };
             }},
            {"Index::timeSeries",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 indexes::IndexSpec s = indexes::index(a[0]);
                 return [=] { return history(indexes::makeIndex(s)->timeSeries()); };
             }},
        });

    }

}
