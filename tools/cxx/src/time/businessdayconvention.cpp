// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/time/businessdayconvention.hpp

#include "convert.hpp"

#include <ql/time/businessdayconvention.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"operator<<(BusinessDayConvention)",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 auto b = enumeration<BusinessDayConvention>(a[0]);
                 return [=] { return json::value(streamed(b)); };
             }},
        });

    }

}
