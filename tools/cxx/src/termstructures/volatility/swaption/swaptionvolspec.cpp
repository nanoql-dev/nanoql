// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "termstructures/volatility/swaption/swaptionvolspec.hpp"

#include <ql/termstructures/volatility/flatsmilesection.hpp>
#include <ql/termstructures/volatility/swaption/swaptionconstantvol.hpp>
#include <ql/utilities/null.hpp>

#include <initializer_list>
#include <string>

using namespace QuantLib;

namespace harness::termstructures {

    namespace {

        // The harness's own swaption structure, which overrides only what SwaptionVolatilityStructure leaves
        // abstract.
        class BaseSwaptionVolatility : public SwaptionVolatilityStructure {
          public:
            BaseSwaptionVolatility(const Date& referenceDate, const Calendar& cal, BusinessDayConvention bdc,
                                   const DayCounter& dc, Volatility volatility, const Period& maxSwapTenor)
            : SwaptionVolatilityStructure(referenceDate, cal, bdc, dc), volatility_(volatility),
              maxSwapTenor_(maxSwapTenor) {}
            Date maxDate() const override { return Date::maxDate(); }
            Rate minStrike() const override { return QL_MIN_REAL; }
            Rate maxStrike() const override { return QL_MAX_REAL; }
            const Period& maxSwapTenor() const override { return maxSwapTenor_; }

          protected:
            ext::shared_ptr<SmileSection> smileSectionImpl(Time optionTime, Time swapLength) const override {
                return ext::make_shared<FlatSmileSection>(optionTime, volatilityImpl(optionTime, swapLength, 0.0),
                                                          dayCounter());
            }
            Volatility volatilityImpl(Time optionTime, Time swapLength, Rate strike) const override {
                return volatility_ + 0.01 * optionTime + 0.001 * swapLength + 0.1 * strike;
            }

          private:
            Volatility volatility_;
            Period maxSwapTenor_;
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

    SwaptionVolSpec swaptionVolatility(const json::value& v) {
        if (!v.is_object())
            throw InvalidParams("expected a swaption volatility spec, got " + json::serialize(v));
        const json::object& o = v.get_object();
        const json::value* type = o.if_contains("type");
        if (type == nullptr || !type->is_string())
            throw InvalidParams("a swaption volatility spec needs a type: " + json::serialize(v));
        SwaptionVolSpec s;
        s.type = std::string(type->get_string());
        const std::string& what = s.type;
        if (s.type == "ConstantSwaptionVolatility") {
            bool hasReference = o.if_contains("referenceDate") != nullptr;
            bool hasDays = o.if_contains("settlementDays") != nullptr;
            if (hasReference == hasDays)
                throw InvalidParams(what + " spec needs one of referenceDate and settlementDays");
            s.fixed = hasReference;
            onlyKeys(o, {"type", s.fixed ? "referenceDate" : "settlementDays", "calendar", "convention", "volatility",
                         "dayCounter", "volatilityType", "shift", "enableExtrapolation"},
                     what);
            s.volatilityType = enumeration<VolatilityType>(key(o, "volatilityType", what));
            s.shift = real(key(o, "shift", what));
        } else if (s.type == "BaseSwaptionVolatility") {
            onlyKeys(o, {"type", "referenceDate", "calendar", "convention", "volatility", "dayCounter", "maxSwapTenor",
                         "enableExtrapolation"},
                     what);
            s.maxSwapTenor = tenor(key(o, "maxSwapTenor", what));
        } else {
            throw InvalidParams("unknown swaption volatility " + s.type);
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

    ext::shared_ptr<SwaptionVolatilityStructure> makeSwaptionVolatility(const SwaptionVolSpec& s) {
        Calendar cal = indexes::makeCalendar(s.calendar);
        DayCounter dc = makeDayCounter(s.dayCounter);
        ext::shared_ptr<SwaptionVolatilityStructure> out;
        if (s.type == "BaseSwaptionVolatility")
            out = ext::make_shared<BaseSwaptionVolatility>(makeDate(s.referenceDate), cal, s.convention, dc,
                                                           s.volatility, makePeriod(s.maxSwapTenor));
        else if (s.fixed)
            out = ext::make_shared<ConstantSwaptionVolatility>(makeDate(s.referenceDate), cal, s.convention,
                                                               s.volatility, dc, s.volatilityType, s.shift);
        else
            out = ext::make_shared<ConstantSwaptionVolatility>(s.settlementDays, cal, s.convention, s.volatility, dc,
                                                               s.volatilityType, s.shift);
        if (s.enableExtrapolation)
            out->enableExtrapolation();
        return out;
    }

}
