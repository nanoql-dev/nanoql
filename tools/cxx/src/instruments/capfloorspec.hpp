// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The cap and floor spec the cap methods share (ql/instruments/capfloor.hpp), with the vectors' input names
// (tools/vectors/gen/instruments/capfloor.py's functions, as tests/NanoQL.Tests/Vectors/CapFloorInputs.cs documents
// them).
//
// An instrument is {"type", "leg", "legType", "capRates", "floorRates"}: the type QuantLib's integer, the leg an IborLeg
// spec of cashflows/couponspec.hpp ("legType" "IborLeg") or null for QuantLib's empty Leg() ("Empty"), built with
// CapFloor's four-argument constructor, as QuantLib's Cap, Floor and Collar build it; or the same with "strikes" in
// place of "capRates" and "floorRates", built with the three-argument constructor. The constructor refuses what
// QuantLib refuses, inside the call.

#ifndef NANOQL_CXX_INSTRUMENTS_CAPFLOORSPEC_HPP
#define NANOQL_CXX_INSTRUMENTS_CAPFLOORSPEC_HPP

#include "cashflows/couponspec.hpp"
#include "convert.hpp"

#include <ql/instruments/capfloor.hpp>
#include <ql/shared_ptr.hpp>

#include <vector>

namespace harness::capfloors {

    struct CapFloorSpec {
        QuantLib::CapFloor::Type type = QuantLib::CapFloor::Cap;
        bool empty = false;  // QuantLib's empty Leg()
        cashflows::LegSpec leg;
        bool strikesForm = false;  // the three-argument constructor
        std::vector<QuantLib::Rate> capRates, floorRates, strikes;
    };

    // The check.
    CapFloorSpec capFloor(const json::value& v);

    // The call.
    QuantLib::ext::shared_ptr<QuantLib::CapFloor> makeCapFloor(const CapFloorSpec& s);

    // A built instrument's inspectors that never throw: {"type", "capRates", "floorRates", "legSize"}.
    json::value inspectors(const QuantLib::CapFloor& c);

}

#endif
