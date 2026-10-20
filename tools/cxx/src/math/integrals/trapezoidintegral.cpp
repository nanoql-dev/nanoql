// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/integrals/trapezoidintegral.hpp: the Default and MidPoint refinements (integrate, nbEvalutions), which the
// wheel doesn't expose. The integrand is named, as tools/vectors/gen/math/integrals/simpsonintegral.py names it.

#include "convert.hpp"

#include <ql/math/integrals/trapezoidintegral.hpp>

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

        template <class Policy>
        Call refine(const Args& a) {
            arity(a, 5);
            std::function<Real(Real)> f = integrand(text(a[0]));
            Real lower = real(a[1]), upper = real(a[2]), I = real(a[3]);
            Integer N = integer(a[4]);
            if (N < 0)
                throw InvalidParams("N must be non-negative");
            return [=] { return json::value(Policy::integrate(f, lower, upper, I, static_cast<Size>(N))); };
        }

        const Register unit({
            {"Default::integrate", [](const Args& a) -> Call { return refine<Default>(a); }},
            {"MidPoint::integrate", [](const Args& a) -> Call { return refine<MidPoint>(a); }},
            {"Default::nbEvalutions",
             [](const Args& a) -> Call {
                 arity(a, 0);
                 return [] { return json::value(static_cast<std::int64_t>(Default::nbEvalutions())); };
             }},
            {"MidPoint::nbEvalutions",
             [](const Args& a) -> Call {
                 arity(a, 0);
                 return [] { return json::value(static_cast<std::int64_t>(MidPoint::nbEvalutions())); };
             }},
        });

    }

}
