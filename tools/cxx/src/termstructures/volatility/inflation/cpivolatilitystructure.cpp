// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/inflation/cpivolatilitystructure.hpp, which the wheel doesn't bind at all, on
// QuantLib's ConstantCPIVolatility (ql/termstructures/volatility/inflation/constantcpivolatility.hpp): the
// inspectors (referenceDate, maxDate, observationLag as [length, units], frequency, indexIsInterpolated, baseDate,
// minStrike, maxStrike, baseLevel), each taking the surface; timeFromBase, taking the surface, the date and the lag;
// volatility and totalVariance on a date and on an option tenor, each taking the surface, the date or tenor
// ([length, units]), the strike, the lag and the extrapolate flag; and volatility on a time, taking the surface, the
// time and the strike. A lag is [length, units], or null for QuantLib's default Period(-1, Days), the surface's own.
// Each is called on a ConstantCPIVolatility spec of inflationvolspec.hpp, built inside the call.

#include "convert.hpp"
#include "termstructures/volatility/inflation/inflationvolspec.hpp"

#include <ql/termstructures/volatility/inflation/cpivolatilitystructure.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using termstructures::InflationVolSpec;
        using termstructures::inflationVolatility;
        using termstructures::lag;
        using termstructures::LagSpec;
        using termstructures::makeCPIVolatility;
        using termstructures::makeLag;

        const char* const CPI = "ConstantCPIVolatility";

        LagSpec tenorOf(const json::value& v) {
            LagSpec t = lag(v);
            if (t.null)
                throw InvalidParams("expected a tenor as [length, units], got null");
            return t;
        }

        json::value lagResult(const Period& p) {
            return json::array{json::value(p.length()), json::value(static_cast<int>(p.units()))};
        }

        const Register unit({
            {"CPIVolatilitySurface::referenceDate",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 InflationVolSpec s = inflationVolatility(a[0], CPI);
                 return [=] { return result(makeCPIVolatility(s)->referenceDate()); };
             }},
            {"CPIVolatilitySurface::maxDate",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 InflationVolSpec s = inflationVolatility(a[0], CPI);
                 return [=] { return result(makeCPIVolatility(s)->maxDate()); };
             }},
            {"CPIVolatilitySurface::observationLag",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 InflationVolSpec s = inflationVolatility(a[0], CPI);
                 return [=] { return lagResult(makeCPIVolatility(s)->observationLag()); };
             }},
            {"CPIVolatilitySurface::frequency",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 InflationVolSpec s = inflationVolatility(a[0], CPI);
                 return [=] { return json::value(static_cast<int>(makeCPIVolatility(s)->frequency())); };
             }},
            {"CPIVolatilitySurface::indexIsInterpolated",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 InflationVolSpec s = inflationVolatility(a[0], CPI);
                 return [=] { return json::value(makeCPIVolatility(s)->indexIsInterpolated()); };
             }},
            {"CPIVolatilitySurface::baseDate",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 InflationVolSpec s = inflationVolatility(a[0], CPI);
                 return [=] { return result(makeCPIVolatility(s)->baseDate()); };
             }},
            {"CPIVolatilitySurface::minStrike",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 InflationVolSpec s = inflationVolatility(a[0], CPI);
                 return [=] { return json::value(makeCPIVolatility(s)->minStrike()); };
             }},
            {"CPIVolatilitySurface::maxStrike",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 InflationVolSpec s = inflationVolatility(a[0], CPI);
                 return [=] { return json::value(makeCPIVolatility(s)->maxStrike()); };
             }},
            {"CPIVolatilitySurface::baseLevel",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 InflationVolSpec s = inflationVolatility(a[0], CPI);
                 return [=] { return json::value(makeCPIVolatility(s)->baseLevel()); };
             }},
            // (spec, date, lag)
            {"CPIVolatilitySurface::timeFromBase",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 InflationVolSpec s = inflationVolatility(a[0], CPI);
                 DateSpec d = date(a[1]);
                 LagSpec l = lag(a[2]);
                 return [=] { return json::value(makeCPIVolatility(s)->timeFromBase(makeDate(d), makeLag(l))); };
             }},
            // (spec, date, strike, lag, extrapolate)
            {"CPIVolatilitySurface::volatility(Date)",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 InflationVolSpec s = inflationVolatility(a[0], CPI);
                 DateSpec d = date(a[1]);
                 Real strike = real(a[2]);
                 LagSpec l = lag(a[3]);
                 bool extrapolate = boolean(a[4]);
                 return [=] {
                     return json::value(
                         makeCPIVolatility(s)->volatility(makeDate(d), strike, makeLag(l), extrapolate));
                 };
             }},
            {"CPIVolatilitySurface::totalVariance(Date)",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 InflationVolSpec s = inflationVolatility(a[0], CPI);
                 DateSpec d = date(a[1]);
                 Real strike = real(a[2]);
                 LagSpec l = lag(a[3]);
                 bool extrapolate = boolean(a[4]);
                 return [=] {
                     return json::value(
                         makeCPIVolatility(s)->totalVariance(makeDate(d), strike, makeLag(l), extrapolate));
                 };
             }},
            // (spec, tenor, strike, lag, extrapolate)
            {"CPIVolatilitySurface::volatility(Period)",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 InflationVolSpec s = inflationVolatility(a[0], CPI);
                 LagSpec t = tenorOf(a[1]);
                 Real strike = real(a[2]);
                 LagSpec l = lag(a[3]);
                 bool extrapolate = boolean(a[4]);
                 return [=] {
                     return json::value(
                         makeCPIVolatility(s)->volatility(makeLag(t), strike, makeLag(l), extrapolate));
                 };
             }},
            {"CPIVolatilitySurface::totalVariance(Period)",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 InflationVolSpec s = inflationVolatility(a[0], CPI);
                 LagSpec t = tenorOf(a[1]);
                 Real strike = real(a[2]);
                 LagSpec l = lag(a[3]);
                 bool extrapolate = boolean(a[4]);
                 return [=] {
                     return json::value(
                         makeCPIVolatility(s)->totalVariance(makeLag(t), strike, makeLag(l), extrapolate));
                 };
             }},
            // (spec, time, strike)
            {"CPIVolatilitySurface::volatility(Time)",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 InflationVolSpec s = inflationVolatility(a[0], CPI);
                 Time t = real(a[1]);
                 Real strike = real(a[2]);
                 return [=] { return json::value(makeCPIVolatility(s)->volatility(t, strike)); };
             }},
        });

    }

}
