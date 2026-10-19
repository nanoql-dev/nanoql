// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/lattices/trinomialtree.hpp, .cpp, which the wheel doesn't expose: TrinomialTree's members (size, dx,
// underlying, descendant, probability, timeGrid), Tree's columns() and the branches constant, on a tree built by the
// call, so that the constructor's refusals are QuantLib's. A tree argument is the spec {"process", "timeGrid",
// "isPositive"}: the process {"type": "OrnsteinUhlenbeckProcess", "speed", "volatility", "x0", "level"}, a TimeGrid
// spec (timegrid.hpp) and a boolean. Indices are non-negative integers; the harness refuses one past the tree's
// vectors, which QuantLib reads unchecked.

#include "timegrid.hpp"

#include <ql/methods/lattices/trinomialtree.hpp>
#include <ql/processes/ornsteinuhlenbeckprocess.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        struct TreeSpec {
            Real speed, volatility, x0, level;
            TimeGridSpec grid;
            bool isPositive;
        };

        const json::value& key(const json::object& o, const char* name, const char* what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string(what) + " needs " + name);
            return *v;
        }

        TreeSpec treeSpec(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a trinomial tree spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            for (const auto& kv : o) {
                if (kv.key() != "process" && kv.key() != "timeGrid" && kv.key() != "isPositive")
                    throw InvalidParams("a trinomial tree takes no key " + std::string(kv.key()));
            }
            const json::value& p = key(o, "process", "a trinomial tree");
            if (!p.is_object())
                throw InvalidParams("expected a process spec, got " + json::serialize(p));
            const json::object& po = p.get_object();
            const json::value& type = key(po, "type", "a process");
            if (!type.is_string() || type.get_string() != "OrnsteinUhlenbeckProcess")
                throw InvalidParams("unknown process " + json::serialize(type));
            for (const auto& kv : po) {
                if (kv.key() != "type" && kv.key() != "speed" && kv.key() != "volatility" && kv.key() != "x0" &&
                    kv.key() != "level")
                    throw InvalidParams("OrnsteinUhlenbeckProcess takes no key " + std::string(kv.key()));
            }
            return {real(key(po, "speed", "OrnsteinUhlenbeckProcess")),
                    real(key(po, "volatility", "OrnsteinUhlenbeckProcess")),
                    real(key(po, "x0", "OrnsteinUhlenbeckProcess")), real(key(po, "level", "OrnsteinUhlenbeckProcess")),
                    timeGrid(key(o, "timeGrid", "a trinomial tree")),
                    boolean(key(o, "isPositive", "a trinomial tree"))};
        }

        TrinomialTree makeTree(const TreeSpec& s) {
            auto process = ext::make_shared<OrnsteinUhlenbeckProcess>(s.speed, s.volatility, s.x0, s.level);
            return TrinomialTree(process, makeTimeGrid(s.grid), s.isPositive);
        }

        Size index(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > INT_MAX)
                throw InvalidParams("expected an index, got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        // The harness's own checks, against the built tree: a column has a step after it, a node is in its column.
        void checkColumn(const TrinomialTree& t, Size i) {
            if (i >= t.columns())
                throw InvalidParams("column " + std::to_string(i) + " past the tree's " + std::to_string(t.columns()));
        }

        void checkNode(const TrinomialTree& t, Size i, Size index, bool step) {
            checkColumn(t, i);
            if (step && i + 1 >= t.columns())
                throw InvalidParams("column " + std::to_string(i) + " has no step after it");
            if (index >= t.size(i))
                throw InvalidParams("node " + std::to_string(index) + " past column " + std::to_string(i) + "'s " +
                                    std::to_string(t.size(i)));
        }

        json::value sizeResult(Size n) { return static_cast<std::int64_t>(n); }

        const Register unit({
            {"TrinomialTree::branches",
             [](const Args& a) -> Call {
                 arity(a, 0);
                 return [] { return sizeResult(TrinomialTree::branches); };
             }},
            {"Tree::columns",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 TreeSpec s = treeSpec(a[0]);
                 return [=] { return sizeResult(makeTree(s).columns()); };
             }},
            {"TrinomialTree::timeGrid",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 TreeSpec s = treeSpec(a[0]);
                 return [=] { return timesResult(makeTree(s).timeGrid()); };
             }},
            {"TrinomialTree::size",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 TreeSpec s = treeSpec(a[0]);
                 Size i = index(a[1]);
                 return [=] {
                     TrinomialTree t = makeTree(s);
                     checkColumn(t, i);
                     return sizeResult(t.size(i));
                 };
             }},
            {"TrinomialTree::dx",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 TreeSpec s = treeSpec(a[0]);
                 Size i = index(a[1]);
                 return [=] {
                     TrinomialTree t = makeTree(s);
                     checkColumn(t, i);
                     return json::value(t.dx(i));
                 };
             }},
            {"TrinomialTree::underlying",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 TreeSpec s = treeSpec(a[0]);
                 Size i = index(a[1]), j = index(a[2]);
                 return [=] {
                     TrinomialTree t = makeTree(s);
                     checkNode(t, i, j, false);
                     return json::value(t.underlying(i, j));
                 };
             }},
            {"TrinomialTree::descendant",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 TreeSpec s = treeSpec(a[0]);
                 Size i = index(a[1]), j = index(a[2]), b = index(a[3]);
                 if (b >= TrinomialTree::branches)
                     throw InvalidParams("branch " + std::to_string(b) + " of a trinomial tree");
                 return [=] {
                     TrinomialTree t = makeTree(s);
                     checkNode(t, i, j, true);
                     return sizeResult(t.descendant(i, j, b));
                 };
             }},
            {"TrinomialTree::probability",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 TreeSpec s = treeSpec(a[0]);
                 Size i = index(a[1]), j = index(a[2]), b = index(a[3]);
                 if (b >= TrinomialTree::branches)
                     throw InvalidParams("branch " + std::to_string(b) + " of a trinomial tree");
                 return [=] {
                     TrinomialTree t = makeTree(s);
                     checkNode(t, i, j, true);
                     return json::value(t.probability(i, j, b));
                 };
             }},
        });

    }

}
