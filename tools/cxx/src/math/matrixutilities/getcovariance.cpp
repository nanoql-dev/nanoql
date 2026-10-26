// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/matrixutilities/getcovariance.hpp, .cpp: the covariance decomposition, which the wheel doesn't expose.
// "CovarianceDecomposition" takes (rows, tolerance), the covariance matrix's rows, and returns {"variances",
// "standardDeviations", "correlationMatrix"}, the last's elements row by row.

#include "convert.hpp"

#include <ql/math/matrixutilities/getcovariance.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"CovarianceDecomposition",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 if (!a[0].is_array())
                     throw InvalidParams("expected the rows, got " + json::serialize(a[0]));
                 std::vector<std::vector<Real>> rows;
                 for (const json::value& row : a[0].get_array())
                     rows.push_back(reals(row));
                 const Size columns = rows.empty() ? 0 : rows[0].size();
                 for (const auto& row : rows)
                     if (row.size() != columns)
                         throw InvalidParams("rows of different sizes");
                 const Real tolerance = real(a[1]);
                 return [=] {
                     Matrix cov(rows.size(), columns);
                     for (Size i = 0; i < rows.size(); ++i)
                         for (Size j = 0; j < columns; ++j)
                             cov[i][j] = rows[i][j];
                     const CovarianceDecomposition d(cov, tolerance);
                     json::array variances, stdDevs, correlation;
                     for (Real x : d.variances())
                         variances.emplace_back(x);
                     for (Real x : d.standardDeviations())
                         stdDevs.emplace_back(x);
                     const Matrix& c = d.correlationMatrix();
                     for (Size i = 0; i < c.rows(); ++i)
                         for (Size j = 0; j < c.columns(); ++j)
                             correlation.emplace_back(c[i][j]);
                     return json::value(json::object{{"variances", variances}, {"standardDeviations", stdDevs},
                                                     {"correlationMatrix", correlation}});
                 };
             }},
        });

    }

}
