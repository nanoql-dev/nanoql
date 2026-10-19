// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "termstructures/yield/ratehelperspec.hpp"

#include <ql/currencies/america.hpp>
#include <ql/currencies/europe.hpp>
#include <ql/indexes/ibor/euribor.hpp>
#include <ql/indexes/ibor/tibor.hpp>
#include <ql/math/interpolations/loginterpolation.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/yield/discountcurve.hpp>
#include <ql/utilities/null.hpp>

#include <initializer_list>
#include <variant>

using namespace QuantLib;

namespace harness::termstructures {

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

        std::pair<Integer, TimeUnit> period(const json::value& v) {
            if (!v.is_array() || v.get_array().size() != 2)
                throw InvalidParams("expected a period as [length, units], got " + json::serialize(v));
            return {integer(v.get_array()[0]), enumeration<TimeUnit>(v.get_array()[1])};
        }

        Natural natural(const json::value& v) {
            if (v.is_null())
                return Null<Natural>();
            Integer n = integer(v);
            if (n == Null<Integer>() || n < 0)
                throw InvalidParams("expected a Natural, got " + json::serialize(v));
            return static_cast<Natural>(n);
        }

        const char* const presetsWithTenor[] = {"Euribor", "Euribor365", "Tibor"};
        const char* const presets[] = {"Euribor1W", "Euribor1M", "Euribor3M", "Euribor6M", "Euribor1Y"};

    }

    HelperIndexSpec helperIndex(const json::value& v) {
        const json::object& o = object(v, "an index");
        HelperIndexSpec s;
        const std::string type = text(key(o, "type", "an index"));
        for (const char* p : presetsWithTenor) {
            if (type == p) {
                onlyKeys(o, {"type", "tenor", "curve"}, type);
                s.preset = type;
                s.tenor = period(key(o, "tenor", type));
                s.curve = indexes::curve(key(o, "curve", type));
                return s;
            }
        }
        for (const char* p : presets) {
            if (type == p) {
                onlyKeys(o, {"type", "curve"}, type);
                s.preset = type;
                s.curve = indexes::curve(key(o, "curve", type));
                return s;
            }
        }
        const json::value* currency = o.if_contains("currency");
        if (currency == nullptr) {
            s.index = indexes::iborIndex(v);
            return s;
        }
        s.currency = text(*currency);
        if (s.currency != "EUR" && s.currency != "USD")
            throw InvalidParams("unknown currency " + s.currency);
        json::value rest = v;
        rest.as_object().erase("currency");
        s.index = indexes::iborIndex(rest);
        if (s.index.type != "IborIndex")
            throw InvalidParams("a currency is given for an IborIndex only");
        return s;
    }

    ext::shared_ptr<IborIndex> makeHelperIndex(const HelperIndexSpec& s) {
        if (s.preset.empty()) {
            if (s.currency.empty())
                return indexes::makeIborIndex(s.index);
            Currency currency = s.currency == "EUR" ? Currency(EURCurrency()) : Currency(USDCurrency());
            const indexes::IndexSpec& i = s.index;
            return ext::make_shared<IborIndex>(i.name, Period(i.tenorLength, i.tenorUnits), i.settlementDays,
                                               currency, indexes::makeCalendar(i.fixingCalendar), i.convention,
                                               i.endOfMonth, makeDayCounter(i.dayCounter),
                                               indexes::makeCurve(i.curve));
        }
        Handle<YieldTermStructure> h = indexes::makeCurve(s.curve);
        Period tenor(s.tenor.first, s.tenor.second);
        if (s.preset == "Euribor")
            return ext::make_shared<Euribor>(tenor, h);
        if (s.preset == "Euribor365")
            return ext::make_shared<Euribor365>(tenor, h);
        if (s.preset == "Tibor")
            return ext::make_shared<Tibor>(tenor, h);
        if (s.preset == "Euribor1W")
            return ext::make_shared<Euribor1W>(h);
        if (s.preset == "Euribor1M")
            return ext::make_shared<Euribor1M>(h);
        if (s.preset == "Euribor3M")
            return ext::make_shared<Euribor3M>(h);
        if (s.preset == "Euribor6M")
            return ext::make_shared<Euribor6M>(h);
        if (s.preset == "Euribor1Y")
            return ext::make_shared<Euribor1Y>(h);
        throw InvalidParams("the index " + s.preset + " was not checked");
    }

    RateHelperSpec rateHelper(const json::value& v) {
        const json::object& o = object(v, "a rate helper");
        RateHelperSpec s;
        s.kind = text(key(o, "kind", "a rate helper"));
        s.form = text(key(o, "form", "a rate helper"));
        const std::string what = s.kind + " (" + s.form + ")";
        if (s.kind == "DepositRateHelper") {
            if (s.form == "tenor") {
                onlyKeys(o, {"kind", "form", "rate", "tenor", "fixingDays", "calendar", "convention", "endOfMonth",
                             "dayCounter"},
                         what);
                s.tenor = period(key(o, "tenor", what));
                s.fixingDays = natural(key(o, "fixingDays", what));
                if (s.fixingDays == Null<Natural>())
                    throw InvalidParams(what + " needs fixing days");
                s.calendar = indexes::calendar(key(o, "calendar", what));
                s.convention = enumeration<BusinessDayConvention>(key(o, "convention", what));
                s.endOfMonth = boolean(key(o, "endOfMonth", what));
                s.dayCounter = dayCounter(key(o, "dayCounter", what));
            } else if (s.form == "index") {
                onlyKeys(o, {"kind", "form", "rate", "index"}, what);
                s.index = helperIndex(key(o, "index", what));
            } else if (s.form == "fixingDate") {
                onlyKeys(o, {"kind", "form", "rate", "fixingDate", "index"}, what);
                s.fixingDate = date(key(o, "fixingDate", what));
                s.index = helperIndex(key(o, "index", what));
            } else {
                throw InvalidParams("unknown form " + what);
            }
        } else if (s.kind == "SwapRateHelper") {
            if (s.form == "tenor") {
                onlyKeys(o, {"kind", "form", "rate", "tenor", "calendar", "fixedFrequency", "fixedConvention",
                             "fixedDayCount", "index", "spread", "fwdStart", "discountingCurve", "settlementDays",
                             "pillar", "customPillarDate", "endOfMonth", "useIndexedCoupons", "floatConvention",
                             "couponPricer"},
                         what);
                s.tenor = period(key(o, "tenor", what));
                s.fwdStart = period(key(o, "fwdStart", what));
                s.settlementDays = natural(key(o, "settlementDays", what));
            } else if (s.form == "dates") {
                onlyKeys(o, {"kind", "form", "rate", "startDate", "endDate", "calendar", "fixedFrequency",
                             "fixedConvention", "fixedDayCount", "index", "spread", "discountingCurve", "pillar",
                             "customPillarDate", "endOfMonth", "useIndexedCoupons", "floatConvention",
                             "couponPricer"},
                         what);
                s.startDate = date(key(o, "startDate", what));
                s.endDate = date(key(o, "endDate", what));
            } else {
                throw InvalidParams("unknown form " + what);
            }
            const json::value& calendar = key(o, "calendar", what);
            s.emptyCalendar = calendar.is_null();
            if (!s.emptyCalendar)
                s.calendar = indexes::calendar(calendar);
            s.fixedFrequency = enumeration<Frequency>(key(o, "fixedFrequency", what));
            s.convention = enumeration<BusinessDayConvention>(key(o, "fixedConvention", what));
            const json::value& dc = key(o, "fixedDayCount", what);
            s.emptyDayCounter = dc.is_null();
            if (!s.emptyDayCounter)
                s.dayCounter = dayCounter(dc);
            s.index = helperIndex(key(o, "index", what));
            s.spread = real(key(o, "spread", what));
            s.discountingCurve = indexes::curve(key(o, "discountingCurve", what));
            s.pillar = enumeration<Pillar::Choice>(key(o, "pillar", what));
            s.customPillarDate = date(key(o, "customPillarDate", what));
            s.endOfMonth = boolean(key(o, "endOfMonth", what));
            s.useIndexedCoupons = optionalBoolean(key(o, "useIndexedCoupons", what));
            const json::value& floatConvention = key(o, "floatConvention", what);
            if (!floatConvention.is_null())
                s.floatConvention = enumeration<BusinessDayConvention>(floatConvention);
            if (const json::value* pricer = o.if_contains("couponPricer"))
                s.couponPricer = cashflows::pricer(*pricer);
        } else if (s.kind == "FraRateHelper") {
            const bool conventions = s.form == "months" || s.form == "periodLength";
            if (s.form == "months") {
                onlyKeys(o, {"kind", "form", "rate", "monthsToStart", "monthsToEnd", "fixingDays", "calendar",
                             "convention", "endOfMonth", "dayCounter", "pillar", "customPillarDate",
                             "useIndexedCoupon"},
                         what);
                s.monthsToStart = natural(key(o, "monthsToStart", what));
                s.monthsToEnd = natural(key(o, "monthsToEnd", what));
            } else if (s.form == "periodLength") {
                onlyKeys(o, {"kind", "form", "rate", "periodToStart", "lengthInMonths", "fixingDays", "calendar",
                             "convention", "endOfMonth", "dayCounter", "pillar", "customPillarDate",
                             "useIndexedCoupon"},
                         what);
                s.periodToStart = period(key(o, "periodToStart", what));
                s.lengthInMonths = natural(key(o, "lengthInMonths", what));
            } else if (s.form == "monthsIndex") {
                onlyKeys(o, {"kind", "form", "rate", "monthsToStart", "index", "pillar", "customPillarDate",
                             "useIndexedCoupon"},
                         what);
                s.monthsToStart = natural(key(o, "monthsToStart", what));
            } else if (s.form == "periodIndex") {
                onlyKeys(o, {"kind", "form", "rate", "periodToStart", "index", "pillar", "customPillarDate",
                             "useIndexedCoupon"},
                         what);
                s.periodToStart = period(key(o, "periodToStart", what));
            } else if (s.form == "imm") {
                onlyKeys(o, {"kind", "form", "rate", "immOffsetStart", "immOffsetEnd", "index", "pillar",
                             "customPillarDate", "useIndexedCoupon"},
                         what);
                s.immOffsetStart = natural(key(o, "immOffsetStart", what));
                s.immOffsetEnd = natural(key(o, "immOffsetEnd", what));
            } else if (s.form == "dates") {
                onlyKeys(o, {"kind", "form", "rate", "startDate", "endDate", "index", "pillar", "customPillarDate",
                             "useIndexedCoupon"},
                         what);
                s.startDate = date(key(o, "startDate", what));
                s.endDate = date(key(o, "endDate", what));
            } else {
                throw InvalidParams("unknown form " + what);
            }
            if (conventions) {
                s.fixingDays = natural(key(o, "fixingDays", what));
                s.calendar = indexes::calendar(key(o, "calendar", what));
                s.convention = enumeration<BusinessDayConvention>(key(o, "convention", what));
                s.endOfMonth = boolean(key(o, "endOfMonth", what));
                s.dayCounter = dayCounter(key(o, "dayCounter", what));
            } else {
                s.index = helperIndex(key(o, "index", what));
            }
            s.pillar = enumeration<Pillar::Choice>(key(o, "pillar", what));
            s.customPillarDate = date(key(o, "customPillarDate", what));
            s.useIndexedCoupon = boolean(key(o, "useIndexedCoupon", what));
        } else if (s.kind == "FuturesRateHelper") {
            if (s.form == "length") {
                onlyKeys(o, {"kind", "form", "rate", "iborStartDate", "lengthInMonths", "calendar", "convention",
                             "endOfMonth", "dayCounter", "convexityAdjustment", "type", "quotes"},
                         what);
                s.lengthInMonths = natural(key(o, "lengthInMonths", what));
                s.calendar = indexes::calendar(key(o, "calendar", what));
                s.convention = enumeration<BusinessDayConvention>(key(o, "convention", what));
                s.endOfMonth = boolean(key(o, "endOfMonth", what));
                s.dayCounter = dayCounter(key(o, "dayCounter", what));
            } else if (s.form == "dates") {
                onlyKeys(o, {"kind", "form", "rate", "iborStartDate", "iborEndDate", "dayCounter",
                             "convexityAdjustment", "type", "quotes"},
                         what);
                s.endDate = date(key(o, "iborEndDate", what));
                s.dayCounter = dayCounter(key(o, "dayCounter", what));
            } else if (s.form == "index") {
                onlyKeys(o, {"kind", "form", "rate", "iborStartDate", "index", "convexityAdjustment", "type",
                             "quotes"},
                         what);
                s.index = helperIndex(key(o, "index", what));
            } else {
                throw InvalidParams("unknown form " + what);
            }
            s.startDate = date(key(o, "iborStartDate", what));
            s.convexityAdjustment = real(key(o, "convexityAdjustment", what));
            s.futuresType = enumeration<Futures::Type>(key(o, "type", what));
            if (const json::value* quotes = o.if_contains("quotes")) {
                if (!quotes->is_array() || quotes->get_array().size() != 2)
                    throw InvalidParams("expected quotes as [price, convexity adjustment], got " +
                                        json::serialize(*quotes));
                s.priceQuote = boolean(quotes->get_array()[0]);
                s.convexityQuote = boolean(quotes->get_array()[1]);
            }
        } else if (s.kind == "FxSwapRateHelper") {
            if (s.form == "tenor") {
                onlyKeys(o, {"kind", "form", "rate", "spotFx", "tenor", "fixingDays", "calendar", "convention",
                             "endOfMonth", "isFxBaseCurrencyCollateralCurrency", "collateralCurve",
                             "tradingCalendar"},
                         what);
                s.tenor = period(key(o, "tenor", what));
                s.fixingDays = natural(key(o, "fixingDays", what));
                s.calendar = indexes::calendar(key(o, "calendar", what));
                s.convention = enumeration<BusinessDayConvention>(key(o, "convention", what));
                s.endOfMonth = boolean(key(o, "endOfMonth", what));
                const json::value& trading = key(o, "tradingCalendar", what);
                s.emptyTradingCalendar = trading.is_null();
                if (!s.emptyTradingCalendar)
                    s.tradingCalendar = indexes::calendar(trading);
            } else if (s.form == "dates") {
                onlyKeys(o, {"kind", "form", "rate", "spotFx", "startDate", "endDate",
                             "isFxBaseCurrencyCollateralCurrency", "collateralCurve"},
                         what);
                s.startDate = date(key(o, "startDate", what));
                s.endDate = date(key(o, "endDate", what));
            } else {
                throw InvalidParams("unknown form " + what);
            }
            s.spotFx = real(key(o, "spotFx", what));
            s.isFxBaseCurrencyCollateralCurrency = boolean(key(o, "isFxBaseCurrencyCollateralCurrency", what));
            s.collateralCurve = indexes::curve(key(o, "collateralCurve", what));
        } else {
            throw InvalidParams("unknown rate helper " + s.kind);
        }
        s.rate = real(key(o, "rate", what));
        return s;
    }

    namespace {

        ext::shared_ptr<RateHelper> makeFraRateHelper(const RateHelperSpec& s) {
            Date custom = makeDate(s.customPillarDate);
            if (s.form == "months")
                return ext::make_shared<FraRateHelper>(
                    s.rate, s.monthsToStart, s.monthsToEnd, s.fixingDays, indexes::makeCalendar(s.calendar),
                    s.convention, s.endOfMonth, makeDayCounter(s.dayCounter), s.pillar, custom, s.useIndexedCoupon);
            if (s.form == "periodLength")
                return ext::make_shared<FraRateHelper>(
                    s.rate, Period(s.periodToStart.first, s.periodToStart.second), s.lengthInMonths, s.fixingDays,
                    indexes::makeCalendar(s.calendar), s.convention, s.endOfMonth, makeDayCounter(s.dayCounter),
                    s.pillar, custom, s.useIndexedCoupon);
            ext::shared_ptr<IborIndex> index = makeHelperIndex(s.index);
            if (s.form == "monthsIndex")
                return ext::make_shared<FraRateHelper>(s.rate, s.monthsToStart, index, s.pillar, custom,
                                                       s.useIndexedCoupon);
            if (s.form == "periodIndex")
                return ext::make_shared<FraRateHelper>(
                    s.rate, Period(s.periodToStart.first, s.periodToStart.second), index, s.pillar, custom,
                    s.useIndexedCoupon);
            if (s.form == "imm")
                return ext::make_shared<FraRateHelper>(s.rate, s.immOffsetStart, s.immOffsetEnd, index, s.pillar,
                                                       custom, s.useIndexedCoupon);
            return ext::make_shared<FraRateHelper>(s.rate, makeDate(s.startDate), makeDate(s.endDate), index,
                                                   s.pillar, custom, s.useIndexedCoupon);
        }

        std::variant<Real, Handle<Quote>> quoteOrValue(Real value, bool quote) {
            if (quote)
                return Handle<Quote>(ext::make_shared<SimpleQuote>(value));
            return value;
        }

        ext::shared_ptr<RateHelper> makeFuturesRateHelper(const RateHelperSpec& s) {
            std::variant<Real, Handle<Quote>> price = quoteOrValue(s.rate, s.priceQuote);
            std::variant<Real, Handle<Quote>> convexity = quoteOrValue(s.convexityAdjustment, s.convexityQuote);
            Date start = makeDate(s.startDate);
            if (s.form == "length")
                return ext::make_shared<FuturesRateHelper>(price, start, s.lengthInMonths,
                                                           indexes::makeCalendar(s.calendar), s.convention,
                                                           s.endOfMonth, makeDayCounter(s.dayCounter), convexity,
                                                           s.futuresType);
            if (s.form == "dates")
                return ext::make_shared<FuturesRateHelper>(price, start, makeDate(s.endDate),
                                                           makeDayCounter(s.dayCounter), convexity, s.futuresType);
            return ext::make_shared<FuturesRateHelper>(price, start, makeHelperIndex(s.index), convexity,
                                                       s.futuresType);
        }

        ext::shared_ptr<RateHelper> makeFxSwapRateHelper(const RateHelperSpec& s) {
            Handle<Quote> points(ext::make_shared<SimpleQuote>(s.rate));
            Handle<Quote> spot(ext::make_shared<SimpleQuote>(s.spotFx));
            Handle<YieldTermStructure> collateral = indexes::makeCurve(s.collateralCurve);
            if (s.form == "tenor")
                return ext::make_shared<FxSwapRateHelper>(
                    points, spot, Period(s.tenor.first, s.tenor.second), s.fixingDays,
                    indexes::makeCalendar(s.calendar), s.convention, s.endOfMonth,
                    s.isFxBaseCurrencyCollateralCurrency, collateral,
                    s.emptyTradingCalendar ? Calendar() : indexes::makeCalendar(s.tradingCalendar));
            return ext::make_shared<FxSwapRateHelper>(points, spot, makeDate(s.startDate), makeDate(s.endDate),
                                                      s.isFxBaseCurrencyCollateralCurrency, collateral);
        }

    }

    ext::shared_ptr<RateHelper> makeRateHelper(const RateHelperSpec& s) {
        if (s.kind == "FraRateHelper")
            return makeFraRateHelper(s);
        if (s.kind == "FuturesRateHelper")
            return makeFuturesRateHelper(s);
        if (s.kind == "FxSwapRateHelper")
            return makeFxSwapRateHelper(s);
        if (s.kind == "DepositRateHelper") {
            if (s.form == "tenor")
                return ext::make_shared<DepositRateHelper>(
                    s.rate, Period(s.tenor.first, s.tenor.second), s.fixingDays, indexes::makeCalendar(s.calendar),
                    s.convention, s.endOfMonth, makeDayCounter(s.dayCounter));
            if (s.form == "index")
                return ext::make_shared<DepositRateHelper>(s.rate, makeHelperIndex(s.index));
            return ext::make_shared<DepositRateHelper>(s.rate, makeDate(s.fixingDate), makeHelperIndex(s.index));
        }
        Calendar calendar = s.emptyCalendar ? Calendar() : indexes::makeCalendar(s.calendar);
        DayCounter dc = s.emptyDayCounter ? DayCounter() : makeDayCounter(s.dayCounter);
        Handle<Quote> spread =
            s.spread == 0.0 ? Handle<Quote>() : Handle<Quote>(ext::make_shared<SimpleQuote>(s.spread));
        Handle<YieldTermStructure> discount = indexes::makeCurve(s.discountingCurve);
        if (s.form == "tenor")
            return ext::make_shared<SwapRateHelper>(
                s.rate, Period(s.tenor.first, s.tenor.second), calendar, s.fixedFrequency, s.convention, dc,
                makeHelperIndex(s.index), spread, Period(s.fwdStart.first, s.fwdStart.second), discount,
                s.settlementDays, s.pillar, makeDate(s.customPillarDate), s.endOfMonth, s.useIndexedCoupons,
                s.floatConvention, cashflows::makePricer(s.couponPricer));
        return ext::make_shared<SwapRateHelper>(
            s.rate, makeDate(s.startDate), makeDate(s.endDate), calendar, s.fixedFrequency, s.convention, dc,
            makeHelperIndex(s.index), spread, discount, s.pillar, makeDate(s.customPillarDate), s.endOfMonth,
            s.useIndexedCoupons, s.floatConvention, cashflows::makePricer(s.couponPricer));
    }

    BoundCurveSpec boundCurve(const json::value& v) {
        BoundCurveSpec s;
        const json::object& o = object(v, "a curve");
        const json::value* type = o.if_contains("type");
        if (type == nullptr) {
            s.flat = indexes::curve(v);
            if (s.flat.null)
                throw InvalidParams("a helper is bound to a curve, not to null");
            return s;
        }
        if (!type->is_string() || type->get_string() != "DiscountCurve")
            throw InvalidParams("unknown curve " + json::serialize(*type));
        onlyKeys(o, {"type", "dates", "discounts", "dayCounter"}, "DiscountCurve");
        s.interpolated = true;
        const json::value& dates = key(o, "dates", "DiscountCurve");
        if (!dates.is_array())
            throw InvalidParams("expected an array of dates, got " + json::serialize(dates));
        for (const json::value& d : dates.get_array())
            s.dates.push_back(date(d));
        s.discounts = reals(key(o, "discounts", "DiscountCurve"));
        s.dayCounter = dayCounter(key(o, "dayCounter", "DiscountCurve"));
        return s;
    }

    ext::shared_ptr<YieldTermStructure> makeBoundCurve(const BoundCurveSpec& s) {
        if (!s.interpolated)
            return indexes::makeCurve(s.flat).currentLink();
        std::vector<Date> dates;
        for (const DateSpec& d : s.dates)
            dates.push_back(makeDate(d));
        return ext::make_shared<InterpolatedDiscountCurve<LogLinear>>(dates, s.discounts,
                                                                     makeDayCounter(s.dayCounter));
    }

}
