// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The swaption and engine specs the swaption methods share (ql/instruments/swaption.hpp and
// ql/pricingengines/swaption/blackswaptionengine.hpp), with the vectors' input names (tools/vectors/gen/instruments/
// swaption.py's functions, as tests/NanoQL.Tests/Vectors/SwaptionInputs.cs documents them).
//
// A swaption is {"swap", "exercise", "settlementType", "settlementMethod"}, the settlement type and method as
// QuantLib's integers. Its swap is {"kind": "VanillaSwap", "type", "nominal", "fixedSchedule", "fixedRate",
// "fixedDayCount", "floatSchedule", "index", "spread", "floatingDayCount", "paymentConvention", "useIndexedCoupons"},
// built with VanillaSwap's constructor (a null payment convention or coupon choice std::nullopt), its schedules
// rule-based specs of cashflows/couponspec.hpp and its index an IborIndex spec of indexes/indexspec.hpp; or an
// overnight-indexed swap spec of instruments/swapspec.hpp. Its exercise is {"type": "EuropeanExercise", "date"} or
// {"type": "BermudanExercise", "dates", "payoffAtExpiry"}.
//
// An engine is {"type": "BlackSwaptionEngine", "form", "vol", "dayCounter", "displacement", "model"} or
// {"type": "BachelierSwaptionEngine", "form", "vol", "dayCounter", "model"}, its form "quote" for the constructor on a
// quote handle (the wheel's) or "volatility" for the constructor on a Volatility (which the wheel doesn't bind), the
// model as CashAnnuityModel's integer; a discount curve is a curve spec of indexes/indexspec.hpp.

#ifndef NANOQL_CXX_INSTRUMENTS_SWAPTIONSPEC_HPP
#define NANOQL_CXX_INSTRUMENTS_SWAPTIONSPEC_HPP

#include "cashflows/couponspec.hpp"
#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "instruments/swapspec.hpp"

#include <ql/instruments/swaption.hpp>
#include <ql/pricingengine.hpp>

#include <optional>
#include <string>
#include <vector>

namespace harness::swaptions {

    struct VanillaSpec {
        QuantLib::Swap::Type type = QuantLib::Swap::Payer;
        QuantLib::Real nominal = 0.0;
        cashflows::ScheduleSpec fixedSchedule;
        QuantLib::Rate fixedRate = 0.0;
        DayCounterSpec fixedDayCount;
        cashflows::ScheduleSpec floatSchedule;
        indexes::IndexSpec index;
        QuantLib::Spread spread = 0.0;
        DayCounterSpec floatingDayCount;
        std::optional<QuantLib::BusinessDayConvention> paymentConvention;
        std::optional<bool> useIndexedCoupons;
    };

    struct SwaptionSpec {
        bool overnight = false;
        VanillaSpec vanilla;
        swaps::OisSpec ois;
        bool bermudan = false;
        std::vector<DateSpec> exerciseDates;
        bool payoffAtExpiry = false;
        QuantLib::Settlement::Type settlementType = QuantLib::Settlement::Physical;
        QuantLib::Settlement::Method settlementMethod = QuantLib::Settlement::PhysicalOTC;
    };

    struct EngineSpec {
        bool black = true;     // BlackSwaptionEngine, or BachelierSwaptionEngine
        bool quote = true;     // on a quote handle, or on a Volatility
        QuantLib::Volatility vol = 0.0;
        DayCounterSpec dayCounter;
        QuantLib::Real displacement = 0.0;
        int model = 1;         // CashAnnuityModel's integer
    };

    // The check.
    SwaptionSpec swaption(const json::value& v);
    EngineSpec engine(const json::value& v);

    // The call.
    QuantLib::ext::shared_ptr<QuantLib::Swaption> makeSwaption(const SwaptionSpec& s);
    QuantLib::ext::shared_ptr<QuantLib::PricingEngine> makeEngine(const EngineSpec& e, const indexes::CurveSpec& curve);

}

#endif
