// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/voltermstructure.hpp: optionDateFromTenor, which the wheel doesn't expose, and
// businessDayConvention, minStrike and maxStrike, each called on QuantLib's ConstantOptionletVolatility, the
// structure spec of termstructures/volatility/optionlet/optionletvolspec.hpp, built inside the call.

#include "convert.hpp"
#include "termstructures/volatility/optionlet/optionletvolspec.hpp"

#include <ql/termstructures/voltermstructure.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using termstructures::makeOptionletVolatility;
        using termstructures::makePeriod;
        using termstructures::OptionletVolSpec;
        using termstructures::optionletVolatility;
        using termstructures::PeriodSpec;
        using termstructures::tenor;

        const Register unit({
            {"VolatilityTermStructure::optionDateFromTenor",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 OptionletVolSpec s = optionletVolatility(a[0]);
                 PeriodSpec p = tenor(a[1]);
                 return [=] { return result(makeOptionletVolatility(s)->optionDateFromTenor(makePeriod(p))); };
             }},
            {"VolatilityTermStructure::businessDayConvention",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 OptionletVolSpec s = optionletVolatility(a[0]);
                 return [=] { return json::value(static_cast<int>(makeOptionletVolatility(s)->businessDayConvention())); };
             }},
            {"VolatilityTermStructure::minStrike",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 OptionletVolSpec s = optionletVolatility(a[0]);
                 return [=] { return json::value(makeOptionletVolatility(s)->minStrike()); };
             }},
            {"VolatilityTermStructure::maxStrike",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 OptionletVolSpec s = optionletVolatility(a[0]);
                 return [=] { return json::value(makeOptionletVolatility(s)->maxStrike()); };
             }},
        });

    }

}
