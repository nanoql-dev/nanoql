// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/timegrid.hpp: the members of TimeGrid the wheel doesn't expose (index, closestIndex, closestTime,
// mandatoryTimes, at, empty, front, back), over a grid built by the call from its spec (timegrid.hpp), so that a
// constructor's refusal is QuantLib's. A time is a real, an index a non-negative integer.

#include "timegrid.hpp"

using namespace QuantLib;

namespace harness {

    namespace {

        Size steps(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > INT_MAX)
                throw InvalidParams("expected a number of steps, got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        Size index(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > INT_MAX)
                throw InvalidParams("expected an index, got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        json::value sizeResult(Size n) { return static_cast<std::int64_t>(n); }

        // A function of one grid.
        Call ofGrid(const Args& a, json::value (*f)(const TimeGrid&)) {
            arity(a, 1);
            TimeGridSpec s = timeGrid(a[0]);
            return [=] { return f(makeTimeGrid(s)); };
        }

        // A function of one grid and a time.
        Call ofGridAndTime(const Args& a, json::value (*f)(const TimeGrid&, Time)) {
            arity(a, 2);
            TimeGridSpec s = timeGrid(a[0]);
            Time t = real(a[1]);
            return [=] { return f(makeTimeGrid(s), t); };
        }

        const Register unit({
            {"TimeGrid::size",
             [](const Args& a) { return ofGrid(a, [](const TimeGrid& g) { return sizeResult(g.size()); }); }},
            {"TimeGrid::empty",
             [](const Args& a) { return ofGrid(a, [](const TimeGrid& g) { return json::value(g.empty()); }); }},
            {"TimeGrid::front",
             [](const Args& a) { return ofGrid(a, [](const TimeGrid& g) { return json::value(g.front()); }); }},
            {"TimeGrid::back",
             [](const Args& a) { return ofGrid(a, [](const TimeGrid& g) { return json::value(g.back()); }); }},
            {"TimeGrid::mandatoryTimes",
             [](const Args& a) {
                 return ofGrid(a, [](const TimeGrid& g) {
                     json::array out;
                     for (Time t : g.mandatoryTimes())
                         out.emplace_back(t);
                     return json::value(std::move(out));
                 });
             }},
            {"TimeGrid::at",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 TimeGridSpec s = timeGrid(a[0]);
                 Size i = index(a[1]);
                 return [=] { return json::value(makeTimeGrid(s).at(i)); };
             }},
            {"TimeGrid::index",
             [](const Args& a) {
                 return ofGridAndTime(a, [](const TimeGrid& g, Time t) { return sizeResult(g.index(t)); });
             }},
            {"TimeGrid::closestIndex",
             [](const Args& a) {
                 return ofGridAndTime(a, [](const TimeGrid& g, Time t) { return sizeResult(g.closestIndex(t)); });
             }},
            {"TimeGrid::closestTime",
             [](const Args& a) {
                 return ofGridAndTime(a, [](const TimeGrid& g, Time t) { return json::value(g.closestTime(t)); });
             }},
        });

    }

    TimeGridSpec timeGrid(const json::value& v) {
        if (!v.is_object())
            throw InvalidParams("expected a TimeGrid spec, got " + json::serialize(v));
        const json::object& o = v.get_object();
        for (const auto& kv : o) {
            if (kv.key() != "end" && kv.key() != "steps" && kv.key() != "times")
                throw InvalidParams("TimeGrid takes no key " + std::string(kv.key()));
        }
        const json::value* end = o.if_contains("end");
        const json::value* n = o.if_contains("steps");
        const json::value* times = o.if_contains("times");
        if (end != nullptr) {
            if (n == nullptr || times != nullptr)
                throw InvalidParams("a regular TimeGrid spec is {\"end\", \"steps\"}, got " + json::serialize(v));
            return {TimeGridSpec::Regular, real(*end), steps(*n), {}};
        }
        if (times != nullptr) {
            if (n == nullptr)
                return {TimeGridSpec::Mandatory, 0.0, 0, reals(*times)};
            return {TimeGridSpec::MandatoryWithSteps, 0.0, steps(*n), reals(*times)};
        }
        if (n != nullptr)
            throw InvalidParams("a TimeGrid spec with steps needs an end or times, got " + json::serialize(v));
        return {TimeGridSpec::Empty, 0.0, 0, {}};
    }

    TimeGrid makeTimeGrid(const TimeGridSpec& s) {
        switch (s.kind) {
            case TimeGridSpec::Empty:
                return TimeGrid();
            case TimeGridSpec::Regular:
                return TimeGrid(s.end, s.steps);
            case TimeGridSpec::Mandatory:
                return TimeGrid(s.times.begin(), s.times.end());
            case TimeGridSpec::MandatoryWithSteps:
                return TimeGrid(s.times.begin(), s.times.end(), s.steps);
        }
        throw InvalidParams("the TimeGrid spec was not checked");
    }

    json::value timesResult(const TimeGrid& g) {
        json::array out;
        for (Time t : g)
            out.emplace_back(t);
        return json::value(std::move(out));
    }

}
