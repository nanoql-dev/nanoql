// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "termstructures/volatility/optionlet/optionletvolspec.hpp"

#include <ql/termstructures/volatility/optionlet/constantoptionletvol.hpp>
#include <ql/utilities/null.hpp>

#include <initializer_list>
#include <string>

using namespace QuantLib;

namespace harness::termstructures {

    namespace {

        // The harness's own optionlet structure, which overrides only what OptionletVolatilityStructure leaves
        // abstract, so that volatilityType() and displacement() are the base's defaults.
        class BaseOptionletVolatility : public OptionletVolatilityStructure {
          public:
            BaseOptionletVolatility(const Date& referenceDate, const Calendar& cal, BusinessDayConvention bdc,
                                    const DayCounter& dc, Volatility volatility)
            : OptionletVolatilityStructure(referenceDate, cal, bdc, dc), volatility_(volatility) {}
            Date maxDate() const override { return Date::maxDate(); }
            Rate minStrike() const override { return QL_MIN_REAL; }
            Rate maxStrike() const override { return QL_MAX_REAL; }

          protected:
            ext::shared_ptr<SmileSection> smileSectionImpl(Time) const override {
                QL_FAIL("the harness's base optionlet structure has no smile section");
            }
            Volatility volatilityImpl(Time, Rate) const override { return volatility_; }

          private:
            Volatility volatility_;
        };

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

    OptionletVolSpec optionletVolatility(const json::value& v) {
        if (!v.is_object())
            throw InvalidParams("expected an optionlet volatility spec, got " + json::serialize(v));
        const json::object& o = v.get_object();
        const json::value* type = o.if_contains("type");
        if (type == nullptr || !type->is_string())
            throw InvalidParams("an optionlet volatility spec needs a type: " + json::serialize(v));
        OptionletVolSpec s;
        s.type = std::string(type->get_string());
        const std::string& what = s.type;
        if (s.type == "ConstantOptionletVolatility") {
            bool hasReference = o.if_contains("referenceDate") != nullptr;
            bool hasDays = o.if_contains("settlementDays") != nullptr;
            if (hasReference == hasDays)
                throw InvalidParams(what + " spec needs one of referenceDate and settlementDays");
            s.fixed = hasReference;
            onlyKeys(o, {"type", s.fixed ? "referenceDate" : "settlementDays", "calendar", "convention", "volatility",
                         "dayCounter", "volatilityType", "displacement", "enableExtrapolation"},
                     what);
            s.volatilityType = enumeration<VolatilityType>(key(o, "volatilityType", what));
            s.displacement = real(key(o, "displacement", what));
        } else if (s.type == "BaseOptionletVolatility") {
            onlyKeys(o, {"type", "referenceDate", "calendar", "convention", "volatility", "dayCounter",
                         "enableExtrapolation"},
                     what);
        } else {
            throw InvalidParams("unknown optionlet volatility " + s.type);
        }
        if (s.fixed)
            s.referenceDate = date(key(o, "referenceDate", what));
        else
            s.settlementDays = natural(key(o, "settlementDays", what));
        s.calendar = indexes::calendar(key(o, "calendar", what));
        s.convention = enumeration<BusinessDayConvention>(key(o, "convention", what));
        s.volatility = real(key(o, "volatility", what));
        s.dayCounter = dayCounter(key(o, "dayCounter", what));
        s.enableExtrapolation = boolean(key(o, "enableExtrapolation", what));
        return s;
    }

    PeriodSpec tenor(const json::value& v) {
        if (!v.is_array() || v.get_array().size() != 2)
            throw InvalidParams("expected a tenor as [length, units], got " + json::serialize(v));
        return {integer(v.get_array()[0]), enumeration<TimeUnit>(v.get_array()[1])};
    }

    ext::shared_ptr<OptionletVolatilityStructure> makeOptionletVolatility(const OptionletVolSpec& s) {
        Calendar cal = indexes::makeCalendar(s.calendar);
        DayCounter dc = makeDayCounter(s.dayCounter);
        ext::shared_ptr<OptionletVolatilityStructure> out;
        if (s.type == "BaseOptionletVolatility")
            out = ext::make_shared<BaseOptionletVolatility>(makeDate(s.referenceDate), cal, s.convention, dc,
                                                            s.volatility);
        else if (s.fixed)
            out = ext::make_shared<ConstantOptionletVolatility>(makeDate(s.referenceDate), cal, s.convention,
                                                                s.volatility, dc, s.volatilityType,
                                                                s.displacement);
        else
            out = ext::make_shared<ConstantOptionletVolatility>(s.settlementDays, cal, s.convention, s.volatility,
                                                                dc, s.volatilityType, s.displacement);
        if (s.enableExtrapolation)
            out->enableExtrapolation();
        return out;
    }

    Period makePeriod(const PeriodSpec& p) { return {p.length, p.units}; }

}
