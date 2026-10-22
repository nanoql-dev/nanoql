// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The inflation specs the methods of cashflows/, instruments/ and pricingengines/inflation/ share, with the vectors'
// input names (tools/vectors/gen/cashflows/indexedcashflow.py's and the generators importing it).
//
// An inflation curve is {"type": "ZeroInflationCurve" or "YoYInflationCurve", "referenceDate", "dates", "rates",
// "frequency", "dayCounter", "seasonality" (null), "enableExtrapolation"}: QuantLib's linear interpolated curve. An
// index is {"type": "UKRPI" or "YYUKRPI", "curve"} or {"type": "YoYRatio", "underlying": {"type": "UKRPI", "curve":
// null}, "curve"}, the curve null for an empty handle; its fixings are the case's settings', stored by name. A
// year-on-year coupon is {"paymentDate", "nominal", "startDate", "endDate", "fixingDays", "observationLag" ([length,
// units]), "interpolation", "dayCounter", "gearing", "spread"}, a CPI coupon {"baseCPI" (or null), "baseDate" (or
// null), "paymentDate", "nominal", "startDate", "endDate", "observationLag", "interpolation", "dayCounter",
// "fixedRate"}, built with QuantLib's constructor taking both bases. A year-on-year leg is
// tools/vectors/gen/cashflows/yoyinflationcoupon.py's leg spec. A pricer is {"type", "volatility", "nominalCurve"}: the
// type "CPICouponPricer", "YoYInflationCouponPricer", "BlackYoYInflationCouponPricer",
// "UnitDisplacedBlackYoYInflationCouponPricer" or "BachelierYoYInflationCouponPricer"; the volatility a
// ConstantCPIVolatility or ConstantYoYOptionletVolatility spec of
// termstructures/volatility/inflation/inflationvolspec.hpp, or null for the constructor taking the nominal curve
// alone; the nominal curve a curve spec of indexes/indexspec.hpp, or null for an empty handle. The enums are
// QuantLib's integers.

#ifndef NANOQL_CXX_CASHFLOWS_INFLATIONSPEC_HPP
#define NANOQL_CXX_CASHFLOWS_INFLATIONSPEC_HPP

#include "cashflows/couponspec.hpp"
#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "termstructures/volatility/inflation/inflationvolspec.hpp"

#include <ql/cashflow.hpp>
#include <ql/cashflows/cpicoupon.hpp>
#include <ql/cashflows/inflationcouponpricer.hpp>
#include <ql/cashflows/yoyinflationcoupon.hpp>
#include <ql/indexes/inflationindex.hpp>
#include <ql/shared_ptr.hpp>
#include <ql/time/frequency.hpp>

#include <string>
#include <vector>

namespace harness::cashflows {

    struct InflationCurveSpec {
        bool null = true;
        bool zero = true;
        DateSpec referenceDate{true, 0};
        std::vector<DateSpec> dates;
        std::vector<QuantLib::Real> rates;
        QuantLib::Frequency frequency = QuantLib::Monthly;
        DayCounterSpec dayCounter;
        bool enableExtrapolation = false;
    };
    InflationCurveSpec inflationCurve(const json::value& v);  // a curve spec, or null for an empty handle
    // A year-on-year curve's handle, empty for a null spec; a zero-inflation curve is the harness's refusal.
    QuantLib::Handle<QuantLib::YoYInflationTermStructure> makeYoYCurve(const InflationCurveSpec& s);

    struct InflationIndexSpec {
        std::string type;  // UKRPI, YYUKRPI or YoYRatio
        InflationCurveSpec curve;
    };
    InflationIndexSpec inflationIndex(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::ZeroInflationIndex> makeZeroIndex(const InflationIndexSpec& s);
    QuantLib::ext::shared_ptr<QuantLib::YoYInflationIndex> makeYoYIndex(const InflationIndexSpec& s);

    struct LagPeriod {
        QuantLib::Integer length = 0;
        QuantLib::TimeUnit units = QuantLib::Months;
    };
    LagPeriod lagPeriod(const json::value& v);  // [length, units]
    QuantLib::Period makePeriod(const LagPeriod& p);

    struct YoYCouponSpec {
        DateSpec paymentDate{true, 0}, startDate{true, 0}, endDate{true, 0};
        QuantLib::Real nominal = 0.0;
        QuantLib::Integer fixingDays = 0;
        LagPeriod lag;
        QuantLib::CPI::InterpolationType interpolation = QuantLib::CPI::Flat;
        DayCounterSpec dayCounter;
        QuantLib::Real gearing = 1.0, spread = 0.0;
    };
    YoYCouponSpec yoyCoupon(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::YoYInflationCoupon> makeYoYCoupon(
        const YoYCouponSpec& s, const QuantLib::ext::shared_ptr<QuantLib::YoYInflationIndex>& index);

    struct CPICouponSpec {
        QuantLib::Real baseCPI = 0.0;
        DateSpec baseDate{true, 0}, paymentDate{true, 0}, startDate{true, 0}, endDate{true, 0};
        QuantLib::Real nominal = 0.0;
        LagPeriod lag;
        QuantLib::CPI::InterpolationType interpolation = QuantLib::CPI::Flat;
        DayCounterSpec dayCounter;
        QuantLib::Real fixedRate = 0.0;
    };
    CPICouponSpec cpiCoupon(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::CPICoupon> makeCPICoupon(
        const CPICouponSpec& s, const QuantLib::ext::shared_ptr<QuantLib::ZeroInflationIndex>& index);

    struct YoYLegSpec {
        ScheduleSpec schedule;
        indexes::CalendarSpec paymentCalendar;
        LagPeriod lag;
        QuantLib::CPI::InterpolationType interpolation = QuantLib::CPI::Flat;
        std::vector<QuantLib::Real> notionals;
        DayCounterSpec paymentDayCounter;
        QuantLib::BusinessDayConvention paymentAdjustment = QuantLib::ModifiedFollowing;
        std::vector<QuantLib::Integer> fixingDays;
        std::vector<QuantLib::Real> gearings, spreads;
    };
    YoYLegSpec yoyLeg(const json::value& v);
    QuantLib::Leg makeYoYLeg(const YoYLegSpec& s, const QuantLib::ext::shared_ptr<QuantLib::YoYInflationIndex>& index);

    struct InflationPricerSpec {
        std::string type;
        bool hasVolatility = false;
        termstructures::InflationVolSpec volatility;
        indexes::CurveSpec nominalCurve;
    };
    InflationPricerSpec inflationPricer(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::InflationCouponPricer> makeInflationPricer(const InflationPricerSpec& s);

}

#endif
