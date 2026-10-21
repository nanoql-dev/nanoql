// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/lattices/binomialtree.hpp, .cpp, which the wheel doesn't expose: the seven binomial trees' members
// (columns, size, underlying, descendant, probability) and the branches constant, on a tree built by the call
// (binomialtree.hpp's spec), so that a constructor's refusal is QuantLib's. Indices are non-negative integers; the
// harness refuses one past the tree, which QuantLib reads unchecked.

#include "binomialtree.hpp"

#include <climits>
#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        template <class T>
        void checkNode(const T& t, Size i, Size j, bool step) {
            if (i >= t.columns())
                throw InvalidParams("column " + std::to_string(i) + " past the tree's " + std::to_string(t.columns()));
            if (step && i + 1 >= t.columns())
                throw InvalidParams("column " + std::to_string(i) + " has no step after it");
            if (j >= t.size(i))
                throw InvalidParams("node " + std::to_string(j) + " past column " + std::to_string(i) + "'s " +
                                    std::to_string(t.size(i)));
        }

        json::value sizeResult(Size n) { return static_cast<std::int64_t>(n); }

        Size branch(const json::value& v) {
            Size b = latticeIndex(v);
            if (b >= 2)
                throw InvalidParams("branch " + std::to_string(b) + " of a binomial tree");
            return b;
        }

        const Register unit({
            {"BinomialTree::branches",
             [](const Args& a) -> Call {
                 arity(a, 0);
                 return [] { return sizeResult(BinomialTree<JarrowRudd>::branches); };
             }},
            {"BinomialTree::columns",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 BinomialTreeSpec s = binomialTreeSpec(a[0]);
                 return [=] { return withTree(s, [](const auto& t) { return sizeResult(t->columns()); }); };
             }},
            {"BinomialTree::size",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 BinomialTreeSpec s = binomialTreeSpec(a[0]);
                 Size i = latticeIndex(a[1]);
                 return [=] {
                     return withTree(s, [=](const auto& t) {
                         if (i >= t->columns())
                             throw InvalidParams("column " + std::to_string(i) + " past the tree's " +
                                                 std::to_string(t->columns()));
                         return sizeResult(t->size(i));
                     });
                 };
             }},
            {"BinomialTree::underlying",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 BinomialTreeSpec s = binomialTreeSpec(a[0]);
                 Size i = latticeIndex(a[1]), j = latticeIndex(a[2]);
                 return [=] {
                     return withTree(s, [=](const auto& t) {
                         checkNode(*t, i, j, false);
                         return json::value(t->underlying(i, j));
                     });
                 };
             }},
            {"BinomialTree::descendant",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 BinomialTreeSpec s = binomialTreeSpec(a[0]);
                 Size i = latticeIndex(a[1]), j = latticeIndex(a[2]), b = branch(a[3]);
                 return [=] {
                     return withTree(s, [=](const auto& t) {
                         checkNode(*t, i, j, true);
                         return sizeResult(t->descendant(i, j, b));
                     });
                 };
             }},
            {"BinomialTree::probability",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 BinomialTreeSpec s = binomialTreeSpec(a[0]);
                 Size i = latticeIndex(a[1]), j = latticeIndex(a[2]), b = branch(a[3]);
                 return [=] {
                     return withTree(s, [=](const auto& t) {
                         checkNode(*t, i, j, true);
                         return json::value(t->probability(i, j, b));
                     });
                 };
             }},
        });

    }

}
