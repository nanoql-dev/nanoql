// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/time/daycounter.hpp

#include "convert.hpp"

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"DayCounter::name",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 DayCounterSpec dc = dayCounter(a[0]);
                 return [=] { return json::value(makeDayCounter(dc).name()); };
             }},
        });

    }

}
