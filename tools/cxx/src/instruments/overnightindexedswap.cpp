// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/instruments/overnightindexedswap.hpp: the inspectors the wheel doesn't bind, telescopicValueDates and
// roundingPrecision, and the fixed day counter and payment calendar of a swap built with QuantLib's empty ones, which
// the wheel can't build. A swap is a spec of instruments/swapspec.hpp; each call builds it with QuantLib's constructor
// of its form, so that a refusal is QuantLib's, and reads one inspector.

#include "convert.hpp"
#include "instruments/swapspec.hpp"

#include <ql/instruments/overnightindexedswap.hpp>

#include <cstdint>

using namespace QuantLib;

namespace harness {

    namespace {

        json::value nameOrNull(const Calendar& c) {
            if (c.empty())
                return nullptr;
            return json::value(c.name());
        }

        const Register unit({
            {"OvernightIndexedSwap::telescopicValueDates",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 swaps::OisSpec s = swaps::ois(a[0]);
                 return [=] { return json::value(swaps::makeOis(s)->telescopicValueDates()); };
             }},
            {"OvernightIndexedSwap::roundingPrecision",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 swaps::OisSpec s = swaps::ois(a[0]);
                 return [=] {
                     std::optional<Integer> p = swaps::makeOis(s)->roundingPrecision();
                     return p ? json::value(static_cast<std::int64_t>(*p)) : json::value(nullptr);
                 };
             }},
            {"OvernightIndexedSwap::fixedDayCount",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 swaps::OisSpec s = swaps::ois(a[0]);
                 return [=] { return json::value(swaps::makeOis(s)->fixedDayCount().name()); };
             }},
            {"OvernightIndexedSwap::paymentCalendar",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 swaps::OisSpec s = swaps::ois(a[0]);
                 return [=] { return nameOrNull(swaps::makeOis(s)->paymentCalendar()); };
             }},
        });

    }

}
