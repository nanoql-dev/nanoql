// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/randomnumbers/latticerules.hpp and latticersg.hpp, which the wheel doesn't bind: a rule's generating vector,
// and the first n points of a fresh LatticeRsg over a rule. A rule is its letter, "A" to "D".
//
//     LatticeRule::getRule     (rule, N):          the 3,600 entries of the rule's generating vector
//     LatticeRsg::nextSequence (rule, N, dim, n):  the first n points

#include "convert.hpp"

#include <ql/math/randomnumbers/latticerules.hpp>
#include <ql/math/randomnumbers/latticersg.hpp>

#include <cstdint>

using namespace QuantLib;

namespace harness {

    namespace {

        LatticeRule::type rule(const json::value& v) {
            std::string s = text(v);
            if (s == "A")
                return LatticeRule::A;
            if (s == "B")
                return LatticeRule::B;
            if (s == "C")
                return LatticeRule::C;
            if (s == "D")
                return LatticeRule::D;
            throw InvalidParams("expected a lattice rule, A to D, got " + s);
        }

        Size count(const json::value& v, std::int64_t most) {
            if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > most)
                throw InvalidParams("expected a count, got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        const Register unit({
            {"LatticeRule::getRule",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 LatticeRule::type name = rule(a[0]);
                 Integer n = integer(a[1]);
                 return [=] {
                     std::vector<Real> z;
                     LatticeRule::getRule(name, z, n);
                     json::array out;
                     for (Real e : z)
                         out.emplace_back(e);
                     return json::value(std::move(out));
                 };
             }},
            {"LatticeRsg::nextSequence",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 LatticeRule::type name = rule(a[0]);
                 Integer n = integer(a[1]);
                 Size dimensionality = count(a[2], 3600);
                 Size draws = count(a[3], 1000000);
                 return [=] {
                     std::vector<Real> z;
                     LatticeRule::getRule(name, z, n);
                     LatticeRsg rsg(dimensionality, z, static_cast<Size>(n));
                     json::array out;
                     for (Size i = 0; i < draws; ++i) {
                         json::array s;
                         for (Real e : rsg.nextSequence().value)
                             s.emplace_back(e);
                         out.emplace_back(std::move(s));
                     }
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
