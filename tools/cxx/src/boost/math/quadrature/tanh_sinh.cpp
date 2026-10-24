// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// boost/math/quadrature/tanh_sinh.hpp, as QuantLib's TanhSinhIntegral calls it: Boost's tanh-sinh quadrature for
// double, whose error estimate the wheel doesn't expose.
//
//     tanh_sinh::integrate  (integrand, a, b, tolerance, maxRefinements, minComplement):
//                           [the integral, the error estimate] of boost::math::quadrature::tanh_sinh<double>
//                           (maxRefinements, minComplement).integrate(f, a, b, tolerance, &error)
//
// A bound at ±DBL_MAX is Boost's infinite one. The integrands are named as in math/integrals/kronrodintegral.cpp.

#include "convert.hpp"

#include <boost/math/quadrature/tanh_sinh.hpp>

#include <cmath>
#include <functional>
#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        std::function<Real(Real)> integrand(const std::string& name) {
            if (name == "exp(x)")
                return [](Real x) { return std::exp(x); };
            if (name == "sin(x)")
                return [](Real x) { return std::sin(x); };
            if (name == "x x")
                return [](Real x) { return x * x; };
            if (name == "x x x x")
                return [](Real x) { return x * x * x * x; };
            if (name == "1/(1+x x)")
                return [](Real x) { return 1.0 / (1.0 + x * x); };
            if (name == "sqrt(x)")
                return [](Real x) { return std::sqrt(x); };
            if (name == "exp(-x x/2)")
                return [](Real x) { return std::exp(-x * x / 2.0); };
            if (name == "x")
                return [](Real x) { return x; };
            throw InvalidParams("unknown integrand " + name);
        }

        const Register unit({
            {"tanh_sinh::integrate",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 std::function<Real(Real)> f = integrand(text(a[0]));
                 Real lower = real(a[1]), upper = real(a[2]), tolerance = real(a[3]);
                 Integer maxRefinements = integer(a[4]);
                 if (maxRefinements < 0)
                     throw InvalidParams("a maximum must be non-negative");
                 Real minComplement = real(a[5]);
                 return [=] {
                     boost::math::quadrature::tanh_sinh<Real> integrator(static_cast<std::size_t>(maxRefinements),
                                                                         minComplement);
                     Real error = 0.0;
                     Real value = integrator.integrate(f, lower, upper, tolerance, &error);
                     json::array out{value, error};
                     return json::value(out);
                 };
             }},
        });

    }

}
