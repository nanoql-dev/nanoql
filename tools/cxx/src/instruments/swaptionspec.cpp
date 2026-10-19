// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "instruments/swaptionspec.hpp"

#include <ql/exercise.hpp>
#include <ql/instruments/vanillaswap.hpp>
#include <ql/pricingengines/swaption/blackswaptionengine.hpp>
#include <ql/quotes/simplequote.hpp>

#include <initializer_list>

using namespace QuantLib;

namespace harness::swaptions {

    namespace {

        const json::object& object(const json::value& v, const std::string& what) {
            if (!v.is_object())
                throw InvalidParams("expected " + what + " spec, got " + json::serialize(v));
            return v.get_object();
        }

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

        VanillaSpec vanilla(const json::value& v) {
            const std::string what = "a vanilla swap";
            const json::object& o = object(v, what);
            onlyKeys(o, {"kind", "type", "nominal", "fixedSchedule", "fixedRate", "fixedDayCount", "floatSchedule",
                         "index", "spread", "floatingDayCount", "paymentConvention", "useIndexedCoupons"},
                     what);
            VanillaSpec s;
            s.type = enumeration<Swap::Type>(key(o, "type", what));
            s.nominal = real(key(o, "nominal", what));
            s.fixedSchedule = cashflows::schedule(key(o, "fixedSchedule", what));
            s.fixedRate = real(key(o, "fixedRate", what));
            s.fixedDayCount = dayCounter(key(o, "fixedDayCount", what));
            s.floatSchedule = cashflows::schedule(key(o, "floatSchedule", what));
            s.index = indexes::iborIndex(key(o, "index", what));
            if (s.index.type != "IborIndex")
                throw InvalidParams("a vanilla swap's index is an IborIndex spec");
            s.spread = real(key(o, "spread", what));
            s.floatingDayCount = dayCounter(key(o, "floatingDayCount", what));
            const json::value& convention = key(o, "paymentConvention", what);
            if (!convention.is_null())
                s.paymentConvention = enumeration<BusinessDayConvention>(convention);
            s.useIndexedCoupons = optionalBoolean(key(o, "useIndexedCoupons", what));
            return s;
        }

    }

    SwaptionSpec swaption(const json::value& v) {
        const std::string what = "a swaption";
        const json::object& o = object(v, what);
        onlyKeys(o, {"swap", "exercise", "settlementType", "settlementMethod"}, what);
        SwaptionSpec s;
        const json::object& swap = object(key(o, "swap", what), "a swap");
        std::string kind = text(key(swap, "kind", "a swap"));
        if (kind == "VanillaSwap") {
            s.vanilla = vanilla(key(o, "swap", what));
        } else if (kind == "OvernightIndexedSwap") {
            s.overnight = true;
            s.ois = swaps::ois(key(o, "swap", what));
        } else {
            throw InvalidParams("unknown swap kind " + kind);
        }
        const json::object& exercise = object(key(o, "exercise", what), "an exercise");
        std::string type = text(key(exercise, "type", "an exercise"));
        if (type == "EuropeanExercise") {
            onlyKeys(exercise, {"type", "date"}, type);
            s.exerciseDates.push_back(date(key(exercise, "date", type)));
        } else if (type == "BermudanExercise") {
            onlyKeys(exercise, {"type", "dates", "payoffAtExpiry"}, type);
            s.bermudan = true;
            const json::value& dates = key(exercise, "dates", type);
            if (!dates.is_array())
                throw InvalidParams("expected an array of date serials, got " + json::serialize(dates));
            for (const json::value& d : dates.get_array())
                s.exerciseDates.push_back(date(d));
            s.payoffAtExpiry = boolean(key(exercise, "payoffAtExpiry", type));
        } else {
            throw InvalidParams("a swaption's exercise is European or Bermudan, not " + type);
        }
        s.settlementType = enumeration<Settlement::Type>(key(o, "settlementType", what));
        s.settlementMethod = enumeration<Settlement::Method>(key(o, "settlementMethod", what));
        return s;
    }

    EngineSpec engine(const json::value& v) {
        const std::string what = "an engine";
        const json::object& o = object(v, what);
        std::string type = text(key(o, "type", what));
        EngineSpec e;
        if (type == "BlackSwaptionEngine") {
            onlyKeys(o, {"type", "form", "vol", "dayCounter", "displacement", "model"}, type);
            e.displacement = real(key(o, "displacement", type));
        } else if (type == "BachelierSwaptionEngine") {
            onlyKeys(o, {"type", "form", "vol", "dayCounter", "model"}, type);
            e.black = false;
        } else {
            throw InvalidParams("unknown engine " + type);
        }
        std::string form = text(key(o, "form", type));
        if (form != "quote" && form != "volatility")
            throw InvalidParams("an engine's form is quote or volatility, not " + form);
        e.quote = form == "quote";
        e.vol = real(key(o, "vol", type));
        e.dayCounter = dayCounter(key(o, "dayCounter", type));
        e.model = integer(key(o, "model", type));
        return e;
    }

    ext::shared_ptr<Swaption> makeSwaption(const SwaptionSpec& s) {
        ext::shared_ptr<FixedVsFloatingSwap> swap;
        if (s.overnight) {
            swap = swaps::makeOis(s.ois);
        } else {
            const VanillaSpec& u = s.vanilla;
            swap = ext::make_shared<VanillaSwap>(
                u.type, u.nominal, cashflows::makeSchedule(u.fixedSchedule), u.fixedRate,
                makeDayCounter(u.fixedDayCount), cashflows::makeSchedule(u.floatSchedule),
                indexes::makeIborIndex(u.index), u.spread, makeDayCounter(u.floatingDayCount), u.paymentConvention,
                u.useIndexedCoupons);
        }
        std::vector<Date> dates;
        for (const DateSpec& d : s.exerciseDates)
            dates.push_back(makeDate(d));
        ext::shared_ptr<Exercise> exercise;
        if (s.bermudan)
            exercise = ext::make_shared<BermudanExercise>(dates, s.payoffAtExpiry);
        else
            exercise = ext::make_shared<EuropeanExercise>(dates.front());
        return ext::make_shared<Swaption>(swap, exercise, s.settlementType, s.settlementMethod);
    }

    ext::shared_ptr<PricingEngine> makeEngine(const EngineSpec& e, const indexes::CurveSpec& curve) {
        Handle<YieldTermStructure> discount = indexes::makeCurve(curve);
        DayCounter dc = makeDayCounter(e.dayCounter);
        if (e.black) {
            auto model = static_cast<BlackSwaptionEngine::CashAnnuityModel>(e.model);
            if (e.quote)
                return ext::make_shared<BlackSwaptionEngine>(
                    discount, Handle<Quote>(ext::make_shared<SimpleQuote>(e.vol)), dc, e.displacement, model);
            return ext::make_shared<BlackSwaptionEngine>(discount, e.vol, dc, e.displacement, model);
        }
        auto model = static_cast<BachelierSwaptionEngine::CashAnnuityModel>(e.model);
        if (e.quote)
            return ext::make_shared<BachelierSwaptionEngine>(
                discount, Handle<Quote>(ext::make_shared<SimpleQuote>(e.vol)), dc, model);
        return ext::make_shared<BachelierSwaptionEngine>(discount, e.vol, dc, model);
    }

}
