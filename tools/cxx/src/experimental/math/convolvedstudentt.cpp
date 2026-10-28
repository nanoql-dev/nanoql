// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/math/convolvedstudentt.hpp, .cpp, which the wheel doesn't expose:
//
//     CumulativeBehrensFisher::evaluate        (degreesFreedom, factors, xs): [[value(x), density(x)]...]
//     CumulativeBehrensFisher::inspect         (degreesFreedom, factors): [degreeFreedom, factors]
//     InverseCumulativeBehrensFisher::evaluate (degreesFreedom, factors, accuracy, qs): [inverse(q)...]

#include "convert.hpp"

#include <ql/experimental/math/convolvedstudentt.hpp>

#include <cstdint>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"CumulativeBehrensFisher::evaluate",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 std::vector<Integer> dfs = integers(a[0]);
                 std::vector<Real> fs = reals(a[1]), xs = reals(a[2]);
                 return [=] {
                     CumulativeBehrensFisher c(dfs, fs);
                     json::array out;
                     for (Real x : xs) {
                         json::array p;
                         p.emplace_back(c(x));
                         p.emplace_back(c.density(x));
                         out.emplace_back(std::move(p));
                     }
                     return json::value(std::move(out));
                 };
             }},
            {"CumulativeBehrensFisher::inspect",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 std::vector<Integer> dfs = integers(a[0]);
                 std::vector<Real> fs = reals(a[1]);
                 return [=] {
                     CumulativeBehrensFisher c(dfs, fs);
                     json::array d, f;
                     for (Integer x : c.degreeFreedom())
                         d.emplace_back(static_cast<std::int64_t>(x));
                     for (Real x : c.factors())
                         f.emplace_back(x);
                     json::array out;
                     out.emplace_back(std::move(d));
                     out.emplace_back(std::move(f));
                     return json::value(std::move(out));
                 };
             }},
            {"InverseCumulativeBehrensFisher::evaluate",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 std::vector<Integer> dfs = integers(a[0]);
                 std::vector<Real> fs = reals(a[1]), qs = reals(a[3]);
                 Real accuracy = real(a[2]);
                 return [=] {
                     InverseCumulativeBehrensFisher c(dfs, fs, accuracy);
                     json::array out;
                     for (Real q : qs) {
                         try {
                             out.emplace_back(c(q));
                         } catch (const Error&) {
                             json::object t;
                             t["throws"] = true;
                             out.emplace_back(std::move(t));
                         }
                     }
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
