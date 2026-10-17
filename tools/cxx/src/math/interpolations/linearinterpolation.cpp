// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/interpolations/linearinterpolation.hpp: the Linear factory's interpolate and traits. Its interpolation is read
// back through its values at the given points, extrapolating where needed: (x, y, points).

#include "math/interpolationspec.hpp"

#include <ql/math/interpolations/linearinterpolation.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"Linear::interpolate",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 std::vector<Real> x = reals(a[0]), y = reals(a[1]), points = reals(a[2]);
                 checkSizes(x, y);
                 return [=] { return valuesAt(Linear().interpolate(x.begin(), x.end(), y.begin()), points); };
             }},
            {"Linear::global",
             [](const Args& a) -> Call {
                 arity(a, 0);
                 return [] { return json::value(Linear::global); };
             }},
            {"Linear::requiredPoints",
             [](const Args& a) -> Call {
                 arity(a, 0);
                 return [] { return json::value(static_cast<std::int64_t>(Linear::requiredPoints)); };
             }},
        });

    }

}
