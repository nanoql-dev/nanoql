// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The bond, leg, schedule, rate and price specs the bond methods share (ql/instruments/bond.hpp and
// ql/instruments/bonds/*.hpp), with the vectors' input names (tools/vectors/gen/instruments/bond.py's functions, as
// tests/NanoQL.Tests/Vectors/BondInputs.cs documents them).
//
// A bond is {"type": "Bond", "settlementDays", "calendar", "issueDate", "coupons"} or {"type": "BondFaceAmount",
// "settlementDays", "calendar", "faceAmount", "maturityDate", "issueDate", "cashflows"}, QuantLib's two public
// constructors; or {"type": "FixedRateBond" | "FloatingRateBond" | "ZeroCouponBond" | "AmortizingFixedRateBond", ...}
// with QuantLib's arguments by bond.py's names, each called directly. A leg is {"fixedRateLeg", "coupons",
// "extraFlows"}: the coupons of a FixedRateLeg (null for none; cashflows.py's spec), fixed-rate coupons given one by
// one (an optional key) and Redemption and AmortizingPayment flows, in that order. A schedule is a rule-based spec of
// cashflows/couponspec.hpp, or {"dates", "calendar", "convention"}, its calendar never null. An ex-coupon period is
// null or {"period", "calendar", "convention", "endOfMonth"}. A null calendar or day counter is QuantLib's empty one,
// a null date its null date, a null fixing days Null<Natural>(). A rate is {"rate", "dayCounter", "compounding",
// "frequency"}, a price {"amount", "type"}.

#ifndef NANOQL_CXX_INSTRUMENTS_BONDSPEC_HPP
#define NANOQL_CXX_INSTRUMENTS_BONDSPEC_HPP

#include "cashflows/couponspec.hpp"
#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/cashflow.hpp>
#include <ql/instruments/bond.hpp>
#include <ql/interestrate.hpp>
#include <ql/time/schedule.hpp>

#include <optional>
#include <string>
#include <vector>

namespace harness::bonds {

    struct ScheduleSpec {
        bool fromDates = false;
        std::vector<DateSpec> dates;
        indexes::CalendarSpec calendar;
        QuantLib::BusinessDayConvention convention = QuantLib::Unadjusted;
        cashflows::ScheduleSpec rule;
    };

    struct ExCouponSpec {
        bool given = false;
        QuantLib::Integer length = 0;
        QuantLib::TimeUnit units = QuantLib::Days;
        std::optional<indexes::CalendarSpec> calendar;
        QuantLib::BusinessDayConvention convention = QuantLib::Unadjusted;
        bool endOfMonth = false;
    };

    struct FixedLegSpec {
        ScheduleSpec schedule;
        std::vector<QuantLib::Real> notionals, couponRates;
        DayCounterSpec dayCounter;
        QuantLib::Compounding compounding = QuantLib::Simple;
        QuantLib::Frequency frequency = QuantLib::Annual;
        std::optional<DayCounterSpec> firstPeriodDayCounter;
        std::optional<indexes::CalendarSpec> paymentCalendar;
        QuantLib::Integer paymentLag = 0;
        QuantLib::BusinessDayConvention paymentAdjustment = QuantLib::Following;
        ExCouponSpec exCoupon;
    };

    struct CouponSpec {
        DateSpec paymentDate, accrualStartDate, accrualEndDate, refPeriodStart, refPeriodEnd, exCouponDate;
        QuantLib::Real nominal, rate;
        DayCounterSpec dayCounter;
    };

    struct FlowSpec {
        bool redemption;
        QuantLib::Real amount;
        DateSpec date;
    };

    struct LegSpec {
        std::optional<FixedLegSpec> fixedRateLeg;
        std::vector<CouponSpec> coupons;
        std::vector<FlowSpec> extraFlows;
    };

    struct BondSpec {
        std::string type;
        QuantLib::Natural settlementDays = 0;
        std::optional<indexes::CalendarSpec> calendar, paymentCalendar;
        QuantLib::Real faceAmount = 0.0, redemption = 100.0;
        DateSpec issueDate{true, 0}, maturityDate{true, 0};
        LegSpec leg;
        ScheduleSpec schedule;
        std::vector<QuantLib::Real> coupons, notionals, redemptions, gearings, spreads, caps, floors;
        std::optional<DayCounterSpec> accrualDayCounter, firstPeriodDayCounter;
        QuantLib::BusinessDayConvention paymentConvention = QuantLib::Following;
        QuantLib::BusinessDayConvention fixingConvention = QuantLib::Preceding;
        ExCouponSpec exCoupon;
        indexes::IndexSpec index;
        QuantLib::Natural fixingDays = 0;  // Null<Natural>() for null
        bool inArrears = false;
        QuantLib::Integer paymentLag = 0;
    };

    struct RateSpec {
        QuantLib::Real rate;
        DayCounterSpec dayCounter;
        QuantLib::Compounding compounding;
        QuantLib::Frequency frequency;
    };

    struct PriceSpec {
        QuantLib::Real amount;
        QuantLib::Bond::Price::Type type;
    };

    // The check.
    BondSpec bond(const json::value& v);
    LegSpec leg(const json::value& v);
    ScheduleSpec schedule(const json::value& v);
    RateSpec rate(const json::value& v);
    PriceSpec price(const json::value& v);
    std::optional<indexes::CalendarSpec> optionalCalendar(const json::value& v);
    std::optional<DayCounterSpec> optionalDayCounter(const json::value& v);

    // The call.
    QuantLib::ext::shared_ptr<QuantLib::Bond> makeBond(const BondSpec& s);
    // The bond priced by a DiscountingBondEngine on the curve, with the engine's includeSettlementDateFlows.
    QuantLib::ext::shared_ptr<QuantLib::Bond> makePricedBond(const BondSpec& s, const indexes::CurveSpec& curve,
                                                             const std::optional<bool>& includeSettlementDateFlows);
    QuantLib::Leg makeLeg(const LegSpec& s);
    QuantLib::Schedule makeSchedule(const ScheduleSpec& s);
    QuantLib::InterestRate makeRate(const RateSpec& s);
    QuantLib::Bond::Price makePrice(const PriceSpec& s);
    QuantLib::Calendar makeCalendar(const std::optional<indexes::CalendarSpec>& s);
    QuantLib::DayCounter makeDayCounter(const std::optional<DayCounterSpec>& s);

    // Results.
    json::value realArray(const std::vector<QuantLib::Real>& x);

}

#endif
