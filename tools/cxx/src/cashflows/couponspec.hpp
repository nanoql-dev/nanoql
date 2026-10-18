// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The volatility, pricer, coupon and Ibor leg specs the coupon methods share (ql/cashflows/*.hpp), with the vectors'
// input names (tools/vectors/gen/cashflows/iborcoupon.py's functions).
//
// A volatility is null, QuantLib's empty handle, or {"referenceDate", "calendar", "convention", "volatility",
// "dayCounter", "volatilityType", "displacement"}, a ConstantOptionletVolatility with a fixed reference date. A pricer
// is null, {"type": "BlackIborCouponPricer", "volatility", "timingAdjustment", "correlation", "useIndexedCoupon"}
// (a null useIndexedCoupon reads the at-par switch when the pricer is built), or {"type": "IndexFixingPricer",
// "adjustment"}, the harness's own pricer of any floating-rate coupon, as tests/NanoQL.Tests/Vectors/CouponInputs.cs
// defines it: the geared index fixing shifted by the adjustment, plus the spread; intrinsic caplets and floorlets;
// prices as rates times the accrual period, undiscounted.
//
// A coupon is {"type": "IborCoupon" | "FloatingRateCoupon", "paymentDate", "nominal", "startDate", "endDate",
// "fixingDays", "index", "gearing", "spread", "refPeriodStart", "refPeriodEnd", "dayCounter", "isInArrears",
// "exCouponDate", "fixingConvention", "pricer"}, the same with "cap" and "floor" for "CappedFlooredIborCoupon", or
// {"type": "CappedFlooredCoupon", "underlying", "cap", "floor", "pricer"}; null fixing days are Null<Natural>(), a null
// day counter QuantLib's empty one, a null cap or floor Null<Rate>(); the index is an Ibor or overnight index spec of
// indexes/indexspec.hpp, and the pricer is set with setPricer once the coupon is built.
//
// A leg is the spec of an IborLeg: {"schedule", "index", "notionals", "paymentDayCounter", "paymentConvention",
// "fixingDays", "gearings", "spreads", "caps", "floors", "isInArrears", "exCouponPeriod", "paymentCalendar",
// "paymentLag", "withIndexedCoupons", "stubIndexSelection", "pricer"}, with optional "zeroPayments",
// "fixingConvention" and "atParCoupons" (withAtParCoupons, set after withIndexedCoupons); a schedule is rule-based {"effectiveDate", "terminationDate", "tenor", "calendar", "convention",
// "terminationDateConvention", "rule", "endOfMonth", "firstDate", "nextToLastDate"} or {"dates"}; an ex-coupon period
// {"period", "calendar", "convention", "endOfMonth"}; a stub selection null or {"convention", "indices"}. The leg's
// pricer, if any, is set with setCouponPricer once the leg is built.

#ifndef NANOQL_CXX_CASHFLOWS_COUPONSPEC_HPP
#define NANOQL_CXX_CASHFLOWS_COUPONSPEC_HPP

#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/cashflow.hpp>
#include <ql/cashflows/couponpricer.hpp>
#include <ql/cashflows/floatingratecoupon.hpp>
#include <ql/cashflows/stubiborcoupon.hpp>
#include <ql/termstructures/volatility/optionlet/optionletvolatilitystructure.hpp>
#include <ql/time/schedule.hpp>

#include <memory>
#include <optional>
#include <string>
#include <vector>

namespace harness::cashflows {

    struct VolatilitySpec {
        bool null = true;
        DateSpec referenceDate{true, 0};
        indexes::CalendarSpec calendar;
        QuantLib::BusinessDayConvention convention = QuantLib::Following;
        QuantLib::Real volatility = 0.0;
        DayCounterSpec dayCounter;
        QuantLib::VolatilityType type = QuantLib::ShiftedLognormal;
        QuantLib::Real displacement = 0.0;
    };
    VolatilitySpec volatility(const json::value& v);
    QuantLib::Handle<QuantLib::OptionletVolatilityStructure> makeVolatility(const VolatilitySpec& s);

