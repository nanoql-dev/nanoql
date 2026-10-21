// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/lattices/bsmlattice.hpp, which the wheel doesn't expose: BlackScholesLattice<T>'s members (riskFreeRate,
// dt, size, discount, underlying, descendant, probability) and TreeLattice1D's grid, on a lattice built by the call
// over a binomial tree (binomialtree.hpp's spec) at a risk-free rate, an end and a step count. Indices are non-negative
// integers; the harness refuses one past the lattice, which QuantLib reads unchecked.

#include "binomialtree.hpp"

#include <ql/methods/lattices/bsmlattice.hpp>

#include <climits>
#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        struct LatticeSpec {
            BinomialTreeSpec tree;
            Real riskFreeRate, end;
            Size steps;
        };

        LatticeSpec latticeSpec(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a lattice spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            for (const auto& kv : o) {
                if (kv.key() != "tree" && kv.key() != "riskFreeRate" && kv.key() != "end" && kv.key() != "steps")
                    throw InvalidParams("a lattice takes no key " + std::string(kv.key()));
            }
            for (const char* k : {"tree", "riskFreeRate", "end", "steps"}) {
                if (o.if_contains(k) == nullptr)
                    throw InvalidParams(std::string("a lattice needs ") + k);
            }
            return {binomialTreeSpec(o.at("tree")), real(o.at("riskFreeRate")), real(o.at("end")),
                    latticeIndex(o.at("steps"))};
        }

        // f called with the lattice over the spec's tree
        template <class F>
        json::value withLattice(const LatticeSpec& s, F f) {
            return withTree(s.tree, [&](const auto& t) {
                using T = typename std::decay_t<decltype(t)>::element_type;
                BlackScholesLattice<T> lattice(t, s.riskFreeRate, s.end, s.steps);
                return f(lattice);
            });
        }

        template <class L>
        void checkNode(const L& l, Size i, Size j, bool step, Size steps) {
            if (i > steps)
                throw InvalidParams("step " + std::to_string(i) + " past the lattice's " + std::to_string(steps));
            if (step && i >= steps)
                throw InvalidParams("step " + std::to_string(i) + " has no step after it");
            if (j >= l.size(i))
                throw InvalidParams("node " + std::to_string(j) + " past step " + std::to_string(i) + "'s " +
                                    std::to_string(l.size(i)));
        }

        json::value sizeResult(Size n) { return static_cast<std::int64_t>(n); }

        const Register unit({
            {"BlackScholesLattice::riskFreeRate",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 LatticeSpec s = latticeSpec(a[0]);
                 return [=] { return withLattice(s, [](const auto& l) { return json::value(l.riskFreeRate()); }); };
             }},
            {"BlackScholesLattice::dt",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 LatticeSpec s = latticeSpec(a[0]);
                 return [=] { return withLattice(s, [](const auto& l) { return json::value(l.dt()); }); };
             }},
            {"BlackScholesLattice::size",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 LatticeSpec s = latticeSpec(a[0]);
                 Size i = latticeIndex(a[1]);
                 return [=] {
                     return withLattice(s, [=](const auto& l) {
                         if (i > s.steps)
                             throw InvalidParams("step " + std::to_string(i) + " past the lattice's " +
                                                 std::to_string(s.steps));
                         return sizeResult(l.size(i));
                     });
                 };
             }},
            {"BlackScholesLattice::discount",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 LatticeSpec s = latticeSpec(a[0]);
                 Size i = latticeIndex(a[1]), j = latticeIndex(a[2]);
                 return [=] {
                     return withLattice(s, [=](const auto& l) {
                         checkNode(l, i, j, false, s.steps);
                         return json::value(l.discount(i, j));
                     });
                 };
             }},
            {"BlackScholesLattice::underlying",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 LatticeSpec s = latticeSpec(a[0]);
                 Size i = latticeIndex(a[1]), j = latticeIndex(a[2]);
                 return [=] {
                     return withLattice(s, [=](const auto& l) {
                         checkNode(l, i, j, false, s.steps);
                         return json::value(l.underlying(i, j));
                     });
                 };
             }},
            {"BlackScholesLattice::descendant",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 LatticeSpec s = latticeSpec(a[0]);
                 Size i = latticeIndex(a[1]), j = latticeIndex(a[2]), b = latticeIndex(a[3]);
                 if (b >= 2)
                     throw InvalidParams("branch " + std::to_string(b) + " of a binomial lattice");
                 return [=] {
                     return withLattice(s, [=](const auto& l) {
                         checkNode(l, i, j, true, s.steps);
                         return sizeResult(l.descendant(i, j, b));
                     });
                 };
             }},
            {"BlackScholesLattice::probability",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 LatticeSpec s = latticeSpec(a[0]);
                 Size i = latticeIndex(a[1]), j = latticeIndex(a[2]), b = latticeIndex(a[3]);
                 if (b >= 2)
                     throw InvalidParams("branch " + std::to_string(b) + " of a binomial lattice");
                 return [=] {
                     return withLattice(s, [=](const auto& l) {
                         checkNode(l, i, j, true, s.steps);
                         return json::value(l.probability(i, j, b));
                     });
                 };
             }},
            {"BlackScholesLattice::stepback",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 LatticeSpec s = latticeSpec(a[0]);
                 Size i = latticeIndex(a[1]);
                 std::vector<Real> values = reals(a[2]);
                 return [=] {
                     return withLattice(s, [=](const auto& l) {
                         if (i >= s.steps)
                             throw InvalidParams("step " + std::to_string(i) + " has no step after it");
                         if (values.size() != l.size(i + 1))
                             throw InvalidParams("values of size " + std::to_string(values.size()) + " at step " +
                                                 std::to_string(i + 1) + " of size " + std::to_string(l.size(i + 1)));
                         Array in(values.begin(), values.end()), out(l.size(i));
                         l.stepback(i, in, out);
                         json::array result;
                         for (Real x : out)
                             result.emplace_back(x);
                         return json::value(result);
                     });
                 };
             }},
            {"BlackScholesLattice::grid",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 LatticeSpec s = latticeSpec(a[0]);
                 Real t = real(a[1]);
                 return [=] {
                     return withLattice(s, [=](const auto& l) {
                         Array g = l.grid(t);
                         json::array out;
                         for (Real x : g)
                             out.emplace_back(x);
                         return json::value(out);
                     });
                 };
             }},
        });

    }

}
