// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/generallinearleastsquares.hpp: the regression, which the wheel doesn't expose, on the basis systems of
// ql/methods/montecarlo/lsmbasissystem.hpp. Each method fits inside the call and returns {"coefficients",
// "residuals", "standardErrors", "error", "size", "dim"}:
//
//     GeneralLinearLeastSquares(Real)   (xs, ys, order, polynomialType)          the one-factor basis on reals
//     GeneralLinearLeastSquares(Array)  (points, ys, dim, order, polynomialType)  the multi-factor basis on arrays
//
// The points and values have the same size: QuantLib checks the values' size against itself and writes the points
// past the matrix's column, so other sizes aren't sent.

#include "convert.hpp"

#include <ql/math/array.hpp>
#include <ql/math/generallinearleastsquares.hpp>
#include <ql/methods/montecarlo/lsmbasissystem.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        Size size(const json::value& v) {
            long long n = int64(v);
            if (n < 0)
                throw InvalidParams("expected a non-negative size");
            return Size(n);
        }

        json::value arrayResult(const Array& a) {
            json::array out;
            for (Real x : a)
                out.emplace_back(x);
            return json::value(std::move(out));
        }

        json::value fitResult(const GeneralLinearLeastSquares& ls) {
            json::object out;
            out["coefficients"] = arrayResult(ls.coefficients());
            out["residuals"] = arrayResult(ls.residuals());
            out["standardErrors"] = arrayResult(ls.standardErrors());
            out["error"] = arrayResult(ls.error());
            out["size"] = static_cast<std::int64_t>(ls.size());
            out["dim"] = static_cast<std::int64_t>(ls.dim());
            return json::value(std::move(out));
        }

        const Register unit({
            {"GeneralLinearLeastSquares(Real)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 std::vector<Real> xs = reals(a[0]), ys = reals(a[1]);
                 Size order = size(a[2]);
                 auto type = enumeration<LsmBasisSystem::PolynomialType>(a[3]);
                 if (xs.size() != ys.size())
                     throw InvalidParams("the points and values have the same size");
                 return [=] {
                     auto basis = LsmBasisSystem::pathBasisSystem(order, type);
                     return fitResult(GeneralLinearLeastSquares(xs, ys, basis));
                 };
             }},
            {"GeneralLinearLeastSquares(Array)",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 if (!a[0].is_array())
                     throw InvalidParams("expected the points, a list of lists of reals");
                 std::vector<std::vector<Real>> points;
                 for (const json::value& p : a[0].get_array())
                     points.push_back(reals(p));
                 std::vector<Real> ys = reals(a[1]);
                 Size dim = size(a[2]), order = size(a[3]);
                 auto type = enumeration<LsmBasisSystem::PolynomialType>(a[4]);
                 if (points.size() != ys.size())
                     throw InvalidParams("the points and values have the same size");
                 for (const auto& p : points) {
                     if (p.size() != dim)
                         throw InvalidParams("a point holds one element per factor");
                 }
                 return [=] {
                     std::vector<Array> xs;
                     for (const auto& p : points)
                         xs.emplace_back(p.begin(), p.end());
                     auto basis = LsmBasisSystem::multiPathBasisSystem(dim, order, type);
                     return fitResult(GeneralLinearLeastSquares(xs, ys, basis));
                 };
             }},
        });

    }

}
