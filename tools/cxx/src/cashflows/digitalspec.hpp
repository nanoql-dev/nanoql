// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The digital coupon specs and outputs the digital coupon and leg methods share (ql/cashflows/digitalcoupon.hpp,
// digitalcmscoupon.hpp, digitaliborcoupon.hpp and ql/experimental/coupons/digitalcmsspreadcoupon.hpp), with the
// vectors' input names (tools/vectors/gen/cxx/cashflows/digitalcoupon.py's).
//
// A replication is null, QuantLib's default, or {"type" (Replication::Type's integer), "gap"}. A digital coupon's terms
// are {"callStrike", "callPosition", "isCallATMIncluded", "callDigitalPayoff", "putStrike", "putPosition",
// "isPutATMIncluded", "putDigitalPayoff", "replication", "nakedOption"}, a null strike or payoff Null<Rate>() and a
// position Position::Type's integer. A leg's digital settings are {"callStrikes", "longCallOption", "callATM",
// "callPayoffs", "putStrikes", "longPutOption", "putATM", "putPayoffs", "replication", "nakedOption"}, the strikes and
// payoffs lists whose null elements are Null<Rate>(); the leg's builder always sets the naked option flag, which
// QuantLib's leaves uninitialized otherwise (the S-cms plan's CMR5 (viii)).
//
// A digital coupon's inspectors are {"callStrike", "putStrike", "callDigitalPayoff", "putDigitalPayoff" (null for
// Null<Rate>()), "hasCall", "hasPut", "hasCollar", "isLongCall", "isLongPut", "date", "accrualStartDate",
// "accrualEndDate", "fixingDate", "nominal", "gearing", "spread"}; its rates {"rate", "callOptionRate",
// "putOptionRate"}, each {"throws": true} where it throws.

#ifndef NANOQL_CXX_CASHFLOWS_DIGITALSPEC_HPP
#define NANOQL_CXX_CASHFLOWS_DIGITALSPEC_HPP

#include "convert.hpp"

#include <ql/cashflows/digitalcoupon.hpp>
#include <ql/cashflows/replication.hpp>
#include <ql/position.hpp>

#include <vector>

namespace harness::digital {

    struct ReplicationSpec {
        bool null = true;
        QuantLib::Replication::Type type = QuantLib::Replication::Central;
        QuantLib::Real gap = 1e-4;
    };

    struct TermsSpec {
        QuantLib::Real callStrike = 0.0, callDigitalPayoff = 0.0, putStrike = 0.0, putDigitalPayoff = 0.0;
        QuantLib::Position::Type callPosition = QuantLib::Position::Long, putPosition = QuantLib::Position::Long;
        bool isCallATMIncluded = false, isPutATMIncluded = false, nakedOption = false;
        ReplicationSpec replication;
    };

    struct LegTermsSpec {
        std::vector<QuantLib::Real> callStrikes, callPayoffs, putStrikes, putPayoffs;
        QuantLib::Position::Type longCallOption = QuantLib::Position::Long, longPutOption = QuantLib::Position::Long;
        bool callATM = false, putATM = false, nakedOption = false;
        ReplicationSpec replication;
    };

    // The checks.
    ReplicationSpec replication(const json::value& v);
    TermsSpec terms(const json::value& v);
    LegTermsSpec legTerms(const json::object& o, const std::string& what);

    // The calls.
    QuantLib::ext::shared_ptr<QuantLib::DigitalReplication> makeReplication(const ReplicationSpec& s);
    json::value inspectors(const QuantLib::DigitalCoupon& c);
    json::value rates(const QuantLib::DigitalCoupon& c);

    // A leg's coupons: each digital coupon's inspectors and, with rates, its rates; a fixed coupon {"digital": false,
    // "date", "rate"}.
    json::value legOutputs(const QuantLib::Leg& leg, bool withRates);

}

#endif
