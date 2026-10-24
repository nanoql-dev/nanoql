// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The specs the CMS pricer and builder methods share (ql/cashflows/conundrumpricer.hpp, lineartsrpricer.hpp and
// ql/instruments/makecms.hpp), with the vectors' input names (tools/vectors/gen/cashflows/conundrumpricer.py's).
//
// A swap index is {"type": "EuriborSwapIsdaFixA", "tenor" ([length, units]), "forwarding"}, with "discounting" for
// exogenous discounting, each curve a spec of indexes/indexspec.hpp. A coupon is {"payment", "start", "end",
// "fixingDays", "index", "gearing", "spread", "dayCounter", "inArrears"}, a CmsCoupon of nominal 1 whose reference
// period is its accrual period. A pricer is {"type" ("NumericHaganPricer", "AnalyticHaganPricer" or
// "LinearTsrPricer"), "model" (the GFunctionFactory::YieldCurveModel's integer; null for the linear TSR pricer),
// "meanReversion", "volatility", "lowerLimit", "upperLimit", "precision" (the numeric pricer's; null for the others)},
// its volatility a matrix spec of termstructures/volatility/swaption/swaptionvolmatrixspec.hpp.

#ifndef NANOQL_CXX_CASHFLOWS_CMSSPEC_HPP
#define NANOQL_CXX_CASHFLOWS_CMSSPEC_HPP

#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "termstructures/volatility/optionlet/optionletvolspec.hpp"
#include "termstructures/volatility/swaption/swaptionvolmatrixspec.hpp"

#include <ql/cashflows/cmscoupon.hpp>
#include <ql/cashflows/couponpricer.hpp>
#include <ql/indexes/swapindex.hpp>

#include <optional>
#include <string>

namespace harness::cms {

    struct SwapIndexSpec {
        termstructures::PeriodSpec tenor{1, QuantLib::Years};
        indexes::CurveSpec forwarding;
        std::optional<indexes::CurveSpec> discounting;
    };

    struct CouponSpec {
        DateSpec payment{true, 0}, start{true, 0}, end{true, 0};
        QuantLib::Integer fixingDays = 2;
        SwapIndexSpec index;
        QuantLib::Real gearing = 1.0, spread = 0.0;
        DayCounterSpec dayCounter;
        bool inArrears = false;
    };

    struct PricerSpec {
        std::string type;
        QuantLib::Integer model = 0;
        QuantLib::Real meanReversion = 0.0;
        termstructures::SwaptionVolMatrixSpec volatility;
        QuantLib::Real lowerLimit = 0.0, upperLimit = 1.0, precision = 1.0e-6;
    };

    // The checks.
    SwapIndexSpec swapIndex(const json::value& v);
    CouponSpec coupon(const json::value& v);
    PricerSpec pricer(const json::value& v);

    // The calls.
    QuantLib::ext::shared_ptr<QuantLib::SwapIndex> makeSwapIndex(const SwapIndexSpec& s);
    QuantLib::ext::shared_ptr<QuantLib::CmsCoupon> makeCoupon(const CouponSpec& s);
    QuantLib::ext::shared_ptr<QuantLib::CmsCouponPricer> makePricer(const PricerSpec& s);

}

#endif
