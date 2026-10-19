// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The cap/floor term-volatility specs the cap volatility methods share (ql/termstructures/volatility/capfloor/), with
// the vectors' input names (tools/vectors/gen/termstructures/volatility/capfloor/capfloortermvolsurface.py's and
// capfloortermvolcurve.py's functions).
//
// A surface is {"settlementDays" or "settlementDate", "calendar", "convention", "optionTenors", "strikes",
// "volatilities", "dayCounter", "enableExtrapolation"}: the tenors as [length, units], the volatilities as the matrix's
// rows, the calendar as in indexes/indexspec.hpp, and enableExtrapolation whether enableExtrapolation() is called on
// the built surface; the settlement-days form's reference date moves with the evaluation date, which the case's
// settings set. A curve is the same without "strikes", its "volatilities" one per tenor. A settlement-days count is a
// Natural, so a negative one is the harness's refusal. (Moved here from capfloortermvolatilitystructure.cpp, whose
// surface it was, for the stripper methods that build surfaces too.)

#ifndef NANOQL_CXX_TERMSTRUCTURES_VOLATILITY_CAPFLOOR_CAPFLOORVOLSPEC_HPP
#define NANOQL_CXX_TERMSTRUCTURES_VOLATILITY_CAPFLOOR_CAPFLOORVOLSPEC_HPP

#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/shared_ptr.hpp>
#include <ql/termstructures/volatility/capfloor/capfloortermvolcurve.hpp>
#include <ql/termstructures/volatility/capfloor/capfloortermvolsurface.hpp>

#include <utility>
#include <vector>

namespace harness::capfloorvols {

    struct SurfaceSpec {
        bool fixed = true;
        DateSpec settlementDate{true, 0};
        QuantLib::Natural settlementDays = 0;
        indexes::CalendarSpec calendar;
        QuantLib::BusinessDayConvention convention = QuantLib::Following;
        std::vector<std::pair<QuantLib::Integer, QuantLib::TimeUnit>> optionTenors;
        std::vector<QuantLib::Rate> strikes;  // empty for a curve
        QuantLib::Size rows = 0, columns = 0;
        std::vector<QuantLib::Real> volatilities;  // row by row; a curve's one per tenor
        DayCounterSpec dayCounter;
        bool enableExtrapolation = false;
    };

    // The checks.
    SurfaceSpec surface(const json::value& v);
    SurfaceSpec curve(const json::value& v);
    std::pair<QuantLib::Integer, QuantLib::TimeUnit> period(const json::value& v);  // [length, units]

    // The calls.
    QuantLib::ext::shared_ptr<QuantLib::CapFloorTermVolSurface> makeSurface(const SurfaceSpec& s);
    QuantLib::ext::shared_ptr<QuantLib::CapFloorTermVolCurve> makeCurve(const SurfaceSpec& s);

}

#endif
