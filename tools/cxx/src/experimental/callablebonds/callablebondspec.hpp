// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The callability, callable bond, engine and volatility specs the callable-bond methods share
// (ql/instruments/callabilityschedule.hpp, ql/experimental/callablebonds/*.hpp), with the vectors' input names
// (tools/vectors/gen/experimental/callablebonds/callablebond.py's functions, as
// tests/NanoQL.Tests/Vectors/CallableBondInputs.cs documents them).
//
// A callability is {"price": {"amount", "type"}, "type", "date"}. A bond is {"type": "CallableFixedRateBond",
// "settlementDays", "faceAmount", "schedule", "coupons", "accrualDayCounter", "paymentConvention", "redemption",
// "issueDate", "putCallSchedule", "exCouponPeriod"} or {"type": "CallableZeroCouponBond", "settlementDays",
// "faceAmount", "calendar", "maturityDate", "dayCounter", "paymentConvention", "redemption", "issueDate",
// "putCallSchedule"}, each called with every argument; its schedule and ex-coupon period are instruments/bondspec.hpp's,
// a null calendar or day counter QuantLib's empty one. An engine is {"type": "TreeSteps" | "TreeZeroSteps", "model",
// "timeSteps", "termStructure"}, {"type": "TreeGrid" | "TreeLattice" | "TreeZeroGrid", "model", "timeGrid",
// "termStructure"} (the fixed-rate or zero-coupon tree engine; a lattice spec is QuantLib's grid engine), a null term
// structure an empty handle; {"type": "Black" | "BlackZero", "volatility", "discountCurve"}, the quote constructor; or
// {"type": "BlackStructure" | "BlackZeroStructure", "volatility", "discountCurve"}, the structure constructor, its
// volatility a volatility spec. Curves, models and grids are models/modelspec.hpp's and timegrid.hpp's. A volatility
// is a constant one, {"referenceDate", "volatility", "dayCounter"} or {"settlementDays", "calendar", "volatility",
// "dayCounter"}, the latter's reference date moving with the evaluation date; or {"type":
// "HarnessCallableBondVolatility", "referenceDate", "volatility", "dayCounter", "maxBondTenor" ([length, units]),
// "minStrike", "maxStrike"}, the harness's own subclass of CallableBondVolatilityStructure, which overrides only what
// the base leaves abstract (a constant volatility, and a flat smile section by time), so that the base's defaults,
// maxBondLength and the volatility by date among them, are what it answers with, where QuantLib's only subclass
// overrides them. tests/NanoQL.Tests/Vectors/CallableBondInputs.cs defines the same subclass.

#ifndef NANOQL_CXX_EXPERIMENTAL_CALLABLEBONDS_CALLABLEBONDSPEC_HPP
#define NANOQL_CXX_EXPERIMENTAL_CALLABLEBONDS_CALLABLEBONDSPEC_HPP

#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "instruments/bondspec.hpp"
#include "models/modelspec.hpp"
#include "timegrid.hpp"

#include <ql/experimental/callablebonds/callablebond.hpp>
#include <ql/experimental/callablebonds/callablebondconstantvol.hpp>
#include <ql/instruments/callabilityschedule.hpp>
#include <ql/pricingengine.hpp>

#include <optional>
#include <string>
#include <vector>

namespace harness::callablebonds {

    struct CallabilitySpec {
        bonds::PriceSpec price;
        QuantLib::Callability::Type type;
        DateSpec date;
    };

    struct CallableBondSpec {
        std::string type;  // CallableFixedRateBond or CallableZeroCouponBond
        QuantLib::Natural settlementDays = 0;
        QuantLib::Real faceAmount = 0.0, redemption = 100.0;
        bonds::ScheduleSpec schedule;
        std::vector<QuantLib::Real> coupons;
        std::optional<DayCounterSpec> dayCounter;  // the accrual or payment day counter
        QuantLib::BusinessDayConvention paymentConvention = QuantLib::Following;
        DateSpec issueDate{true, 0}, maturityDate{true, 0};
        std::optional<indexes::CalendarSpec> calendar;
        std::vector<CallabilitySpec> putCallSchedule;
        bool exCoupon = false;
        QuantLib::Integer exCouponLength = 0;
        QuantLib::TimeUnit exCouponUnits = QuantLib::Days;
        std::optional<indexes::CalendarSpec> exCouponCalendar;
        QuantLib::BusinessDayConvention exCouponConvention = QuantLib::Unadjusted;
        bool exCouponEndOfMonth = false;
    };

    struct ConstantVolSpec {
        bool harness = false;  // the harness's subclass
        QuantLib::Integer maxBondTenorLength = 0;
        QuantLib::TimeUnit maxBondTenorUnits = QuantLib::Years;
        QuantLib::Real minStrike = 0.0, maxStrike = 0.0;
        bool moving = false;
        DateSpec referenceDate{true, 0};
        QuantLib::Natural settlementDays = 0;
        indexes::CalendarSpec calendar;
        QuantLib::Real volatility = 0.0;
        DayCounterSpec dayCounter;
    };

    struct EngineSpec {
        std::string type;
        models::ModelSpec model;
        QuantLib::Size timeSteps = 0;
        TimeGridSpec grid;
        std::optional<models::CurveSpec> termStructure;
        QuantLib::Real volatility = 0.0;
        ConstantVolSpec structure;
        models::CurveSpec discountCurve;
    };

    // The check.
    CallabilitySpec callability(const json::value& v);
    CallableBondSpec bond(const json::value& v);
    ConstantVolSpec constantVol(const json::value& v);
    EngineSpec engine(const json::value& v);

    // The call.
    QuantLib::ext::shared_ptr<QuantLib::Callability> makeCallability(const CallabilitySpec& s);
    QuantLib::ext::shared_ptr<QuantLib::CallableBond> makeBond(const CallableBondSpec& s);
    QuantLib::ext::shared_ptr<QuantLib::CallableBondVolatilityStructure> makeVolatility(const ConstantVolSpec& s);
    QuantLib::ext::shared_ptr<QuantLib::PricingEngine> makeEngine(const EngineSpec& s);
    // The bond with the engine attached.
    QuantLib::ext::shared_ptr<QuantLib::CallableBond> makePricedBond(const CallableBondSpec& b, const EngineSpec& e);

}

#endif
