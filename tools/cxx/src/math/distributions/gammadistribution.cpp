// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/distributions/gammadistribution.hpp: GammaFunction::value, which the wheel doesn't expose.

#include "convert.hpp"

#include <ql/math/distributions/gammadistribution.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"GammaFunction::value",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Real x = real(a[0]);
                 return [=] { return json::value(GammaFunction().value(x)); };
             }},
        });

    }

}
