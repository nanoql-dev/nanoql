// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "cashflows/inflationspec.hpp"

#include <ql/cashflows/cpicouponpricer.hpp>
#include <ql/indexes/inflation/ukrpi.hpp>
#include <ql/math/interpolations/linearinterpolation.hpp>
#include <ql/termstructures/inflation/interpolatedyoyinflationcurve.hpp>
#include <ql/termstructures/inflation/interpolatedzeroinflationcurve.hpp>
#include <ql/utilities/null.hpp>

#include <initializer_list>

using namespace QuantLib;

namespace harness::cashflows {

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

        Handle<ZeroInflationTermStructure> zeroHandle(const InflationCurveSpec& s) {
            if (s.null)
                return {};
            if (!s.zero)
                throw InvalidParams("a zero-inflation index takes a zero-inflation curve");
            std::vector<Date> dates;
            for (const auto& d : s.dates)
                dates.push_back(makeDate(d));
            auto c = ext::make_shared<InterpolatedZeroInflationCurve<Linear>>(
                makeDate(s.referenceDate), dates, s.rates, s.frequency, makeDayCounter(s.dayCounter));
            if (s.enableExtrapolation)
                c->enableExtrapolation();
            return Handle<ZeroInflationTermStructure>(c);
        }

        Handle<YoYInflationTermStructure> yoyHandle(const InflationCurveSpec& s) {
            if (s.null)
                return {};
            if (s.zero)
                throw InvalidParams("a year-on-year index takes a year-on-year curve");
            std::vector<Date> dates;
            for (const auto& d : s.dates)
                dates.push_back(makeDate(d));
            auto c = ext::make_shared<InterpolatedYoYInflationCurve<Linear>>(
                makeDate(s.referenceDate), dates, s.rates, s.frequency, makeDayCounter(s.dayCounter));
            if (s.enableExtrapolation)
                c->enableExtrapolation();
            return Handle<YoYInflationTermStructure>(c);
        }

    }

    InflationCurveSpec inflationCurve(const json::value& v) {
        InflationCurveSpec s;
        if (v.is_null())
            return s;
        const std::string what = "an inflation curve";
        const json::object& o = object(v, what);
        onlyKeys(o, {"type", "referenceDate", "dates", "rates", "frequency", "dayCounter", "seasonality",
                     "enableExtrapolation"},
                 what);
        s.null = false;
        std::string type = text(key(o, "type", what));
        if (type != "ZeroInflationCurve" && type != "YoYInflationCurve")
            throw InvalidParams("unknown inflation curve " + type);
        s.zero = type == "ZeroInflationCurve";
        s.referenceDate = date(key(o, "referenceDate", what));
        const json::value& dates = key(o, "dates", what);
        if (!dates.is_array())
            throw InvalidParams("expected the curve's dates as an array, got " + json::serialize(dates));
        for (const auto& d : dates.get_array())
            s.dates.push_back(date(d));
        s.rates = reals(key(o, "rates", what));
        s.frequency = enumeration<Frequency>(key(o, "frequency", what));
        s.dayCounter = dayCounter(key(o, "dayCounter", what));
        if (!key(o, "seasonality", what).is_null())
            throw InvalidParams("the harness's inflation curves take no seasonality");
        s.enableExtrapolation = boolean(key(o, "enableExtrapolation", what));
        return s;
    }

    InflationIndexSpec inflationIndex(const json::value& v) {
        const std::string what = "an inflation index";
        const json::object& o = object(v, what);
        InflationIndexSpec s;
        s.type = text(key(o, "type", what));
        if (s.type == "UKRPI" || s.type == "YYUKRPI") {
            onlyKeys(o, {"type", "curve"}, s.type);
        } else if (s.type == "YoYRatio") {
            onlyKeys(o, {"type", "underlying", "curve"}, s.type);
            const json::object& u = object(key(o, "underlying", s.type), "an underlying index");
            onlyKeys(u, {"type", "curve"}, "an underlying index");
            if (text(key(u, "type", "an underlying index")) != "UKRPI" || !key(u, "curve", "an underlying index").is_null())
                throw InvalidParams("the harness's ratio index is on UK RPI with no curve");
        } else {
            throw InvalidParams("unknown inflation index " + s.type);
        }
        s.curve = inflationCurve(key(o, "curve", s.type));
        return s;
    }

    ext::shared_ptr<ZeroInflationIndex> makeZeroIndex(const InflationIndexSpec& s) {
        if (s.type != "UKRPI")
            throw InvalidParams("expected a zero-inflation index, got " + s.type);
        return ext::make_shared<UKRPI>(zeroHandle(s.curve));
    }

    ext::shared_ptr<YoYInflationIndex> makeYoYIndex(const InflationIndexSpec& s) {
        if (s.type == "YYUKRPI")
            return ext::make_shared<YYUKRPI>(yoyHandle(s.curve));
        if (s.type == "YoYRatio")
            return ext::make_shared<YoYInflationIndex>(ext::make_shared<UKRPI>(), yoyHandle(s.curve));
        throw InvalidParams("expected a year-on-year index, got " + s.type);
    }

    LagPeriod lagPeriod(const json::value& v) {
        if (!v.is_array() || v.get_array().size() != 2)
            throw InvalidParams("expected a period as [length, units], got " + json::serialize(v));
        LagPeriod p;
        p.length = integer(v.get_array()[0]);
        p.units = enumeration<TimeUnit>(v.get_array()[1]);
        return p;
    }

    Period makePeriod(const LagPeriod& p) { return {p.length, p.units}; }

    YoYCouponSpec yoyCoupon(const json::value& v) {
        const std::string what = "a year-on-year coupon";
        const json::object& o = object(v, what);
        onlyKeys(o, {"paymentDate", "nominal", "startDate", "endDate", "fixingDays", "observationLag", "interpolation",
                     "dayCounter", "gearing", "spread"},
                 what);
        YoYCouponSpec s;
        s.paymentDate = date(key(o, "paymentDate", what));
        s.nominal = real(key(o, "nominal", what));
        s.startDate = date(key(o, "startDate", what));
        s.endDate = date(key(o, "endDate", what));
        s.fixingDays = integer(key(o, "fixingDays", what));
        if (s.fixingDays == Null<Integer>() || s.fixingDays < 0)
            throw InvalidParams("expected the fixing days as a Natural");
        s.lag = lagPeriod(key(o, "observationLag", what));
        s.interpolation = enumeration<CPI::InterpolationType>(key(o, "interpolation", what));
        s.dayCounter = dayCounter(key(o, "dayCounter", what));
        s.gearing = real(key(o, "gearing", what));
        s.spread = real(key(o, "spread", what));
        return s;
    }

    ext::shared_ptr<YoYInflationCoupon> makeYoYCoupon(const YoYCouponSpec& s,
                                                      const ext::shared_ptr<YoYInflationIndex>& index) {
        return ext::make_shared<YoYInflationCoupon>(makeDate(s.paymentDate), s.nominal, makeDate(s.startDate),
                                                    makeDate(s.endDate), static_cast<Natural>(s.fixingDays), index,
                                                    makePeriod(s.lag), s.interpolation, makeDayCounter(s.dayCounter),
                                                    s.gearing, s.spread);
    }

    CPICouponSpec cpiCoupon(const json::value& v) {
        const std::string what = "a CPI coupon";
        const json::object& o = object(v, what);
        onlyKeys(o, {"baseCPI", "baseDate", "paymentDate", "nominal", "startDate", "endDate", "observationLag",
                     "interpolation", "dayCounter", "fixedRate"},
                 what);
        CPICouponSpec s;
        s.baseCPI = real(key(o, "baseCPI", what));
        s.baseDate = date(key(o, "baseDate", what));
        s.paymentDate = date(key(o, "paymentDate", what));
        s.nominal = real(key(o, "nominal", what));
        s.startDate = date(key(o, "startDate", what));
        s.endDate = date(key(o, "endDate", what));
        s.lag = lagPeriod(key(o, "observationLag", what));
        s.interpolation = enumeration<CPI::InterpolationType>(key(o, "interpolation", what));
        s.dayCounter = dayCounter(key(o, "dayCounter", what));
        s.fixedRate = real(key(o, "fixedRate", what));
        return s;
    }

    ext::shared_ptr<CPICoupon> makeCPICoupon(const CPICouponSpec& s, const ext::shared_ptr<ZeroInflationIndex>& index) {
        return ext::make_shared<CPICoupon>(s.baseCPI, makeDate(s.baseDate), makeDate(s.paymentDate), s.nominal,
                                           makeDate(s.startDate), makeDate(s.endDate), index, makePeriod(s.lag),
                                           s.interpolation, makeDayCounter(s.dayCounter), s.fixedRate);
    }

    YoYLegSpec yoyLeg(const json::value& v) {
        const std::string what = "a year-on-year leg";
        const json::object& o = object(v, what);
        onlyKeys(o, {"schedule", "paymentCalendar", "observationLag", "interpolation", "notionals", "paymentDayCounter",
                     "paymentAdjustment", "fixingDays", "gearings", "spreads"},
                 what);
        YoYLegSpec s;
        s.schedule = schedule(key(o, "schedule", what));
        s.paymentCalendar = indexes::calendar(key(o, "paymentCalendar", what));
        s.lag = lagPeriod(key(o, "observationLag", what));
        s.interpolation = enumeration<CPI::InterpolationType>(key(o, "interpolation", what));
        s.notionals = reals(key(o, "notionals", what));
        s.paymentDayCounter = dayCounter(key(o, "paymentDayCounter", what));
        s.paymentAdjustment = enumeration<BusinessDayConvention>(key(o, "paymentAdjustment", what));
        s.fixingDays = integers(key(o, "fixingDays", what));
        s.gearings = reals(key(o, "gearings", what));
        s.spreads = reals(key(o, "spreads", what));
        return s;
    }

    Leg makeYoYLeg(const YoYLegSpec& s, const ext::shared_ptr<YoYInflationIndex>& index) {
        std::vector<Natural> days;
        for (Integer n : s.fixingDays) {
            if (n == Null<Integer>() || n < 0)
                throw InvalidParams("expected fixing days as Naturals");
            days.push_back(static_cast<Natural>(n));
        }
        return yoyInflationLeg(makeSchedule(s.schedule), indexes::makeCalendar(s.paymentCalendar), index,
                               makePeriod(s.lag), s.interpolation)
            .withNotionals(s.notionals)
            .withPaymentDayCounter(makeDayCounter(s.paymentDayCounter))
            .withPaymentAdjustment(s.paymentAdjustment)
            .withFixingDays(days)
            .withGearings(s.gearings)
            .withSpreads(s.spreads);
    }

    InflationPricerSpec inflationPricer(const json::value& v) {
        const std::string what = "an inflation coupon pricer";
        const json::object& o = object(v, what);
        onlyKeys(o, {"type", "volatility", "nominalCurve"}, what);
        InflationPricerSpec s;
        s.type = text(key(o, "type", what));
        const bool cpi = s.type == "CPICouponPricer";
        if (!cpi && s.type != "YoYInflationCouponPricer" && s.type != "BlackYoYInflationCouponPricer" &&
            s.type != "UnitDisplacedBlackYoYInflationCouponPricer" && s.type != "BachelierYoYInflationCouponPricer")
            throw InvalidParams("unknown inflation coupon pricer " + s.type);
        const json::value& vol = key(o, "volatility", what);
        s.hasVolatility = !vol.is_null();
        if (s.hasVolatility)
            s.volatility = termstructures::inflationVolatility(
                vol, cpi ? "ConstantCPIVolatility" : "ConstantYoYOptionletVolatility");
        s.nominalCurve = indexes::curve(key(o, "nominalCurve", what));
        return s;
    }

    ext::shared_ptr<InflationCouponPricer> makeInflationPricer(const InflationPricerSpec& s) {
        Handle<YieldTermStructure> nominal = indexes::makeCurve(s.nominalCurve);
        if (s.type == "CPICouponPricer") {
            if (!s.hasVolatility)
                return ext::make_shared<CPICouponPricer>(nominal);
            return ext::make_shared<CPICouponPricer>(
                Handle<CPIVolatilitySurface>(termstructures::makeCPIVolatility(s.volatility)), nominal);
        }
        Handle<YoYOptionletVolatilitySurface> vol;
        if (s.hasVolatility)
            vol = Handle<YoYOptionletVolatilitySurface>(termstructures::makeYoYVolatility(s.volatility));
        if (s.type == "YoYInflationCouponPricer")
            return s.hasVolatility ? ext::make_shared<YoYInflationCouponPricer>(vol, nominal)
                                   : ext::make_shared<YoYInflationCouponPricer>(nominal);
        if (s.type == "BlackYoYInflationCouponPricer")
            return s.hasVolatility ? ext::make_shared<BlackYoYInflationCouponPricer>(vol, nominal)
                                   : ext::make_shared<BlackYoYInflationCouponPricer>(nominal);
        if (s.type == "UnitDisplacedBlackYoYInflationCouponPricer")
            return s.hasVolatility ? ext::make_shared<UnitDisplacedBlackYoYInflationCouponPricer>(vol, nominal)
                                   : ext::make_shared<UnitDisplacedBlackYoYInflationCouponPricer>(nominal);
        return s.hasVolatility ? ext::make_shared<BachelierYoYInflationCouponPricer>(vol, nominal)
                               : ext::make_shared<BachelierYoYInflationCouponPricer>(nominal);
    }

}
