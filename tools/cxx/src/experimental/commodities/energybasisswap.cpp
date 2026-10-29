// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/commodities/energybasisswap.hpp, .cpp: an energy basis swap's calculation.
// "EnergyBasisSwap::calculate" takes (swap): {"calendar", "spreadIndex", "payIndex", "receiveIndex",
// "spreadToPayLeg", "payCurrency", "receiveCurrency", "periods", "basis", "commodityType", "secondaryCosts",
// "payLegCurve", "receiveLegCurve", "discountCurve"}, the specs of commodityspec.hpp, the spread index null or an
// index, the curves indexes::curve's; the case's fixings are the Settings call's. It returns
// commodities::swapResult's, or {"throws": true}. The generator prices on the base unit and currency, the basis in
// the base currency (the S-xrt plan's XRR5 (i) and (ix) leave others out).

#include "experimental/commodities/commodityspec.hpp"

#include <ql/experimental/commodities/energybasisswap.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"EnergyBasisSwap::calculate",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 const json::value& v = a[0];
                 if (!v.is_object() || v.as_object().size() != 14)
                     throw InvalidParams("expected an energy basis swap's 14 terms");
                 indexes::CalendarSpec calendar = indexes::calendar(v.at("calendar"));
                 std::vector<commodities::IndexSpec> spread;
                 if (!v.at("spreadIndex").is_null())
                     spread.push_back(commodities::index(v.at("spreadIndex")));
                 commodities::IndexSpec payIndex = commodities::index(v.at("payIndex"));
                 commodities::IndexSpec receiveIndex = commodities::index(v.at("receiveIndex"));
                 const bool spreadToPayLeg = boolean(v.at("spreadToPayLeg"));
                 const std::string payCurrency = commodities::currency(v.at("payCurrency"));
                 const std::string receiveCurrency = commodities::currency(v.at("receiveCurrency"));
                 std::vector<commodities::PeriodSpec> periods = commodities::periods(v.at("periods"));
                 commodities::UnitCostSpec basis = commodities::unitCost(v.at("basis"));
                 commodities::CommodityTypeSpec type = commodities::commodityType(v.at("commodityType"));
                 auto costs = commodities::secondaryCosts(v.at("secondaryCosts"));
                 indexes::CurveSpec payCurve = indexes::curve(v.at("payLegCurve"));
                 indexes::CurveSpec receiveCurve = indexes::curve(v.at("receiveLegCurve"));
                 indexes::CurveSpec discountCurve = indexes::curve(v.at("discountCurve"));
                 return [=] {
                     commodities::resetGlobals();
                     EnergyBasisSwap swap(
                         indexes::makeCalendar(calendar),
                         spread.empty() ? nullptr : commodities::makeIndex(spread.front()),
                         commodities::makeIndex(payIndex), commodities::makeIndex(receiveIndex), spreadToPayLeg,
                         commodities::makeCurrency(payCurrency), commodities::makeCurrency(receiveCurrency),
                         commodities::makePeriods(periods), commodities::makeUnitCost(basis),
                         commodities::makeCommodityType(type), commodities::makeSecondaryCosts(costs),
                         indexes::makeCurve(payCurve), indexes::makeCurve(receiveCurve),
                         indexes::makeCurve(discountCurve));
                     return commodities::swapResult(swap);
                 };
             }},
        });

    }

}
