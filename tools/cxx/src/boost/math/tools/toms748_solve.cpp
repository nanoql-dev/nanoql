// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// boost/math/tools/toms748_solve.hpp for double under the default policy: Algorithm 748 and the bracketing solver
// that Boost's generic quantile calls, on fixed functions, with eps_tolerance's termination test.
//
//     toms748::toms748_solve           (f, a, b, bits, maxIter):
//                                       [the bracket's ends, the calls made] of
//                                       boost::math::tools::toms748_solve(f, a, b, eps_tolerance<double>(bits), maxIter)
//     toms748::bracket_and_solve_root  (f, guess, factor, rising, bits, maxIter):
//                                       [the bracket's ends, the calls made] of
//                                       boost::math::tools::bracket_and_solve_root(f, guess, factor, rising,
//                                       eps_tolerance<double>(bits), maxIter)
//
// Boost's errors throw under the default policy, so a refused argument reads as a throw.

#include "convert.hpp"

#include <boost/math/tools/toms748_solve.hpp>

#include <cmath>
#include <cstdint>
#include <functional>
#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        std::function<Real(Real)> function(const std::string& name) {
            if (name == "x x x - 2")
                return [](Real x) { return x * x * x - 2.0; };
            if (name == "exp(x) - 3")
                return [](Real x) { return std::exp(x) - 3.0; };
            if (name == "log(x) - 1")
                return [](Real x) { return std::log(x) - 1.0; };
            if (name == "2 - x x")
                return [](Real x) { return 2.0 - x * x; };
            if (name == "1/x - 3")
                return [](Real x) { return 1.0 / x - 3.0; };
            if (name == "x - 1e-300")
                return [](Real x) { return x - 1e-300; };
            if (name == "atan(x) - 1.5")
                return [](Real x) { return std::atan(x) - 1.5; };
            if (name == "x + 0.5")
                return [](Real x) { return x + 0.5; };
            if (name == "1 - exp(x)")
                return [](Real x) { return 1.0 - std::exp(x); };
            throw InvalidParams("unknown function " + name);
        }

        std::uintmax_t iterations(Integer n) {
            if (n < 0)
                throw InvalidParams("a maximum must be non-negative");
            return static_cast<std::uintmax_t>(n);
        }

        const Register unit({
            {"toms748::toms748_solve",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 std::function<Real(Real)> f = function(text(a[0]));
                 Real lower = real(a[1]), upper = real(a[2]);
                 Integer bits = integer(a[3]);
                 if (bits < 0)
                     throw InvalidParams("bits must be non-negative");
                 std::uintmax_t maxIter = iterations(integer(a[4]));
                 return [=] {
                     std::uintmax_t n = maxIter;
                     std::pair<Real, Real> r = boost::math::tools::toms748_solve(
                         f, lower, upper, boost::math::tools::eps_tolerance<Real>(static_cast<unsigned>(bits)), n);
                     json::array out{r.first, r.second, static_cast<double>(n)};
                     return json::value(out);
                 };
             }},
            {"toms748::bracket_and_solve_root",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 std::function<Real(Real)> f = function(text(a[0]));
                 Real guess = real(a[1]), factor = real(a[2]);
                 bool rising = boolean(a[3]);
                 Integer bits = integer(a[4]);
                 if (bits < 0)
                     throw InvalidParams("bits must be non-negative");
                 std::uintmax_t maxIter = iterations(integer(a[5]));
                 return [=] {
                     std::uintmax_t n = maxIter;
                     std::pair<Real, Real> r = boost::math::tools::bracket_and_solve_root(
                         f, guess, factor, rising, boost::math::tools::eps_tolerance<Real>(static_cast<unsigned>(bits)),
                         n);
                     json::array out{r.first, r.second, static_cast<double>(n)};
                     return json::value(out);
                 };
             }},
        });

    }

}
