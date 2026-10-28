// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/matrixutilities/sparseilupreconditioner.hpp, .cpp, which the wheel doesn't bind: the incomplete LU factors
// of a square sparse matrix, given by its rows (its zeros not stored), and the preconditioner applied to vectors.
//
//     SparseILUPreconditioner  (rows, lfil, [b, ...]):  {"l": rows, "u": rows, "apply": [x, ...]}

#include "convert.hpp"

#include <ql/math/matrixutilities/sparseilupreconditioner.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        json::value rowsResult(const SparseMatrix& m) {
            json::array out;
            for (Size i = 0; i < m.size1(); ++i) {
                json::array row;
                for (Size j = 0; j < m.size2(); ++j)
                    row.emplace_back(Real(m(i, j)));
                out.emplace_back(std::move(row));
            }
            return out;
        }

        const Register unit({
            {"SparseILUPreconditioner",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 if (!a[0].is_array() || a[0].get_array().empty())
                     throw InvalidParams("expected the matrix's rows, got " + json::serialize(a[0]));
                 const json::array& rows = a[0].get_array();
                 Size n = rows.size();
                 std::vector<std::vector<Real>> dense;
                 for (Size i = 0; i < n; ++i) {
                     dense.push_back(reals(rows[i]));
                     if (dense.back().size() != n)
                         throw InvalidParams("expected a square matrix");
                 }
                 Integer lfil = integer(a[1]);
                 if (!a[2].is_array())
                     throw InvalidParams("expected the vectors to apply to");
                 std::vector<std::vector<Real>> bs;
                 for (const json::value& b : a[2].get_array()) {
                     bs.push_back(reals(b));
                     if (bs.back().size() != n)
                         throw InvalidParams("a vector of the matrix's size");
                 }
                 return [=] {
                     SparseMatrix m(n, n);
                     for (Size i = 0; i < n; ++i)
                         for (Size j = 0; j < n; ++j)
                             if (dense[i][j] != 0.0)
                                 m(i, j) = dense[i][j];
                     SparseILUPreconditioner ilu(m, lfil);
                     json::array applied;
                     for (const std::vector<Real>& b : bs) {
                         json::array x;
                         for (Real xi : ilu.apply(Array(b.begin(), b.end())))
                             x.emplace_back(xi);
                         applied.emplace_back(std::move(x));
                     }
                     json::object out;
                     out["l"] = rowsResult(ilu.L());
                     out["u"] = rowsResult(ilu.U());
                     out["apply"] = std::move(applied);
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
