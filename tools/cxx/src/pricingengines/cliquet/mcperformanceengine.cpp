// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/cliquet/mcperformanceengine.hpp, .cpp: PerformanceOptionPathPricer on paths given by the caller,
// which the wheel can't reach.
//
// "PerformanceOptionPathPricer::value" takes (optionType, strike, discounts, values): the pricer's arguments and the
// path's values on a uniform grid to a year (a step between each two values). It returns the pricer's value; its
// refusal of a path whose length isn't the discounts' and one more is QuantLib's, its message the error's.

#include "convert.hpp"

#include <ql/methods/montecarlo/path.hpp>
#include <ql/pricingengines/cliquet/mcperformanceengine.hpp>
#include <ql/timegrid.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"PerformanceOptionPathPricer::value",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 const auto type = enumeration<Option::Type>(a[0]);
                 const Real strike = real(a[1]);
                 const std::vector<Real> discounts = reals(a[2]), values = reals(a[3]);
                 if (values.size() < 2)
                     throw InvalidParams("a path of two values at least");
                 return [=] {
                     PerformanceOptionPathPricer pricer(type, strike, discounts);
                     Path path(TimeGrid(1.0, values.size() - 1), Array(values.begin(), values.end()));
                     return json::value(pricer(path));
                 };
             }},
        });

    }

}
