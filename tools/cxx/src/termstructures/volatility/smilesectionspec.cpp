// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "termstructures/volatility/smilesectionspec.hpp"

#include <ql/termstructures/volatility/flatsmilesection.hpp>
#include <ql/termstructures/volatility/sabrsmilesection.hpp>
#include <ql/utilities/null.hpp>

#include <initializer_list>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness::termstructures {

    namespace {

        // The harness's own section, which overrides only what SmileSection leaves abstract, and volatilityType()
        // and shift() where the spec overrides them.
        class HarnessSmileSection : public SmileSection {
          public:
            HarnessSmileSection(const Date& d, const DayCounter& dc, const Date& referenceDate, VolatilityType type,
                                Real shift, const SmileSectionSpec& s)
            : SmileSection(d, dc, referenceDate, type, shift), s_(s) {}
            HarnessSmileSection(Time exerciseTime, const DayCounter& dc, VolatilityType type, Real shift,
                                const SmileSectionSpec& s)
            : SmileSection(exerciseTime, dc, type, shift), s_(s) {}
            Real minStrike() const override { return s_.minStrike; }
            Real maxStrike() const override { return s_.maxStrike; }
            Real atmLevel() const override { return s_.atmLevel; }
            VolatilityType volatilityType() const override {
                return s_.overriddenType ? *s_.overriddenType : SmileSection::volatilityType();
            }
            Rate shift() const override { return s_.overriddenShift ? *s_.overriddenShift : SmileSection::shift(); }

          protected:
            Volatility volatilityImpl(Rate k) const override { return s_.a + s_.b * k + s_.c * k * k; }

          private:
            SmileSectionSpec s_;
        };

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " spec needs " + name);
            return *v;
        }

        void onlyKeys(const json::object& o, const std::vector<const char*>& keys, const std::string& what) {
            for (const auto& kv : o) {
                bool known = false;
                for (const char* k : keys)
                    known = known || kv.key() == k;
                if (!known)
                    throw InvalidParams(what + " takes no key " + std::string(kv.key()));
            }
        }

    }

    SmileSectionSpec smileSection(const json::value& v) {
        if (!v.is_object())
            throw InvalidParams("expected a smile section spec, got " + json::serialize(v));
        const json::object& o = v.get_object();
        const json::value* type = o.if_contains("type");
        const json::value* form = o.if_contains("form");
        if (type == nullptr || !type->is_string() || form == nullptr || !form->is_string())
            throw InvalidParams("a smile section spec needs a type and a form: " + json::serialize(v));
        SmileSectionSpec s;
        s.type = std::string(type->get_string());
        s.form = std::string(form->get_string());
        const std::string what = s.type + " (" + s.form + ")";
        std::vector<const char*> keys{"type", "form", "dayCounter", "volatilityType", "shift"};
        if (s.type != "SabrSmileSection")
            keys.push_back("atmLevel");
        if (s.form == "date")
            keys.insert(keys.end(), {"exerciseDate", "referenceDate"});
        else if (s.form == "floating" && (s.type == "FlatSmileSection" || s.type == "SabrSmileSection"))
            keys.push_back("exerciseDate");
        else if (s.form == "time")
            keys.push_back("exerciseTime");
        else
            throw InvalidParams("unknown form for " + what);
        if (s.type == "FlatSmileSection")
            keys.push_back("volatility");
        else if (s.type == "HarnessSmileSection")
            keys.insert(keys.end(), {"smile", "minStrike", "maxStrike", "overriddenType", "overriddenShift"});
        else if (s.type == "SabrSmileSection")
            keys.insert(keys.end(), {"forward", "sabrParameters"});
        else
            throw InvalidParams("unknown smile section " + s.type);
        onlyKeys(o, keys, what);

        if (s.form == "time")
            s.exerciseTime = real(key(o, "exerciseTime", what));
        else
            s.exerciseDate = date(key(o, "exerciseDate", what));
        if (s.form == "date")
            s.referenceDate = date(key(o, "referenceDate", what));
        const json::value& dc = key(o, "dayCounter", what);
        s.hasDayCounter = !dc.is_null();
        if (s.hasDayCounter)
            s.dayCounter = dayCounter(dc);
        if (s.type != "SabrSmileSection")
            s.atmLevel = real(key(o, "atmLevel", what));
        s.volatilityType = enumeration<VolatilityType>(key(o, "volatilityType", what));
        s.shift = real(key(o, "shift", what));
        if (s.type == "FlatSmileSection") {
            s.volatility = real(key(o, "volatility", what));
        } else if (s.type == "SabrSmileSection") {
            s.forward = real(key(o, "forward", what));
            s.sabrParameters = reals(key(o, "sabrParameters", what));
            // QuantLib reads the first four unchecked.
            if (s.sabrParameters.size() < 4)
                throw InvalidParams(what + " needs at least 4 SABR parameters");
        } else {
            std::vector<Real> smile = reals(key(o, "smile", what));
            if (smile.size() != 3)
                throw InvalidParams(what + " needs a smile of 3 coefficients");
            s.a = smile[0];
            s.b = smile[1];
            s.c = smile[2];
            s.minStrike = real(key(o, "minStrike", what));
            s.maxStrike = real(key(o, "maxStrike", what));
            const json::value& t = key(o, "overriddenType", what);
            if (!t.is_null())
                s.overriddenType = enumeration<VolatilityType>(t);
            const json::value& sh = key(o, "overriddenShift", what);
            if (!sh.is_null())
                s.overriddenShift = real(sh);
        }
        return s;
    }

    ext::shared_ptr<SmileSection> makeSmileSection(const SmileSectionSpec& s) {
        DayCounter dc = s.hasDayCounter ? makeDayCounter(s.dayCounter) : DayCounter();
        if (s.type == "HarnessSmileSection") {
            if (s.form == "time")
                return ext::make_shared<HarnessSmileSection>(s.exerciseTime, dc, s.volatilityType, s.shift, s);
            return ext::make_shared<HarnessSmileSection>(makeDate(s.exerciseDate), dc, makeDate(s.referenceDate),
                                                         s.volatilityType, s.shift, s);
        }
        if (s.type == "SabrSmileSection") {
            if (s.form == "time")
                return ext::make_shared<SabrSmileSection>(s.exerciseTime, s.forward, s.sabrParameters, s.shift,
                                                          s.volatilityType);
            // The floating form passes Date(), QuantLib's default reference date.
            Date referenceDate = s.form == "date" ? makeDate(s.referenceDate) : Date();
            return ext::make_shared<SabrSmileSection>(makeDate(s.exerciseDate), s.forward, s.sabrParameters,
                                                      referenceDate, dc, s.shift, s.volatilityType);
        }
        if (s.form == "time")
            return ext::make_shared<FlatSmileSection>(s.exerciseTime, s.volatility, dc, s.atmLevel, s.volatilityType,
                                                      s.shift);
        // The floating form passes Date(), QuantLib's default reference date.
        Date referenceDate = s.form == "date" ? makeDate(s.referenceDate) : Date();
        return ext::make_shared<FlatSmileSection>(makeDate(s.exerciseDate), s.volatility, dc, referenceDate,
                                                  s.atmLevel, s.volatilityType, s.shift);
    }

    json::value nullableReal(Real x) { return x == Null<Real>() ? json::value(nullptr) : json::value(x); }

    json::value sectionSummary(const SmileSection& section, Real strike) {
        json::array out;
        out.emplace_back(result(section.exerciseDate()));
        out.emplace_back(section.exerciseTime());
        out.emplace_back(static_cast<int>(section.volatilityType()));
        out.emplace_back(section.shift());
        out.emplace_back(nullableReal(section.atmLevel()));
        out.emplace_back(section.volatility(strike));
        return json::value(std::move(out));
    }

}
