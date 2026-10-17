// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/utilities/dataparsers.hpp

#include "convert.hpp"

#include <ql/utilities/dataparsers.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"DateParser::parseISO",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 std::string s = text(a[0]);
                 return [=] { return result(DateParser::parseISO(s)); };
             }},
        });

    }

}
