// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/time/date.hpp

#include "convert.hpp"

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"Date(Day,Month,Year)",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 Integer d = integer(a[0]);
                 auto m = enumeration<Month>(a[1]);
                 Integer y = integer(a[2]);
                 return [=] { return result(Date(d, m, y)); };
             }},
            {"Date::isLeap",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Integer y = integer(a[0]);
                 return [=] { return json::value(Date::isLeap(y)); };
             }},
            {"io::short_date",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 DateSpec d = date(a[0]);
                 return [=] { return json::value(streamed(io::short_date(makeDate(d)))); };
             }},
        });

    }

}
