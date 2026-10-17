// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/errorfunction.hpp

#include "convert.hpp"

#include <ql/math/errorfunction.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"ErrorFunction::operator()",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Real x = real(a[0]);
                 return [=] { return json::value(ErrorFunction()(x)); };
             }},
        });

    }

}
