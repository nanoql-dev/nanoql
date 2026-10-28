// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// boost/math/special_functions/gamma.hpp and detail/igamma_inverse.hpp for double under the default policy, as
// QuantLib's CEV engine and density calculator call them, with the functions nanoql's port of them exposes:
//
//     gamma::tgamma              (z):    boost::math::tgamma(z)
//     gamma::lgamma              (z):    boost::math::lgamma(z)
//     gamma::tgamma1pm1          (z):    boost::math::tgamma1pm1(z)
//     gamma::gamma_p             (a, x): boost::math::gamma_p(a, x)
//     gamma::gamma_q             (a, x): boost::math::gamma_q(a, x)
//     gamma::gamma_p_derivative  (a, x): boost::math::gamma_p_derivative(a, x)
//     gamma::gamma_p_inv         (a, p): boost::math::gamma_p_inv(a, p)
//     gamma::gamma_q_inv         (a, q): boost::math::gamma_q_inv(a, q)
//     gamma::tgamma_delta_ratio  (z, d): boost::math::tgamma_delta_ratio(z, d)
//
// Boost's errors throw under the default policy, so a refused argument reads as a throw.

#include "convert.hpp"

// MSVC's C4714, a __forceinline function left out of line, which Boost's gamma code raises at /O2: a note on
// inlining, not on the code, so it is off here and /WX still stops on every other warning.
#pragma warning(disable : 4714)

#include <boost/math/special_functions/gamma.hpp>

#include <functional>

using namespace QuantLib;

namespace harness {

    namespace {

        Method one(std::function<Real(Real)> f) {
            return [f](const Args& a) -> Call {
                arity(a, 1);
                Real z = real(a[0]);
                return [=] { return json::value(f(z)); };
            };
        }

        Method two(std::function<Real(Real, Real)> f) {
            return [f](const Args& a) -> Call {
                arity(a, 2);
                Real x = real(a[0]), y = real(a[1]);
                return [=] { return json::value(f(x, y)); };
            };
        }

        const Register unit({
            {"gamma::tgamma", one([](Real z) { return boost::math::tgamma(z); })},
            {"gamma::lgamma", one([](Real z) { return boost::math::lgamma(z); })},
            {"gamma::tgamma1pm1", one([](Real z) { return boost::math::tgamma1pm1(z); })},
            {"gamma::gamma_p", two([](Real a, Real x) { return boost::math::gamma_p(a, x); })},
            {"gamma::gamma_q", two([](Real a, Real x) { return boost::math::gamma_q(a, x); })},
            {"gamma::gamma_p_derivative", two([](Real a, Real x) { return boost::math::gamma_p_derivative(a, x); })},
            {"gamma::gamma_p_inv", two([](Real a, Real p) { return boost::math::gamma_p_inv(a, p); })},
            {"gamma::gamma_q_inv", two([](Real a, Real q) { return boost::math::gamma_q_inv(a, q); })},
            {"gamma::tgamma_delta_ratio",
             two([](Real z, Real d) { return boost::math::tgamma_delta_ratio(z, d); })},
        });

    }

}