    struct PricerSpec {
        std::string type;  // empty for a null pricer
        VolatilitySpec volatility;
        QuantLib::BlackIborCouponPricer::TimingAdjustment timing = QuantLib::BlackIborCouponPricer::Black76;
        QuantLib::Real correlation = 1.0;
        std::optional<bool> useIndexedCoupon;
        QuantLib::Real adjustment = 0.0;
    };
    PricerSpec pricer(const json::value& v);
    std::vector<PricerSpec> pricers(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::FloatingRateCouponPricer> makePricer(const PricerSpec& s);

    struct CouponSpec {
        std::string type;
        DateSpec paymentDate{true, 0}, startDate{true, 0}, endDate{true, 0};
        QuantLib::Real nominal = 0.0;
        QuantLib::Natural fixingDays = 0;  // Null<Natural>() for null
        indexes::IndexSpec index;
        QuantLib::Real gearing = 1.0, spread = 0.0;
        DateSpec refPeriodStart{true, 0}, refPeriodEnd{true, 0}, exCouponDate{true, 0};
        bool emptyDayCounter = true;
        DayCounterSpec dayCounter;
        bool isInArrears = false;
        QuantLib::BusinessDayConvention fixingConvention = QuantLib::Preceding;
        QuantLib::Real cap = 0.0, floor = 0.0;  // Null<Rate>() for null
        std::shared_ptr<CouponSpec> underlying;
        PricerSpec pricer;
    };
    CouponSpec coupon(const json::value& v);
    std::vector<CouponSpec> coupons(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::FloatingRateCoupon> makeCoupon(const CouponSpec& s);
    QuantLib::Leg makeCoupons(const std::vector<CouponSpec>& s);

    struct SelectionSpec {
        bool empty = true;
        QuantLib::StubIndexSelection::Convention convention = QuantLib::StubIndexSelection::ClosestIndex;
        std::vector<indexes::IndexSpec> indices;
    };
    SelectionSpec selection(const json::value& v);
    QuantLib::StubIndexSelection makeSelection(const SelectionSpec& s);

    struct ScheduleSpec {
        bool fromDates = false;
        std::vector<DateSpec> dates;
        DateSpec effectiveDate{true, 0}, terminationDate{true, 0}, firstDate{true, 0}, nextToLastDate{true, 0};
        QuantLib::Integer tenorLength = 0;
        QuantLib::TimeUnit tenorUnits = QuantLib::Days;
        indexes::CalendarSpec calendar;
        QuantLib::BusinessDayConvention convention = QuantLib::Following;
        QuantLib::BusinessDayConvention terminationDateConvention = QuantLib::Following;
        QuantLib::DateGeneration::Rule rule = QuantLib::DateGeneration::Backward;
        bool endOfMonth = false;
    };
    ScheduleSpec schedule(const json::value& v);
    QuantLib::Schedule makeSchedule(const ScheduleSpec& s);

    struct LegSpec {
        ScheduleSpec schedule;
        indexes::IndexSpec index;
        std::vector<QuantLib::Real> notionals;
        bool emptyPaymentDayCounter = true;
        DayCounterSpec paymentDayCounter;
        QuantLib::BusinessDayConvention paymentConvention = QuantLib::Following;
        std::vector<QuantLib::Natural> fixingDays;
        std::vector<QuantLib::Real> gearings, spreads, caps, floors;
        bool isInArrears = false;
        bool hasExCoupon = false;
        QuantLib::Integer exCouponLength = 0;
        QuantLib::TimeUnit exCouponUnits = QuantLib::Days;
        indexes::CalendarSpec exCouponCalendar;
        QuantLib::BusinessDayConvention exCouponConvention = QuantLib::Unadjusted;
        bool exCouponEndOfMonth = false;
        bool hasPaymentCalendar = false;
        indexes::CalendarSpec paymentCalendar;
        QuantLib::Integer paymentLag = 0;
        std::optional<bool> withIndexedCoupons;
        SelectionSpec stubIndexSelection;
        std::optional<bool> zeroPayments;
        std::optional<QuantLib::BusinessDayConvention> fixingConvention;
        std::optional<bool> atParCoupons;
        PricerSpec pricer;
    };
    LegSpec leg(const json::value& v);
    QuantLib::Leg makeLeg(const LegSpec& s);

    // A floating-rate coupon's inspectors that never throw: {"date", "nominal", "accrualStartDate", "accrualEndDate",
    // "referencePeriodStart", "referencePeriodEnd", "exCouponDate", "accrualPeriod", "fixingDate", "fixingDays",
    // "gearing", "spread", "isInArrears", "fixingConvention", "dayCounter", "index", "hasPricer"}.
    json::value inspectors(const QuantLib::FloatingRateCoupon& c);

    // The first of the given pricers a cash flow holds, by position, or null for none (or a cash flow that isn't a
    // floating-rate coupon).
    json::value positions(const QuantLib::Leg& leg,
                          const std::vector<QuantLib::ext::shared_ptr<QuantLib::FloatingRateCouponPricer>>& p);

}

#endif
