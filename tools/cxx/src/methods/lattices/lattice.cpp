// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/lattices/lattice.hpp, which the wheel doesn't expose: TreeLattice's statePrices(i), on a short-rate
// model's tree built by the call from a lattice spec (models/modelspec.hpp). Its rollbacks and presentValue run through
// the discretized assets (discretizedasset.cpp). The index is a non-negative integer; the harness refuses one past the
// tree's grid, which QuantLib reads past.

#include "models/modelspec.hpp"

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"TreeLattice::statePrices",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 models::LatticeSpec s = models::lattice(a[0]);
                 Size i = models::index(a[1]);
                 return [=] {
                     auto t = models::makeLattice(s);
                     if (i >= t->timeGrid().size())
                         throw InvalidParams("step " + std::to_string(i) + " past the tree's grid of " +
                                             std::to_string(t->timeGrid().size()));
                     return models::arrayResult(t->statePrices(i));
                 };
             }},
        });

    }

}
