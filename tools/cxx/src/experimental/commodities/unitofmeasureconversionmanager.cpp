// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/commodities/unitofmeasureconversionmanager.hpp, .cpp: the registry's lookups.
// "UnitOfMeasureConversionManager::lookups" takes (adds, queries): the conversions added after clear(), in order (specs
// of commodityspec.hpp), and the lookups [commodityType, source, target, type], type 0 for Direct and 1 for Derived;
// it returns {"lookups"}: for each, [factor, source, target, type, unit, amount] (the factor, the found conversion's
// units' codes and type, and its convert of one unit of the query's source), or {"throws": true}. The generator
// queries no chained search QuantLib's recursion can't end (the S-xrt run's R1-1).

#include "experimental/commodities/commodityspec.hpp"

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct QuerySpec {
            commodities::CommodityTypeSpec commodityType;
            std::string source, target;
            int type;
        };

        const Register unit({
            {"UnitOfMeasureConversionManager::lookups",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 if (!a[0].is_array() || !a[1].is_array())
                     throw InvalidParams("expected (adds, queries)");
                 std::vector<commodities::ConversionSpec> adds;
                 for (const auto& c : a[0].as_array())
                     adds.push_back(commodities::conversion(c));
                 std::vector<QuerySpec> queries;
                 for (const auto& q : a[1].as_array()) {
                     if (!q.is_array() || q.as_array().size() != 4)
                         throw InvalidParams("a lookup is [commodityType, source, target, type]");
                     const json::array& x = q.as_array();
                     queries.push_back({commodities::commodityType(x[0]), commodities::unit(x[1]),
                                        commodities::unit(x[2]), integer(x[3])});
                 }
                 return [=] {
                     commodities::resetGlobals();
                     UnitOfMeasureConversionManager& m = UnitOfMeasureConversionManager::instance();
                     for (const auto& c : adds)
                         m.add(commodities::makeConversion(c));
                     json::array out;
                     for (const auto& q : queries) {
                         try {
                             const CommodityType type = commodities::makeCommodityType(q.commodityType);
                             const UnitOfMeasure source = commodities::makeUnit(q.source);
                             UnitOfMeasureConversion c = m.lookup(type, source, commodities::makeUnit(q.target),
                                                                  static_cast<UnitOfMeasureConversion::Type>(q.type));
                             const Quantity one = c.convert(Quantity(type, source, 1.0));
                             out.emplace_back(json::array{json::value(c.conversionFactor()),
                                                          json::value(c.source().code()),
                                                          json::value(c.target().code()),
                                                          json::value(static_cast<int>(c.type())),
                                                          json::value(one.unitOfMeasure().code()),
                                                          json::value(one.amount())});
                         } catch (const Error&) {
                             out.emplace_back(json::object{{"throws", true}});
                         }
                     }
                     commodities::resetGlobals();
                     json::object result;
                     result["lookups"] = out;
                     return json::value(result);
                 };
             }},
        });

    }

}
