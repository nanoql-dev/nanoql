// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/optionlet/optionletvolatilitystructure.hpp: volatility and blackVariance on an option
// tenor, which the wheel doesn't expose, and on a date and a time, for the range and strike refusals, each taking the
// structure, the tenor ([length, units]), date or time, the strike and the extrapolate flag; and volatilityType and
// displacement, whose base defaults the harness's own structure keeps; and smileSection on a tenor, a date and a time,
// each taking the structure, the tenor, date or time, the extrapolate flag and the strike its section's summary reads
// (termstructures/volatility/smilesectionspec.hpp). Each is called on a structure spec of optionletvolspec.hpp, built
// inside the call.

#include "convert.hpp"
#include "termstructures/volatility/optionlet/optionletvolspec.hpp"
#include "termstructures/volatility/smilesectionspec.hpp"

#include <ql/termstructures/volatility/optionlet/optionletvolatilitystructure.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using termstructures::makeOptionletVolatility;
        using termstructures::makePeriod;
        using termstructures::OptionletVolSpec;
        using termstructures::optionletVolatility;
        using termstructures::PeriodSpec;
        using termstructures::sectionSummary;
        using termstructures::tenor;

        const Register unit({
            {"OptionletVolatilityStructure::volatility(Period)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 OptionletVolSpec s = optionletVolatility(a[0]);
                 PeriodSpec p = tenor(a[1]);
                 Real strike = real(a[2]);
                 bool extrapolate = boolean(a[3]);
                 return [=] {
                     return json::value(makeOptionletVolatility(s)->volatility(makePeriod(p), strike, extrapolate));
                 };
             }},
            {"OptionletVolatilityStructure::blackVariance(Period)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 OptionletVolSpec s = optionletVolatility(a[0]);
                 PeriodSpec p = tenor(a[1]);
                 Real strike = real(a[2]);
                 bool extrapolate = boolean(a[3]);
                 return [=] {
                     return json::value(makeOptionletVolatility(s)->blackVariance(makePeriod(p), strike, extrapolate));
                 };
             }},
            {"OptionletVolatilityStructure::volatility(Date)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 OptionletVolSpec s = optionletVolatility(a[0]);
                 DateSpec d = date(a[1]);
                 Real strike = real(a[2]);
                 bool extrapolate = boolean(a[3]);
                 return [=] {
                     return json::value(makeOptionletVolatility(s)->volatility(makeDate(d), strike, extrapolate));
                 };
             }},
            {"OptionletVolatilityStructure::blackVariance(Date)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 OptionletVolSpec s = optionletVolatility(a[0]);
                 DateSpec d = date(a[1]);
                 Real strike = real(a[2]);
                 bool extrapolate = boolean(a[3]);
                 return [=] {
                     return json::value(makeOptionletVolatility(s)->blackVariance(makeDate(d), strike, extrapolate));
                 };
             }},
            {"OptionletVolatilityStructure::volatility(Time)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 OptionletVolSpec s = optionletVolatility(a[0]);
                 Time t = real(a[1]);
                 Real strike = real(a[2]);
                 bool extrapolate = boolean(a[3]);
                 return [=] { return json::value(makeOptionletVolatility(s)->volatility(t, strike, extrapolate)); };
             }},
            {"OptionletVolatilityStructure::blackVariance(Time)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 OptionletVolSpec s = optionletVolatility(a[0]);
                 Time t = real(a[1]);
                 Real strike = real(a[2]);
                 bool extrapolate = boolean(a[3]);
                 return [=] {
                     return json::value(makeOptionletVolatility(s)->blackVariance(t, strike, extrapolate));
                 };
             }},
            {"OptionletVolatilityStructure::volatilityType",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 OptionletVolSpec s = optionletVolatility(a[0]);
                 return [=] { return json::value(static_cast<int>(makeOptionletVolatility(s)->volatilityType())); };
             }},
            {"OptionletVolatilityStructure::displacement",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 OptionletVolSpec s = optionletVolatility(a[0]);
                 return [=] { return json::value(makeOptionletVolatility(s)->displacement()); };
             }},
            // (spec, tenor, extrapolate, strike)
            {"OptionletVolatilityStructure::smileSection(Period)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 OptionletVolSpec s = optionletVolatility(a[0]);
                 PeriodSpec p = tenor(a[1]);
                 bool extrapolate = boolean(a[2]);
                 Real strike = real(a[3]);
                 return [=] {
                     auto section = makeOptionletVolatility(s)->smileSection(makePeriod(p), extrapolate);
                     return sectionSummary(*section, strike);
                 };
             }},
            // (spec, date, extrapolate, strike)
            {"OptionletVolatilityStructure::smileSection(Date)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 OptionletVolSpec s = optionletVolatility(a[0]);
                 DateSpec d = date(a[1]);
                 bool extrapolate = boolean(a[2]);
                 Real strike = real(a[3]);
                 return [=] {
                     auto section = makeOptionletVolatility(s)->smileSection(makeDate(d), extrapolate);
                     return sectionSummary(*section, strike);
                 };
             }},
            // (spec, time, extrapolate, strike)
            {"OptionletVolatilityStructure::smileSection(Time)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 OptionletVolSpec s = optionletVolatility(a[0]);
                 Time t = real(a[1]);
                 bool extrapolate = boolean(a[2]);
                 Real strike = real(a[3]);
                 return [=] { return sectionSummary(*makeOptionletVolatility(s)->smileSection(t, extrapolate), strike); };
             }},
        });

    }

}
