// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "instruments/capfloorspec.hpp"

#include <cstdint>
#include <initializer_list>
#include <string>

using namespace QuantLib;

namespace harness::capfloors {

    namespace {

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " spec needs " + name);
            return *v;
        }

        void onlyKeys(const json::object& o, std::initializer_list<const char*> keys, const std::string& what) {
            for (const auto& kv : o) {
                bool known = false;
                for (const char* k : keys)
                    known = known || kv.key() == k;
                if (!known)
                    throw InvalidParams(what + " takes no key " + std::string(kv.key()));
            }
        }

        json::value realArray(const std::vector<Real>& v) {
            json::array out;
            for (Real x : v)
                out.emplace_back(x);
            return json::value(std::move(out));
        }

    }

    CapFloorSpec capFloor(const json::value& v) {
        const std::string what = "a cap or floor";
        if (!v.is_object())
            throw InvalidParams("expected " + what + " spec, got " + json::serialize(v));
        const json::object& o = v.get_object();
        CapFloorSpec s;
        s.strikesForm = o.if_contains("strikes") != nullptr;
        if (s.strikesForm)
            onlyKeys(o, {"type", "leg", "legType", "strikes"}, what);
        else
            onlyKeys(o, {"type", "leg", "legType", "capRates", "floorRates"}, what);
        s.type = enumeration<CapFloor::Type>(key(o, "type", what));
        std::string legType = text(key(o, "legType", what));
        if (legType == "Empty") {
            if (!key(o, "leg", what).is_null())
                throw InvalidParams("an empty leg's spec is null");
            s.empty = true;
        } else if (legType == "IborLeg") {
            s.leg = cashflows::leg(key(o, "leg", what));
        } else {
            throw InvalidParams("unknown leg type " + legType);
        }
        if (s.strikesForm) {
            s.strikes = reals(key(o, "strikes", what));
        } else {
            s.capRates = reals(key(o, "capRates", what));
            s.floorRates = reals(key(o, "floorRates", what));
        }
        return s;
    }

    ext::shared_ptr<CapFloor> makeCapFloor(const CapFloorSpec& s) {
        Leg leg = s.empty ? Leg() : cashflows::makeLeg(s.leg);
        if (s.strikesForm)
            return ext::make_shared<CapFloor>(s.type, leg, s.strikes);
        return ext::make_shared<CapFloor>(s.type, leg, s.capRates, s.floorRates);
    }

    json::value inspectors(const CapFloor& c) {
        json::object out;
        out["type"] = static_cast<std::int64_t>(c.type());
        out["capRates"] = realArray(c.capRates());
        out["floorRates"] = realArray(c.floorRates());
        out["legSize"] = static_cast<std::int64_t>(c.floatingLeg().size());
        return json::value(std::move(out));
    }

}
