// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/shortrate/onefactormodels/hullwhite.hpp, .cpp: the static FixedReversion(), which the wheel doesn't bind.
// Hull-White's other members the wheel doesn't reach run through their declaring classes' methods on a Hull-White
// spec (models/modelspec.hpp): its fitted tree(grid) through the lattice methods, dynamics() through
// onefactormodel.cpp's, its five-argument discountBondOption through model.cpp's AffineModel method, and a() and
// sigma() through vasicek.cpp's.

#include "convert.hpp"

#include <ql/models/shortrate/onefactormodels/hullwhite.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"HullWhite::FixedReversion",
             [](const Args& a) -> Call {
                 arity(a, 0);
                 return [] {
                     json::array out;
                     for (bool fixed : HullWhite::FixedReversion())
                         out.emplace_back(fixed);
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
