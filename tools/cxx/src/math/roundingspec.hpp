// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The Rounding spec, shared by the units whose methods take a rounding (math/rounding.cpp, currency.cpp): null for
// Rounding(), QuantLib's default construction, which doesn't round; {"precision", "type", "digit"} for
// Rounding(precision, type, digit), the type as Rounding::Type's integer. The precision must lie in [0, 16], the
// range QuantLib documents (rounding.hpp:31-34): outside it QuantLib's behaviour is undefined, so the harness refuses
// to drive it. QuantLib decides on the type and doesn't check the digit.

#ifndef NANOQL_CXX_MATH_ROUNDINGSPEC_HPP
#define NANOQL_CXX_MATH_ROUNDINGSPEC_HPP

#include "convert.hpp"

#include <ql/math/rounding.hpp>

namespace harness {

    struct RoundingSpec {
        bool isDefault;
        QuantLib::Integer precision;
        QuantLib::Rounding::Type type;
        QuantLib::Integer digit;
    };

    // The check.
    RoundingSpec rounding(const json::value& v);

    // The call.
    QuantLib::Rounding makeRounding(const RoundingSpec& s);

}

#endif
