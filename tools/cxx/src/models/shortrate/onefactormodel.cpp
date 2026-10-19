// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/shortrate/onefactormodel.hpp, .cpp, which the wheel doesn't expose: a model's dynamics() and its members
// (variable, shortRate, and the process's x0, drift, diffusion, expectation, variance and stdDeviation), and the
// members of OneFactorModel::ShortRateTree (size, discount, underlying, descendant, probability) on a model's tree,
// with setSpread through the lattice spec. A model or lattice argument is a spec of models/modelspec.hpp, built by the
// call. Indices are non-negative integers; the harness refuses one past the tree, which QuantLib reads unchecked.

#include "models/modelspec.hpp"

#include <ql/stochasticprocess.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using models::index;

        // The harness's own checks, against the built tree: a column of its grid, a step after it where the member
        // reads one, a node of the column.
        void checkNode(const OneFactorModel::ShortRateTree& t, Size i, Size j, bool step) {
            const Size columns = t.timeGrid().size();
            if (i >= columns || (step && i + 1 >= columns))
                throw InvalidParams("column " + std::to_string(i) + " past the tree's grid of " +
                                    std::to_string(columns));
            if (j >= t.size(i))
                throw InvalidParams("node " + std::to_string(j) + " past column " + std::to_string(i) + "'s " +
                                    std::to_string(t.size(i)));
        }

        // A function of a model's process at (t, x).
        Call ofProcess(const Args& a, Real (*f)(const StochasticProcess1D&, Time, Real)) {
            arity(a, 3);
            models::ModelSpec s = models::model(a[0]);
            Time t = real(a[1]);
            Real x = real(a[2]);
            return [=] { return json::value(f(*models::makeModel(s)->dynamics()->process(), t, x)); };
        }

        // A function of a model's process at (t0, x0, dt).
        Call ofProcessStep(const Args& a, Real (*f)(const StochasticProcess1D&, Time, Real, Time)) {
            arity(a, 4);
            models::ModelSpec s = models::model(a[0]);
            Time t = real(a[1]);
            Real x = real(a[2]);
            Time dt = real(a[3]);
            return [=] { return json::value(f(*models::makeModel(s)->dynamics()->process(), t, x, dt)); };
        }

        const Register unit({
            {"OneFactorModel::dynamics()->variable",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 models::ModelSpec s = models::model(a[0]);
                 Time t = real(a[1]);
                 Rate r = real(a[2]);
                 return [=] { return json::value(models::makeModel(s)->dynamics()->variable(t, r)); };
             }},
            {"OneFactorModel::dynamics()->shortRate",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 models::ModelSpec s = models::model(a[0]);
                 Time t = real(a[1]);
                 Real x = real(a[2]);
                 return [=] { return json::value(models::makeModel(s)->dynamics()->shortRate(t, x)); };
             }},
            {"OneFactorModel::dynamics()->process()->x0",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 models::ModelSpec s = models::model(a[0]);
                 return [=] { return json::value(models::makeModel(s)->dynamics()->process()->x0()); };
             }},
            {"OneFactorModel::dynamics()->process()->drift",
             [](const Args& a) {
                 return ofProcess(a, [](const StochasticProcess1D& p, Time t, Real x) { return p.drift(t, x); });
             }},
            {"OneFactorModel::dynamics()->process()->diffusion",
             [](const Args& a) {
                 return ofProcess(a, [](const StochasticProcess1D& p, Time t, Real x) { return p.diffusion(t, x); });
             }},
            {"OneFactorModel::dynamics()->process()->expectation",
             [](const Args& a) {
                 return ofProcessStep(
                     a, [](const StochasticProcess1D& p, Time t, Real x, Time dt) { return p.expectation(t, x, dt); });
             }},
            {"OneFactorModel::dynamics()->process()->stdDeviation",
             [](const Args& a) {
                 return ofProcessStep(
                     a, [](const StochasticProcess1D& p, Time t, Real x, Time dt) { return p.stdDeviation(t, x, dt); });
             }},
            {"OneFactorModel::dynamics()->process()->variance",
             [](const Args& a) {
                 return ofProcessStep(
                     a, [](const StochasticProcess1D& p, Time t, Real x, Time dt) { return p.variance(t, x, dt); });
             }},
            {"OneFactorModel::ShortRateTree::size",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 models::LatticeSpec s = models::lattice(a[0]);
                 Size i = index(a[1]);
                 return [=] {
                     auto t = models::makeLattice(s);
                     if (i >= t->timeGrid().size())
                         throw InvalidParams("column " + std::to_string(i) + " past the tree's grid");
                     return json::value(static_cast<std::int64_t>(t->size(i)));
                 };
             }},
            {"OneFactorModel::ShortRateTree::discount",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 models::LatticeSpec s = models::lattice(a[0]);
                 Size i = index(a[1]), j = index(a[2]);
                 return [=] {
                     auto t = models::makeLattice(s);
                     checkNode(*t, i, j, true);
                     return json::value(t->discount(i, j));
                 };
             }},
            {"OneFactorModel::ShortRateTree::underlying",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 models::LatticeSpec s = models::lattice(a[0]);
                 Size i = index(a[1]), j = index(a[2]);
                 return [=] {
                     auto t = models::makeLattice(s);
                     checkNode(*t, i, j, false);
                     return json::value(t->underlying(i, j));
                 };
             }},
            {"OneFactorModel::ShortRateTree::descendant",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 models::LatticeSpec s = models::lattice(a[0]);
                 Size i = index(a[1]), j = index(a[2]), b = index(a[3]);
                 if (b >= TrinomialTree::branches)
                     throw InvalidParams("branch " + std::to_string(b) + " of a trinomial tree");
                 return [=] {
                     auto t = models::makeLattice(s);
                     checkNode(*t, i, j, true);
                     return json::value(static_cast<std::int64_t>(t->descendant(i, j, b)));
                 };
             }},
            {"OneFactorModel::ShortRateTree::probability",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 models::LatticeSpec s = models::lattice(a[0]);
                 Size i = index(a[1]), j = index(a[2]), b = index(a[3]);
                 if (b >= TrinomialTree::branches)
                     throw InvalidParams("branch " + std::to_string(b) + " of a trinomial tree");
                 return [=] {
                     auto t = models::makeLattice(s);
                     checkNode(*t, i, j, true);
                     return json::value(t->probability(i, j, b));
                 };
             }},
        });

    }

}
