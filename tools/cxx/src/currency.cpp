// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/currency.hpp: the members of Currency the wheel doesn't expose (the constructor's ninth argument,
// minorUnitCodes, the inspector of the same name, and operator<<), over a currency the call builds from its spec. A
// Currency argument is null for Currency(), QuantLib's empty currency, or the spec {"name", "code", "numericCode",
// "symbol", "fractionSymbol", "fractionsPerUnit", "rounding", "triangulationCurrency", "minorUnitCodes"}, the
// vectors' input names: the rounding a Rounding spec (math/roundingspec.hpp), the triangulation currency a Currency
// argument, and the minor unit codes a list of strings, which the constructor's std::set sorts and deduplicates.
//
// The constructor's result is its inspectors, {"name", "code", "numericCode", "symbol", "fractionSymbol",
// "fractionsPerUnit", "empty", "triangulationCurrency" (its code, or null when it is empty), "minorUnitCodes" (in
// the set's order)}.

#include "convert.hpp"
#include "math/roundingspec.hpp"

#include <ql/currency.hpp>

#include <memory>
#include <set>

using namespace QuantLib;

namespace harness {

    namespace {

        struct CurrencySpec {
            std::string name, code;
            Integer numericCode;
            std::string symbol, fractionSymbol;
            Integer fractionsPerUnit;
            RoundingSpec rounding;
            std::shared_ptr<const CurrencySpec> triangulation;  // null for Currency()
            std::vector<std::string> minorUnitCodes;
        };

        // A Currency argument: null for Currency().
        using CurrencyArgument = std::shared_ptr<const CurrencySpec>;

        const json::value& key(const json::object& o, const char* name) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string("a Currency spec needs ") + name);
            return *v;
        }

        Integer integerArgument(const json::value& v) {
            if (v.is_null())
                throw InvalidParams("expected an Integer, got null");
            return integer(v);
        }

        std::vector<std::string> texts(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected a list of strings, got " + json::serialize(v));
            std::vector<std::string> out;
            for (const json::value& x : v.get_array())
                out.push_back(text(x));
            return out;
        }

        CurrencyArgument currency(const json::value& v) {
            if (v.is_null())
                return nullptr;
            if (!v.is_object())
                throw InvalidParams("expected a Currency spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            for (const auto& kv : o) {
                if (kv.key() != "name" && kv.key() != "code" && kv.key() != "numericCode" && kv.key() != "symbol" &&
                    kv.key() != "fractionSymbol" && kv.key() != "fractionsPerUnit" && kv.key() != "rounding" &&
                    kv.key() != "triangulationCurrency" && kv.key() != "minorUnitCodes")
                    throw InvalidParams("Currency takes no key " + std::string(kv.key()));
            }
            return std::make_shared<const CurrencySpec>(CurrencySpec{
                text(key(o, "name")), text(key(o, "code")), integerArgument(key(o, "numericCode")),
                text(key(o, "symbol")), text(key(o, "fractionSymbol")), integerArgument(key(o, "fractionsPerUnit")),
                rounding(key(o, "rounding")), currency(key(o, "triangulationCurrency")),
                texts(key(o, "minorUnitCodes"))});
        }

        Currency make(const CurrencyArgument& s) {
            if (!s)
                return {};
            return {s->name,
                    s->code,
                    s->numericCode,
                    s->symbol,
                    s->fractionSymbol,
                    s->fractionsPerUnit,
                    makeRounding(s->rounding),
                    make(s->triangulation),
                    std::set<std::string>(s->minorUnitCodes.begin(), s->minorUnitCodes.end())};
        }

        json::value codes(const std::set<std::string>& c) {
            json::array out;
            for (const std::string& x : c)
                out.emplace_back(x);
            return out;
        }

        json::value inspectors(const Currency& c) {
            const Currency& t = c.triangulationCurrency();
            return json::object{{"name", c.name()},
                                {"code", c.code()},
                                {"numericCode", c.numericCode()},
                                {"symbol", c.symbol()},
                                {"fractionSymbol", c.fractionSymbol()},
                                {"fractionsPerUnit", c.fractionsPerUnit()},
                                {"empty", c.empty()},
                                {"triangulationCurrency", t.empty() ? json::value(nullptr) : json::value(t.code())},
                                {"minorUnitCodes", codes(c.minorUnitCodes())}};
        }

        const Register unit({
            {"Currency",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 json::object spec{{"name", a[0]},
                                   {"code", a[1]},
                                   {"numericCode", a[2]},
                                   {"symbol", a[3]},
                                   {"fractionSymbol", a[4]},
                                   {"fractionsPerUnit", a[5]},
                                   {"rounding", a[6]},
                                   {"triangulationCurrency", a[7]},
                                   {"minorUnitCodes", a[8]}};
                 CurrencyArgument s = currency(spec);
                 return [=] { return inspectors(make(s)); };
             }},
            {"Currency::rounding",
             [](const Args& a) -> Call {
                 // The currency's rounding, applied to a value: Rounding's operator() on rounding()'s result.
                 arity(a, 2);
                 CurrencyArgument s = currency(a[0]);
                 Decimal value = real(a[1]);
                 return [=] { return json::value(make(s).rounding()(value)); };
             }},
            {"operator<<(Currency)",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 CurrencyArgument s = currency(a[0]);
                 return [=] { return json::value(streamed(make(s))); };
             }},
        });

    }

}
