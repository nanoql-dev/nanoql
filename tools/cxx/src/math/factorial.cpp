// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/factorial.hpp. A Natural argument is an integer in [0, 4294967295], QuantLib's 32-bit unsigned Natural.

#include "convert.hpp"

#include <ql/math/factorial.hpp>

#include <cstdint>

using namespace QuantLib;

namespace harness {

    namespace {

        Natural natural(const json::value& v) {
            if (!v.is_int64() || v.as_int64() < 0 || v.as_int64() > static_cast<std::int64_t>(UINT_MAX))
                throw InvalidParams("expected a Natural, got " + json::serialize(v));
            return static_cast<Natural>(v.as_int64());
        }

        const Register unit({
            {"Factorial::get",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Natural n = natural(a[0]);
                 return [=] { return json::value(Factorial::get(n)); };
             }},
            {"Factorial::ln",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Natural n = natural(a[0]);
                 return [=] { return json::value(Factorial::ln(n)); };
             }},
        });

    }

}
