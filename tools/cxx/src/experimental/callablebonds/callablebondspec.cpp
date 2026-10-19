// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "experimental/callablebonds/callablebondspec.hpp"

#include <ql/experimental/callablebonds/blackcallablebondengine.hpp>
#include <ql/experimental/callablebonds/treecallablebondengine.hpp>
#include <ql/handle.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/flatsmilesection.hpp>
#include <ql/time/schedule.hpp>
#include <ql/utilities/null.hpp>

#include <initializer_list>

using namespace QuantLib;

namespace harness::callablebonds {

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

        const json::array& array(const json::value& v, const std::string& what) {
            if (!v.is_array())
                throw InvalidParams("expected " + what + ", got " + json::serialize(v));
            return v.get_array();
        }

        Natural natural(const json::value& v) {
            Integer n = integer(v);
            if (n == Null<Integer>() || n < 0)
                throw InvalidParams("expected a Natural, got " + json::serialize(v));
            return static_cast<Natural>(n);
        }

        Size size(const json::value& v) {
            Integer n = integer(v);
            if (n == Null<Integer>() || n < 0)
                throw InvalidParams("expected a Size, got " + json::serialize(v));
            return static_cast<Size>(n);
        }

        std::optional<models::CurveSpec> optionalCurve(const json::value& v) {
            if (v.is_null())
                return std::nullopt;
            return models::curve(v);
        }

        // The harness's subclass: it overrides only what CallableBondVolatilityStructure leaves abstract.
        class HarnessCallableBondVolatility : public CallableBondVolatilityStructure {
          public:
            HarnessCallableBondVolatility(const Date& referenceDate, Volatility vol, const DayCounter& dc,
                                          const Period& maxBondTenor, Rate minStrike, Rate maxStrike)
            : CallableBondVolatilityStructure(referenceDate, Calendar(), dc), vol_(vol), maxBondTenor_(maxBondTenor),
              minStrike_(minStrike), maxStrike_(maxStrike) {}
            Date maxDate() const override { return Date::maxDate(); }
            const Period& maxBondTenor() const override { return maxBondTenor_; }
            Rate minStrike() const override { return minStrike_; }
            Rate maxStrike() const override { return maxStrike_; }

          protected:
            ext::shared_ptr<SmileSection> smileSectionImpl(Time optionTime, Time) const override {
                return ext::make_shared<FlatSmileSection>(optionTime, vol_, dayCounter());
            }
            Volatility volatilityImpl(Time, Time, Rate) const override { return vol_; }

          private:
            Volatility vol_;
            Period maxBondTenor_;
            Rate minStrike_, maxStrike_;
        };

