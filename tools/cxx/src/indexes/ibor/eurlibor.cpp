// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/indexes/ibor/eurlibor.hpp: DailyTenorEURLibor and EURLiborON, which the wheel doesn't bind.
// "DailyTenorEURLibor" takes the settlement days, the dates and the forecast curves, and "EURLiborON" the dates and the
// forecast curves; each returns the preset's case as indexes/indexspec.hpp's presetCase writes it.

#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/indexes/ibor/eurlibor.hpp>
#include <ql/utilities/null.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"DailyTenorEURLibor",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 Integer days = integer(a[0]);
                 if (days == Null<Integer>() || days < 0)
                     throw InvalidParams("expected a Natural, got " + json::serialize(a[0]));
                 auto settlementDays = static_cast<Natural>(days);
                 return indexes::presetCase(
                     [settlementDays](const Handle<YieldTermStructure>& h) -> ext::shared_ptr<IborIndex> {
                         return ext::make_shared<DailyTenorEURLibor>(settlementDays, h);
                     },
                     a[1], a[2]);
             }},
            {"EURLiborON",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 return indexes::presetCase(
                     [](const Handle<YieldTermStructure>& h) -> ext::shared_ptr<IborIndex> {
                         return ext::make_shared<EURLiborON>(h);
                     },
                     a[0], a[1]);
             }},
        });

    }

}
