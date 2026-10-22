// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/barrieroption/vannavolgainterpolation.hpp: VannaVolgaInterpolation and the VannaVolga factory,
// which no wheel class exposes, each read back through its values at the given points, extrapolating where needed.
//
//     VannaVolgaInterpolation::VannaVolgaInterpolation   (x, y, spot, dDiscount, fDiscount, T, points)
//                                                        the interpolation of the three volatilities y at the
//                                                        strikes x, updated once, as its constructor does
//     VannaVolga::interpolate                            (spot, dDiscount, fDiscount, T, x, y, points)
//                                                        the factory's interpolation
//     VannaVolga::requiredPoints                         ()   the factory's trait
//
// The interpolation's own check (exactly three strikes) is QuantLib's; the harness refuses only fewer volatilities
// than strikes, which QuantLib would read past.

#include "math/interpolationspec.hpp"

#include <ql/experimental/barrieroption/vannavolgainterpolation.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"VannaVolgaInterpolation::VannaVolgaInterpolation",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 std::vector<Real> x = reals(a[0]), y = reals(a[1]);
                 Real spot = real(a[2]), dDiscount = real(a[3]), fDiscount = real(a[4]), t = real(a[5]);
                 std::vector<Real> points = reals(a[6]);
                 checkSizes(x, y);
                 return [=] {
                     VannaVolgaInterpolation f(x.begin(), x.end(), y.begin(), spot, dDiscount, fDiscount, t);
                     return valuesAt(f, points);
                 };
             }},
            {"VannaVolga::interpolate",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 Real spot = real(a[0]), dDiscount = real(a[1]), fDiscount = real(a[2]), t = real(a[3]);
                 std::vector<Real> x = reals(a[4]), y = reals(a[5]), points = reals(a[6]);
                 checkSizes(x, y);
                 return [=] {
                     const VannaVolga factory(spot, dDiscount, fDiscount, t);
                     return valuesAt(factory.interpolate(x.begin(), x.end(), y.begin()), points);
                 };
             }},
            {"VannaVolga::requiredPoints",
             [](const Args& a) -> Call {
                 arity(a, 0);
                 return [] { return json::value(static_cast<std::int64_t>(VannaVolga::requiredPoints)); };
             }},
        });

    }

}
