// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/callablebonds/blackcallablebondengine.hpp, .cpp: the Black engines the wheel can't build, the
// fixed-rate engine on a volatility structure and the zero-coupon engine on either, on a callable bond spec and a Black
// engine spec of callablebondspec.hpp ("Black", "BlackZero", "BlackStructure" or "BlackZeroStructure"), each built
// inside the call. The bond is priced as QuantLib's instrument prices it, through its engine.
//
//     BlackCallableFixedRateBondEngine::NPV, settlementValue
//                                                  (bond, engine): the instrument's result

#include "convert.hpp"
#include "experimental/callablebonds/callablebondspec.hpp"

#include <ql/experimental/callablebonds/callablebond.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        // A result of the bond priced by a Black engine: f(bond).
        template <class F>
        Call priced(const Args& a, F f) {
            arity(a, 2);
            callablebonds::CallableBondSpec s = callablebonds::bond(a[0]);
            callablebonds::EngineSpec e = callablebonds::engine(a[1]);
            if (e.type.rfind("Black", 0) != 0)
                throw InvalidParams("expected a Black engine, got " + e.type);
            return [=] { return json::value(f(*callablebonds::makePricedBond(s, e))); };
        }

        const Register unit({
            {"BlackCallableFixedRateBondEngine::NPV",
             [](const Args& a) { return priced(a, [](const CallableBond& b) { return b.NPV(); }); }},
            {"BlackCallableFixedRateBondEngine::settlementValue",
             [](const Args& a) { return priced(a, [](const CallableBond& b) { return b.settlementValue(); }); }},
        });

    }

}
