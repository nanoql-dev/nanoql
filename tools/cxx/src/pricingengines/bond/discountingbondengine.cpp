// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/bond/discountingbondengine.hpp: the engine's includeSettlementDateFlows, which the wheel can't
// pass, on bond specs of instruments/bondspec.hpp priced on a curve spec of indexes/indexspec.hpp, each built inside
// the call; the results are read through the bond, as QuantLib's instrument gives them.
//
//     DiscountingBondEngine::NPV, errorEstimate, valuationDate, settlementValue
//                                                  (bond, curve, includeSettlementDateFlows): the result (null for
//                                                  std::nullopt), or QuantLib's "... not provided"

#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "instruments/bondspec.hpp"

#include <ql/instruments/bond.hpp>

#include <optional>

using namespace QuantLib;

namespace harness {

    namespace {

        // A result of the bond priced on the curve: f(bond).
        template <class F>
        Call priced(const Args& a, F f) {
            arity(a, 3);
            bonds::BondSpec s = bonds::bond(a[0]);
            indexes::CurveSpec curve = indexes::curve(a[1]);
            std::optional<bool> include = optionalBoolean(a[2]);
            return [=] { return json::value(f(*bonds::makePricedBond(s, curve, include))); };
        }

        const Register unit({
            {"DiscountingBondEngine::NPV",
             [](const Args& a) { return priced(a, [](const Bond& b) { return b.NPV(); }); }},
            {"DiscountingBondEngine::errorEstimate",
             [](const Args& a) { return priced(a, [](const Bond& b) { return b.errorEstimate(); }); }},
            {"DiscountingBondEngine::valuationDate",
             [](const Args& a) { return priced(a, [](const Bond& b) { return result(b.valuationDate()); }); }},
            {"DiscountingBondEngine::settlementValue",
             [](const Args& a) { return priced(a, [](const Bond& b) { return b.settlementValue(); }); }},
        });

    }

}
