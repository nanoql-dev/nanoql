// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// boost/math/distributions/gamma.hpp for double under the default policy: the density, the square-root process's
// stationary law Gamma(alpha, theta/alpha) checked against SquareRootProcessRNDCalculator's stationary density where
// QuantLib's product overflows (tests/NanoQL.Tests/Divergences/SquareRootStationaryDensityTests.cs).
//
//     gamma::pdf  (shape, scale, x): boost::math::pdf(gamma_distribution<double>(shape, scale), x)
//
// Boost's errors throw under the default policy, so a refused argument reads as a throw.

#include "convert.hpp"

// MSVC's C4714, a __forceinline function left out of line, which Boost's gamma code raises at /O2: a note on
// inlining, not on the code, so it is off here and /WX still stops on every other warning.
#pragma warning(disable : 4714)

#include <boost/math/distributions/gamma.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"gamma::pdf",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 Real shape = real(a[0]), scale = real(a[1]), x = real(a[2]);
                 return [=] {
                     return json::value(boost::math::pdf(boost::math::gamma_distribution<Real>(shape, scale), x));
                 };
             }},
        });

    }

}
