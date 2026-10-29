// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/commodities/quantity.hpp, .cpp: a quantity's arithmetic under each conversion type.
// "Quantity::arithmetic" takes (settings, pairs): settings {"conversionType", "baseUnit"} (Quantity's two statics,
// the unit a code or null for none) and [q1, q2] pairs of quantities; it returns {"pairs", "equal"}: for each pair,
// [sum, difference, ratio, <, <=, >, >=, close, close_enough, +q1, -q1, q1 * 2.5, 2.5 * q1, q1 / 4], a quantity
// [unit, amount], each of the first nine {"throws": true} where QuantLib refuses it, and its ==, apart, which the
// vector test maps where QuantLib throws on a mismatch without conversion. The generator keeps to conversions whose
// results are whole units, where QuantLib's rounding to the unit leaves them as they are (the S-xrt plan's XRR10).

#include "experimental/commodities/commodityspec.hpp"

#include <ql/math/comparison.hpp>

#include <functional>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        json::value attempt(const std::function<json::value()>& f) {
            try {
                return f();
            } catch (const Error&) {
                return json::object{{"throws", true}};
            }
        }

        const Register unit({
            {"Quantity::arithmetic",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 if (!a[0].is_object() || a[0].as_object().size() != 2)
                     throw InvalidParams("expected settings {conversionType, baseUnit}");
                 const int conversionType = integer(a[0].at("conversionType"));
                 const json::value& base = a[0].at("baseUnit");
                 std::optional<std::string> baseUnit;
                 if (!base.is_null())
                     baseUnit = commodities::unit(base);
                 if (!a[1].is_array())
                     throw InvalidParams("expected pairs of quantities");
                 std::vector<std::pair<commodities::QuantitySpec, commodities::QuantitySpec>> pairs;
                 for (const auto& p : a[1].as_array()) {
                     if (!p.is_array() || p.as_array().size() != 2)
                         throw InvalidParams("a pair is [q1, q2]");
                     pairs.emplace_back(commodities::quantity(p.as_array()[0]), commodities::quantity(p.as_array()[1]));
                 }
                 return [=] {
                     commodities::resetGlobals();
                     Quantity::conversionType = static_cast<Quantity::ConversionType>(conversionType);
                     if (baseUnit)
                         Quantity::baseUnitOfMeasure = commodities::makeUnit(*baseUnit);
                     json::array out, equal;
                     for (const auto& p : pairs) {
                         const Quantity q1 = commodities::makeQuantity(p.first);
                         const Quantity q2 = commodities::makeQuantity(p.second);
                         out.emplace_back(json::array{
                             attempt([&] { return commodities::quantityResult(q1 + q2); }),
                             attempt([&] { return commodities::quantityResult(q1 - q2); }),
                             attempt([&] { return json::value(q1 / q2); }),
                             attempt([&] { return json::value(q1 < q2); }),
                             attempt([&] { return json::value(q1 <= q2); }),
                             attempt([&] { return json::value(q1 > q2); }),
                             attempt([&] { return json::value(q1 >= q2); }),
                             attempt([&] { return json::value(close(q1, q2)); }),
                             attempt([&] { return json::value(close_enough(q1, q2)); }),
                             commodities::quantityResult(+q1), commodities::quantityResult(-q1),
                             commodities::quantityResult(q1 * 2.5),
                             commodities::quantityResult(2.5 * q1), commodities::quantityResult(q1 / 4.0)});
                         equal.emplace_back(attempt([&] { return json::value(q1 == q2); }));
                     }
                     commodities::resetGlobals();
                     json::object result;
                     result["pairs"] = out;
                     result["equal"] = equal;
                     return json::value(result);
                 };
             }},
        });

    }

}
