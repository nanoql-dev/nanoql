// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The stripper spec the optionlet stripping methods share (ql/termstructures/volatility/optionlet/), with the vectors'
// input names (tools/vectors/gen/cxx/termstructures/volatility/optionlet/optionletstripper.py's functions).
//
// A stripper is {"surface" (a surface of capfloor/capfloorvolspec.hpp), "index" (an Ibor or overnight index of
// indexes/indexspec.hpp, its forwarding curve inside), "discount" (null, QuantLib's empty handle, or a curve of
// indexes/indexspec.hpp), "switchStrike" (null for Null<Rate>()), "accuracy", "maxIter", "type", "displacement",
// "dontThrow", "optionletFrequency" (null for std::nullopt, or [length, units]), "paymentLag"}: an OptionletStripper1
// with every argument given. A count is a Natural, so a negative one is the harness's refusal.

#ifndef NANOQL_CXX_TERMSTRUCTURES_VOLATILITY_OPTIONLET_STRIPPERSPEC_HPP
#define NANOQL_CXX_TERMSTRUCTURES_VOLATILITY_OPTIONLET_STRIPPERSPEC_HPP

#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "termstructures/volatility/capfloor/capfloorvolspec.hpp"

#include <ql/shared_ptr.hpp>
#include <ql/termstructures/volatility/optionlet/optionletstripper1.hpp>
#include <ql/termstructures/volatility/volatilitytype.hpp>

#include <optional>
#include <utility>

namespace harness::strippers {

    struct StripperSpec {
        capfloorvols::SurfaceSpec surface;
        indexes::IndexSpec index;
        indexes::CurveSpec discount;  // null for QuantLib's empty handle
        QuantLib::Rate switchStrike = QuantLib::Null<QuantLib::Rate>();
        QuantLib::Real accuracy = 1.0e-6;
        QuantLib::Natural maxIter = 100;
        QuantLib::VolatilityType type = QuantLib::ShiftedLognormal;
        QuantLib::Real displacement = 0.0;
        bool dontThrow = false;
        std::optional<std::pair<QuantLib::Integer, QuantLib::TimeUnit>> optionletFrequency;
        QuantLib::Natural paymentLag = 0;
    };

    // The check.
    StripperSpec stripper(const json::value& v);

    // The call.
    QuantLib::ext::shared_ptr<QuantLib::OptionletStripper1> makeStripper(const StripperSpec& s);

}

#endif
