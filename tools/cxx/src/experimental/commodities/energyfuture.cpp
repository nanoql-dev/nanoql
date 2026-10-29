// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/commodities/energyfuture.hpp, .cpp: an energy future's calculation.
// "EnergyFuture::calculate" takes (future): {"buySell", "quantity", "tradePrice", "index", "commodityType",
// "secondaryCosts"}, the specs of commodityspec.hpp, the trade price a unit cost; the case's fixings are the Settings
// call's. It returns {"NPV", "errors", "costs", "isExpired", "quantity"}, or {"throws": true}. The generator prices
// on the base unit and currency at one lot, its quote a fixing on the evaluation date or the forward curve (the S-xrt
// plan's XRR5 (vii), (xxxix) and (xl) leave the rest out).

#include "experimental/commodities/commodityspec.hpp"

#include <ql/experimental/commodities/energyfuture.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"EnergyFuture::calculate",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 const json::value& v = a[0];
                 if (!v.is_object() || v.as_object().size() != 6)
                     throw InvalidParams("expected an energy future's 6 terms");
                 const Integer buySell = integer(v.at("buySell"));
                 commodities::QuantitySpec q = commodities::quantity(v.at("quantity"));
                 commodities::UnitCostSpec tradePrice = commodities::unitCost(v.at("tradePrice"));
                 commodities::IndexSpec index = commodities::index(v.at("index"));
                 commodities::CommodityTypeSpec type = commodities::commodityType(v.at("commodityType"));
                 auto costs = commodities::secondaryCosts(v.at("secondaryCosts"));
                 return [=] {
                     commodities::resetGlobals();
                     EnergyFuture future(buySell, commodities::makeQuantity(q), commodities::makeUnitCost(tradePrice),
                                         commodities::makeIndex(index), commodities::makeCommodityType(type),
                                         commodities::makeSecondaryCosts(costs));
                     json::object o;
                     o["NPV"] = future.NPV();
                     o["isExpired"] = future.isExpired();
                     o["errors"] = commodities::errorsResult(future.pricingErrors());
                     o["costs"] = commodities::costsResult(future.secondaryCostAmounts());
                     o["quantity"] = commodities::quantityResult(future.quantity());
                     return json::value(o);
                 };
             }},
        });

    }

}
