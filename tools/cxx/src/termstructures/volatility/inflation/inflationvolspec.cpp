// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "termstructures/volatility/inflation/inflationvolspec.hpp"

#include <ql/termstructures/volatility/inflation/constantcpivolatility.hpp>
#include <ql/utilities/null.hpp>

#include <initializer_list>
#include <string>

using namespace QuantLib;

namespace harness::termstructures {

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

        Natural natural(const json::value& v) {
            Integer n = integer(v);
            if (n == Null<Integer>() || n < 0)
                throw InvalidParams("expected a Natural, got " + json::serialize(v));
            return static_cast<Natural>(n);
        }

    }

    InflationVolSpec inflationVolatility(const json::value& v, const std::string& type) {
        if (!v.is_object())
            throw InvalidParams("expected an inflation volatility spec, got " + json::serialize(v));
        const json::object& o = v.get_object();
        const json::value* t = o.if_contains("type");
        if (t == nullptr || !t->is_string())
            throw InvalidParams("an inflation volatility spec needs a type: " + json::serialize(v));
        InflationVolSpec s;
        s.type = std::string(t->get_string());
        const std::string& what = s.type;
        if (s.type != type)
            throw InvalidParams("expected a " + type + " spec, got " + what);
        if (s.type == "ConstantCPIVolatility") {
            onlyKeys(o, {"type", "volatility", "settlementDays", "calendar", "convention", "dayCounter",
                         "observationLag", "frequency", "indexIsInterpolated", "enableExtrapolation"},
                     what);
        } else if (s.type == "ConstantYoYOptionletVolatility") {
            onlyKeys(o, {"type", "volatility", "settlementDays", "calendar", "convention", "dayCounter",
                         "observationLag", "frequency", "indexIsInterpolated", "minStrike", "maxStrike",
                         "volatilityType", "displacement", "enableExtrapolation"},
                     what);
            s.minStrike = real(key(o, "minStrike", what));
            s.maxStrike = real(key(o, "maxStrike", what));
            s.volatilityType = enumeration<VolatilityType>(key(o, "volatilityType", what));
            s.displacement = real(key(o, "displacement", what));
        } else {
            throw InvalidParams("unknown inflation volatility " + s.type);
        }
        s.volatility = real(key(o, "volatility", what));
        s.settlementDays = natural(key(o, "settlementDays", what));
        s.calendar = indexes::calendar(key(o, "calendar", what));
        s.convention = enumeration<BusinessDayConvention>(key(o, "convention", what));
        s.dayCounter = dayCounter(key(o, "dayCounter", what));
        LagSpec l = lag(key(o, "observationLag", what));
        if (l.null)
            throw InvalidParams(what + " spec needs an observation lag");
        s.lagLength = l.length;
        s.lagUnits = l.units;
        s.frequency = enumeration<Frequency>(key(o, "frequency", what));
        s.indexIsInterpolated = boolean(key(o, "indexIsInterpolated", what));
        s.enableExtrapolation = boolean(key(o, "enableExtrapolation", what));
        return s;
    }

    LagSpec lag(const json::value& v) {
        if (v.is_null())
            return {};
        if (!v.is_array() || v.get_array().size() != 2)
            throw InvalidParams("expected a lag as [length, units] or null, got " + json::serialize(v));
        return {false, integer(v.get_array()[0]), enumeration<TimeUnit>(v.get_array()[1])};
    }

    ext::shared_ptr<CPIVolatilitySurface> makeCPIVolatility(const InflationVolSpec& s) {
        auto out = ext::make_shared<ConstantCPIVolatility>(
            s.volatility, s.settlementDays, indexes::makeCalendar(s.calendar), s.convention,
            makeDayCounter(s.dayCounter), Period(s.lagLength, s.lagUnits), s.frequency, s.indexIsInterpolated);
        if (s.enableExtrapolation)
            out->enableExtrapolation();
        return out;
    }

    ext::shared_ptr<YoYOptionletVolatilitySurface> makeYoYVolatility(const InflationVolSpec& s) {
        auto out = ext::make_shared<ConstantYoYOptionletVolatility>(
            s.volatility, s.settlementDays, indexes::makeCalendar(s.calendar), s.convention,
            makeDayCounter(s.dayCounter), Period(s.lagLength, s.lagUnits), s.frequency, s.indexIsInterpolated,
            s.minStrike, s.maxStrike, s.volatilityType, s.displacement);
        if (s.enableExtrapolation)
            out->enableExtrapolation();
        return out;
    }

    Period makeLag(const LagSpec& l) { return l.null ? Period(-1, Days) : Period(l.length, l.units); }

}
