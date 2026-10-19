// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The stripper spec: see stripperspec.hpp.

#include "termstructures/volatility/optionlet/stripperspec.hpp"

#include <ql/time/period.hpp>

#include <initializer_list>
#include <string>

using namespace QuantLib;

namespace harness::strippers {

    namespace {

        const json::value& key(const json::object& o, const char* name) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string("a stripper spec needs ") + name);
            return *v;
        }

        Natural natural(const json::value& v) {
            Integer n = integer(v);
            if (n == Null<Integer>() || n < 0)
                throw InvalidParams("expected a Natural, got " + json::serialize(v));
            return static_cast<Natural>(n);
        }

    }

    StripperSpec stripper(const json::value& v) {
        if (!v.is_object())
            throw InvalidParams("expected a stripper spec, got " + json::serialize(v));
        const json::object& o = v.get_object();
        for (const auto& kv : o) {
            bool known = false;
            for (const char* k : {"surface", "index", "discount", "switchStrike", "accuracy", "maxIter", "type",
                                  "displacement", "dontThrow", "optionletFrequency", "paymentLag"})
                known = known || kv.key() == k;
            if (!known)
                throw InvalidParams("a stripper takes no key " + std::string(kv.key()));
        }
        StripperSpec s;
        s.surface = capfloorvols::surface(key(o, "surface"));
        s.index = indexes::iborIndex(key(o, "index"));
        s.discount = indexes::curve(key(o, "discount"));
        s.switchStrike = real(key(o, "switchStrike"));
        s.accuracy = real(key(o, "accuracy"));
        s.maxIter = natural(key(o, "maxIter"));
        s.type = enumeration<VolatilityType>(key(o, "type"));
        s.displacement = real(key(o, "displacement"));
        s.dontThrow = boolean(key(o, "dontThrow"));
        const json::value& frequency = key(o, "optionletFrequency");
        if (!frequency.is_null())
            s.optionletFrequency = capfloorvols::period(frequency);
        s.paymentLag = natural(key(o, "paymentLag"));
        return s;
    }

    ext::shared_ptr<OptionletStripper1> makeStripper(const StripperSpec& s) {
        std::optional<Period> frequency;
        if (s.optionletFrequency)
            frequency = Period(s.optionletFrequency->first, s.optionletFrequency->second);
        return ext::make_shared<OptionletStripper1>(capfloorvols::makeSurface(s.surface),
                                                    indexes::makeIborIndex(s.index), s.switchStrike, s.accuracy,
                                                    s.maxIter, indexes::makeCurve(s.discount), s.type,
                                                    s.displacement, s.dontThrow, frequency, s.paymentLag);
    }

}
