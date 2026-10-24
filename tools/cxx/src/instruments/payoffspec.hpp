// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The payoff spec, shared by the units whose methods take a payoff (instruments/payoffs.cpp,
// pricingengines/blackformula.cpp): {"type", ...} with the constructor's arguments under QuantLib's names, the option
// type as Option::Type's integer and the position as Position::Type's, which QuantLib decides on.
//
//     NullPayoff               {}
//     FloatingTypePayoff       optionType
//     PlainVanillaPayoff       optionType, strike
//     PercentageStrikePayoff   optionType, moneyness
//     AssetOrNothingPayoff     optionType, strike
//     CashOrNothingPayoff      optionType, strike, cashPayoff
//     GapPayoff                optionType, strike, secondStrike
//     SuperFundPayoff          strike, secondStrike
//     SuperSharePayoff         strike, secondStrike, cashPayoff
//     ForwardTypePayoff        position, strike (ql/instruments/forward.hpp)
//
// Each is built inside the call, so that SuperFundPayoff's and SuperSharePayoff's refusals are QuantLib's.

#ifndef NANOQL_CXX_INSTRUMENTS_PAYOFFSPEC_HPP
#define NANOQL_CXX_INSTRUMENTS_PAYOFFSPEC_HPP

#include "convert.hpp"

#include <ql/instruments/payoffs.hpp>
#include <ql/position.hpp>

#include <string>

namespace harness::payoffs {

    struct PayoffSpec {
        std::string type;
        QuantLib::Option::Type optionType;
        QuantLib::Real strike, secondStrike, cashPayoff;
        QuantLib::Position::Type position;
    };

    // The check: any payoff, or a PlainVanillaPayoff only, as the Black formulas take it.
    PayoffSpec payoff(const json::value& v);
    PayoffSpec plainVanillaPayoff(const json::value& v);

    // The call.
    QuantLib::ext::shared_ptr<QuantLib::Payoff> makePayoff(const PayoffSpec& s);
    QuantLib::ext::shared_ptr<QuantLib::PlainVanillaPayoff> makePlainVanillaPayoff(const PayoffSpec& s);

}

#endif
