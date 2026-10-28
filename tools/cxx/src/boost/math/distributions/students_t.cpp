// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// boost/math/distributions/students_t.hpp for double under the default policy, as QuantLib's InverseCumulativeStudent
// calls it, with the function nanoql's port of it exposes:
//
//     students_t::quantile  (df, p): boost::math::quantile(boost::math::students_t_distribution<double>(df), p)
//
// Boost's errors throw under the default policy, so a refused argument reads as a throw.

#include "convert.hpp"

// MSVC's C4714, a __forceinline function left out of line, which Boost's gamma code raises at /O2: a note on
// inlining, not on the code, so it is off here and /WX still stops on every other warning.
#pragma warning(disable : 4714)

#include <boost/math/distributions/students_t.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"students_t::quantile",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 Real df = real(a[0]), p = real(a[1]);
                 return [=] {
                     return json::value(
                         boost::math::quantile(boost::math::students_t_distribution<Real>(df), p));
                 };
             }},
        });

    }

}
