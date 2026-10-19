// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/instruments/callabilityschedule.hpp: Callability's hasOccurred, the member it inherits from Event, which the
// wheel's Callability doesn't, on a callability spec (experimental/callablebonds/callablebondspec.hpp) built inside the
// call.
//
//     Callability::hasOccurred                     (callability, refDate, includeRefDate): Event::hasOccurred, a null
//                                                  date for Date() and a null flag for std::nullopt

#include "convert.hpp"
#include "experimental/callablebonds/callablebondspec.hpp"

#include <ql/instruments/callabilityschedule.hpp>

#include <optional>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"Callability::hasOccurred",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 callablebonds::CallabilitySpec s = callablebonds::callability(a[0]);
                 DateSpec d = date(a[1]);
                 std::optional<bool> include = optionalBoolean(a[2]);
                 return [=] {
                     return json::value(callablebonds::makeCallability(s)->hasOccurred(makeDate(d), include));
                 };
             }},
        });

    }

}
