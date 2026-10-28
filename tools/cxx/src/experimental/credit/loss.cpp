// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/credit/loss.hpp, which the wheel doesn't expose:
//
//     Loss::inspect   (time, amount, or none for the default constructor): [time, amount]
//     Loss::compare   (pairs [[t1, a1, t2, a2]...]): each pair's [l1 < l2, l1 > l2, l1 == l2, l1 != l2]

#include "convert.hpp"

#include <ql/experimental/credit/loss.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"Loss::inspect",
             [](const Args& a) -> Call {
                 if (a.empty())
                     return [] {
                         Loss l;
                         json::array out;
                         out.emplace_back(l.time);
                         out.emplace_back(l.amount);
                         return json::value(std::move(out));
                     };
                 arity(a, 2);
                 Real t = real(a[0]), x = real(a[1]);
                 return [=] {
                     Loss l(t, x);
                     json::array out;
                     out.emplace_back(l.time);
                     out.emplace_back(l.amount);
                     return json::value(std::move(out));
                 };
             }},
            {"Loss::compare",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 if (!a[0].is_array())
                     throw InvalidParams("expected an array of pairs");
                 std::vector<std::vector<Real>> pairs;
                 for (const json::value& p : a[0].get_array())
                     pairs.push_back(reals(p));
                 return [=] {
                     json::array out;
                     for (const std::vector<Real>& p : pairs) {
                         if (p.size() != 4)
                             throw InvalidParams("expected [t1, a1, t2, a2]");
                         Loss l1(p[0], p[1]), l2(p[2], p[3]);
                         json::array r;
                         r.emplace_back(l1 < l2);
                         r.emplace_back(l1 > l2);
                         r.emplace_back(l1 == l2);
                         r.emplace_back(l1 != l2);
                         out.emplace_back(std::move(r));
                     }
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
