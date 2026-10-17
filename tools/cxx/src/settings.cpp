// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/settings.hpp: the four values a vector case sets, as tools/vectors/vectors.py's settings() sets them.

#include "convert.hpp"

#include <ql/settings.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        // Settings(evaluationDate, includeReferenceDateEvents, includeTodaysCashFlows, enforcesTodaysHistoricFixings)
        const Register unit({
            {"Settings",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 DateSpec today = date(a[0]);
                 bool referenceDateEvents = boolean(a[1]);
                 std::optional<bool> todaysCashFlows = optionalBoolean(a[2]);
                 bool historicFixings = boolean(a[3]);
                 return [=] {
                     Settings& s = Settings::instance();
                     s.evaluationDate() = makeDate(today);
                     s.includeReferenceDateEvents() = referenceDateEvents;
                     s.includeTodaysCashFlows() = todaysCashFlows;
                     s.enforcesTodaysHistoricFixings() = historicFixings;
                     return json::value(nullptr);
                 };
             }},
        });

    }

}
