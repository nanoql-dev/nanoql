// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/lattices/lattice1d.hpp, which the wheel doesn't expose: TreeLattice1D's grid(t), the underlying's values
// at a time's nodes, on a short-rate model's tree built by the call from a lattice spec (models/modelspec.hpp). A time
// off the grid is QuantLib's refusal, through TimeGrid::index.

#include "models/modelspec.hpp"

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"TreeLattice1D::grid",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 models::LatticeSpec s = models::lattice(a[0]);
                 Time t = real(a[1]);
                 return [=] { return models::arrayResult(models::makeLattice(s)->grid(t)); };
             }},
        });

    }

}
