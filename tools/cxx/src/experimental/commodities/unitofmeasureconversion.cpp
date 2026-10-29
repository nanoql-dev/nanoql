// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/commodities/unitofmeasureconversion.hpp, .cpp: a conversion's convert and chain.
// "UnitOfMeasureConversion::convert" takes (conversion, quantities) and returns {"code", "converted", "texts",
// "units"}: the conversion's code; its convert of each quantity, a quantity [unit, amount] or {"throws": true}, and
// that quantity as QuantLib's operator<< writes it; and its units and commodity type as operator<< writes them, with
// whether the type differs from the null type.
// "UnitOfMeasureConversion::chain" takes (first, second, quantities), two conversions, and returns {"factor",
// "source", "target", "type", "converted"} for their chain, or {"throws": true} where QuantLib refuses to chain them.

#include "experimental/commodities/commodityspec.hpp"

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        std::vector<commodities::QuantitySpec> quantities(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected quantities, got " + json::serialize(v));
            std::vector<commodities::QuantitySpec> out;
            for (const auto& q : v.as_array())
                out.push_back(commodities::quantity(q));
            return out;
        }

        json::array converted(const UnitOfMeasureConversion& c, const std::vector<commodities::QuantitySpec>& qs) {
            json::array out;
            for (const auto& q : qs) {
                try {
                    out.emplace_back(commodities::quantityResult(c.convert(commodities::makeQuantity(q))));
                } catch (const Error&) {
                    out.emplace_back(json::object{{"throws", true}});
                }
            }
            return out;
        }

        const Register unit({
            {"UnitOfMeasureConversion::convert",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 commodities::ConversionSpec c = commodities::conversion(a[0]);
                 std::vector<commodities::QuantitySpec> qs = quantities(a[1]);
                 return [=] {
                     commodities::resetGlobals();
                     UnitOfMeasureConversion conversion = commodities::makeConversion(c);
                     json::object o;
                     o["code"] = conversion.code();
                     o["converted"] = converted(conversion, qs);
                     json::array texts;
                     for (const auto& q : qs) {
                         try {
                             texts.emplace_back(streamed(conversion.convert(commodities::makeQuantity(q))));
                         } catch (const Error&) {
                             texts.emplace_back(json::object{{"throws", true}});
                         }
                     }
                     o["texts"] = texts;
                     o["units"] = json::array{json::value(streamed(conversion.source())),
                                              json::value(streamed(conversion.target())),
                                              json::value(streamed(conversion.commodityType())),
                                              json::value(conversion.commodityType() != NullCommodityType())};
                     return json::value(o);
                 };
             }},
            {"UnitOfMeasureConversion::chain",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 commodities::ConversionSpec c1 = commodities::conversion(a[0]);
                 commodities::ConversionSpec c2 = commodities::conversion(a[1]);
                 std::vector<commodities::QuantitySpec> qs = quantities(a[2]);
                 return [=] {
                     commodities::resetGlobals();
                     UnitOfMeasureConversion chained = UnitOfMeasureConversion::chain(
                         commodities::makeConversion(c1), commodities::makeConversion(c2));
                     json::object o;
                     o["factor"] = chained.conversionFactor();
                     o["source"] = chained.source().code();
                     o["target"] = chained.target().code();
                     o["type"] = static_cast<int>(chained.type());
                     o["converted"] = converted(chained, qs);
                     return json::value(o);
                 };
             }},
        });

    }

}
