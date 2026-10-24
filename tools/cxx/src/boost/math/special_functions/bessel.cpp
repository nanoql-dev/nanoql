// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// boost/math/special_functions/bessel.hpp for double under the default policy: the modified Bessel function of the
// first kind, which Boost's non-central chi-squared density calls.
//
//     bessel::cyl_bessel_i  (v, x): boost::math::cyl_bessel_i(v, x)
//
// Boost's errors throw under the default policy, so a refused argument reads as a throw.

#include "convert.hpp"

// MSVC's C4714, a __forceinline function left out of line, which Boost's gamma code raises at /O2: a note on
// inlining, not on the code, so it is off here and /WX still stops on every other warning.
#pragma warning(disable : 4714)

#include <boost/math/special_functions/bessel.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"bessel::cyl_bessel_i",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 Real v = real(a[0]), x = real(a[1]);
                 return [=] { return json::value(boost::math::cyl_bessel_i(v, x)); };
             }},
        });

    }

}
