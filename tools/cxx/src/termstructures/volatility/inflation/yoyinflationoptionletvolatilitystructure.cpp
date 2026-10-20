// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/inflation/yoyinflationoptionletvolatilitystructure.hpp: the members the wheel doesn't
// bind, on QuantLib's ConstantYoYOptionletVolatility built with a volatility type and a displacement, which the
// wheel's constructor doesn't take: volatilityType and displacement, each taking the surface, which also show the
// constructor's refusal of a displacement other than 0 or 1; and volatility on a time, taking the surface, the time
// and the strike, which applies no lag, range or strike check. Each is called on a ConstantYoYOptionletVolatility spec
// of inflationvolspec.hpp, built inside the call.

#include "convert.hpp"
#include "termstructures/volatility/inflation/inflationvolspec.hpp"

#include <ql/termstructures/volatility/inflation/yoyinflationoptionletvolatilitystructure.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using termstructures::InflationVolSpec;
        using termstructures::inflationVolatility;
        using termstructures::makeYoYVolatility;

        const char* const YOY = "ConstantYoYOptionletVolatility";

        const Register unit({
            {"YoYOptionletVolatilitySurface::volatilityType",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 InflationVolSpec s = inflationVolatility(a[0], YOY);
                 return [=] { return json::value(static_cast<int>(makeYoYVolatility(s)->volatilityType())); };
             }},
            {"YoYOptionletVolatilitySurface::displacement",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 InflationVolSpec s = inflationVolatility(a[0], YOY);
                 return [=] { return json::value(makeYoYVolatility(s)->displacement()); };
             }},
            // (spec, time, strike)
            {"YoYOptionletVolatilitySurface::volatility(Time)",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 InflationVolSpec s = inflationVolatility(a[0], YOY);
                 Time t = real(a[1]);
                 Real strike = real(a[2]);
                 return [=] { return json::value(makeYoYVolatility(s)->volatility(t, strike)); };
             }},
        });

    }

}
