// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/inflation/polynomial2Dspline.hpp: the 2-D polynomial spline, a parabola in y per column and a
// monotonic natural cubic spline in x across them, which the wheel doesn't bind. "Polynomial2DSpline::values" takes
// (x, y, z, points, allowExtrapolation): the grid, z a row per y value and a column per x value, and the points [x, y]
// it is read at; it builds the interpolation through the Polynomial factory inside the call and returns each point's
// value, called with the flag, {"throws": true} where QuantLib refuses it (outside the grid without the flag). A grid
// QuantLib refuses (z's rows not one per y value) is the generator's whole-case refusal.

#include "convert.hpp"

#include <ql/experimental/inflation/polynomial2Dspline.hpp>
#include <ql/math/matrix.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            // (x, y, z, points, allowExtrapolation)
            {"Polynomial2DSpline::values",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 std::vector<Real> x = reals(a[0]);
                 std::vector<Real> y = reals(a[1]);
                 if (!a[2].is_array())
                     throw InvalidParams("z is an array of rows");
                 std::vector<std::vector<Real>> z;
                 for (const auto& r : a[2].as_array())
                     z.push_back(reals(r));
                 if (!a[3].is_array())
                     throw InvalidParams("points is an array of [x, y]");
                 std::vector<std::pair<Real, Real>> points;
                 for (const auto& p : a[3].as_array()) {
                     std::vector<Real> xy = reals(p);
                     if (xy.size() != 2)
                         throw InvalidParams("a point is [x, y]");
                     points.emplace_back(xy[0], xy[1]);
                 }
                 bool extrapolate = boolean(a[4]);
                 return [=] {
                     Matrix m(z.size(), z.empty() ? 0 : z[0].size());
                     for (Size i = 0; i < z.size(); ++i) {
                         if (z[i].size() != m.columns())
                             throw InvalidParams("z's rows have one length");
                         for (Size j = 0; j < z[i].size(); ++j)
                             m[i][j] = z[i][j];
                     }
                     Interpolation2D f = Polynomial().interpolate(x.begin(), x.end(), y.begin(), y.end(), m);
                     json::array values;
                     for (const auto& p : points) {
                         try {
                             values.emplace_back(f(p.first, p.second, extrapolate));
                         } catch (const QuantLib::Error&) {
                             values.emplace_back(json::object{{"throws", true}});
                         }
                     }
                     return json::value(values);
                 };
             }},
        });

    }

}
