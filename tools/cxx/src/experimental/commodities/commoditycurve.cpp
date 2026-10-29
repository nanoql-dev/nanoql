// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/commodities/commoditycurve.hpp, .cpp: a curve's prices.
// "CommodityCurve::prices" takes (curve, dates, contracts, nearbyOffset): a curve spec of commodityspec.hpp (with its
// basis curve, on the same reference date, day counter and unit, the S-xrt plan's XRR5 (xi)), the dates priced, and
// the contracts and offset of a nearby read. It returns {"nodes", "text", "times", "maxDate", "price", "basisOfPrice",
// "nearby"}: the nodes [date, price], the curve as QuantLib's operator<< writes it, the node times, the last date,
// each date's price(d, null, 0) and basisOfPrice(d), and each date's price through the nearby contract with its
// underlyingPriceDate, [price, date], or {"throws": true}.

#include "experimental/commodities/commodityspec.hpp"

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"CommodityCurve::prices",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 commodities::CurveSpec c = commodities::curve(a[0]);
                 if (!a[1].is_array())
                     throw InvalidParams("expected dates");
                 std::vector<DateSpec> dates;
                 for (const auto& d : a[1].as_array())
                     dates.push_back(date(d));
                 std::optional<std::vector<commodities::ContractSpec>> contracts = commodities::contracts(a[2]);
                 const Integer nearbyOffset = integer(a[3]);
                 return [=] {
                     commodities::resetGlobals();
                     auto curve = commodities::makeCurve(c);
                     auto exchangeContracts = commodities::makeContracts(contracts);
                     json::array times, price, basis, nearby;
                     for (Time t : curve->times())
                         times.emplace_back(t);
                     for (const auto& d : dates) {
                         const Date day = makeDate(d);
                         price.emplace_back(curve->price(day, nullptr, 0));
                         basis.emplace_back(curve->basisOfPrice(day));
                         try {
                             const Real p = curve->price(day, exchangeContracts, nearbyOffset);
                             const Date u = curve->underlyingPriceDate(day, exchangeContracts, nearbyOffset);
                             nearby.emplace_back(json::array{p, result(u)});
                         } catch (const Error&) {
                             nearby.emplace_back(json::object{{"throws", true}});
                         }
                     }
                     json::array nodes;
                     for (const auto& n : curve->nodes())
                         nodes.emplace_back(json::array{result(n.first), json::value(n.second)});
                     json::object o;
                     o["nodes"] = nodes;
                     o["text"] = streamed(*curve);
                     o["times"] = times;
                     o["maxDate"] = result(curve->maxDate());
                     o["price"] = price;
                     o["basisOfPrice"] = basis;
                     o["nearby"] = nearby;
                     return json::value(o);
                 };
             }},
        });

    }

}
