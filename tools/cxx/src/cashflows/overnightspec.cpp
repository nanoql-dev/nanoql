// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "cashflows/overnightspec.hpp"

#include <ql/cashflows/blackovernightindexedcouponpricer.hpp>
#include <ql/termstructures/volatility/optionlet/constantoptionletvol.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/utilities/null.hpp>

#include <initializer_list>

using namespace QuantLib;

namespace harness::overnight {

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

        Natural natural(const json::value& v) {
            Integer n = integer(v);
            if (n == Null<Integer>() || n < 0)
                throw InvalidParams("expected a Natural, got " + json::serialize(v));
            return static_cast<Natural>(n);
        }

        std::optional<Natural> optionalNatural(const json::value& v) {
            if (v.is_null())
                return std::nullopt;
            return natural(v);
        }

        std::optional<Integer> optionalInteger(const json::value& v) {
            if (v.is_null())
                return std::nullopt;
            Integer n = integer(v);
            if (n == Null<Integer>())
                throw InvalidParams("expected an Integer, got " + json::serialize(v));
            return n;
        }

        std::optional<DayCounterSpec> optionalDayCounter(const json::value& v) {
            if (v.is_null())
                return std::nullopt;
            return dayCounter(v);
        }

        std::optional<indexes::CalendarSpec> optionalCalendar(const json::value& v) {
            if (v.is_null())
                return std::nullopt;
            return indexes::calendar(v);
        }

        std::optional<std::pair<Integer, TimeUnit>> optionalPeriod(const json::value& v) {
            if (v.is_null())
                return std::nullopt;
            if (!v.is_array() || v.get_array().size() != 2)
                throw InvalidParams("expected a period as [length, units], got " + json::serialize(v));
            return std::make_pair(integer(v.get_array()[0]), enumeration<TimeUnit>(v.get_array()[1]));
        }

        std::vector<DateSpec> dates(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected an array of dates, got " + json::serialize(v));
            std::vector<DateSpec> out;
            for (const json::value& d : v.get_array())
                out.push_back(date(d));
            return out;
        }

        std::vector<Date> makeDates(const std::vector<DateSpec>& d) {
            std::vector<Date> out;
            for (const auto& x : d)
                out.push_back(makeDate(x));
            return out;
        }

        std::vector<bool> booleans(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected an array of booleans, got " + json::serialize(v));
            std::vector<bool> out;
            for (const json::value& b : v.get_array())
                out.push_back(boolean(b));
            return out;
        }

        indexes::IndexSpec overnightIndex(const json::value& v) {
            indexes::IndexSpec s = indexes::iborIndex(v);
            if (s.type != "OvernightIndex")
                throw InvalidParams("expected an OvernightIndex spec, got " + json::serialize(v));
            return s;
        }

        ext::shared_ptr<OvernightIndex> makeOvernightIndex(const indexes::IndexSpec& s) {
            auto index = ext::dynamic_pointer_cast<OvernightIndex>(indexes::makeIborIndex(s));
            if (!index)
                throw InvalidParams("the index " + s.type + " is no overnight index");
            return index;
        }

        VolSpec vol(const json::value& v) {
            VolSpec s;
            if (v.is_null())
                return s;
            const json::object& o = object(v, "a volatility");
            onlyKeys(o, {"referenceDate", "calendar", "convention", "volatility", "dayCounter", "type", "displacement"},
                     "a volatility");
            s.null = false;
            s.referenceDate = date(key(o, "referenceDate", "a volatility"));
            s.calendar = indexes::calendar(key(o, "calendar", "a volatility"));
            s.convention = enumeration<BusinessDayConvention>(key(o, "convention", "a volatility"));
            s.volatility = real(key(o, "volatility", "a volatility"));
            s.dayCounter = dayCounter(key(o, "dayCounter", "a volatility"));
            s.type = enumeration<VolatilityType>(key(o, "type", "a volatility"));
            s.displacement = real(key(o, "displacement", "a volatility"));
            return s;
        }

