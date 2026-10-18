// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The overnight coupon, capped coupon, pricer, volatility, schedule and leg specs the overnight methods share
// (ql/cashflows/overnightindexedcoupon.hpp and overnightindexedcouponpricer.hpp), with the vectors' input names
// (tools/vectors/gen/cashflows/overnightindexedcoupon.py's functions, as tests/NanoQL.Tests/Vectors/OvernightInputs.cs
// documents them).
//
// A coupon is {"paymentDate", "nominal", "startDate", "endDate", "index", "gearing", "spread", "refPeriodStart",
// "refPeriodEnd", "dayCounter", "telescopicValueDates", "averagingMethod", "lookbackDays", "lockoutDays",
// "applyObservationShift", "compoundSpreadDaily", "rateComputationStartDate", "rateComputationEndDate",
// "exCouponDate", "roundingPrecision"}, its index an OvernightIndex spec of indexes/indexspec.hpp; the harness builds
// no telescopic coupon, whose expected values the plan's telescopic rule takes from the wheel. A capped coupon is
// {"underlying", "cap", "floor", "nakedOption", "dailyCapFloor"}. A pricer is {"type", "vol",
// "effectiveVolatilityInput"} with the types Compounding, ArithmeticAveraged (adding "meanReversion", "volatility" and
// "byApprox"), BlackCompounding and BlackAveraging. A volatility is null or a ConstantOptionletVolatility,
// {"referenceDate", "calendar", "convention", "volatility", "dayCounter", "type", "displacement"}. A schedule is
// rule-based, {"effectiveDate", "terminationDate", "tenor", "calendar", "convention", "terminationDateConvention",
// "rule", "endOfMonth", "firstDate", "nextToLastDate"}, or {"dates", "calendar", "tenor", "isRegular"}. A leg is the
// wheel's OvernightLeg keywords as specs, {"schedule", "index", "notionals", "paymentDayCounter", "paymentConvention",
// "paymentCalendar", "paymentLag", "gearings", "spreads", "telescopicValueDates", "averagingMethod", "lookbackDays",
// "lockoutDays", "applyObservationShift", "compoundSpreadDaily", "caps", "floors", "dailyCapFloor", "inArrears",
// "nakedOption", "paymentDates", "roundingPrecision"}, and the setters the wheel doesn't reach, "lastRecentPeriod"
// ([length, units] or null), "lastRecentPeriodCalendar" and "couponPricer" (a pricer spec or null).

#ifndef NANOQL_CXX_CASHFLOWS_OVERNIGHTSPEC_HPP
#define NANOQL_CXX_CASHFLOWS_OVERNIGHTSPEC_HPP

#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/cashflow.hpp>
#include <ql/cashflows/floatingratecoupon.hpp>
#include <ql/cashflows/overnightindexedcoupon.hpp>
#include <ql/cashflows/overnightindexedcouponpricer.hpp>
#include <ql/cashflows/rateaveraging.hpp>
#include <ql/termstructures/volatility/volatilitytype.hpp>
#include <ql/time/schedule.hpp>

#include <optional>
#include <string>
#include <vector>

namespace harness::overnight {

    struct CouponSpec {
        DateSpec paymentDate{true, 0};
        QuantLib::Real nominal = 0.0;
        DateSpec startDate{true, 0};
        DateSpec endDate{true, 0};
        indexes::IndexSpec index;
        QuantLib::Real gearing = 1.0;
        QuantLib::Real spread = 0.0;
        DateSpec refPeriodStart{true, 0};
        DateSpec refPeriodEnd{true, 0};
        std::optional<DayCounterSpec> dayCounter;
        QuantLib::RateAveraging::Type averagingMethod = QuantLib::RateAveraging::Compound;
        std::optional<QuantLib::Natural> lookbackDays;
        QuantLib::Natural lockoutDays = 0;
        bool applyObservationShift = false;
        bool compoundSpreadDaily = false;
        DateSpec rateComputationStartDate{true, 0};
        DateSpec rateComputationEndDate{true, 0};
        DateSpec exCouponDate{true, 0};
        std::optional<QuantLib::Integer> roundingPrecision;
    };

