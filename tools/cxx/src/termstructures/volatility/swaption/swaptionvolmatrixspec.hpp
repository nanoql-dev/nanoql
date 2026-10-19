// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The swaption volatility matrix spec the discrete swaption volatility methods share
// (ql/termstructures/volatility/swaption/swaptionvoldiscrete.hpp and swaptionvolmatrix.hpp), with the vectors' input
// names (tools/vectors/gen/termstructures/volatility/swaption/swaptionvolmatrix.py's functions).
//
// A matrix is {"form", "referenceDate", "calendar", "convention", "optionTenors" or "optionDates", "swapTenors",
// "volatilities", "dayCounter", "flatExtrapolation", "type", "shifts", "enableExtrapolation"}: its form "tenors" (a
// fixed reference date on option tenors), "dates" (on option dates) or "floating" (the reference date the evaluation
// date adjusted on the calendar, which the case's settings set; no "referenceDate"), each QuantLib's constructor on a
// Matrix; tenors as [length, units], the volatilities and shifts as arrays of rows, null shifts Matrix(), the
// convention and the volatility type as QuantLib's integers and the calendar as in indexes/indexspec.hpp; and
// enableExtrapolation, whether enableExtrapolation() is called on the built matrix.

#ifndef NANOQL_CXX_TERMSTRUCTURES_VOLATILITY_SWAPTION_SWAPTIONVOLMATRIXSPEC_HPP
#define NANOQL_CXX_TERMSTRUCTURES_VOLATILITY_SWAPTION_SWAPTIONVOLMATRIXSPEC_HPP

#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "termstructures/volatility/optionlet/optionletvolspec.hpp"

#include <ql/shared_ptr.hpp>
#include <ql/termstructures/volatility/swaption/swaptionvolmatrix.hpp>
#include <ql/termstructures/volatility/volatilitytype.hpp>
#include <ql/time/businessdayconvention.hpp>

#include <string>
#include <vector>

namespace harness::termstructures {

    struct SwaptionVolMatrixSpec {
        std::string form;  // tenors, dates or floating
        DateSpec referenceDate{true, 0};
        indexes::CalendarSpec calendar;
        QuantLib::BusinessDayConvention convention = QuantLib::Following;
        std::vector<PeriodSpec> optionTenors;
        std::vector<DateSpec> optionDates;
        std::vector<PeriodSpec> swapTenors;
        std::vector<std::vector<QuantLib::Real>> volatilities;
        DayCounterSpec dayCounter;
        bool flatExtrapolation = false;
        QuantLib::VolatilityType type = QuantLib::ShiftedLognormal;
        bool hasShifts = false;
        std::vector<std::vector<QuantLib::Real>> shifts;
        bool enableExtrapolation = false;
    };

    // The check.
    SwaptionVolMatrixSpec swaptionVolMatrix(const json::value& v);

    // The call.
    QuantLib::ext::shared_ptr<QuantLib::SwaptionVolatilityMatrix> makeSwaptionVolMatrix(const SwaptionVolMatrixSpec& s);

}

#endif