        Handle<YieldTermStructure> curveHandle(const std::optional<models::CurveSpec>& s) {
            if (!s)
                return {};
            return Handle<YieldTermStructure>(models::makeCurve(*s));
        }

    }

    CallabilitySpec callability(const json::value& v) {
        const std::string what = "a callability";
        const json::object& o = object(v, what);
        onlyKeys(o, {"price", "type", "date"}, what);
        return {bonds::price(key(o, "price", what)), enumeration<Callability::Type>(key(o, "type", what)),
                date(key(o, "date", what))};
    }

    CallableBondSpec bond(const json::value& v) {
        const std::string what = "a callable bond";
        const json::object& o = object(v, what);
        CallableBondSpec s;
        s.type = text(key(o, "type", what));
        if (s.type == "CallableFixedRateBond") {
            onlyKeys(o, {"type", "settlementDays", "faceAmount", "schedule", "coupons", "accrualDayCounter",
                         "paymentConvention", "redemption", "issueDate", "putCallSchedule", "exCouponPeriod"},
                     s.type);
            s.schedule = bonds::schedule(key(o, "schedule", s.type));
            s.coupons = reals(key(o, "coupons", s.type));
            s.dayCounter = bonds::optionalDayCounter(key(o, "accrualDayCounter", s.type));
            const json::value& ex = key(o, "exCouponPeriod", s.type);
            if (!ex.is_null()) {
                const std::string exWhat = "an ex-coupon period";
                const json::object& e = object(ex, exWhat);
                onlyKeys(e, {"period", "calendar", "convention", "endOfMonth"}, exWhat);
                const json::array& period = array(key(e, "period", exWhat), "a period as [length, units]");
                if (period.size() != 2)
                    throw InvalidParams("expected a period as [length, units]");
                s.exCoupon = true;
                s.exCouponLength = integer(period[0]);
                s.exCouponUnits = enumeration<TimeUnit>(period[1]);
                s.exCouponCalendar = bonds::optionalCalendar(key(e, "calendar", exWhat));
                s.exCouponConvention = enumeration<BusinessDayConvention>(key(e, "convention", exWhat));
                s.exCouponEndOfMonth = boolean(key(e, "endOfMonth", exWhat));
            }
        } else if (s.type == "CallableZeroCouponBond") {
            onlyKeys(o, {"type", "settlementDays", "faceAmount", "calendar", "maturityDate", "dayCounter",
                         "paymentConvention", "redemption", "issueDate", "putCallSchedule"},
                     s.type);
            s.calendar = bonds::optionalCalendar(key(o, "calendar", s.type));
            s.maturityDate = date(key(o, "maturityDate", s.type));
            s.dayCounter = bonds::optionalDayCounter(key(o, "dayCounter", s.type));
        } else {
            throw InvalidParams("unknown callable bond " + s.type);
        }
        s.settlementDays = natural(key(o, "settlementDays", s.type));
        s.faceAmount = real(key(o, "faceAmount", s.type));
        s.paymentConvention = enumeration<BusinessDayConvention>(key(o, "paymentConvention", s.type));
        s.redemption = real(key(o, "redemption", s.type));
        s.issueDate = date(key(o, "issueDate", s.type));
        for (const json::value& c : array(key(o, "putCallSchedule", s.type), "a list of callabilities"))
            s.putCallSchedule.push_back(callability(c));
        return s;
    }

    ConstantVolSpec constantVol(const json::value& v) {
        const std::string what = "a callable-bond volatility";
        const json::object& o = object(v, what);
        ConstantVolSpec s;
        if (const json::value* type = o.if_contains("type")) {
            if (text(*type) != "HarnessCallableBondVolatility")
                throw InvalidParams("unknown callable-bond volatility " + json::serialize(*type));
            onlyKeys(o, {"type", "referenceDate", "volatility", "dayCounter", "maxBondTenor", "minStrike", "maxStrike"},
                     what);
            s.harness = true;
            s.referenceDate = date(key(o, "referenceDate", what));
            const json::array& tenor = array(key(o, "maxBondTenor", what), "a period as [length, units]");
            if (tenor.size() != 2)
                throw InvalidParams("expected a period as [length, units]");
            s.maxBondTenorLength = integer(tenor[0]);
            s.maxBondTenorUnits = enumeration<TimeUnit>(tenor[1]);
            s.minStrike = real(key(o, "minStrike", what));
            s.maxStrike = real(key(o, "maxStrike", what));
        } else if (o.if_contains("referenceDate") != nullptr) {
            onlyKeys(o, {"referenceDate", "volatility", "dayCounter"}, what);
            s.referenceDate = date(key(o, "referenceDate", what));
        } else {
            onlyKeys(o, {"settlementDays", "calendar", "volatility", "dayCounter"}, what);
            s.moving = true;
            s.settlementDays = natural(key(o, "settlementDays", what));
            s.calendar = indexes::calendar(key(o, "calendar", what));
        }
        s.volatility = real(key(o, "volatility", what));
        s.dayCounter = dayCounter(key(o, "dayCounter", what));
        return s;
    }

    EngineSpec engine(const json::value& v) {
        const std::string what = "a callable-bond engine";
        const json::object& o = object(v, what);
        EngineSpec s;
        s.type = text(key(o, "type", what));
        if (s.type == "TreeSteps" || s.type == "TreeZeroSteps") {
            onlyKeys(o, {"type", "model", "timeSteps", "termStructure"}, s.type);
            s.model = models::model(key(o, "model", s.type));
            s.timeSteps = size(key(o, "timeSteps", s.type));
            s.termStructure = optionalCurve(key(o, "termStructure", s.type));
        } else if (s.type == "TreeGrid" || s.type == "TreeLattice" || s.type == "TreeZeroGrid") {
            onlyKeys(o, {"type", "model", "timeGrid", "termStructure"}, s.type);
            s.model = models::model(key(o, "model", s.type));
            s.grid = timeGrid(key(o, "timeGrid", s.type));
            s.termStructure = optionalCurve(key(o, "termStructure", s.type));
        } else if (s.type == "Black" || s.type == "BlackZero") {
            onlyKeys(o, {"type", "volatility", "discountCurve"}, s.type);
            s.volatility = real(key(o, "volatility", s.type));
            s.discountCurve = models::curve(key(o, "discountCurve", s.type));
        } else if (s.type == "BlackStructure" || s.type == "BlackZeroStructure") {
            onlyKeys(o, {"type", "volatility", "discountCurve"}, s.type);
            s.structure = constantVol(key(o, "volatility", s.type));
            s.discountCurve = models::curve(key(o, "discountCurve", s.type));
        } else {
            throw InvalidParams("unknown callable-bond engine " + s.type);
        }
        return s;
    }

    ext::shared_ptr<Callability> makeCallability(const CallabilitySpec& s) {
        return ext::make_shared<Callability>(bonds::makePrice(s.price), s.type, makeDate(s.date));
    }

    ext::shared_ptr<CallableBond> makeBond(const CallableBondSpec& s) {
        CallabilitySchedule calls;
        for (const CallabilitySpec& c : s.putCallSchedule)
            calls.push_back(makeCallability(c));
        if (s.type == "CallableFixedRateBond") {
            Period exPeriod = s.exCoupon ? Period(s.exCouponLength, s.exCouponUnits) : Period();
            return ext::make_shared<CallableFixedRateBond>(
                s.settlementDays, s.faceAmount, bonds::makeSchedule(s.schedule), s.coupons,
                bonds::makeDayCounter(s.dayCounter), s.paymentConvention, s.redemption, makeDate(s.issueDate), calls,
                exPeriod, bonds::makeCalendar(s.exCouponCalendar), s.exCouponConvention, s.exCouponEndOfMonth);
        }
        return ext::make_shared<CallableZeroCouponBond>(s.settlementDays, s.faceAmount, bonds::makeCalendar(s.calendar),
                                                        makeDate(s.maturityDate), bonds::makeDayCounter(s.dayCounter),
                                                        s.paymentConvention, s.redemption, makeDate(s.issueDate),
                                                        calls);
    }

    ext::shared_ptr<CallableBondVolatilityStructure> makeVolatility(const ConstantVolSpec& s) {
        if (s.harness)
            return ext::make_shared<HarnessCallableBondVolatility>(
                makeDate(s.referenceDate), s.volatility, makeDayCounter(s.dayCounter),
                Period(s.maxBondTenorLength, s.maxBondTenorUnits), s.minStrike, s.maxStrike);
        if (s.moving)
            return ext::make_shared<CallableBondConstantVolatility>(
                s.settlementDays, indexes::makeCalendar(s.calendar), s.volatility, makeDayCounter(s.dayCounter));
        return ext::make_shared<CallableBondConstantVolatility>(makeDate(s.referenceDate), s.volatility,
                                                                makeDayCounter(s.dayCounter));
    }

    ext::shared_ptr<PricingEngine> makeEngine(const EngineSpec& s) {
        if (s.type == "Black" || s.type == "BlackZero") {
            Handle<Quote> vol(ext::make_shared<SimpleQuote>(s.volatility));
            Handle<YieldTermStructure> curve(models::makeCurve(s.discountCurve));
            if (s.type == "BlackZero")
                return ext::make_shared<BlackCallableZeroCouponBondEngine>(vol, curve);
            return ext::make_shared<BlackCallableFixedRateBondEngine>(vol, curve);
        }
        if (s.type == "BlackStructure" || s.type == "BlackZeroStructure") {
            Handle<CallableBondVolatilityStructure> vol(makeVolatility(s.structure));
            Handle<YieldTermStructure> curve(models::makeCurve(s.discountCurve));
            if (s.type == "BlackZeroStructure")
                return ext::make_shared<BlackCallableZeroCouponBondEngine>(vol, curve);
            return ext::make_shared<BlackCallableFixedRateBondEngine>(vol, curve);
        }
        ext::shared_ptr<ShortRateModel> model = models::makeModel(s.model);
        Handle<YieldTermStructure> ts = curveHandle(s.termStructure);
        if (s.type == "TreeSteps")
            return ext::make_shared<TreeCallableFixedRateBondEngine>(model, s.timeSteps, ts);
        if (s.type == "TreeZeroSteps")
            return ext::make_shared<TreeCallableZeroCouponBondEngine>(model, s.timeSteps, ts);
        if (s.type == "TreeZeroGrid")
            return ext::make_shared<TreeCallableZeroCouponBondEngine>(model, makeTimeGrid(s.grid), ts);
        return ext::make_shared<TreeCallableFixedRateBondEngine>(model, makeTimeGrid(s.grid), ts);
    }

    ext::shared_ptr<CallableBond> makePricedBond(const CallableBondSpec& b, const EngineSpec& e) {
        auto bond = makeBond(b);
        bond->setPricingEngine(makeEngine(e));
        return bond;
    }

}