        Handle<OptionletVolatilityStructure> makeVol(const VolSpec& s) {
            if (s.null)
                return {};
            return Handle<OptionletVolatilityStructure>(ext::make_shared<ConstantOptionletVolatility>(
                makeDate(s.referenceDate), indexes::makeCalendar(s.calendar), s.convention, s.volatility,
                makeDayCounter(s.dayCounter), s.type, s.displacement));
        }

        CappedSpec capped(const json::value& v) {
            const json::object& o = object(v, "a capped coupon");
            onlyKeys(o, {"underlying", "cap", "floor", "nakedOption", "dailyCapFloor"}, "a capped coupon");
            CappedSpec s;
            s.underlying = coupon(key(o, "underlying", "a capped coupon"));
            s.cap = real(key(o, "cap", "a capped coupon"));
            s.floor = real(key(o, "floor", "a capped coupon"));
            s.nakedOption = boolean(key(o, "nakedOption", "a capped coupon"));
            s.dailyCapFloor = boolean(key(o, "dailyCapFloor", "a capped coupon"));
            return s;
        }

        ScheduleSpec schedule(const json::value& v) {
            const json::object& o = object(v, "a schedule");
            ScheduleSpec s;
            if (o.if_contains("dates") != nullptr) {
                onlyKeys(o, {"dates", "calendar", "tenor", "isRegular"}, "a dates schedule");
                s.dates = true;
                s.dateList = dates(key(o, "dates", "a dates schedule"));
                s.calendar = optionalCalendar(key(o, "calendar", "a dates schedule"));
                s.tenor = optionalPeriod(key(o, "tenor", "a dates schedule"));
                s.isRegular = booleans(key(o, "isRegular", "a dates schedule"));
                return s;
            }
            onlyKeys(o, {"effectiveDate", "terminationDate", "tenor", "calendar", "convention",
                         "terminationDateConvention", "rule", "endOfMonth", "firstDate", "nextToLastDate"},
                     "a schedule");
            s.effectiveDate = date(key(o, "effectiveDate", "a schedule"));
            s.terminationDate = date(key(o, "terminationDate", "a schedule"));
            s.tenor = optionalPeriod(key(o, "tenor", "a schedule"));
            if (!s.tenor)
                throw InvalidParams("a rule-based schedule needs a tenor");
            s.calendar = indexes::calendar(key(o, "calendar", "a schedule"));
            s.convention = enumeration<BusinessDayConvention>(key(o, "convention", "a schedule"));
            s.terminationDateConvention =
                enumeration<BusinessDayConvention>(key(o, "terminationDateConvention", "a schedule"));
            s.rule = enumeration<DateGeneration::Rule>(key(o, "rule", "a schedule"));
            s.endOfMonth = boolean(key(o, "endOfMonth", "a schedule"));
            s.firstDate = date(key(o, "firstDate", "a schedule"));
            s.nextToLastDate = date(key(o, "nextToLastDate", "a schedule"));
            return s;
        }

