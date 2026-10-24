// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/interpolations/lagrangeinterpolation.hpp: the interpolation of other y values on the same nodes, which the
// wheel doesn't expose.
//
//     LagrangeInterpolation::value  (x, y, otherY, points): value(otherY, z) at each point z of an interpolation built
//                                   on (x, y), otherY holding as many values as x

#include "convert.hpp"

#include <ql/math/array.hpp>
#include <ql/math/interpolations/lagrangeinterpolation.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"LagrangeInterpolation::value",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 std::vector<Real> x = reals(a[0]), y = reals(a[1]), other = reals(a[2]), points = reals(a[3]);
                 if (x.size() < 2 || y.size() != x.size() || other.size() != x.size())
                     throw InvalidParams("at least two nodes, and as many values of each set");
                 return [=] {
                     LagrangeInterpolation interpolation(x.begin(), x.end(), y.begin());
                     Array otherY(other.begin(), other.end());
                     json::array out;
                     for (Real z : points)
                         out.push_back(json::value(interpolation.value(otherY, z)));
                     return json::value(out);
                 };
             }},
        });

    }

}
