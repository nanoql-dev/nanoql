// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/matrixutilities/tqreigendecomposition.hpp, .cpp, which the wheel doesn't expose: the eigenvalues, the
// eigenvectors (every row, none or the first, by the calculation asked) and the iteration count of a symmetric
// tridiagonal matrix, under each shift strategy.

#include "convert.hpp"

#include <ql/math/matrixutilities/tqreigendecomposition.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"TqrEigenDecomposition",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 std::vector<Real> diag = reals(a[0]), sub = reals(a[1]);
                 auto calc = enumeration<TqrEigenDecomposition::EigenVectorCalculation>(a[2]);
                 auto strategy = enumeration<TqrEigenDecomposition::ShiftStrategy>(a[3]);
                 return [=] {
                     TqrEigenDecomposition tqr(Array(diag.begin(), diag.end()), Array(sub.begin(), sub.end()), calc,
                                               strategy);
                     json::array values;
                     for (Real d : tqr.eigenvalues())
                         values.push_back(json::value(d));
                     json::array vectors;
                     const Matrix& ev = tqr.eigenvectors();
                     for (Size i = 0; i < ev.rows(); ++i) {
                         json::array row;
                         for (Size j = 0; j < ev.columns(); ++j)
                             row.push_back(json::value(ev[i][j]));
                         vectors.push_back(json::value(row));
                     }
                     return json::value(json::array{values, vectors, static_cast<std::int64_t>(tqr.iterations())});
                 };
             }},
        });

    }

}
