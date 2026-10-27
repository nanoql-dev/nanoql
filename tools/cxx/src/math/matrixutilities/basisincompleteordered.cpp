// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/matrixutilities/basisincompleteordered.hpp, .cpp, which the wheel doesn't expose. "BasisIncompleteOrdered"
// takes (euclideanDimension, vectors) and returns {"added" (each addVector's result), "basisSize",
// "euclideanDimension", "basis" (getBasisAsRowsInMatrix, a row each)}; "OrthogonalProjections" takes
// (originalVectors, a row each, multiplierCutOff, tolerance) and returns {"validVectors", "numberValidVectors",
// "vectors" (GetVector(i) of each)}.

#include "convert.hpp"

#include <ql/math/matrixutilities/basisincompleteordered.hpp>

#include <cstdint>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using Mat = std::vector<std::vector<Real>>;

        Mat rows(const json::value& v) {
            Mat out;
            for (const json::value& row : v.as_array())
                out.push_back(reals(row));
            return out;
        }

        json::value result(const Matrix& m) {
            json::array out;
            for (Size i = 0; i < m.rows(); ++i) {
                json::array row;
                for (Size j = 0; j < m.columns(); ++j)
                    row.emplace_back(m[i][j]);
                out.emplace_back(row);
            }
            return out;
        }

        const Register unit({
            {"BasisIncompleteOrdered",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 const Size dimension = static_cast<Size>(integer(a[0]));
                 const Mat vectors = rows(a[1]);
                 return [=] {
                     BasisIncompleteOrdered basis(dimension);
                     json::array added;
                     for (const std::vector<Real>& x : vectors)
                         added.emplace_back(basis.addVector(Array(x.begin(), x.end())));
                     json::object out;
                     out["added"] = added;
                     out["basisSize"] = static_cast<std::int64_t>(basis.basisSize());
                     out["euclideanDimension"] = static_cast<std::int64_t>(basis.euclideanDimension());
                     out["basis"] = result(basis.getBasisAsRowsInMatrix());
                     return json::value(out);
                 };
             }},
            {"OrthogonalProjections",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 const Mat vectors = rows(a[0]);
                 const Real multiplierCutOff = real(a[1]), tolerance = real(a[2]);
                 return [=] {
                     Matrix original(vectors.size(), vectors.empty() ? 0 : vectors[0].size());
                     for (Size i = 0; i < original.rows(); ++i)
                         for (Size j = 0; j < original.columns(); ++j)
                             original[i][j] = vectors[i][j];
                     OrthogonalProjections projector(original, multiplierCutOff, tolerance);
                     json::array valid, projected;
                     for (Size i = 0; i < original.rows(); ++i) {
                         valid.emplace_back(static_cast<bool>(projector.validVectors()[i]));
                         json::array x;
                         for (Real xi : projector.GetVector(i))
                             x.emplace_back(xi);
                         projected.emplace_back(x);
                     }
                     json::object out;
                     out["validVectors"] = valid;
                     out["numberValidVectors"] = static_cast<std::int64_t>(projector.numberValidVectors());
                     out["vectors"] = projected;
                     return json::value(out);
                 };
             }},
        });

    }

}
