// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The swaption volatility spec the swaption volatility methods share (ql/termstructures/volatility/swaption/
// swaptionvolstructure.hpp and swaptionconstantvol.hpp), with the vectors' input names (tools/vectors/gen/
// termstructures/volatility/swaption/swaptionconstantvol.py's functions).
//
// A structure is {"type": "ConstantSwaptionVolatility", "referenceDate" or "settlementDays", "calendar",
// "convention", "volatility", "dayCounter", "volatilityType", "shift", "enableExtrapolation"}: QuantLib's
// ConstantSwaptionVolatility with a fixed reference date, or with its reference date the given business days after
// the evaluation date, which the case's settings set; the convention and the volatility type as QuantLib's integers,
// the calendar as in indexes/indexspec.hpp; and enableExtrapolation, whether enableExtrapolation() is called on the
// built structure. A settlement-days count is a Natural, so a negative one is the harness's refusal.
//
// Or {"type": "BaseSwaptionVolatility", "referenceDate", "calendar", "convention", "volatility", "dayCounter",
// "maxSwapTenor" ([length, units]), "enableExtrapolation"}: the harness's own structure with a fixed reference date,
// which overrides only what SwaptionVolatilityStructure leaves abstract, so that the date and tenor queries, the shift
// and the volatility type are the base's defaults. Its volatility at an option time t, a swap length l and a strike k
// is volatility + 0.01 t + 0.001 l + 0.1 k, its smile section at (t, l) a FlatSmileSection by time at the volatility
// for strike 0, with the structure's day counter, and its maximum date Date::maxDate(), as
// tests/NanoQL.Tests/Vectors/VolatilityInputs.cs defines it.

#ifndef NANOQL_CXX_TERMSTRUCTURES_VOLATILITY_SWAPTION_SWAPTIONVOLSPEC_HPP
#define NANOQL_CXX_TERMSTRUCTURES_VOLATILITY_SWAPTION_SWAPTIONVOLSPEC_HPP

#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "termstructures/volatility/optionlet/optionletvolspec.hpp"

#include <ql/shared_ptr.hpp>
#include <ql/termstructures/volatility/swaption/swaptionvolstructure.hpp>
#include <ql/termstructures/volatility/volatilitytype.hpp>
#include <ql/time/businessdayconvention.hpp>

#include <string>

namespace harness::termstructures {

    struct SwaptionVolSpec {
        std::string type;   // ConstantSwaptionVolatility or BaseSwaptionVolatility
        bool fixed = true;  // a fixed reference date, or settlement days
        DateSpec referenceDate{true, 0};
        QuantLib::Natural settlementDays = 0;
        indexes::CalendarSpec calendar;
        QuantLib::BusinessDayConvention convention = QuantLib::Following;
        QuantLib::Real volatility = 0.0;
        DayCounterSpec dayCounter;
        QuantLib::VolatilityType volatilityType = QuantLib::ShiftedLognormal;
        QuantLib::Real shift = 0.0;
        PeriodSpec maxSwapTenor{0, QuantLib::Years};  // BaseSwaptionVolatility's
        bool enableExtrapolation = false;
    };

    // The check.
    SwaptionVolSpec swaptionVolatility(const json::value& v);

    // The call.
    QuantLib::ext::shared_ptr<QuantLib::SwaptionVolatilityStructure> makeSwaptionVolatility(const SwaptionVolSpec& s);

}

#endif
