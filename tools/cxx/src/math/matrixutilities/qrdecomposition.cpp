// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/matrixutilities/qrdecomposition.hpp, .cpp, which the wheel doesn't bind: the QR decomposition with and
// without pivoting, and the least-squares solve. Matrices are given and returned by their rows.
//
//     qrDecomposition  (rows, pivot):  {"q": rows, "r": rows, "ipvt": [...]}
//     qrSolve          (rows, b, pivot, d):  x, d an empty array for QuantLib's default

#include "convert.hpp"

#include <ql/math/matrix.hpp>
#include <ql/math/matrixutilities/qrdecomposition.hpp>

#include <cstdint>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        Matrix rowsMatrix(const json::value& v) {
            if (!v.is_array() || v.get_array().empty())
                throw InvalidParams("expected the matrix's rows, got " + json::serialize(v));
            const json::array& rows = v.get_array();
            std::vector<Real> first = reals(rows[0]);
            Matrix m(rows.size(), first.size());
            for (Size i = 0; i < rows.size(); ++i) {
                std::vector<Real> row = reals(rows[i]);
                if (row.size() != m.columns())
                    throw InvalidParams("expected rows of one length");
                for (Size j = 0; j < m.columns(); ++j)
                    m[i][j] = row[j];
            }
            return m;
        }

        json::value rowsResult(const Matrix& m) {
            json::array out;
            for (Size i = 0; i < m.rows(); ++i) {
                json::array row;
                for (Size j = 0; j < m.columns(); ++j)
                    row.emplace_back(m[i][j]);
                out.emplace_back(std::move(row));
            }
            return out;
        }

        const Register unit({
            {"qrDecomposition",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 Matrix m = rowsMatrix(a[0]);
                 bool pivot = boolean(a[1]);
                 return [=] {
                     Matrix q, r;
                     std::vector<Size> ipvt = qrDecomposition(m, q, r, pivot);
                     json::array p;
                     for (Size i : ipvt)
                         p.emplace_back(static_cast<std::int64_t>(i));
                     json::object out;
                     out["q"] = rowsResult(q);
                     out["r"] = rowsResult(r);
                     out["ipvt"] = std::move(p);
                     return json::value(std::move(out));
                 };
             }},
            {"qrSolve",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Matrix m = rowsMatrix(a[0]);
                 std::vector<Real> b = reals(a[1]);
                 bool pivot = boolean(a[2]);
                 std::vector<Real> d = reals(a[3]);
                 return [=] {
                     Array x = qrSolve(m, Array(b.begin(), b.end()), pivot, Array(d.begin(), d.end()));
                     json::array out;
                     for (Real xi : x)
                         out.emplace_back(xi);
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
