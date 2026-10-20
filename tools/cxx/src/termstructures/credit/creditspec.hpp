// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The credit units' shared specs, as tools/vectors/gen/termstructures/defaulttermstructure.py and
// tools/vectors/gen/instruments/creditdefaultswap.py write them: calendars (WeekendsOnly besides
// indexes/indexspec.hpp's), day counters (Actual/360 including the last day besides convert.hpp's), credit curves (a
// flat hazard rate on a date or after settlement days, or an interpolated curve of hazard rates, default densities or
// survival probabilities on a named interpolator, with jumps), discount curves (indexes/indexspec.hpp's flat forward,
// or a log-linear DiscountCurve on nodes), credit default swaps and CDS helpers; and a swap's inspectors and results.

#ifndef NANOQL_CXX_TERMSTRUCTURES_CREDIT_CREDITSPEC_HPP
#define NANOQL_CXX_TERMSTRUCTURES_CREDIT_CREDITSPEC_HPP

#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/handle.hpp>
#include <ql/instruments/creditdefaultswap.hpp>
#include <ql/termstructures/credit/defaultprobabilityhelpers.hpp>
#include <ql/termstructures/defaulttermstructure.hpp>
#include <ql/termstructures/yieldtermstructure.hpp>
#include <ql/time/calendar.hpp>
#include <ql/time/schedule.hpp>

#include <optional>
#include <string>
#include <vector>

namespace harness::credit {

    struct CalendarSpec {
        bool weekendsOnly = false;
        indexes::CalendarSpec other;
    };
    CalendarSpec calendar(const json::value& v);
    QuantLib::Calendar makeCalendar(const CalendarSpec& s);

    struct DayCounterSpec {
        bool actual360Inclusive = false;
        harness::DayCounterSpec other;
    };
    DayCounterSpec dayCounter(const json::value& v);
    QuantLib::DayCounter makeDayCounter(const DayCounterSpec& s);

    struct CurveSpec {
        std::string type;  // flat, flatSettlement, HazardRateCurve, DefaultDensityCurve or SurvivalProbabilityCurve
        DateSpec referenceDate{true, 0};
        QuantLib::Integer settlementDays = 0;
        CalendarSpec calendar;
        QuantLib::Real hazardRate = 0.0;
        std::vector<DateSpec> dates;
        std::vector<QuantLib::Real> data;
        DayCounterSpec dayCounter;
        std::string interpolator;  // BackwardFlat, Linear or LogLinear; the wheel's for the type when not given
        std::vector<QuantLib::Real> jumps;
        std::vector<DateSpec> jumpDates;
    };
    CurveSpec curve(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::DefaultProbabilityTermStructure> makeCurve(const CurveSpec& s);

    struct DiscountSpec {
        bool nodes = false;
        indexes::CurveSpec flat;
        std::vector<DateSpec> dates;
        std::vector<QuantLib::Real> dfs;
        DayCounterSpec dayCounter;
    };
    DiscountSpec discount(const json::value& v);
    QuantLib::Handle<QuantLib::YieldTermStructure> makeDiscount(const DiscountSpec& s);

    struct ScheduleSpec {
        DateSpec effectiveDate{true, 0}, terminationDate{true, 0};
        QuantLib::Integer tenorLength = 0;
        QuantLib::TimeUnit tenorUnits = QuantLib::Months;
        CalendarSpec calendar;
        QuantLib::BusinessDayConvention convention = QuantLib::Following;
        QuantLib::BusinessDayConvention terminationDateConvention = QuantLib::Unadjusted;
        QuantLib::DateGeneration::Rule rule = QuantLib::DateGeneration::CDS2015;
    };
    ScheduleSpec schedule(const json::value& v);
    QuantLib::Schedule makeSchedule(const ScheduleSpec& s);

    struct SwapSpec {
        QuantLib::Protection::Side side = QuantLib::Protection::Buyer;
        QuantLib::Real notional = 0.0;
        std::optional<QuantLib::Real> upfront;
        QuantLib::Real spread = 0.0;
        ScheduleSpec schedule;
        QuantLib::BusinessDayConvention paymentConvention = QuantLib::Following;
        DayCounterSpec dayCounter;
        bool settlesAccrual = true, paysAtDefaultTime = true, rebatesAccrual = true;
        DateSpec protectionStart{true, 0}, upfrontDate{true, 0}, tradeDate{true, 0};
        std::optional<DayCounterSpec> lastPeriodDayCounter;
        QuantLib::Integer cashSettlementDays = 3;
    };
    SwapSpec swap(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::CreditDefaultSwap> makeSwap(const SwapSpec& s);

    struct HelperSpec {
        bool upfront = false;
        QuantLib::Real quote = 0.0, runningSpread = 0.0, recoveryRate = 0.0;
        QuantLib::Integer tenorLength = 0;
        QuantLib::TimeUnit tenorUnits = QuantLib::Years;
        QuantLib::Integer settlementDays = 0, upfrontSettlementDays = 3;
        CalendarSpec calendar;
        QuantLib::Frequency frequency = QuantLib::Quarterly;
        QuantLib::BusinessDayConvention convention = QuantLib::Following;
        QuantLib::DateGeneration::Rule rule = QuantLib::DateGeneration::CDS2015;
        DayCounterSpec dayCounter, lastPeriodDayCounter;
        DiscountSpec discount;
        bool settlesAccrual = true, paysAtDefaultTime = true, rebatesAccrual = true;
        DateSpec startDate{true, 0}, tradeDate{true, 0};
        QuantLib::CreditDefaultSwap::PricingModel model = QuantLib::CreditDefaultSwap::Midpoint;
    };
    HelperSpec helper(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::DefaultProbabilityHelper> makeHelper(const HelperSpec& s,
                                                                             QuantLib::Real quote);

    // The swap's inspectors, as tools/vectors/gen/instruments/creditdefaultswap.py's inspectors() writes them.
    json::value inspectors(const QuantLib::CreditDefaultSwap& cds);
    // The engine's results: null where QuantLib's are "not available"; any other failure is the call's.
    json::value results(const QuantLib::CreditDefaultSwap& cds);

}

#endif
