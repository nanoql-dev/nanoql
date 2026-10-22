// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/matrixutilities/pseudosqrt.hpp, .cpp: rankReducedSqrt, which the wheel doesn't expose. "rankReducedSqrt"
// takes (matrix, maxRank, componentRetainedPercentage, salvagingAlgorithm), the matrix as its list of rows and the
// algorithm as SalvagingAlgorithm::Type's integer, and returns the result's list of rows.

#include "convert.hpp"

#include <ql/math/matrix.hpp>
#include <ql/math/matrixutilities/pseudosqrt.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using Rows = std::vector<std::vector<Real>>;

        Rows rows(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected a matrix's list of rows, got " + json::serialize(v));
            Rows out;
            for (const json::value& row : v.as_array())
                out.push_back(reals(row));
            for (const auto& row : out) {
                if (row.size() != out[0].size())
                    throw InvalidParams("a matrix's rows have one length");
            }
            return out;
        }

        Matrix make(const Rows& r) {
            Matrix m(r.size(), r.empty() ? 0 : r[0].size());
            for (Size i = 0; i < m.rows(); ++i)
                for (Size j = 0; j < m.columns(); ++j)
                    m[i][j] = r[i][j];
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
            return json::value(std::move(out));
        }

        const Register unit({
            {"rankReducedSqrt",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Rows m = rows(a[0]);
                 Integer maxRank = integer(a[1]);
                 if (maxRank < 0)
                     throw InvalidParams("maxRank is a Size");
                 Real percentage = real(a[2]);
                 auto sa = enumeration<SalvagingAlgorithm::Type>(a[3]);
                 return [=] { return rowsResult(rankReducedSqrt(make(m), Size(maxRank), percentage, sa)); };
             }},
        });

    }

}
