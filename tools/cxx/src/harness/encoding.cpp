// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The encoding self-vector's values, each produced in C++ (tools/vectors/gen/cxx/harness/encoding.py).

#include "convert.hpp"

#include <ql/utilities/null.hpp>

#include <limits>

using namespace QuantLib;

namespace harness {

    namespace {

        Call none(const Args& a, json::value (*f)()) {
            arity(a, 0);
            return f;
        }

        const Register unit({
            {"harness.nullReal",
             [](const Args& a) { return none(a, [] { return json::value(static_cast<Real>(Null<Real>())); }); }},
            {"harness.nullInteger",
             [](const Args& a) { return none(a, [] { return json::value(static_cast<Integer>(Null<Integer>())); }); }},
            {"harness.nullDate", [](const Args& a) { return none(a, [] { return result(Date()); }); }},
            {"harness.nan",
             [](const Args& a) {
                 return none(a, [] { return json::value(std::numeric_limits<Real>::quiet_NaN()); });
             }},
            {"harness.infinity",
             [](const Args& a) {
                 return none(a, [] { return json::value(std::numeric_limits<Real>::infinity()); });
             }},
            {"harness.minusInfinity",
             [](const Args& a) {
                 return none(a, [] { return json::value(-std::numeric_limits<Real>::infinity()); });
             }},
            {"harness.sum",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 Real x = real(a[0]), y = real(a[1]);
                 return [=] { return json::value(x + y); };
             }},
            {"harness.reals",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 std::vector<Real> xs = reals(a[0]);
                 return [=] {
                     json::array out;
                     for (Real x : xs)
                         out.emplace_back(x);
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
