// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The smile section spec the section methods share (ql/termstructures/volatility/smilesection.hpp,
// flatsmilesection.hpp and sabrsmilesection.hpp), with the vectors' input names (tools/vectors/gen/termstructures/
// volatility/flatsmilesection.py's and sabrsmilesection.py's functions), and the summary the methods returning a
// section give.
//
// A section is {"type": "FlatSmileSection", "form", "volatility", "dayCounter", "atmLevel", "volatilityType",
// "shift"}, with the form's own keys: "date" takes "exerciseDate" and "referenceDate" (QuantLib's date constructor
// with a reference date), "floating" takes "exerciseDate" (the date constructor without one, whose reference date is
// the evaluation date the case's settings set), and "time" takes "exerciseTime". The day counter is null for
// QuantLib's empty DayCounter(), and the ATM level null for Null<Real>().
//
// Or {"type": "HarnessSmileSection", "form" ("date" or "time", with the same keys), "dayCounter", "volatilityType",
// "shift", "smile" ([a, b, c]), "atmLevel", "minStrike", "maxStrike", "overriddenType", "overriddenShift"}: the
// harness's own section, which overrides only what SmileSection leaves abstract, with the volatility a + b k + c k^2 at
// a strike k, so that the base's variance, prices, vega, density and converted volatilities are its defaults; where
// "overriddenType" or "overriddenShift" isn't null it also overrides volatilityType() or shift() to return it, so that
// the type and shift the section was built with differ from those it reports. tests/NanoQL.Tests/Vectors/
// SmileSectionInputs.cs defines the same section.
//
// Or {"type": "SabrSmileSection", "form" ("date", "floating" or "time", with the same keys), "forward",
// "sabrParameters" (alpha, beta, nu, rho; at least four, since QuantLib reads four unchecked), "dayCounter" (read by
// the date forms only), "volatilityType", "shift"}: QuantLib's SabrSmileSection, which has no ATM level of its own to
// give (its forward is its ATM level).

#ifndef NANOQL_CXX_TERMSTRUCTURES_VOLATILITY_SMILESECTIONSPEC_HPP
#define NANOQL_CXX_TERMSTRUCTURES_VOLATILITY_SMILESECTIONSPEC_HPP

#include "convert.hpp"

#include <ql/shared_ptr.hpp>
#include <ql/termstructures/volatility/smilesection.hpp>
#include <ql/termstructures/volatility/volatilitytype.hpp>

#include <optional>
#include <string>
#include <vector>

namespace harness::termstructures {

    struct SmileSectionSpec {
        std::string type;  // FlatSmileSection, HarnessSmileSection or SabrSmileSection
        std::string form;  // date, floating or time
        DateSpec exerciseDate{true, 0}, referenceDate{true, 0};
        QuantLib::Real exerciseTime = 0.0;
        bool hasDayCounter = false;  // false for QuantLib's empty DayCounter()
        DayCounterSpec dayCounter;
        QuantLib::Real volatility = 0.0;  // FlatSmileSection's
        QuantLib::Real a = 0.0, b = 0.0, c = 0.0;  // HarnessSmileSection's smile
        QuantLib::Real atmLevel = 0.0;  // Null<Real>() for none
        QuantLib::Real forward = 0.0;  // SabrSmileSection's
        std::vector<QuantLib::Real> sabrParameters;  // SabrSmileSection's
        QuantLib::Real minStrike = 0.0, maxStrike = 0.0;
        QuantLib::VolatilityType volatilityType = QuantLib::ShiftedLognormal;
        QuantLib::Real shift = 0.0;
        std::optional<QuantLib::VolatilityType> overriddenType;
        std::optional<QuantLib::Real> overriddenShift;
    };

    // The check.
    SmileSectionSpec smileSection(const json::value& v);

    // The call.
    QuantLib::ext::shared_ptr<QuantLib::SmileSection> makeSmileSection(const SmileSectionSpec& s);

    // A section's summary, as the methods returning a section give it: [exerciseDate, exerciseTime, volatilityType,
    // shift, atmLevel, volatility(strike)], the date as a serial or null, the type as its integer and the ATM level
    // null for Null<Real>().
    json::value sectionSummary(const QuantLib::SmileSection& section, QuantLib::Real strike);

    // A real result, null for Null<Real>().
    json::value nullableReal(QuantLib::Real x);

}

#endif
