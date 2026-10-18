// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The rate-helper and curve specs the bootstrap methods share (ql/termstructures/yield/ratehelpers.hpp and
// ql/termstructures/yield/discountcurve.hpp), with the vectors' input names (tools/vectors/gen/termstructures/yield/
// ratehelpers.py's functions, as tests/NanoQL.Tests/Vectors/RateHelperInputs.cs documents them).
//
// A helper is {"kind": "DepositRateHelper", "form": "tenor", "rate", "tenor", "fixingDays", "calendar", "convention",
// "endOfMonth", "dayCounter"}, {"kind": "DepositRateHelper", "form": "index", "rate", "index"}, {"kind":
// "DepositRateHelper", "form": "fixingDate", "rate", "fixingDate", "index"}, or {"kind": "SwapRateHelper", "form":
// "tenor", "rate", "tenor", "calendar", "fixedFrequency", "fixedConvention", "fixedDayCount", "index", "spread",
// "fwdStart", "discountingCurve", "settlementDays", "pillar", "customPillarDate", "endOfMonth", "useIndexedCoupons",
// "floatConvention"}, the same with "form": "dates", "startDate" and "endDate" for "tenor", and without "fwdStart" and
// "settlementDays"; a swap helper may add "couponPricer", a pricer spec of cashflows/couponspec.hpp. A period is
// [length, units]; a calendar is a spec of indexes/indexspec.hpp and a day counter one of convert.hpp, each null for
// QuantLib's empty one in a swap helper; the discounting curve is null or a flat forward spec of indexes/indexspec.hpp;
// null settlement days are Null<Natural>(), a null custom pillar date Date(), and a null coupon choice or floating
// convention std::nullopt. The quote and the spread are values: a zero spread is QuantLib's empty handle, which it
// reads as 0.
//
// An index is a preset, {"type": "Euribor" | "Euribor365" | "Tibor", "tenor", "curve"} or {"type": "Euribor1W" |
// "Euribor1M" | "Euribor3M" | "Euribor6M" | "Euribor1Y", "curve"}; an IborIndex spec of indexes/indexspec.hpp; or the
// same with a "currency" code, "EUR" or "USD", built with that currency.
//
// A curve a helper is bound to is a flat forward spec of indexes/indexspec.hpp, or {"type": "DiscountCurve", "dates",
// "discounts", "dayCounter"}, an InterpolatedDiscountCurve<LogLinear>.

#ifndef NANOQL_CXX_TERMSTRUCTURES_YIELD_RATEHELPERSPEC_HPP
#define NANOQL_CXX_TERMSTRUCTURES_YIELD_RATEHELPERSPEC_HPP

#include "cashflows/couponspec.hpp"
#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/termstructures/yield/ratehelpers.hpp>
#include <ql/termstructures/yieldtermstructure.hpp>

#include <optional>
#include <string>
#include <utility>
#include <vector>

namespace harness::termstructures {

    struct HelperIndexSpec {
        std::string preset;  // a preset's class, or empty for an IborIndex spec
        std::pair<QuantLib::Integer, QuantLib::TimeUnit> tenor{0, QuantLib::Days};
        indexes::CurveSpec curve;
        indexes::IndexSpec index;
        std::string currency;  // empty for QuantLib's empty currency
    };
    HelperIndexSpec helperIndex(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::IborIndex> makeHelperIndex(const HelperIndexSpec& s);

    struct RateHelperSpec {
        std::string kind, form;
        QuantLib::Real rate = 0.0;
        std::pair<QuantLib::Integer, QuantLib::TimeUnit> tenor{0, QuantLib::Days};
        QuantLib::Natural fixingDays = 0;
        bool emptyCalendar = false;
        indexes::CalendarSpec calendar;
        QuantLib::BusinessDayConvention convention = QuantLib::Following;
        bool endOfMonth = false;
        bool emptyDayCounter = false;
        DayCounterSpec dayCounter;
        DateSpec fixingDate{true, 0}, startDate{true, 0}, endDate{true, 0};
        HelperIndexSpec index;
        QuantLib::Frequency fixedFrequency = QuantLib::Annual;
        QuantLib::Spread spread = 0.0;
        std::pair<QuantLib::Integer, QuantLib::TimeUnit> fwdStart{0, QuantLib::Days};
        indexes::CurveSpec discountingCurve;
        QuantLib::Natural settlementDays = 0;  // Null<Natural>() for null
        QuantLib::Pillar::Choice pillar = QuantLib::Pillar::LastRelevantDate;
        DateSpec customPillarDate{true, 0};
        std::optional<bool> useIndexedCoupons;
        std::optional<QuantLib::BusinessDayConvention> floatConvention;
        cashflows::PricerSpec couponPricer;
    };
    RateHelperSpec rateHelper(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::RateHelper> makeRateHelper(const RateHelperSpec& s);

    struct BoundCurveSpec {
        bool interpolated = false;
        indexes::CurveSpec flat;
        std::vector<DateSpec> dates;
        std::vector<QuantLib::Real> discounts;
        DayCounterSpec dayCounter;
    };
    BoundCurveSpec boundCurve(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::YieldTermStructure> makeBoundCurve(const BoundCurveSpec& s);

}

#endif
