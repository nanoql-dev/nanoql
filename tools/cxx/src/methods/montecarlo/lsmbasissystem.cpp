// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/montecarlo/lsmbasissystem.hpp, .cpp: the basis systems, whose functions the wheel doesn't expose. Each
// method builds the basis inside the call and returns each function's values at the points, a list per function:
//
//     LsmBasisSystem::pathBasisSystem       (order, polynomialType, xs)          xs a list of reals
//     LsmBasisSystem::multiPathBasisSystem  (dim, order, polynomialType, points)  points a list of lists of reals
//
// The polynomial type is LsmBasisSystem::PolynomialType's integer, which QuantLib decides on.

#include "convert.hpp"

#include <ql/math/array.hpp>
#include <ql/methods/montecarlo/lsmbasissystem.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        Size size(const json::value& v) {
            long long n = int64(v);
            if (n < 0)
                throw InvalidParams("expected a non-negative size");
            return Size(n);
        }

        const Register unit({
            {"LsmBasisSystem::pathBasisSystem",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 Size order = size(a[0]);
                 auto type = enumeration<LsmBasisSystem::PolynomialType>(a[1]);
                 std::vector<Real> xs = reals(a[2]);
                 return [=] {
                     auto basis = LsmBasisSystem::pathBasisSystem(order, type);
                     json::array out;
                     for (const auto& f : basis) {
                         json::array values;
                         for (Real x : xs)
                             values.emplace_back(f(x));
                         out.emplace_back(std::move(values));
                     }
                     return json::value(std::move(out));
                 };
             }},
            {"LsmBasisSystem::multiPathBasisSystem",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Size dim = size(a[0]), order = size(a[1]);
                 auto type = enumeration<LsmBasisSystem::PolynomialType>(a[2]);
                 if (!a[3].is_array())
                     throw InvalidParams("expected the points, a list of lists of reals");
                 std::vector<std::vector<Real>> points;
                 for (const json::value& p : a[3].get_array()) {
                     points.push_back(reals(p));
                     // QuantLib's functions read one element per factor, past the end otherwise
                     if (points.back().size() != dim)
                         throw InvalidParams("a point holds one element per factor");
                 }
                 return [=] {
                     auto basis = LsmBasisSystem::multiPathBasisSystem(dim, order, type);
                     json::array out;
                     for (const auto& f : basis) {
                         json::array values;
                         for (const auto& p : points)
                             values.emplace_back(f(Array(p.begin(), p.end())));
                         out.emplace_back(std::move(values));
                     }
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
