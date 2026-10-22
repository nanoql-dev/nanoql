// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The index, calendar and curve specs the index methods share (ql/index.hpp and ql/indexes/*.hpp), with the vectors'
// input names (tools/vectors/gen/indexes/interestrateindex.py's functions).
//
// An index is {"type": "IborIndex", "familyName", "tenor" ([length, units]), "settlementDays", "fixingCalendar",
// "convention", "endOfMonth", "dayCounter", "curve"} or {"type": "OvernightIndex", "familyName", "settlementDays",
// "fixingCalendar", "dayCounter", "curve"}, each built with QuantLib's empty currency; or {"type":
// "NoNativeFixingsIndex", "name"}, the harness's own index whose allowsNativeFixings() is false, which accepts every
// date and forecasts nothing, as tests/NanoQL.Tests/Vectors/IndexInputs.cs defines it. A calendar is {"type"} with
// TARGET or NullCalendar, or {"type", "market"} with UnitedStates or UnitedKingdom. A curve is null, QuantLib's empty
// handle, or {"referenceDate", "forward", "dayCounter", "compounding", "frequency"}, a FlatForward with a fixed
// reference date. A fixing-days count is a Natural, so a negative one is the harness's refusal.

#ifndef NANOQL_CXX_INDEXES_INDEXSPEC_HPP
#define NANOQL_CXX_INDEXES_INDEXSPEC_HPP

#include "convert.hpp"

#include <ql/handle.hpp>
#include <ql/index.hpp>
#include <ql/indexes/iborindex.hpp>
#include <ql/shared_ptr.hpp>
#include <ql/termstructures/yieldtermstructure.hpp>
#include <ql/time/businessdayconvention.hpp>
#include <ql/time/calendar.hpp>
#include <ql/time/timeunit.hpp>

#include <functional>
#include <string>

namespace harness::indexes {

    struct CalendarSpec {
        std::string type;
        int market = 0;  // UnitedStates' and UnitedKingdom's market
    };
    CalendarSpec calendar(const json::value& v);
    QuantLib::Calendar makeCalendar(const CalendarSpec& s);

    struct CurveSpec {
        bool null = true;
        DateSpec referenceDate{true, 0};
        QuantLib::Real forward = 0.0;
        DayCounterSpec dayCounter;
        QuantLib::Compounding compounding = QuantLib::Continuous;
        QuantLib::Frequency frequency = QuantLib::Annual;
    };
    CurveSpec curve(const json::value& v);
    QuantLib::Handle<QuantLib::YieldTermStructure> makeCurve(const CurveSpec& s);

    struct IndexSpec {
        std::string type;  // IborIndex, OvernightIndex or NoNativeFixingsIndex
        std::string name;  // the family name, or NoNativeFixingsIndex's name
        QuantLib::Integer tenorLength = 0;
        QuantLib::TimeUnit tenorUnits = QuantLib::Days;
        QuantLib::Natural settlementDays = 0;
        CalendarSpec fixingCalendar;
        QuantLib::BusinessDayConvention convention = QuantLib::Following;
        bool endOfMonth = false;
        DayCounterSpec dayCounter;
        CurveSpec curve;
    };
    // The check: any of the three types, or only the two Ibor types.
    IndexSpec index(const json::value& v);
    IndexSpec iborIndex(const json::value& v);

    // The call.
    QuantLib::ext::shared_ptr<QuantLib::Index> makeIndex(const IndexSpec& s);
    QuantLib::ext::shared_ptr<QuantLib::IborIndex> makeIborIndex(const IndexSpec& s);

    // A preset's case, as tools/vectors/gen/indexes/ibor/euribor.py's index_forecast_case writes it, for the Ibor
    // presets the wheel doesn't bind (indexes/ibor/chflibor.cpp, eurlibor.cpp). The check reads the dates (serials)
    // and the forecast curves, tools/vectors/gen/processes/blackscholesprocess.py's specs: {"type": "FlatForward",
    // "referenceDate", "forward", "dayCounter"} or {"type": "ZeroCurve", "dates", "rates", "dayCounter"}, QuantLib's
    // linear zero curve. The call builds the preset on QuantLib's empty handle for its inspectors and its fixing, value
    // and maturity dates at each date, then on each curve for its fixings there ("forecast", one array per curve),
    // each {"throws": true} where QuantLib refuses it.
    using IborPreset = std::function<QuantLib::ext::shared_ptr<QuantLib::IborIndex>(
        const QuantLib::Handle<QuantLib::YieldTermStructure>&)>;
    Call presetCase(IborPreset make, const json::value& dates, const json::value& curves);

}

#endif
