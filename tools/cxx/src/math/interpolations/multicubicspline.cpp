// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/interpolations/multicubicspline.hpp: the n-dimensional cubic spline, which the wheel doesn't expose.
// "MultiCubicSpline::operator()" takes (grid, values, extrapolation, x): the grid's 1 to 6 dimensions, each a list of
// points; the values at the grid's points, flattened with the first dimension slowest; the extrapolation flags, one
// per dimension or none (QuantLib's default, none extrapolating); and the point. It builds the spline's data table
// through FdmNdimSolver<N>::setValue, QuantLib's own nested write, constructs MultiCubicSpline<N> (its checks of the
// grid) and returns its value at the point.

#include "convert.hpp"

// QuantLib's FdmNdimSolver sums the layout's Size coordinates into an unsigned long (fdmndimsolver.hpp:103),
// which MSVC warns of; its own build doesn't treat warnings as errors
#pragma warning(disable : 4267)

#include <ql/math/interpolations/multicubicspline.hpp>
#include <ql/methods/finitedifferences/solvers/fdmndimsolver.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        template <Size N>
        Real splineAt(const SplineGrid& grid, const std::vector<Real>& values, const std::vector<bool>& ae,
                      const std::vector<Real>& x) {
            typename MultiCubicSpline<N>::data_table f(grid);
            std::vector<Size> coordinates(N);
            for (Size k = 0; k < values.size(); ++k) {
                Size rest = k;
                for (Size j = N; j-- > 0;) {
                    coordinates[j] = rest % grid[j].size();
                    rest /= grid[j].size();
                }
                FdmNdimSolver<N>::setValue(f, coordinates, values[k]);
            }
            MultiCubicSpline<N> spline(grid, f, ae);
            return spline(x);
        }

        const Register unit({
            {"MultiCubicSpline::operator()",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 if (!a[0].is_array())
                     throw InvalidParams("expected a grid, got " + json::serialize(a[0]));
                 SplineGrid grid;
                 for (const json::value& axis : a[0].get_array())
                     grid.push_back(reals(axis));
                 if (grid.empty() || grid.size() > 6)
                     throw InvalidParams("the grid has 1 to 6 dimensions");
                 Size total = 1;
                 for (const auto& axis : grid) {
                     if (axis.empty())
                         throw InvalidParams("an empty dimension");
                     total *= axis.size();
                 }
                 const std::vector<Real> values = reals(a[1]);
                 if (values.size() != total)
                     throw InvalidParams("the values don't fill the grid");
                 if (!a[2].is_array())
                     throw InvalidParams("expected the extrapolation flags, got " + json::serialize(a[2]));
                 std::vector<bool> ae;
                 for (const json::value& flag : a[2].get_array())
                     ae.push_back(boolean(flag));
                 if (!ae.empty() && ae.size() != grid.size())
                     throw InvalidParams("one extrapolation flag per dimension");
                 const std::vector<Real> x = reals(a[3]);
                 if (x.size() != grid.size())
                     throw InvalidParams("one coordinate per dimension");
                 return [=] {
                     switch (grid.size()) {
                       case 1: return json::value(splineAt<1>(grid, values, ae, x));
                       case 2: return json::value(splineAt<2>(grid, values, ae, x));
                       case 3: return json::value(splineAt<3>(grid, values, ae, x));
                       case 4: return json::value(splineAt<4>(grid, values, ae, x));
                       case 5: return json::value(splineAt<5>(grid, values, ae, x));
                       default: return json::value(splineAt<6>(grid, values, ae, x));
                     }
                 };
             }},
        });

    }

}
