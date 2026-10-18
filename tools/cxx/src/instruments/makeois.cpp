// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/instruments/makeois.hpp: withSettlementCalendar, which the wheel's MakeOIS doesn't reach, with the setters it
// works with (settlement days, explicit dates, the calendars, the end-of-month flag). A build is a spec of
// instruments/swapspec.hpp; each call builds the swap and describes it: its start and maturity dates, both schedules'
// dates and its fixed rate (the par rate where none is given, priced on the index's own forecasting curve, as
// QuantLib's builder prices it).

#include "convert.hpp"
#include "instruments/swapspec.hpp"

#include <ql/instruments/makeois.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"MakeOIS::withSettlementCalendar",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 swaps::BuildSpec s = swaps::oisBuild(a[0]);
                 return [=] { return swaps::describe(*swaps::makeOisFromBuild(s), "overnightSchedule"); };
             }},
        });

    }

}
