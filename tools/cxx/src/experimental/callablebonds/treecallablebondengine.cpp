// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/callablebonds/treecallablebondengine.hpp, .cpp: the tree engines with a spread, which only
// CallableBond's NPVSpreadHelper sets, on a callable bond spec and a tree engine spec of callablebondspec.hpp
// ("TreeSteps", "TreeGrid", "TreeZeroSteps" or "TreeZeroGrid"), each built inside the call. The bond's arguments are
// set up as NPVSpreadHelper sets them up, the spread written into them, and the engine run directly, as it runs it.
//
//     TreeCallableFixedRateBondEngine::calculateWithSpread
//                                                  (bond, engine, spread): {"value", "settlementValue"}

#include "convert.hpp"
#include "experimental/callablebonds/callablebondspec.hpp"

#include <ql/experimental/callablebonds/callablebond.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"TreeCallableFixedRateBondEngine::calculateWithSpread",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 callablebonds::CallableBondSpec s = callablebonds::bond(a[0]);
                 callablebonds::EngineSpec e = callablebonds::engine(a[1]);
                 if (e.type.rfind("Tree", 0) != 0)
                     throw InvalidParams("expected a tree engine, got " + e.type);
                 Real spread = real(a[2]);
                 return [=] {
                     auto bond = callablebonds::makeBond(s);
                     auto engine = callablebonds::makeEngine(e);
                     bond->setupArguments(engine->getArguments());
                     auto* args = dynamic_cast<CallableBond::arguments*>(engine->getArguments());
                     if (args == nullptr)
                         throw InvalidParams("the engine takes no callable bond's arguments");
                     args->spread = spread;
                     engine->calculate();
                     const auto* results = dynamic_cast<const CallableBond::results*>(engine->getResults());
                     if (results == nullptr)
                         throw InvalidParams("the engine gives no callable bond's results");
                     return json::value(json::object{{"value", results->value},
                                                     {"settlementValue", results->settlementValue}});
                 };
             }},
        });

    }

}
