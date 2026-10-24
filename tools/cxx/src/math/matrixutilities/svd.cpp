// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/matrixutilities/svd.hpp, .cpp: the members of SVD the wheel doesn't expose. Each method takes the matrix as
// a list of its rows, each a list of reals, builds the decomposition inside the call, and returns:
//
//     SVD::norm2     (rows)       the 2-norm
//     SVD::cond      (rows)       the condition number
//     SVD::rank      (rows)       the numerical rank
//     SVD::solveFor  (rows, b)    the least-squares solution, a list of reals
//
// A matrix with a NaN or infinite entry isn't sent: QuantLib's loop never ends on it.

#include "convert.hpp"

#include <ql/math/matrixutilities/svd.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using Rows = std::vector<std::vector<Real>>;

        Rows rows(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected a matrix's rows, got " + json::serialize(v));
            Rows out;
            for (const json::value& r : v.get_array())
                out.push_back(reals(r));
            if (out.empty() || out[0].empty())
                throw InvalidParams("expected a non-empty matrix");
            for (const auto& r : out) {
                if (r.size() != out[0].size())
                    throw InvalidParams("a matrix's rows hold as many elements");
            }
            return out;
        }

        Matrix make(const Rows& r) {
            Matrix m(r.size(), r[0].size());
            for (Size i = 0; i < r.size(); ++i)
                for (Size j = 0; j < r[i].size(); ++j)
                    m[i][j] = r[i][j];
            return m;
        }

        Call ofSvd(const Args& a, json::value (*f)(const SVD&)) {
            arity(a, 1);
            Rows r = rows(a[0]);
            return [=] { return f(SVD(make(r))); };
        }

        const Register unit({
            {"SVD::norm2", [](const Args& a) { return ofSvd(a, [](const SVD& s) { return json::value(s.norm2()); }); }},
            {"SVD::cond", [](const Args& a) { return ofSvd(a, [](const SVD& s) { return json::value(s.cond()); }); }},
            {"SVD::rank",
             [](const Args& a) {
                 return ofSvd(a, [](const SVD& s) { return json::value(static_cast<std::int64_t>(s.rank())); });
             }},
            {"SVD::solveFor",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 Rows r = rows(a[0]);
                 std::vector<Real> b = reals(a[1]);
                 return [=] {
                     SVD s(make(r));
                     Array x = s.solveFor(Array(b.begin(), b.end()));
                     json::array out;
                     for (Real e : x)
                         out.emplace_back(e);
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
