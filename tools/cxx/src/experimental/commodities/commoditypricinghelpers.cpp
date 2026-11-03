// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/commodities/commoditypricinghelpers.hpp, .cpp: monthly pricing periods and unit costs.
// "CommodityPricingHelper::createPricingPeriods" takes (startDate, endDate, quantity, deliverySchedule,
// qtyPeriodicity, paymentTerm): the term {"name", "eventType", "offsetDays", "calendar"}; it returns {"term",
// "periods"}: the term as QuantLib's operator<< writes it, with its == against itself and its != and == against a term
// of another name; and each period [startDate, endDate, paymentDate, quantity], or {"throws": true}. The generator
// builds monthly schedules alone (a daily period's quantity is one day short in QuantLib; the other schedules are
// refused). "CommodityPricingHelper::calculateUnitCost" takes (commodityType, unitCost, baseCurrency, baseUnit,
// evaluationDate) and returns {"text", "unitCost"}: the cost as QuantLib's operator<< writes it, and the cost per base
// unit, on the cost's own unit (a factor of 1; others are left out), or {"throws": true}.

#include "experimental/commodities/commodityspec.hpp"

#include <ql/experimental/commodities/commoditypricinghelpers.hpp>
#include <ql/experimental/commodities/paymentterm.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"CommodityPricingHelper::createPricingPeriods",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 DateSpec start = date(a[0]);
                 DateSpec end = date(a[1]);
                 commodities::QuantitySpec q = commodities::quantity(a[2]);
                 const int schedule = integer(a[3]);
                 const int periodicity = integer(a[4]);
                 if (!a[5].is_object() || a[5].as_object().size() != 4)
                     throw InvalidParams("expected a payment term {name, eventType, offsetDays, calendar}");
                 const std::string termName = text(a[5].at("name"));
                 const int eventType = integer(a[5].at("eventType"));
                 const Integer offsetDays = integer(a[5].at("offsetDays"));
                 indexes::CalendarSpec calendar = indexes::calendar(a[5].at("calendar"));
                 return [=] {
                     commodities::resetGlobals();
                     PaymentTerm term(termName, static_cast<PaymentTerm::EventType>(eventType), offsetDays,
                                      indexes::makeCalendar(calendar));
                     const PaymentTerm other(termName + " (other)", static_cast<PaymentTerm::EventType>(eventType),
                                             offsetDays, indexes::makeCalendar(calendar));
                     json::object o;
                     o["term"] = json::array{json::value(streamed(term)), json::value(term == term),
                                             json::value(term != other), json::value(term == other)};
                     try {
                         PricingPeriods periods;
                         CommodityPricingHelper::createPricingPeriods(
                             makeDate(start), makeDate(end), commodities::makeQuantity(q),
                             static_cast<EnergyCommodity::DeliverySchedule>(schedule),
                             static_cast<EnergyCommodity::QuantityPeriodicity>(periodicity), term, periods);
                         json::array out;
                         for (const auto& p : periods)
                             out.emplace_back(json::array{result(p->startDate()), result(p->endDate()),
                                                          result(p->paymentDate()),
                                                          commodities::quantityResult(p->quantity())});
                         o["periods"] = out;
                     } catch (const Error&) {
                         o["periods"] = json::object{{"throws", true}};
                     }
                     return json::value(o);
                 };
             }},
            {"CommodityPricingHelper::calculateUnitCost",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 commodities::CommodityTypeSpec type = commodities::commodityType(a[0]);
                 commodities::UnitCostSpec cost = commodities::unitCost(a[1]);
                 const std::string baseCurrency = commodities::currency(a[2]);
                 const std::string baseUnit = commodities::unit(a[3]);
                 DateSpec evaluationDate = date(a[4]);
                 return [=] {
                     commodities::resetGlobals();
                     json::object o;
                     o["text"] = streamed(commodities::makeUnitCost(cost));
                     try {
                         o["unitCost"] = CommodityPricingHelper::calculateUnitCost(
                             commodities::makeCommodityType(type), commodities::makeUnitCost(cost),
                             commodities::makeCurrency(baseCurrency), commodities::makeUnit(baseUnit),
                             makeDate(evaluationDate));
                     } catch (const Error&) {
                         o["unitCost"] = json::object{{"throws", true}};
                     }
                     return json::value(o);
                 };
             }},
        });

    }

}
