// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The swap and builder specs the swap methods share (ql/instruments/*.hpp), with the vectors' input names
// (tools/vectors/gen/instruments/fixedvsfloatingswap.py's and makevanillaswap.py's functions, as
// tests/NanoQL.Tests/Vectors/SwapInputs.cs documents them).
//
// An overnight-indexed swap is {"kind": "OvernightIndexedSwap", "form", "type", "fixedNominals", "fixedSchedule",
// "fixedRate", "fixedDC", "overnightNominals", "overnightSchedule", "index", "spread", "paymentLag",
// "paymentAdjustment", "paymentCalendar", "telescopicValueDates", "averagingMethod", "lookbackDays", "lockoutDays",
// "applyObservationShift", "roundingPrecision"}, its form one of QuantLib's four constructors ("nominal", "nominals",
// "nominal, two schedules", "nominals, two schedules"), each called directly; a null fixed day counter or payment
// calendar is QuantLib's empty one, a null lookback Null<Natural>(), a null precision std::nullopt. Its schedules are
// rule-based specs of cashflows/couponspec.hpp, its index an OvernightIndex spec of indexes/indexspec.hpp.
//
// A build is {"swapTenor", "index", "fixedRate", "forwardStart", "setters"}: the tenor and forward start as [length,
// units], the fixed rate or null for Null<Rate>(), and the setters as [name, value] pairs, called in order after
// withFixedRate and withForwardStart, as QuantLib-Python's builders call them. The setters the harness takes are
// settlementDays, settlementCalendar, effectiveDate, terminationDate, fixedLegTenor, fixedLegCalendar,
// fixedLegDayCount and floatingLegCalendar for MakeVanillaSwap, on an IborIndex spec; and settlementDays,
// settlementCalendar, effectiveDate, terminationDate, calendar, fixedLegCalendar, overnightLegCalendar and endOfMonth
// for MakeOIS, on an OvernightIndex spec.

#ifndef NANOQL_CXX_INSTRUMENTS_SWAPSPEC_HPP
#define NANOQL_CXX_INSTRUMENTS_SWAPSPEC_HPP

#include "cashflows/couponspec.hpp"
#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/instruments/makeois.hpp>
#include <ql/instruments/makevanillaswap.hpp>
#include <ql/instruments/overnightindexedswap.hpp>
#include <ql/time/schedule.hpp>

#include <optional>
#include <string>
#include <utility>
#include <vector>

namespace harness::swaps {

    struct OisSpec {
        std::string form;
        QuantLib::Swap::Type type = QuantLib::Swap::Payer;
        std::vector<QuantLib::Real> fixedNominals;
        cashflows::ScheduleSpec fixedSchedule;
        QuantLib::Rate fixedRate = 0.0;
        std::optional<DayCounterSpec> fixedDC;
        std::vector<QuantLib::Real> overnightNominals;
        cashflows::ScheduleSpec overnightSchedule;
        indexes::IndexSpec index;
        QuantLib::Spread spread = 0.0;
        QuantLib::Integer paymentLag = 0;
        QuantLib::BusinessDayConvention paymentAdjustment = QuantLib::Following;
        std::optional<indexes::CalendarSpec> paymentCalendar;
        bool telescopicValueDates = false;
        QuantLib::RateAveraging::Type averagingMethod = QuantLib::RateAveraging::Compound;
        std::optional<QuantLib::Natural> lookbackDays;
        QuantLib::Natural lockoutDays = 0;
        bool applyObservationShift = false;
        std::optional<QuantLib::Integer> roundingPrecision;
    };

    // One setter: its name and the values it may hold.
    struct SetterSpec {
        std::string name;
        QuantLib::Natural natural = 0;
        DateSpec date{true, 0};
        std::pair<QuantLib::Integer, QuantLib::TimeUnit> period{0, QuantLib::Days};
        indexes::CalendarSpec calendar;
        DayCounterSpec dayCounter;
        bool flag = false;
    };

    struct BuildSpec {
        std::pair<QuantLib::Integer, QuantLib::TimeUnit> swapTenor{0, QuantLib::Days};
        indexes::IndexSpec index;
        QuantLib::Rate fixedRate = 0.0;  // Null<Rate>() for none
        std::pair<QuantLib::Integer, QuantLib::TimeUnit> forwardStart{0, QuantLib::Days};
        std::vector<SetterSpec> setters;
    };

    // The check.
    OisSpec ois(const json::value& v);
    BuildSpec vanillaBuild(const json::value& v);
    BuildSpec oisBuild(const json::value& v);

    // The call.
    QuantLib::ext::shared_ptr<QuantLib::OvernightIndexedSwap> makeOis(const OisSpec& s);
    QuantLib::ext::shared_ptr<QuantLib::VanillaSwap> makeVanillaSwap(const BuildSpec& s);
    QuantLib::ext::shared_ptr<QuantLib::OvernightIndexedSwap> makeOisFromBuild(const BuildSpec& s);

    // A schedule's dates, as serials.
    json::value scheduleDates(const QuantLib::Schedule& s);

    // A built swap's start and maturity dates, both schedules' dates (under the given name for the floating one) and
    // its fixed rate.
    json::value describe(const QuantLib::FixedVsFloatingSwap& swap, const char* floatingSchedule);

}

#endif
