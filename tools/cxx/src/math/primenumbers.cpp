// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/primenumbers.hpp. A Size argument is a non-negative integer; the BigNatural result is an integer.

#include "convert.hpp"

#include <ql/math/primenumbers.hpp>

#include <cstdint>

using namespace QuantLib;

namespace harness {

    namespace {

        Size sizeArgument(const json::value& v) {
            if (!v.is_int64() || v.as_int64() < 0)
                throw InvalidParams("expected a Size, got " + json::serialize(v));
            return static_cast<Size>(v.as_int64());
        }

        const Register unit({
            {"PrimeNumbers::get",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Size absoluteIndex = sizeArgument(a[0]);
                 return [=] { return json::value(static_cast<std::int64_t>(PrimeNumbers::get(absoluteIndex))); };
             }},
        });

    }

}
