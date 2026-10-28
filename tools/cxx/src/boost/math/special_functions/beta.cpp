// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// boost/math/special_functions/beta.hpp for double under the default policy, as QuantLib's Student t quantile calls
// it (through boost/math/special_functions/detail/t_distribution_inv.hpp), with the functions nanoql's port of it
// exposes:
//
//     beta::beta              (a, b):    boost::math::beta(a, b)
//     beta::ibeta             (a, b, x): boost::math::ibeta(a, b, x)
//     beta::ibetac            (a, b, x): boost::math::ibetac(a, b, x)
//     beta::ibeta_derivative  (a, b, x): boost::math::ibeta_derivative(a, b, x)
//
// Boost's errors throw under the default policy, so a refused argument reads as a throw.

#include "convert.hpp"

// MSVC's C4714, a __forceinline function left out of line, which Boost's gamma code raises at /O2: a note on
// inlining, not on the code, so it is off here and /WX still stops on every other warning.
#pragma warning(disable : 4714)

#include <boost/math/special_functions/beta.hpp>

#include <functional>

using namespace QuantLib;

namespace harness {

    namespace {

        Method two(std::function<Real(Real, Real)> f) {
            return [f](const Args& a) -> Call {
                arity(a, 2);
                Real x = real(a[0]), y = real(a[1]);
                return [=] { return json::value(f(x, y)); };
            };
        }

        Method three(std::function<Real(Real, Real, Real)> f) {
            return [f](const Args& a) -> Call {
                arity(a, 3);
                Real x = real(a[0]), y = real(a[1]), z = real(a[2]);
                return [=] { return json::value(f(x, y, z)); };
            };
        }

        const Register unit({
            {"beta::beta", two([](Real a, Real b) { return boost::math::beta(a, b); })},
            {"beta::ibeta", three([](Real a, Real b, Real x) { return boost::math::ibeta(a, b, x); })},
            {"beta::ibetac", three([](Real a, Real b, Real x) { return boost::math::ibetac(a, b, x); })},
            {"beta::ibeta_derivative",
             three([](Real a, Real b, Real x) { return boost::math::ibeta_derivative(a, b, x); })},
        });

    }

}