        Schedule makeSchedule(const ScheduleSpec& s) {
            if (s.dates) {
                Calendar cal = s.calendar ? indexes::makeCalendar(*s.calendar) : Calendar(NullCalendar());
                std::optional<Period> tenor;
                if (s.tenor)
                    tenor = Period(s.tenor->first, s.tenor->second);
                return Schedule(makeDates(s.dateList), cal, Unadjusted, std::nullopt, tenor, std::nullopt,
                                std::nullopt, s.isRegular);
            }
            return Schedule(makeDate(s.effectiveDate), makeDate(s.terminationDate),
                            Period(s.tenor->first, s.tenor->second), indexes::makeCalendar(*s.calendar),
                            s.convention, s.terminationDateConvention, s.rule, s.endOfMonth, makeDate(s.firstDate),
                            makeDate(s.nextToLastDate));
        }

    }

    CouponSpec coupon(const json::value& v) {
        const json::object& o = object(v, "a coupon");
        onlyKeys(o, {"paymentDate", "nominal", "startDate", "endDate", "index", "gearing", "spread", "refPeriodStart",
                     "refPeriodEnd", "dayCounter", "telescopicValueDates", "averagingMethod", "lookbackDays",
                     "lockoutDays", "applyObservationShift", "compoundSpreadDaily", "rateComputationStartDate",
                     "rateComputationEndDate", "exCouponDate", "roundingPrecision"},
                 "a coupon");
        CouponSpec s;
        s.paymentDate = date(key(o, "paymentDate", "a coupon"));
        s.nominal = real(key(o, "nominal", "a coupon"));
        s.startDate = date(key(o, "startDate", "a coupon"));
        s.endDate = date(key(o, "endDate", "a coupon"));
        s.index = overnightIndex(key(o, "index", "a coupon"));
        s.gearing = real(key(o, "gearing", "a coupon"));
        s.spread = real(key(o, "spread", "a coupon"));
        s.refPeriodStart = date(key(o, "refPeriodStart", "a coupon"));
        s.refPeriodEnd = date(key(o, "refPeriodEnd", "a coupon"));
        s.dayCounter = optionalDayCounter(key(o, "dayCounter", "a coupon"));
        if (boolean(key(o, "telescopicValueDates", "a coupon")))
            throw InvalidParams("the harness builds no telescopic coupon: the telescopic rule takes their "
                                "expected values from the wheel");
        s.averagingMethod = enumeration<RateAveraging::Type>(key(o, "averagingMethod", "a coupon"));
        s.lookbackDays = optionalNatural(key(o, "lookbackDays", "a coupon"));
        s.lockoutDays = natural(key(o, "lockoutDays", "a coupon"));
        s.applyObservationShift = boolean(key(o, "applyObservationShift", "a coupon"));
        s.compoundSpreadDaily = boolean(key(o, "compoundSpreadDaily", "a coupon"));
        s.rateComputationStartDate = date(key(o, "rateComputationStartDate", "a coupon"));
        s.rateComputationEndDate = date(key(o, "rateComputationEndDate", "a coupon"));
        s.exCouponDate = date(key(o, "exCouponDate", "a coupon"));
        s.roundingPrecision = optionalInteger(key(o, "roundingPrecision", "a coupon"));
        return s;
    }

    AnyCouponSpec anyCoupon(const json::value& v) {
        AnyCouponSpec s;
        const json::object& o = object(v, "a coupon");
        if (o.if_contains("underlying") != nullptr) {
            s.capped = true;
            s.spec = capped(v);
        } else {
            s.spec.underlying = coupon(v);
        }
        return s;
    }

    PricerSpec pricer(const json::value& v) {
        const json::object& o = object(v, "a pricer");
        PricerSpec s;
        s.type = text(key(o, "type", "a pricer"));
        if (s.type == "ArithmeticAveraged") {
            onlyKeys(o, {"type", "vol", "effectiveVolatilityInput", "meanReversion", "volatility", "byApprox"},
                     s.type);
            s.meanReversion = real(key(o, "meanReversion", s.type));
            s.volatility = real(key(o, "volatility", s.type));
            s.byApprox = boolean(key(o, "byApprox", s.type));
        } else if (s.type == "Compounding" || s.type == "BlackCompounding" || s.type == "BlackAveraging") {
            onlyKeys(o, {"type", "vol", "effectiveVolatilityInput"}, s.type);
        } else {
            throw InvalidParams("unknown pricer " + s.type);
        }
        s.vol = vol(key(o, "vol", s.type));
        s.effectiveVolatilityInput = boolean(key(o, "effectiveVolatilityInput", s.type));
        return s;
    }

    LegSpec leg(const json::value& v) {
        const json::object& o = object(v, "a leg");
        onlyKeys(o, {"schedule", "index", "notionals", "paymentDayCounter", "paymentConvention", "paymentCalendar",
                     "paymentLag", "gearings", "spreads", "telescopicValueDates", "averagingMethod", "lookbackDays",
                     "lockoutDays", "applyObservationShift", "compoundSpreadDaily", "caps", "floors", "dailyCapFloor",
                     "inArrears", "nakedOption", "paymentDates", "roundingPrecision", "lastRecentPeriod",
                     "lastRecentPeriodCalendar", "couponPricer"},
                 "a leg");
        LegSpec s;
        s.schedule = schedule(key(o, "schedule", "a leg"));
        s.index = overnightIndex(key(o, "index", "a leg"));
        s.notionals = reals(key(o, "notionals", "a leg"));
        s.paymentDayCounter = optionalDayCounter(key(o, "paymentDayCounter", "a leg"));
        s.paymentConvention = enumeration<BusinessDayConvention>(key(o, "paymentConvention", "a leg"));
        s.paymentCalendar = optionalCalendar(key(o, "paymentCalendar", "a leg"));
        s.paymentLag = integer(key(o, "paymentLag", "a leg"));
        s.gearings = reals(key(o, "gearings", "a leg"));
        s.spreads = reals(key(o, "spreads", "a leg"));
        if (boolean(key(o, "telescopicValueDates", "a leg")))
            throw InvalidParams("the harness builds no telescopic leg: the telescopic rule takes their "
                                "expected values from the wheel");
        s.averagingMethod = enumeration<RateAveraging::Type>(key(o, "averagingMethod", "a leg"));
        s.lookbackDays = optionalNatural(key(o, "lookbackDays", "a leg"));
        s.lockoutDays = natural(key(o, "lockoutDays", "a leg"));
        s.applyObservationShift = boolean(key(o, "applyObservationShift", "a leg"));
        s.compoundSpreadDaily = boolean(key(o, "compoundSpreadDaily", "a leg"));
        s.caps = reals(key(o, "caps", "a leg"));
        s.floors = reals(key(o, "floors", "a leg"));
        s.dailyCapFloor = boolean(key(o, "dailyCapFloor", "a leg"));
        s.inArrears = boolean(key(o, "inArrears", "a leg"));
        s.nakedOption = boolean(key(o, "nakedOption", "a leg"));
        s.paymentDates = dates(key(o, "paymentDates", "a leg"));
        s.roundingPrecision = optionalInteger(key(o, "roundingPrecision", "a leg"));
        if (const json::value* p = o.if_contains("lastRecentPeriod")) {
            s.hasLastRecentPeriod = true;
            s.lastRecentPeriod = optionalPeriod(*p);
        }
        if (const json::value* c = o.if_contains("lastRecentPeriodCalendar"))
            s.lastRecentPeriodCalendar = optionalCalendar(*c);
        if (const json::value* p = o.if_contains("couponPricer")) {
            if (!p->is_null())
                s.couponPricer = pricer(*p);
        }
        return s;
    }

    ext::shared_ptr<OvernightIndexedCoupon> makeCoupon(const CouponSpec& s) {
        DayCounter dc = s.dayCounter ? makeDayCounter(*s.dayCounter) : DayCounter();
        return ext::make_shared<OvernightIndexedCoupon>(
            makeDate(s.paymentDate), s.nominal, makeDate(s.startDate), makeDate(s.endDate),
            makeOvernightIndex(s.index), s.gearing, s.spread, makeDate(s.refPeriodStart), makeDate(s.refPeriodEnd), dc,
            false, s.averagingMethod, s.lookbackDays ? *s.lookbackDays : Null<Natural>(), s.lockoutDays,
            s.applyObservationShift, s.compoundSpreadDaily, makeDate(s.rateComputationStartDate),
            makeDate(s.rateComputationEndDate), makeDate(s.exCouponDate), s.roundingPrecision);
    }

    ext::shared_ptr<FloatingRateCoupon> makeAnyCoupon(const AnyCouponSpec& s) {
        auto underlying = makeCoupon(s.spec.underlying);
        if (!s.capped)
            return underlying;
        return ext::make_shared<CappedFlooredOvernightIndexedCoupon>(underlying, s.spec.cap, s.spec.floor,
                                                                     s.spec.nakedOption, s.spec.dailyCapFloor);
    }

    ext::shared_ptr<OvernightIndexedCouponPricer> makePricer(const PricerSpec& s) {
        Handle<OptionletVolatilityStructure> v = makeVol(s.vol);
        if (s.type == "Compounding")
            return ext::make_shared<CompoundingOvernightIndexedCouponPricer>(v, s.effectiveVolatilityInput);
        if (s.type == "ArithmeticAveraged")
            return ext::make_shared<ArithmeticAveragedOvernightIndexedCouponPricer>(
                s.meanReversion, s.volatility, s.byApprox, v, s.effectiveVolatilityInput);
        if (s.type == "BlackCompounding")
            return ext::make_shared<BlackCompoundingOvernightIndexedCouponPricer>(v, s.effectiveVolatilityInput);
        if (s.type == "BlackAveraging")
            return ext::make_shared<BlackAveragingOvernightIndexedCouponPricer>(v, s.effectiveVolatilityInput);
        throw InvalidParams("the pricer " + s.type + " was not checked");
    }

    Leg makeLeg(const LegSpec& s) {
        OvernightLeg leg(makeSchedule(s.schedule), makeOvernightIndex(s.index));
        leg.withNotionals(s.notionals)
            .withPaymentAdjustment(s.paymentConvention)
            .withPaymentLag(s.paymentLag)
            .withGearings(s.gearings)
            .withSpreads(s.spreads)
            .withTelescopicValueDates(false)
            .withAveragingMethod(s.averagingMethod)
            .withLockoutDays(s.lockoutDays)
            .withObservationShift(s.applyObservationShift)
            .compoundingSpreadDaily(s.compoundSpreadDaily)
            .withCaps(s.caps)
            .withFloors(s.floors)
            .withDailyCapFloor(s.dailyCapFloor)
            .inArrears(s.inArrears)
            .withNakedOption(s.nakedOption)
            .withPaymentDates(makeDates(s.paymentDates));
        if (s.paymentDayCounter)
            leg.withPaymentDayCounter(makeDayCounter(*s.paymentDayCounter));
        if (s.paymentCalendar)
            leg.withPaymentCalendar(indexes::makeCalendar(*s.paymentCalendar));
        if (s.lookbackDays)
            leg.withLookbackDays(*s.lookbackDays);
        if (s.roundingPrecision)
            leg.withRoundingPrecision(*s.roundingPrecision);
        if (s.hasLastRecentPeriod) {
            std::optional<Period> p;
            if (s.lastRecentPeriod)
                p = Period(s.lastRecentPeriod->first, s.lastRecentPeriod->second);
            leg.withLastRecentPeriod(p);
        }
        if (s.lastRecentPeriodCalendar)
            leg.withLastRecentPeriodCalendar(indexes::makeCalendar(*s.lastRecentPeriodCalendar));
        if (s.couponPricer)
            leg.withCouponPricer(makePricer(*s.couponPricer));
        return leg;
    }

    json::value pricerName(const ext::shared_ptr<FloatingRateCouponPricer>& p) {
        if (!p)
            return nullptr;
        if (ext::dynamic_pointer_cast<BlackCompoundingOvernightIndexedCouponPricer>(p))
            return "BlackCompoundingOvernightIndexedCouponPricer";
        if (ext::dynamic_pointer_cast<BlackAveragingOvernightIndexedCouponPricer>(p))
            return "BlackAveragingOvernightIndexedCouponPricer";
        if (ext::dynamic_pointer_cast<CompoundingOvernightIndexedCouponPricer>(p))
            return "CompoundingOvernightIndexedCouponPricer";
        if (ext::dynamic_pointer_cast<ArithmeticAveragedOvernightIndexedCouponPricer>(p))
            return "ArithmeticAveragedOvernightIndexedCouponPricer";
        return "?";
    }

}
