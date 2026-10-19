// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "instruments/payoffspec.hpp"

#include <initializer_list>
#include <map>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness::payoffs {

    namespace {

        // Each payoff's constructor arguments, besides its type.
        const std::map<std::string, std::vector<const char*>>& payoffKeys() {
            static const std::map<std::string, std::vector<const char*>> keys = {
                {"NullPayoff", {}},
                {"FloatingTypePayoff", {"optionType"}},
                {"PlainVanillaPayoff", {"optionType", "strike"}},
                {"PercentageStrikePayoff", {"optionType", "moneyness"}},
                {"AssetOrNothingPayoff", {"optionType", "strike"}},
                {"CashOrNothingPayoff", {"optionType", "strike", "cashPayoff"}},
                {"GapPayoff", {"optionType", "strike", "secondStrike"}},
                {"SuperFundPayoff", {"strike", "secondStrike"}},
                {"SuperSharePayoff", {"strike", "secondStrike", "cashPayoff"}},
            };
            return keys;
        }

        bool takes(const std::vector<const char*>& keys, const std::string& name) {
            for (const char* k : keys) {
                if (name == k)
                    return true;
            }
            return false;
        }

    }

    PayoffSpec payoff(const json::value& v) {
        if (!v.is_object())
            throw InvalidParams("expected a payoff spec, got " + json::serialize(v));
        const json::object& o = v.get_object();
        const json::value* type = o.if_contains("type");
        if (type == nullptr || !type->is_string())
            throw InvalidParams("a payoff spec needs a type: " + json::serialize(v));
        PayoffSpec s{std::string(type->get_string()), Option::Call, 0.0, 0.0, 0.0};
        auto found = payoffKeys().find(s.type);
        if (found == payoffKeys().end())
            throw InvalidParams("unknown payoff " + s.type);
        const std::vector<const char*>& keys = found->second;
        for (const auto& kv : o) {
            if (kv.key() != "type" && !takes(keys, std::string(kv.key())))
                throw InvalidParams(s.type + " takes no key " + std::string(kv.key()));
        }
        for (const char* k : keys) {
            const json::value* x = o.if_contains(k);
            if (x == nullptr)
                throw InvalidParams(s.type + " spec needs " + k);
            std::string name(k);
            if (name == "optionType")
                s.optionType = enumeration<Option::Type>(*x);
            else if (name == "strike" || name == "moneyness")
                s.strike = real(*x);
            else if (name == "secondStrike")
                s.secondStrike = real(*x);
            else
                s.cashPayoff = real(*x);
        }
        return s;
    }

    PayoffSpec plainVanillaPayoff(const json::value& v) {
        PayoffSpec s = payoff(v);
        if (s.type != "PlainVanillaPayoff")
            throw InvalidParams("expected a PlainVanillaPayoff spec, got " + s.type);
        return s;
    }

    ext::shared_ptr<Payoff> makePayoff(const PayoffSpec& s) {
        if (s.type == "NullPayoff")
            return ext::make_shared<NullPayoff>();
        if (s.type == "FloatingTypePayoff")
            return ext::make_shared<FloatingTypePayoff>(s.optionType);
        if (s.type == "PlainVanillaPayoff")
            return ext::make_shared<PlainVanillaPayoff>(s.optionType, s.strike);
        if (s.type == "PercentageStrikePayoff")
            return ext::make_shared<PercentageStrikePayoff>(s.optionType, s.strike);
        if (s.type == "AssetOrNothingPayoff")
            return ext::make_shared<AssetOrNothingPayoff>(s.optionType, s.strike);
        if (s.type == "CashOrNothingPayoff")
            return ext::make_shared<CashOrNothingPayoff>(s.optionType, s.strike, s.cashPayoff);
        if (s.type == "GapPayoff")
            return ext::make_shared<GapPayoff>(s.optionType, s.strike, s.secondStrike);
        if (s.type == "SuperFundPayoff")
            return ext::make_shared<SuperFundPayoff>(s.strike, s.secondStrike);
        if (s.type == "SuperSharePayoff")
            return ext::make_shared<SuperSharePayoff>(s.strike, s.secondStrike, s.cashPayoff);
        throw InvalidParams("unknown payoff " + s.type);
    }

    ext::shared_ptr<PlainVanillaPayoff> makePlainVanillaPayoff(const PayoffSpec& s) {
        return ext::make_shared<PlainVanillaPayoff>(s.optionType, s.strike);
    }

}
