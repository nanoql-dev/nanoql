// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/indexes/iborindex.hpp: forecastFixing(const Date&) called directly, which the wheel reaches only through fixing()
// on later dates. An index argument is an Ibor or overnight index spec of indexes/indexspec.hpp, built inside the
// call; OvernightIndex inherits IborIndex's forecast.

#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/indexes/iborindex.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"IborIndex::forecastFixing(Date)",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 indexes::IndexSpec s = indexes::iborIndex(a[0]);
                 DateSpec d = date(a[1]);
                 return [=] { return json::value(indexes::makeIborIndex(s)->forecastFixing(makeDate(d))); };
             }},
        });

    }

}
