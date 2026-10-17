// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/time/period.hpp

#include "convert.hpp"

#include <ql/time/period.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        // A Period argument is the spec {"length", "units"}, exactly those keys; a Period result is the same object.
        struct PeriodSpec {
            Integer length;
            TimeUnit units;
        };

        const json::value& key(const json::object& o, const char* name) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string("a Period spec needs ") + name);
            return *v;
        }

        PeriodSpec period(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a Period spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            for (const auto& kv : o) {
                if (kv.key() != "length" && kv.key() != "units")
                    throw InvalidParams("Period takes no key " + std::string(kv.key()));
            }
            return {integer(key(o, "length")), enumeration<TimeUnit>(key(o, "units"))};
        }

        Period make(const PeriodSpec& s) { return {s.length, s.units}; }

        json::value periodResult(const Period& p) {
            return json::object{{"length", p.length()}, {"units", static_cast<int>(p.units())}};
        }

        const Register unit({
            {"days",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 PeriodSpec s = period(a[0]);
                 return [=] { return json::value(days(make(s))); };
             }},
            {"io::long_period",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 PeriodSpec s = period(a[0]);
                 return [=] { return json::value(streamed(io::long_period(make(s)))); };
             }},
            {"operator*(TimeUnit,Integer)",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 auto units = enumeration<TimeUnit>(a[0]);
                 Integer n = integer(a[1]);
                 return [=] { return periodResult(units * n); };
             }},
        });

    }

}
