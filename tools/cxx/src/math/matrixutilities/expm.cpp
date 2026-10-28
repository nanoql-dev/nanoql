// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/matrixutilities/expm.hpp, .cpp, which the wheel doesn't bind: the matrix exponential by adaptive Runge-Kutta.
// Matrices are given and returned by their rows.
//
//     Expm  (rows, t, tol):  rows of exp(t M)

#include "convert.hpp"

#include <ql/math/matrix.hpp>
#include <ql/math/matrixutilities/expm.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"Expm",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 if (!a[0].is_array() || a[0].get_array().empty())
                     throw InvalidParams("expected the matrix's rows, got " + json::serialize(a[0]));
                 const json::array& rows = a[0].get_array();
                 Size n = rows.size();
                 Matrix m(n, n);
                 for (Size i = 0; i < n; ++i) {
                     std::vector<Real> row = reals(rows[i]);
                     if (row.size() != n)
                         throw InvalidParams("expected a square matrix");
                     for (Size j = 0; j < n; ++j)
                         m[i][j] = row[j];
                 }
                 Real t = real(a[1]), tol = real(a[2]);
                 return [=] {
                     Matrix e = Expm(m, t, tol);
                     json::array out;
                     for (Size i = 0; i < e.rows(); ++i) {
                         json::array row;
                         for (Size j = 0; j < e.columns(); ++j)
                             row.emplace_back(e[i][j]);
                         out.emplace_back(std::move(row));
                     }
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
