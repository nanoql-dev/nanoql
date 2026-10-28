// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/matrixutilities/tapcorrelations.hpp, .cpp, which the wheel doesn't bind: the triangular angles
// parametrizations and the Frobenius cost. Matrices are given and returned by their rows.
//
//     triangularAnglesParametrization                  (angles, matrixSize, rank):  rows
//     lmmTriangularAnglesParametrization               (angles, matrixSize, rank):  rows
//     triangularAnglesParametrizationUnconstrained     (x, matrixSize, rank):       rows
//     lmmTriangularAnglesParametrizationUnconstrained  (x, matrixSize, rank):       rows
//     triangularAnglesParametrizationRankThree         (alpha, t0, epsilon, nbRows):  rows
//     triangularAnglesParametrizationRankThreeVectorial  (parameters, nbRows):      rows
//     FrobeniusCostFunction                            (target rows, parametrization, matrixSize, rank, x):
//                                                      {"value": v, "values": [...]}, the parametrization one of
//                                                      the four names above taking (angles, matrixSize, rank)

#include "convert.hpp"

#include <ql/math/matrix.hpp>
#include <ql/math/matrixutilities/tapcorrelations.hpp>

#include <functional>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

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

        Size size(const json::value& v) {
            Integer n = integer(v);
            if (n < 0)
                throw InvalidParams("a size is non-negative");
            return static_cast<Size>(n);
        }

        using Parametrization = std::function<Matrix(const Array&, Size, Size)>;

        Parametrization parametrization(const std::string& name) {
            if (name == "triangularAnglesParametrization")
                return triangularAnglesParametrization;
            if (name == "lmmTriangularAnglesParametrization")
                return lmmTriangularAnglesParametrization;
            if (name == "triangularAnglesParametrizationUnconstrained")
                return triangularAnglesParametrizationUnconstrained;
            if (name == "lmmTriangularAnglesParametrizationUnconstrained")
                return lmmTriangularAnglesParametrizationUnconstrained;
            throw InvalidParams("unknown parametrization " + name);
        }

        Method angles(const std::string& name) {
            return [name](const Args& a) -> Call {
                arity(a, 3);
                std::vector<Real> x = reals(a[0]);
                Size n = size(a[1]), rank = size(a[2]);
                Parametrization f = parametrization(name);
                return [=] { return rowsResult(f(Array(x.begin(), x.end()), n, rank)); };
            };
        }

        const Register unit({
            {"triangularAnglesParametrization", angles("triangularAnglesParametrization")},
            {"lmmTriangularAnglesParametrization", angles("lmmTriangularAnglesParametrization")},
            {"triangularAnglesParametrizationUnconstrained", angles("triangularAnglesParametrizationUnconstrained")},
            {"lmmTriangularAnglesParametrizationUnconstrained",
             angles("lmmTriangularAnglesParametrizationUnconstrained")},
            {"triangularAnglesParametrizationRankThree",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Real alpha = real(a[0]), t0 = real(a[1]), epsilon = real(a[2]);
                 Size n = size(a[3]);
                 return [=] { return rowsResult(triangularAnglesParametrizationRankThree(alpha, t0, epsilon, n)); };
             }},
            {"triangularAnglesParametrizationRankThreeVectorial",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 std::vector<Real> p = reals(a[0]);
                 Size n = size(a[1]);
                 return [=] {
                     return rowsResult(triangularAnglesParametrizationRankThreeVectorial(Array(p.begin(), p.end()), n));
                 };
             }},
            {"FrobeniusCostFunction",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 if (!a[0].is_array() || a[0].get_array().empty())
                     throw InvalidParams("expected the target's rows, got " + json::serialize(a[0]));
                 const json::array& rows = a[0].get_array();
                 Size n = rows.size();
                 Matrix target(n, n);
                 for (Size i = 0; i < n; ++i) {
                     std::vector<Real> row = reals(rows[i]);
                     if (row.size() != n)
                         throw InvalidParams("expected a square target");
                     for (Size j = 0; j < n; ++j)
                         target[i][j] = row[j];
                 }
                 Parametrization f = parametrization(text(a[1]));
                 Size size1 = size(a[2]), rank = size(a[3]);
                 std::vector<Real> x = reals(a[4]);
                 return [=] {
                     FrobeniusCostFunction cost(target, f, size1, rank);
                     Array point(x.begin(), x.end());
                     json::array values;
                     for (Real v : cost.values(point))
                         values.emplace_back(v);
                     json::object out;
                     out["value"] = cost.value(point);
                     out["values"] = std::move(values);
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
