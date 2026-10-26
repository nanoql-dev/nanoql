// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/instruments/doublebarriertype.hpp, .cpp, whose stream operator the wheel doesn't bind: the stream text of a
// DoubleBarrier::Type, QuantLib's refusal of a value outside the type.

#include "convert.hpp"

#include <ql/instruments/doublebarriertype.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"operator<<(DoubleBarrier::Type)",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 auto t = enumeration<DoubleBarrier::Type>(a[0]);
                 return [=] { return json::value(streamed(t)); };
             }},
        });

    }

}
