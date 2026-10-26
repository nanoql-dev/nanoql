// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/matrixutilities/householder.hpp, .cpp: the Householder transformation and reflection, which the wheel doesn't
// expose. "HouseholderTransformation::operator()" takes (v, x) and returns the transformed vector;
// "HouseholderTransformation::getMatrix" takes (v) and returns the matrix's elements row by row;
// "HouseholderReflection::operator()" and "HouseholderReflection::reflectionVector" take (e, a) and return the
// reflected vector and the reflection's vector.

#include "convert.hpp"

#include <ql/math/matrixutilities/householder.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        Array arrayOf(const json::value& v) {
            const std::vector<Real> x = reals(v);
            return Array(x.begin(), x.end());
        }

        json::value result(const Array& a) {
            json::array out;
            for (Real x : a)
                out.emplace_back(x);
            return out;
        }

        const Register unit({
            {"HouseholderTransformation::operator()",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 const Array v = arrayOf(a[0]), x = arrayOf(a[1]);
                 return [=] { return result(HouseholderTransformation(v)(x)); };
             }},
            {"HouseholderTransformation::getMatrix",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 const Array v = arrayOf(a[0]);
                 return [=] {
                     const Matrix m = HouseholderTransformation(v).getMatrix();
                     json::array out;
                     for (Size i = 0; i < m.rows(); ++i)
                         for (Size j = 0; j < m.columns(); ++j)
                             out.emplace_back(m[i][j]);
                     return json::value(out);
                 };
             }},
            {"HouseholderReflection::operator()",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 const Array e = arrayOf(a[0]), x = arrayOf(a[1]);
                 return [=] { return result(HouseholderReflection(e)(x)); };
             }},
            {"HouseholderReflection::reflectionVector",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 const Array e = arrayOf(a[0]), x = arrayOf(a[1]);
                 return [=] { return result(HouseholderReflection(e).reflectionVector(x)); };
             }},
        });

    }

}
