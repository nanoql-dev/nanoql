// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/indexes/region.hpp: operator== and operator!=, which the wheel's proxies don't compare, each taking two region
// specs: {"type": "CustomRegion", "name", "code"}, or {"type"} naming a preset region (AustraliaRegion, EURegion,
// FranceRegion, UKRegion, USRegion, ZARegion), built inside the call.

#include "convert.hpp"

#include <ql/indexes/region.hpp>

#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        struct RegionSpec {
            std::string type;
            std::string name;
            std::string code;
        };

        RegionSpec region(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a region spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            const json::value* type = o.if_contains("type");
            if (type == nullptr || !type->is_string())
                throw InvalidParams("a region spec needs a type: " + json::serialize(v));
            RegionSpec s{std::string(type->get_string()), "", ""};
            if (s.type == "CustomRegion") {
                const json::value* name = o.if_contains("name");
                const json::value* code = o.if_contains("code");
                if (name == nullptr || code == nullptr || o.size() != 3)
                    throw InvalidParams("CustomRegion takes type, name and code: " + json::serialize(v));
                s.name = text(*name);
                s.code = text(*code);
            } else if (s.type != "AustraliaRegion" && s.type != "EURegion" && s.type != "FranceRegion" &&
                       s.type != "UKRegion" && s.type != "USRegion" && s.type != "ZARegion") {
                throw InvalidParams("unknown region " + s.type);
            } else if (o.size() != 1) {
                throw InvalidParams(s.type + " takes only its type: " + json::serialize(v));
            }
            return s;
        }

        Region makeRegion(const RegionSpec& s) {
            if (s.type == "CustomRegion")
                return CustomRegion(s.name, s.code);
            if (s.type == "AustraliaRegion")
                return AustraliaRegion();
            if (s.type == "EURegion")
                return EURegion();
            if (s.type == "FranceRegion")
                return FranceRegion();
            if (s.type == "UKRegion")
                return UKRegion();
            if (s.type == "USRegion")
                return USRegion();
            return ZARegion();
        }

        const Register unit({
            {"Region::operator==",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 RegionSpec r1 = region(a[0]);
                 RegionSpec r2 = region(a[1]);
                 return [=] { return json::value(makeRegion(r1) == makeRegion(r2)); };
             }},
            {"Region::operator!=",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 RegionSpec r1 = region(a[0]);
                 RegionSpec r2 = region(a[1]);
                 return [=] { return json::value(makeRegion(r1) != makeRegion(r2)); };
             }},
        });

    }

}
