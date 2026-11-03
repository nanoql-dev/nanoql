// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/commodities/commodityindex.hpp, .cpp: an index's fixings and forward prices.
// "CommodityIndex::values" takes (index, dates): an index spec of commodityspec.hpp, its curve in the index's unit
// (the generator leaves other units out), and the dates read; the case's fixings are the Settings call's. It returns
// {"name", "text", "lastQuoteDate", "empty", "forwardCurveEmpty", "lotQuantity", "validFixingDate", "fixing",
// "forwardPrice"}, the text as QuantLib's operator<< writes the index, its curve's with it; each date's
// isValidFixingDate, fixing (its stored fixing, or {"throws": true} where none is stored or the date is no fixing
// date: QuantLib returns Null<Real>() for none, which the harness writes as a refusal) and forwardPrice; lastQuoteDate
// is null for an index with no fixings, where QuantLib throws.

#include "experimental/commodities/commodityspec.hpp"

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"CommodityIndex::values",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 commodities::IndexSpec s = commodities::index(a[0]);
                 if (!a[1].is_array())
                     throw InvalidParams("expected dates");
                 std::vector<DateSpec> dates;
                 for (const auto& d : a[1].as_array())
                     dates.push_back(date(d));
                 return [=] {
                     commodities::resetGlobals();
                     auto index = commodities::makeIndex(s);
                     json::array valid, fixing, forward;
                     for (const auto& d : dates) {
                         const Date day = makeDate(d);
                         valid.emplace_back(index->isValidFixingDate(day));
                         try {
                             const Real f = index->fixing(day);
                             if (f == Null<Real>())
                                 fixing.emplace_back(json::object{{"throws", true}});
                             else
                                 fixing.emplace_back(f);
                         } catch (const Error&) {
                             fixing.emplace_back(json::object{{"throws", true}});
                         }
                         try {
                             forward.emplace_back(index->forwardPrice(day));
                         } catch (const Error&) {
                             forward.emplace_back(json::object{{"throws", true}});
                         }
                     }
                     json::object o;
                     o["name"] = index->name();
                     o["text"] = streamed(*index);
                     o["empty"] = index->empty();
                     o["lastQuoteDate"] = index->empty() ? json::value(nullptr) : result(index->lastQuoteDate());
                     o["forwardCurveEmpty"] = index->forwardCurveEmpty();
                     o["lotQuantity"] = index->lotQuantity();
                     o["validFixingDate"] = valid;
                     o["fixing"] = fixing;
                     o["forwardPrice"] = forward;
                     return json::value(o);
                 };
             }},
        });

    }

}
