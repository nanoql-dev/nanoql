// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/matrixutilities/gmres.hpp, .cpp: the result's relative residuals, which the wheel's solve and
// solveWithRestart don't return, with the solution. The system is given by its rows; the preconditioner is none,
// Jacobi's (the diagonal's inverse) or the matrix's inverse, as tools/vectors/gen/math/matrixutilities/bicgstab.py
// builds them.

#include "convert.hpp"

#include <ql/math/matrix.hpp>
#include <ql/math/matrixutilities/gmres.hpp>

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

        GMRES::MatrixMult preconditioner(const std::string& kind, const Matrix& m) {
            if (kind == "none")
                return GMRES::MatrixMult();
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

        json::value result(const GMRESResult& r) {
            json::array errors, x;
            for (Real e : r.errors)
                errors.push_back(json::value(e));
            for (Real xi : r.x)
                x.push_back(json::value(xi));
            return json::value(json::array{errors, x});
        }

        Call solve(const Args& a, bool restarted) {
            arity(a, restarted ? 7 : 6);
            Size k = restarted ? 1 : 0;
            Matrix m = rowsMatrix(a[k]);
            std::vector<Real> b = reals(a[k + 1]);
            std::vector<Real> x0 = a[k + 2].is_null() ? std::vector<Real>() : reals(a[k + 2]);
            Integer maxIter = integer(a[k + 3]);
            Real relTol = real(a[k + 4]);
            std::string kind = text(a[k + 5]);
            Integer restart = restarted ? integer(a[0]) : 0;
            if (maxIter < 0 || restart < 0)
                throw InvalidParams("a count must be non-negative");
            return [=] {
                GMRES solver([m](const Array& x) { return Array(m * x); }, static_cast<Size>(maxIter), relTol,
                             preconditioner(kind, m));
                Array rhs(b.begin(), b.end()), guess(x0.begin(), x0.end());
                return result(restarted ? solver.solveWithRestart(static_cast<Size>(restart), rhs, guess)
                                        : solver.solve(rhs, guess));
            };
        }

        const Register unit({
            {"GMRES::solve", [](const Args& a) -> Call { return solve(a, false); }},
            {"GMRES::solveWithRestart", [](const Args& a) -> Call { return solve(a, true); }},
        });

    }

}
