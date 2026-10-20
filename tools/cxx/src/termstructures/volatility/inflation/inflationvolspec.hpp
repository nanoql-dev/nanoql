// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The inflation volatility specs the methods of termstructures/volatility/inflation/ share, with the vectors' input
// names (tools/vectors/gen/termstructures/volatility/inflation/yoyinflationoptionletvolatilitystructure.py's functions).
//
// A surface is {"type": "ConstantCPIVolatility", "volatility", "settlementDays", "calendar", "convention",
// "dayCounter", "observationLag" ([length, units]), "frequency", "indexIsInterpolated", "enableExtrapolation"}, or
// {"type": "ConstantYoYOptionletVolatility", the same keys, "minStrike", "maxStrike", "volatilityType",
// "displacement"}: QuantLib's constant surfaces, whose reference date is the given business days after the evaluation
// date, which the case's settings set; the enums as QuantLib's integers, the calendar as in indexes/indexspec.hpp; and
// enableExtrapolation, whether enableExtrapolation() is called on the built surface. A settlement-days count is a
// Natural, so a negative one is the harness's refusal.

#ifndef NANOQL_CXX_TERMSTRUCTURES_VOLATILITY_INFLATION_INFLATIONVOLSPEC_HPP
#define NANOQL_CXX_TERMSTRUCTURES_VOLATILITY_INFLATION_INFLATIONVOLSPEC_HPP

#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/shared_ptr.hpp>
#include <ql/termstructures/volatility/inflation/cpivolatilitystructure.hpp>
#include <ql/termstructures/volatility/inflation/yoyinflationoptionletvolatilitystructure.hpp>
#include <ql/termstructures/volatility/volatilitytype.hpp>
#include <ql/time/businessdayconvention.hpp>
#include <ql/time/frequency.hpp>
#include <ql/time/period.hpp>

#include <string>

namespace harness::termstructures {

    struct InflationVolSpec {
        std::string type;  // ConstantCPIVolatility or ConstantYoYOptionletVolatility
        QuantLib::Real volatility = 0.0;
        QuantLib::Natural settlementDays = 0;
        indexes::CalendarSpec calendar;
        QuantLib::BusinessDayConvention convention = QuantLib::Following;
        DayCounterSpec dayCounter;
        QuantLib::Integer lagLength = 0;
        QuantLib::TimeUnit lagUnits = QuantLib::Months;
        QuantLib::Frequency frequency = QuantLib::Monthly;
        bool indexIsInterpolated = false;
        QuantLib::Real minStrike = -1.0;
        QuantLib::Real maxStrike = 100.0;
        QuantLib::VolatilityType volatilityType = QuantLib::ShiftedLognormal;
        QuantLib::Real displacement = 0.0;
        bool enableExtrapolation = false;
    };

    // The checks: a surface of either type, or of the one type named.
    InflationVolSpec inflationVolatility(const json::value& v, const std::string& type);

    struct LagSpec {
        bool null = true;  // QuantLib's default Period(-1, Days)
        QuantLib::Integer length = 0;
        QuantLib::TimeUnit units = QuantLib::Days;
    };
    LagSpec lag(const json::value& v);  // [length, units], or null for the default

    // The calls.
    QuantLib::ext::shared_ptr<QuantLib::CPIVolatilitySurface> makeCPIVolatility(const InflationVolSpec& s);
    QuantLib::ext::shared_ptr<QuantLib::YoYOptionletVolatilitySurface> makeYoYVolatility(const InflationVolSpec& s);
    QuantLib::Period makeLag(const LagSpec& l);

}

#endif
