// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/distributions/bivariatestudenttdistribution.hpp, which the wheel doesn't bind: the cumulative function of n
// degrees of freedom and correlation rho at each (x, y) pair.
//
//     BivariateCumulativeStudentDistribution  (n, rho, xs, ys)

#include "convert.hpp"

#include <ql/math/distributions/bivariatestudenttdistribution.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"BivariateCumulativeStudentDistribution",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Integer n = integer(a[0]);
                 Real rho = real(a[1]);
                 std::vector<Real> xs = reals(a[2]), ys = reals(a[3]);
                 if (xs.size() != ys.size())
                     throw InvalidParams("the xs and ys differ in size");
                 return [=] {
                     BivariateCumulativeStudentDistribution f(static_cast<Natural>(n), rho);
                     json::array out;
                     for (Size i = 0; i < xs.size(); ++i)
                         out.emplace_back(f(xs[i], ys[i]));
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
