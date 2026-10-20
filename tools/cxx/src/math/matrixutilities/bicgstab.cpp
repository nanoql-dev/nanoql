// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/matrixutilities/bicgstab.hpp, .cpp: the result's iteration count and error, which the wheel's solve doesn't
// return, with the solution. The system is given by its rows; the preconditioner is none, Jacobi's (the diagonal's
// inverse) or the matrix's inverse, as tools/vectors/gen/math/matrixutilities/bicgstab.py builds them.

#include "convert.hpp"

#include <ql/math/matrix.hpp>
#include <ql/math/matrixutilities/bicgstab.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        Matrix rowsMatrix(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected the matrix's rows, got " + json::serialize(v));
            const json::array& rows = v.get_array();
            Size n = rows.size();
            Matrix m(n, n);
            for (Size i = 0; i < n; ++i) {
                std::vector<Real> row = reals(rows[i]);
                if (row.size() != n)
                    throw InvalidParams("expected a square matrix");
                for (Size j = 0; j < n; ++j)
                    m[i][j] = row[j];
            }
            return m;
        }

        BiCGstab::MatrixMult preconditioner(const std::string& kind, const Matrix& m) {
            if (kind == "none")
                return BiCGstab::MatrixMult();
            if (kind == "jacobi")
                return [m](const Array& x) {
                    Array y(x.size());
                    for (Size i = 0; i < x.size(); ++i)
                        y[i] = x[i] / m[i][i];
                    return y;
                };
            if (kind == "inverse") {
                Matrix inv = inverse(m);
                return [inv](const Array& x) { return Array(inv * x); };
            }
            throw InvalidParams("unknown preconditioner " + kind);
        }

        const Register unit({
            {"BiCGstab::solve",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 Matrix m = rowsMatrix(a[0]);
                 std::vector<Real> b = reals(a[1]);
                 std::vector<Real> x0 = a[2].is_null() ? std::vector<Real>() : reals(a[2]);
                 Integer maxIter = integer(a[3]);
                 if (maxIter < 0)
                     throw InvalidParams("maxIter must be non-negative");
                 Real relTol = real(a[4]);
                 std::string kind = text(a[5]);
                 return [=] {
                     BiCGstab solver([m](const Array& x) { return Array(m * x); }, static_cast<Size>(maxIter), relTol,
                                     preconditioner(kind, m));
                     BiCGStabResult r = solver.solve(Array(b.begin(), b.end()), Array(x0.begin(), x0.end()));
                     json::array x;
                     for (Real xi : r.x)
                         x.push_back(json::value(xi));
                     return json::value(json::array{static_cast<std::int64_t>(r.iterations), r.error, x});
                 };
             }},
        });

    }

}
