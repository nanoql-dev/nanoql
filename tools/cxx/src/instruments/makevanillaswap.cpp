// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/instruments/makevanillaswap.hpp: withSettlementCalendar, which the wheel's MakeVanillaSwap doesn't reach, with
// the setters it works with (settlement days, explicit dates, the fixed leg's tenor, calendar and day counter, the
// floating leg's calendar). A build is a spec of instruments/swapspec.hpp; each call builds the swap and describes it:
// its start and maturity dates, both schedules' dates and its fixed rate (the par rate where none is given, priced on
// the index's own forecasting curve, as QuantLib's builder prices it).

#include "convert.hpp"
#include "instruments/swapspec.hpp"

#include <ql/instruments/makevanillaswap.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"MakeVanillaSwap::withSettlementCalendar",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 swaps::BuildSpec s = swaps::vanillaBuild(a[0]);
                 return [=] { return swaps::describe(*swaps::makeVanillaSwap(s), "floatingSchedule"); };
             }},
        });

    }

}
