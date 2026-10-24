// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/integrals/kronrodintegral.hpp, .cpp and integral.hpp: what the wheel doesn't bind of the Gauss-Kronrod rules.
//
//     GaussKronrodNonAdaptive::integrate       (absoluteAccuracy, maxEvaluations, relativeAccuracy, integrand, a, b):
//                                              [the integral, numberOfEvaluations(), absoluteError(),
//                                              integrationSuccess()] after the call, on an interval with a < b (on an
//                                              empty one QuantLib leaves the error unset, so the harness refuses it)
//     GaussKronrodNonAdaptive::relativeAccuracy  (absoluteAccuracy, maxEvaluations, relativeAccuracy, newRelative):
//                                              [relativeAccuracy() as constructed, after setRelativeAccuracy]
//
// The integrands are named as in math/integrals/trapezoidintegral.cpp. The adaptive rule's absoluteError() and
// integrationSuccess() read a member it never sets, so neither is vectored.

#include "convert.hpp"

#include <ql/math/integrals/kronrodintegral.hpp>

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
            {"GaussKronrodNonAdaptive::integrate",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 Real absolute = real(a[0]), relative = real(a[2]);
                 Integer maxEvaluations = integer(a[1]);
                 if (maxEvaluations < 0)
                     throw InvalidParams("a maximum must be non-negative");
                 std::function<Real(Real)> f = integrand(text(a[3]));
                 Real lower = real(a[4]), upper = real(a[5]);
                 if (!(lower < upper))
                     throw InvalidParams("QuantLib leaves the error unset on an empty or reversed interval");
                 return [=] {
                     GaussKronrodNonAdaptive I(absolute, static_cast<Size>(maxEvaluations), relative);
                     Real value = I(f, lower, upper);
                     json::array out{value, static_cast<std::int64_t>(I.numberOfEvaluations()), I.absoluteError(),
                                     I.integrationSuccess()};
                     return json::value(out);
                 };
             }},
            {"GaussKronrodNonAdaptive::relativeAccuracy",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Real absolute = real(a[0]), relative = real(a[2]), newRelative = real(a[3]);
                 Integer maxEvaluations = integer(a[1]);
                 if (maxEvaluations < 0)
                     throw InvalidParams("a maximum must be non-negative");
                 return [=] {
                     GaussKronrodNonAdaptive I(absolute, static_cast<Size>(maxEvaluations), relative);
                     json::array out{I.relativeAccuracy()};
                     I.setRelativeAccuracy(newRelative);
                     out.push_back(json::value(I.relativeAccuracy()));
                     return json::value(out);
                 };
             }},
        });

    }

}
