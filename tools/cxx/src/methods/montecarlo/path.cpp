// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/montecarlo/path.hpp: Path built by its user, which the wheel can't do (its constructor is private in
// QuantLib-SWIG), and the members the wheel's Path lacks. A path argument is the spec {"timeGrid", "values"}: a
// TimeGrid spec (timegrid.hpp) and the values, a list of reals, or null for Path's default, an empty Array, which the
// constructor replaces by Array(n), n the grid's size, whose elements QuantLib leaves uninitialized. The call builds
// the grid and then the path, so that the constructor's refusal is QuantLib's. An index is a non-negative integer.

#include "timegrid.hpp"

#include <ql/methods/montecarlo/path.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        struct PathSpec {
            TimeGridSpec grid;
            bool defaultValues;
            std::vector<Real> values;
        };

        PathSpec pathSpec(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a Path spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            for (const auto& kv : o) {
                if (kv.key() != "timeGrid" && kv.key() != "values")
                    throw InvalidParams("Path takes no key " + std::string(kv.key()));
            }
            const json::value* grid = o.if_contains("timeGrid");
            const json::value* values = o.if_contains("values");
            if (grid == nullptr || values == nullptr)
                throw InvalidParams("a Path spec is {\"timeGrid\", \"values\"}, got " + json::serialize(v));
            if (values->is_null())
                return {timeGrid(*grid), true, {}};
            return {timeGrid(*grid), false, reals(*values)};
        }

        Path makePath(const PathSpec& s) {
            TimeGrid grid = makeTimeGrid(s.grid);
            if (s.defaultValues)
                return Path(grid);
            return Path(grid, Array(s.values.begin(), s.values.end()));
        }

        Size index(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > INT_MAX)
                throw InvalidParams("expected an index, got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        // A function of one path.
        Call ofPath(const Args& a, json::value (*f)(const Path&)) {
            arity(a, 1);
            PathSpec s = pathSpec(a[0]);
            return [=] { return f(makePath(s)); };
        }

        // A function of one path and an index.
        Call ofPathAndIndex(const Args& a, json::value (*f)(const Path&, Size)) {
            arity(a, 2);
            PathSpec s = pathSpec(a[0]);
            Size i = index(a[1]);
            return [=] { return f(makePath(s), i); };
        }

        const Register unit({
            {"Path::length",
             [](const Args& a) {
                 return ofPath(a, [](const Path& p) { return json::value(static_cast<std::int64_t>(p.length())); });
             }},
            {"Path::empty", [](const Args& a) { return ofPath(a, [](const Path& p) { return json::value(p.empty()); }); }},
            {"Path::front", [](const Args& a) { return ofPath(a, [](const Path& p) { return json::value(p.front()); }); }},
            {"Path::back", [](const Args& a) { return ofPath(a, [](const Path& p) { return json::value(p.back()); }); }},
            {"Path::timeGrid", [](const Args& a) { return ofPath(a, [](const Path& p) { return timesResult(p.timeGrid()); }); }},
            {"Path::begin",
             // the values from begin() to end()
             [](const Args& a) {
                 return ofPath(a, [](const Path& p) {
                     json::array out;
                     for (auto i = p.begin(); i != p.end(); ++i)
                         out.emplace_back(*i);
                     return json::value(std::move(out));
                 });
             }},
            {"Path::operator[]",
             [](const Args& a) {
                 return ofPathAndIndex(a, [](const Path& p, Size i) { return json::value(p[i]); });
             }},
            {"Path::at",
             [](const Args& a) {
                 return ofPathAndIndex(a, [](const Path& p, Size i) { return json::value(p.at(i)); });
             }},
            {"Path::value",
             [](const Args& a) {
                 return ofPathAndIndex(a, [](const Path& p, Size i) { return json::value(p.value(i)); });
             }},
            {"Path::time",
             [](const Args& a) {
                 return ofPathAndIndex(a, [](const Path& p, Size i) { return json::value(p.time(i)); });
             }},
        });

    }

}