    struct CappedSpec {
        CouponSpec underlying;
        QuantLib::Real cap = 0.0;    // Null<Real>() for none
        QuantLib::Real floor = 0.0;  // Null<Real>() for none
        bool nakedOption = false;
        bool dailyCapFloor = false;
    };

    // A plain coupon or a capped one, as a pricer member takes either.
    struct AnyCouponSpec {
        bool capped = false;
        CappedSpec spec;  // the plain coupon is spec.underlying
    };

    struct VolSpec {
        bool null = true;
        DateSpec referenceDate{true, 0};
        indexes::CalendarSpec calendar;
        QuantLib::BusinessDayConvention convention = QuantLib::Following;
        QuantLib::Real volatility = 0.0;
        DayCounterSpec dayCounter;
        QuantLib::VolatilityType type = QuantLib::ShiftedLognormal;
        QuantLib::Real displacement = 0.0;
    };

    struct PricerSpec {
        std::string type;
        VolSpec vol;
        bool effectiveVolatilityInput = false;
        QuantLib::Real meanReversion = 0.03;
        QuantLib::Real volatility = 0.0;
        bool byApprox = false;
    };

    struct ScheduleSpec {
        bool dates = false;
        std::vector<DateSpec> dateList;
        std::optional<indexes::CalendarSpec> calendar;
        std::optional<std::pair<QuantLib::Integer, QuantLib::TimeUnit>> tenor;
        std::vector<bool> isRegular;
        DateSpec effectiveDate{true, 0};
        DateSpec terminationDate{true, 0};
        QuantLib::BusinessDayConvention convention = QuantLib::Following;
        QuantLib::BusinessDayConvention terminationDateConvention = QuantLib::Following;
        QuantLib::DateGeneration::Rule rule = QuantLib::DateGeneration::Forward;
        bool endOfMonth = false;
        DateSpec firstDate{true, 0};
        DateSpec nextToLastDate{true, 0};
    };

    struct LegSpec {
        ScheduleSpec schedule;
        indexes::IndexSpec index;
        std::vector<QuantLib::Real> notionals;
        std::optional<DayCounterSpec> paymentDayCounter;
        QuantLib::BusinessDayConvention paymentConvention = QuantLib::Following;
        std::optional<indexes::CalendarSpec> paymentCalendar;
        QuantLib::Integer paymentLag = 0;
        std::vector<QuantLib::Real> gearings;
        std::vector<QuantLib::Real> spreads;
        QuantLib::RateAveraging::Type averagingMethod = QuantLib::RateAveraging::Compound;
        std::optional<QuantLib::Natural> lookbackDays;
        QuantLib::Natural lockoutDays = 0;
        bool applyObservationShift = false;
        bool compoundSpreadDaily = false;
        std::vector<QuantLib::Real> caps;
        std::vector<QuantLib::Real> floors;
        bool dailyCapFloor = false;
        bool inArrears = true;
        bool nakedOption = false;
        std::vector<DateSpec> paymentDates;
        std::optional<QuantLib::Integer> roundingPrecision;
        bool hasLastRecentPeriod = false;
        std::optional<std::pair<QuantLib::Integer, QuantLib::TimeUnit>> lastRecentPeriod;
        std::optional<indexes::CalendarSpec> lastRecentPeriodCalendar;
        std::optional<PricerSpec> couponPricer;
    };

    // The check.
    CouponSpec coupon(const json::value& v);
    AnyCouponSpec anyCoupon(const json::value& v);
    PricerSpec pricer(const json::value& v);
    LegSpec leg(const json::value& v);

    // The call.
    QuantLib::ext::shared_ptr<QuantLib::OvernightIndexedCoupon> makeCoupon(const CouponSpec& s);
    QuantLib::ext::shared_ptr<QuantLib::FloatingRateCoupon> makeAnyCoupon(const AnyCouponSpec& s);
    QuantLib::ext::shared_ptr<QuantLib::OvernightIndexedCouponPricer> makePricer(const PricerSpec& s);
    QuantLib::Leg makeLeg(const LegSpec& s);

    // A pricer's class name, or null for none.
    json::value pricerName(const QuantLib::ext::shared_ptr<QuantLib::FloatingRateCouponPricer>& p);

}

#endif
