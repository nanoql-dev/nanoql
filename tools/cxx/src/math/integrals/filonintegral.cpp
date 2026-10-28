// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/integrals/filonintegral.hpp, .cpp, which the wheel doesn't bind: Filon's sine and cosine integrals of a
// named integrand.
//
//     FilonIntegral  (type ("Sine" or "Cosine"), t, intervals, integrand, a, b):  the integral

#include "convert.hpp"

#include <ql/math/integrals/filonintegral.hpp>

#include <cmath>
#include <functional>
#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        std::function<Real(Real)> integrand(const std::string& name) {
            if (name == "1")
                return [](Real) { return 1.0; };
            if (name == "exp(-x/2)")
                return [](Real x) { return std::exp(-0.5 * x); };
            if (name == "x x")
                return [](Real x) { return x * x; };
            if (name == "1/(1+x x)")
                return [](Real x) { return 1.0 / (1.0 + x * x); };
            throw InvalidParams("unknown integrand " + name);
        }

        const Register unit({
            {"FilonIntegral",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 std::string type = text(a[0]);
                 FilonIntegral::Type kind = type == "Sine"     ? FilonIntegral::Sine
                                            : type == "Cosine" ? FilonIntegral::Cosine
                                                               : throw InvalidParams("expected Sine or Cosine");
                 Real t = real(a[1]);
                 Integer intervals = integer(a[2]);
                 if (intervals < 1)
                     throw InvalidParams("QuantLib divides by 0 intervals");
                 std::function<Real(Real)> f = integrand(text(a[3]));
                 Real lower = real(a[4]), upper = real(a[5]);
                 return [=] { return json::value(FilonIntegral(kind, t, static_cast<Size>(intervals))(f, lower, upper)); };
             }},
        });

    }

}
