// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The optionlet volatility spec the volatility methods share (ql/termstructures/voltermstructure.hpp and
// ql/termstructures/volatility/optionlet/optionletvolatilitystructure.hpp), with the vectors' input names
// (tools/vectors/gen/termstructures/volatility/optionlet/constantoptionletvol.py's functions).
//
// A structure is {"type": "ConstantOptionletVolatility", "referenceDate" or "settlementDays", "calendar",
// "convention", "volatility", "dayCounter", "volatilityType", "displacement", "enableExtrapolation"}: QuantLib's
// ConstantOptionletVolatility with a fixed reference date, or with its reference date the given business days after
// the evaluation date, which the case's settings set; the convention and the volatility type as QuantLib's integers,
// the calendar as in indexes/indexspec.hpp; and enableExtrapolation, whether enableExtrapolation() is called on the
// built structure. A settlement-days count is a Natural, so a negative one is the harness's refusal.
//
// Or {"type": "BaseOptionletVolatility", "referenceDate", "calendar", "convention", "volatility", "dayCounter",
// "enableExtrapolation"}: the harness's own structure with a fixed reference date, which overrides only what
// OptionletVolatilityStructure leaves abstract (a constant volatility over every date and strike, and a smile section
// by time, a FlatSmileSection of that volatility with the structure's day counter), so that volatilityType(),
// displacement() and the smile section by date are the base's defaults, as tests/NanoQL.Tests/Vectors/
// VolatilityInputs.cs defines it.

#ifndef NANOQL_CXX_TERMSTRUCTURES_VOLATILITY_OPTIONLET_OPTIONLETVOLSPEC_HPP
#define NANOQL_CXX_TERMSTRUCTURES_VOLATILITY_OPTIONLET_OPTIONLETVOLSPEC_HPP

#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/shared_ptr.hpp>
#include <ql/termstructures/volatility/optionlet/optionletvolatilitystructure.hpp>
#include <ql/termstructures/volatility/volatilitytype.hpp>
#include <ql/time/businessdayconvention.hpp>
#include <ql/time/period.hpp>

#include <string>

namespace harness::termstructures {

    struct OptionletVolSpec {
        std::string type;   // ConstantOptionletVolatility or BaseOptionletVolatility
        bool fixed = true;  // a fixed reference date, or settlement days
        DateSpec referenceDate{true, 0};
        QuantLib::Natural settlementDays = 0;
        indexes::CalendarSpec calendar;
        QuantLib::BusinessDayConvention convention = QuantLib::Following;
        QuantLib::Real volatility = 0.0;
        DayCounterSpec dayCounter;
        QuantLib::VolatilityType volatilityType = QuantLib::ShiftedLognormal;
        QuantLib::Real displacement = 0.0;
        bool enableExtrapolation = false;
    };

    // The check.
    OptionletVolSpec optionletVolatility(const json::value& v);

    struct PeriodSpec {
        QuantLib::Integer length;
        QuantLib::TimeUnit units;
    };
    PeriodSpec tenor(const json::value& v);  // [length, units]

    // The call.
    QuantLib::ext::shared_ptr<QuantLib::OptionletVolatilityStructure> makeOptionletVolatility(
        const OptionletVolSpec& s);
    QuantLib::Period makePeriod(const PeriodSpec& p);

}

#endif
