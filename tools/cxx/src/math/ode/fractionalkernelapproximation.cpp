// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/ode/fractionalkernelapproximation.hpp: the sum-of-exponentials approximation of the fractional kernel, which
// the wheel doesn't expose. "FractionalKernelApproximation::members" takes (alpha, n, tMax, ts) and returns {"size",
// "weights", "rates", "values" (the approximation at each t)}.

#include "convert.hpp"

#include <ql/math/ode/fractionalkernelapproximation.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"FractionalKernelApproximation::members",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Real alpha = real(a[0]);
                 Integer n = integer(a[1]);
                 if (n < 0)
                     throw InvalidParams("n must be non-negative");
                 Real tMax = real(a[2]);
                 std::vector<Real> ts = reals(a[3]);
                 return [=] {
                     FractionalKernelApproximation kernel(alpha, static_cast<Size>(n), tMax);
                     json::array weights, rates, values;
                     for (Real c : kernel.weights())
                         weights.push_back(c);
                     for (Real x : kernel.rates())
                         rates.push_back(x);
                     for (Real t : ts)
                         values.push_back(kernel(t));
                     json::object out;
                     out["size"] = kernel.size();
                     out["weights"] = weights;
                     out["rates"] = rates;
                     out["values"] = values;
                     return json::value(out);
                 };
             }},
        });

    }

}
