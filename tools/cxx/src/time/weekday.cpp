// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/time/weekday.hpp

#include "convert.hpp"

#include <ql/time/weekday.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"io::long_weekday",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 auto w = enumeration<Weekday>(a[0]);
                 return [=] { return json::value(streamed(io::long_weekday(w))); };
             }},
            {"io::short_weekday",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 auto w = enumeration<Weekday>(a[0]);
                 return [=] { return json::value(streamed(io::short_weekday(w))); };
             }},
            {"io::shortest_weekday",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 auto w = enumeration<Weekday>(a[0]);
                 return [=] { return json::value(streamed(io::shortest_weekday(w))); };
             }},
            {"operator<<(Weekday)",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 auto w = enumeration<Weekday>(a[0]);
                 return [=] { return json::value(streamed(w)); };
             }},
        });

    }

}
