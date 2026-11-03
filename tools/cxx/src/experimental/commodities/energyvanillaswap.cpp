// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/commodities/energyvanillaswap.hpp, .cpp: a vanilla energy swap's calculation.
// "EnergyVanillaSwap::calculate" takes (swap): {"payer", "calendar", "fixedPrice" ({"amount", "currency"}),
// "fixedPriceUnit", "index", "payCurrency", "receiveCurrency", "periods", "commodityType", "secondaryCosts",
// "payLegCurve", "receiveLegCurve", "discountCurve"}, the specs of commodityspec.hpp, the curves indexes::curve's;
// the case's fixings are the Settings call's. It returns commodities::swapResult's, or {"throws": true}. The
// generator prices on the base unit and currency, where QuantLib's unit factors are 1.

#include "experimental/commodities/commodityspec.hpp"

#include <ql/experimental/commodities/energyvanillaswap.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"EnergyVanillaSwap::calculate",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 const json::value& v = a[0];
                 if (!v.is_object() || v.as_object().size() != 13)
                     throw InvalidParams("expected a vanilla energy swap's 13 terms");
                 const bool payer = boolean(v.at("payer"));
                 indexes::CalendarSpec calendar = indexes::calendar(v.at("calendar"));
                 const Real fixedAmount = real(v.at("fixedPrice").at("amount"));
                 const std::string fixedCurrency = commodities::currency(v.at("fixedPrice").at("currency"));
                 const std::string fixedUnit = commodities::unit(v.at("fixedPriceUnit"));
                 commodities::IndexSpec index = commodities::index(v.at("index"));
                 const std::string payCurrency = commodities::currency(v.at("payCurrency"));
                 const std::string receiveCurrency = commodities::currency(v.at("receiveCurrency"));
                 std::vector<commodities::PeriodSpec> periods = commodities::periods(v.at("periods"));
                 commodities::CommodityTypeSpec type = commodities::commodityType(v.at("commodityType"));
                 auto costs = commodities::secondaryCosts(v.at("secondaryCosts"));
                 indexes::CurveSpec payCurve = indexes::curve(v.at("payLegCurve"));
                 indexes::CurveSpec receiveCurve = indexes::curve(v.at("receiveLegCurve"));
                 indexes::CurveSpec discountCurve = indexes::curve(v.at("discountCurve"));
                 return [=] {
                     commodities::resetGlobals();
                     EnergyVanillaSwap swap(payer, indexes::makeCalendar(calendar),
                                            Money(commodities::makeCurrency(fixedCurrency), fixedAmount),
                                            commodities::makeUnit(fixedUnit), commodities::makeIndex(index),
                                            commodities::makeCurrency(payCurrency),
                                            commodities::makeCurrency(receiveCurrency),
                                            commodities::makePeriods(periods), commodities::makeCommodityType(type),
                                            commodities::makeSecondaryCosts(costs), indexes::makeCurve(payCurve),
                                            indexes::makeCurve(receiveCurve), indexes::makeCurve(discountCurve));
                     json::value out = commodities::swapResult(swap);
                     out.as_object()["payReceive"] = swap.payReceive();
                     return out;
                 };
             }},
        });

    }

}
