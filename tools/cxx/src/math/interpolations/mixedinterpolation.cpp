// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/interpolations/mixedinterpolation.hpp: the MixedLinearCubic factory's interpolate and traits. A factory's
// interpolation is read back through its values at the given points, extrapolating where needed. The mixed
// interpolations themselves are interpolation specs (math/interpolationspec.hpp), read through Interpolation's members.

#include "math/interpolationspec.hpp"

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            // (spec, x, y, points).
            {"MixedLinearCubic::interpolate",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 InterpolatorSpec s = interpolatorSpec(a[0], {"MixedLinearCubic"});
                 std::vector<Real> x = reals(a[1]), y = reals(a[2]), points = reals(a[3]);
                 checkSizes(x, y);
                 return [=] {
                     const MixedLinearCubic factory = makeMixedLinearCubicInterpolator(s);
                     return valuesAt(factory.interpolate(x.begin(), x.end(), y.begin()), points);
                 };
             }},
            {"MixedLinearCubic::global",
             [](const Args& a) -> Call {
                 arity(a, 0);
                 return [] { return json::value(MixedLinearCubic::global); };
             }},
            {"MixedLinearCubic::requiredPoints",
             [](const Args& a) -> Call {
                 arity(a, 0);
                 return [] { return json::value(static_cast<std::int64_t>(MixedLinearCubic::requiredPoints)); };
             }},
        });

    }

}
