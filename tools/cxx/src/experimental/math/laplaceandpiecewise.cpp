// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/math/laplaceinterpolation.hpp, .cpp, moorepenroseinverse.hpp, piecewisefunction.hpp and
// piecewiseintegral.hpp, .cpp: Laplace interpolation, the Moore-Penrose inverse and the piecewise function and
// integral, which the wheel doesn't bind.
//
// "laplaceInterpolation" takes (rows, x, y, relTol, maxIterMultiplier): the matrix's rows, null for a missing value,
// and the grids, empty for an equidistant one; it returns {"filled"}, the filled matrix row by row.
//
// "LaplaceInterpolation::values" takes (values, grids, relTol, maxIterMultiplier): the values of every grid point in
// the layout's order (the first coordinate fastest), null for a missing one; it returns {"values"}, every point's in
// the same order.
//
// "moorePenroseInverse" takes (rows, tol), tol null for the default: a tall or square matrix; it returns {"inverse"},
// row by row.
//
// "PiecewiseIntegral::integrate" takes (criticalPoints, avoid, integrator, function, intervals): the integrator
// "segment" [intervals] or "simpson" [accuracy, maxIterations], the function "step" [X..., Y...] split by the
// parameter "breaks" (QL_PIECEWISE_FUNCTION) or "exp"; it returns {"values"}, each interval's integral.
//
// "QL_PIECEWISE_FUNCTION" takes (X, Y, points): it returns {"values"}.

#include "convert.hpp"

#include <ql/experimental/math/laplaceinterpolation.hpp>
#include <ql/experimental/math/moorepenroseinverse.hpp>
#include <ql/experimental/math/piecewisefunction.hpp>
#include <ql/experimental/math/piecewiseintegral.hpp>
#include <ql/math/integrals/segmentintegral.hpp>
#include <ql/math/integrals/simpsonintegral.hpp>

#include <cmath>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        std::vector<std::vector<Real>> rowsOf(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected a list of rows, got " + json::serialize(v));
            std::vector<std::vector<Real>> out;
            for (const json::value& r : v.get_array())
                out.push_back(reals(r));
            return out;
        }

        Matrix matrixOf(const std::vector<std::vector<Real>>& rows) {
            Size c = rows.empty() ? 0 : rows[0].size();
            Matrix m(rows.size(), c);
            for (Size i = 0; i < rows.size(); ++i) {
                if (rows[i].size() != c)
                    throw InvalidParams("rows of different lengths");
                for (Size j = 0; j < c; ++j)
                    m(i, j) = rows[i][j];
            }
            return m;
        }

        json::array flat(const Matrix& m) {
            json::array out;
            for (Size i = 0; i < m.rows(); ++i)
                for (Size j = 0; j < m.columns(); ++j)
                    out.emplace_back(m(i, j));
            return out;
        }

        const Register unit({
            {"laplaceInterpolation",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 auto rows = rowsOf(a[0]);
                 std::vector<Real> x = reals(a[1]), y = reals(a[2]);
                 Real relTol = real(a[3]);
                 Size multiplier = static_cast<Size>(integer(a[4]));
                 return [=] {
                     Matrix m = matrixOf(rows);
                     laplaceInterpolation(m, x, y, relTol, multiplier);
                     json::object out;
                     out["filled"] = flat(m);
                     return json::value(out);
                 };
             }},
            {"LaplaceInterpolation::values",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 std::vector<Real> values = reals(a[0]);
                 auto gridRows = rowsOf(a[1]);
                 Real relTol = real(a[2]);
                 Size multiplier = static_cast<Size>(integer(a[3]));
                 return [=] {
                     std::vector<Size> dims;
                     for (const auto& g : gridRows)
                         dims.push_back(g.size());
                     auto index = [dims](const std::vector<Size>& c) {
                         Size k = 0, stride = 1;
                         for (Size d = 0; d < dims.size(); ++d) {
                             k += c[d] * stride;
                             stride *= dims[d];
                         }
                         return k;
                     };
                     LaplaceInterpolation l([&](const std::vector<Size>& c) { return values[index(c)]; }, gridRows,
                                            relTol, multiplier);
                     json::array out;
                     std::vector<Size> c(dims.size(), 0);
                     for (Size k = 0; k < values.size(); ++k) {
                         out.emplace_back(l(c));
                         for (Size d = 0; d < dims.size(); ++d) {
                             if (++c[d] < dims[d])
                                 break;
                             c[d] = 0;
                         }
                     }
                     json::object o;
                     o["values"] = out;
                     return json::value(o);
                 };
             }},
            {"moorePenroseInverse",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 auto rows = rowsOf(a[0]);
                 Real tol = real(a[1]);
                 return [=] {
                     Matrix m = matrixOf(rows);
                     json::object out;
                     out["inverse"] = flat(moorePenroseInverse(m, tol));
                     return json::value(out);
                 };
             }},
            {"PiecewiseIntegral::integrate",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 std::vector<Real> critical = reals(a[0]);
                 bool avoid = boolean(a[1]);
                 std::string integrator = text(a[2]);
                 std::vector<Real> ip = reals(a[3]);
                 std::string function = text(a[4]);
                 std::vector<Real> fp = reals(a[5]);
                 auto intervals = rowsOf(a[6]);
                 return [=] {
                     ext::shared_ptr<Integrator> inner;
                     if (integrator == "segment")
                         inner = ext::make_shared<SegmentIntegral>(static_cast<Size>(ip[0]));
                     else if (integrator == "simpson")
                         inner = ext::make_shared<SimpsonIntegral>(ip[0], static_cast<Size>(ip[1]));
                     else
                         throw InvalidParams("unknown integrator " + integrator);
                     PiecewiseIntegral piecewise(inner, critical, avoid);
                     std::vector<Real> X, Y;
                     if (function == "step") {
                         Size breaks = static_cast<Size>(fp[0]);
                         X.assign(fp.begin() + 1, fp.begin() + 1 + breaks);
                         Y.assign(fp.begin() + 1 + breaks, fp.end());
                     }
                     auto f = [&](Real t) {
                         if (function == "step")
                             return QL_PIECEWISE_FUNCTION(X, Y, t);
                         return std::exp(t);
                     };
                     json::array values;
                     for (const auto& ab : intervals)
                         values.emplace_back(piecewise(f, ab[0], ab[1]));
                     json::object out;
                     out["values"] = values;
                     return json::value(out);
                 };
             }},
            {"QL_PIECEWISE_FUNCTION",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 std::vector<Real> X = reals(a[0]), Y = reals(a[1]), points = reals(a[2]);
                 return [=] {
                     json::array values;
                     for (Real t : points)
                         values.emplace_back(QL_PIECEWISE_FUNCTION(X, Y, t));
                     json::object out;
                     out["values"] = values;
                     return json::value(out);
                 };
             }},
        });

    }

}
