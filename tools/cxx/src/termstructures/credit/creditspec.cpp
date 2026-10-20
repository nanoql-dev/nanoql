// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "termstructures/credit/creditspec.hpp"

#include <ql/cashflows/coupon.hpp>
#include <ql/cashflows/simplecashflow.hpp>
#include <ql/instruments/claim.hpp>
#include <ql/math/interpolations/backwardflatinterpolation.hpp>
#include <ql/math/interpolations/linearinterpolation.hpp>
#include <ql/math/interpolations/loginterpolation.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/credit/flathazardrate.hpp>
#include <ql/termstructures/credit/interpolateddefaultdensitycurve.hpp>
#include <ql/termstructures/credit/interpolatedhazardratecurve.hpp>
#include <ql/termstructures/credit/interpolatedsurvivalprobabilitycurve.hpp>
#include <ql/termstructures/yield/discountcurve.hpp>
#include <ql/time/calendars/weekendsonly.hpp>
#include <ql/time/daycounters/actual360.hpp>

#include <initializer_list>

using namespace QuantLib;

namespace harness::credit {

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

        std::vector<DateSpec> dates(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected an array of dates, got " + json::serialize(v));
            std::vector<DateSpec> out;
            for (const json::value& d : v.get_array())
                out.push_back(date(d));
            return out;
        }

        std::vector<Date> makeDates(const std::vector<DateSpec>& ds) {
            std::vector<Date> out;
            for (const DateSpec& d : ds)
                out.push_back(makeDate(d));
            return out;
        }

        void period(const json::value& v, Integer& length, TimeUnit& units) {
            if (!v.is_array() || v.get_array().size() != 2)
                throw InvalidParams("expected a period as [length, units], got " + json::serialize(v));
            length = integer(v.get_array()[0]);
            units = enumeration<TimeUnit>(v.get_array()[1]);
        }

        Integer natural(const json::value& v, const std::string& what) {
            Integer n = integer(v);
            if (n == Null<Integer>() || n < 0)
                throw InvalidParams("expected " + what + " as a Natural");
            return n;
        }

        template <template <class> class C, class I>
        ext::shared_ptr<DefaultProbabilityTermStructure> interpolated(const CurveSpec& s) {
            std::vector<Handle<Quote>> jumps;
            for (Real j : s.jumps)
                jumps.emplace_back(ext::make_shared<SimpleQuote>(j));
            return ext::make_shared<C<I>>(makeDates(s.dates), s.data, makeDayCounter(s.dayCounter), Calendar(), jumps,
                                          makeDates(s.jumpDates));
        }

        template <template <class> class C>
        ext::shared_ptr<DefaultProbabilityTermStructure> interpolatedOn(const CurveSpec& s) {
            if (s.interpolator == "BackwardFlat")
                return interpolated<C, BackwardFlat>(s);
            if (s.interpolator == "Linear")
                return interpolated<C, Linear>(s);
            if (s.interpolator == "LogLinear")
                return interpolated<C, LogLinear>(s);
            throw InvalidParams("unknown interpolator " + s.interpolator);
        }

        // A result QuantLib may leave unset: null where it throws "... not available".
        template <class F>
        json::value available(F f) {
            try {
                return json::value(f());
            } catch (const Error& e) {
                if (std::string(e.what()).find("not available") != std::string::npos)
                    return nullptr;
                throw;
            }
        }

    }

    CalendarSpec calendar(const json::value& v) {
        const json::object& o = object(v, "a calendar");
        CalendarSpec s;
        if (text(key(o, "type", "a calendar")) == "WeekendsOnly") {
            onlyKeys(o, {"type"}, "WeekendsOnly");
            s.weekendsOnly = true;
            return s;
        }
        s.other = indexes::calendar(v);
        return s;
    }

    Calendar makeCalendar(const CalendarSpec& s) {
        return s.weekendsOnly ? Calendar(WeekendsOnly()) : indexes::makeCalendar(s.other);
    }

    DayCounterSpec dayCounter(const json::value& v) {
        const json::object& o = object(v, "a day counter");
        DayCounterSpec s;
        if (const json::value* inc = o.if_contains("includeLastDay"); inc != nullptr) {
            onlyKeys(o, {"type", "includeLastDay"}, "an inclusive day counter");
            if (text(key(o, "type", "a day counter")) != "Actual360" || !boolean(*inc))
                throw InvalidParams("only Actual360 includes its last day: " + json::serialize(v));
            s.actual360Inclusive = true;
            return s;
        }
        s.other = harness::dayCounter(v);
        return s;
    }

    DayCounter makeDayCounter(const DayCounterSpec& s) {
        return s.actual360Inclusive ? DayCounter(Actual360(true)) : harness::makeDayCounter(s.other);
    }

    CurveSpec curve(const json::value& v) {
        const std::string what = "a credit curve";
        const json::object& o = object(v, what);
        CurveSpec s;
        s.type = text(key(o, "type", what));
        if (s.type == "flat") {
            onlyKeys(o, {"type", "referenceDate", "hazardRate", "dayCounter"}, what);
            s.referenceDate = date(key(o, "referenceDate", what));
        } else if (s.type == "flatSettlement") {
            onlyKeys(o, {"type", "settlementDays", "calendar", "hazardRate", "dayCounter"}, what);
            s.settlementDays = natural(key(o, "settlementDays", what), "the settlement days");
            s.calendar = calendar(key(o, "calendar", what));
        } else if (s.type == "HazardRateCurve" || s.type == "DefaultDensityCurve" ||
                   s.type == "SurvivalProbabilityCurve") {
            onlyKeys(o, {"type", "dates", "data", "dayCounter", "interpolator", "jumps", "jumpDates"}, what);
            s.dates = dates(key(o, "dates", what));
            s.data = reals(key(o, "data", what));
            s.interpolator = s.type == "HazardRateCurve" ? "BackwardFlat" : "Linear";
            if (const json::value* i = o.if_contains("interpolator"); i != nullptr)
                s.interpolator = text(*i);
            if (s.interpolator != "BackwardFlat" && s.interpolator != "Linear" && s.interpolator != "LogLinear")
                throw InvalidParams("unknown interpolator " + s.interpolator);
            if (const json::value* j = o.if_contains("jumps"); j != nullptr)
                s.jumps = reals(*j);
            if (const json::value* j = o.if_contains("jumpDates"); j != nullptr)
                s.jumpDates = dates(*j);
            s.dayCounter = dayCounter(key(o, "dayCounter", what));
            return s;
        } else {
            throw InvalidParams("unknown credit curve " + s.type);
        }
        s.hazardRate = real(key(o, "hazardRate", what));
        s.dayCounter = dayCounter(key(o, "dayCounter", what));
        return s;
    }

    ext::shared_ptr<DefaultProbabilityTermStructure> makeCurve(const CurveSpec& s) {
        if (s.type == "flat")
            return ext::make_shared<FlatHazardRate>(makeDate(s.referenceDate),
                                                    Handle<Quote>(ext::make_shared<SimpleQuote>(s.hazardRate)),
                                                    makeDayCounter(s.dayCounter));
        if (s.type == "flatSettlement")
            return ext::make_shared<FlatHazardRate>(s.settlementDays, makeCalendar(s.calendar),
                                                    Handle<Quote>(ext::make_shared<SimpleQuote>(s.hazardRate)),
                                                    makeDayCounter(s.dayCounter));
        if (s.type == "HazardRateCurve")
            return interpolatedOn<InterpolatedHazardRateCurve>(s);
        if (s.type == "DefaultDensityCurve")
            return interpolatedOn<InterpolatedDefaultDensityCurve>(s);
        return interpolatedOn<InterpolatedSurvivalProbabilityCurve>(s);
    }

    DiscountSpec discount(const json::value& v) {
        const std::string what = "a discount curve";
        const json::object& o = object(v, what);
        DiscountSpec s;
        if (const json::value* t = o.if_contains("type"); t != nullptr && t->is_string() && text(*t) == "DiscountCurve") {
            onlyKeys(o, {"type", "dates", "dfs", "dayCounter"}, what);
            s.nodes = true;
            s.dates = dates(key(o, "dates", what));
            s.dfs = reals(key(o, "dfs", what));
            s.dayCounter = dayCounter(key(o, "dayCounter", what));
            return s;
        }
        s.flat = indexes::curve(v);
        return s;
    }

    Handle<YieldTermStructure> makeDiscount(const DiscountSpec& s) {
        if (s.nodes)
            return Handle<YieldTermStructure>(
                ext::make_shared<DiscountCurve>(makeDates(s.dates), s.dfs, makeDayCounter(s.dayCounter)));
        return indexes::makeCurve(s.flat);
    }

    ScheduleSpec schedule(const json::value& v) {
        const std::string what = "a CDS schedule";
        const json::object& o = object(v, what);
        onlyKeys(o, {"effectiveDate", "terminationDate", "tenor", "calendar", "convention", "terminationDateConvention",
                     "rule"},
                 what);
        ScheduleSpec s;
        s.effectiveDate = date(key(o, "effectiveDate", what));
        s.terminationDate = date(key(o, "terminationDate", what));
        period(key(o, "tenor", what), s.tenorLength, s.tenorUnits);
        s.calendar = calendar(key(o, "calendar", what));
        s.convention = enumeration<BusinessDayConvention>(key(o, "convention", what));
        s.terminationDateConvention = enumeration<BusinessDayConvention>(key(o, "terminationDateConvention", what));
        s.rule = enumeration<DateGeneration::Rule>(key(o, "rule", what));
        return s;
    }

    Schedule makeSchedule(const ScheduleSpec& s) {
        return Schedule(makeDate(s.effectiveDate), makeDate(s.terminationDate), Period(s.tenorLength, s.tenorUnits),
                        makeCalendar(s.calendar), s.convention, s.terminationDateConvention, s.rule, false);
    }

    SwapSpec swap(const json::value& v) {
        const std::string what = "a credit default swap";
        const json::object& o = object(v, what);
        onlyKeys(o, {"side", "notional", "upfront", "spread", "schedule", "paymentConvention", "dayCounter",
                     "settlesAccrual", "paysAtDefaultTime", "protectionStart", "upfrontDate", "lastPeriodDayCounter",
                     "rebatesAccrual", "tradeDate", "cashSettlementDays"},
                 what);
        SwapSpec s;
        s.side = enumeration<Protection::Side>(key(o, "side", what));
        s.notional = real(key(o, "notional", what));
        if (const json::value& u = key(o, "upfront", what); !u.is_null())
            s.upfront = real(u);
        s.spread = real(key(o, "spread", what));
        s.schedule = schedule(key(o, "schedule", what));
        s.paymentConvention = enumeration<BusinessDayConvention>(key(o, "paymentConvention", what));
        s.dayCounter = dayCounter(key(o, "dayCounter", what));
        s.settlesAccrual = boolean(key(o, "settlesAccrual", what));
        s.paysAtDefaultTime = boolean(key(o, "paysAtDefaultTime", what));
        s.protectionStart = date(key(o, "protectionStart", what));
        s.upfrontDate = date(key(o, "upfrontDate", what));
        if (const json::value& d = key(o, "lastPeriodDayCounter", what); !d.is_null())
            s.lastPeriodDayCounter = dayCounter(d);
        s.rebatesAccrual = boolean(key(o, "rebatesAccrual", what));
        s.tradeDate = date(key(o, "tradeDate", what));
        s.cashSettlementDays = natural(key(o, "cashSettlementDays", what), "the cash settlement days");
        return s;
    }

    ext::shared_ptr<CreditDefaultSwap> makeSwap(const SwapSpec& s) {
        DayCounter last = s.lastPeriodDayCounter ? makeDayCounter(*s.lastPeriodDayCounter) : DayCounter();
        Schedule sched = makeSchedule(s.schedule);
        if (s.upfront)
            return ext::make_shared<CreditDefaultSwap>(
                s.side, s.notional, *s.upfront, s.spread, sched, s.paymentConvention, makeDayCounter(s.dayCounter),
                s.settlesAccrual, s.paysAtDefaultTime, makeDate(s.protectionStart), makeDate(s.upfrontDate),
                ext::shared_ptr<Claim>(), last, s.rebatesAccrual, makeDate(s.tradeDate),
                static_cast<Natural>(s.cashSettlementDays));
        return ext::make_shared<CreditDefaultSwap>(
            s.side, s.notional, s.spread, sched, s.paymentConvention, makeDayCounter(s.dayCounter), s.settlesAccrual,
            s.paysAtDefaultTime, makeDate(s.protectionStart), ext::shared_ptr<Claim>(), last, s.rebatesAccrual,
            makeDate(s.tradeDate), static_cast<Natural>(s.cashSettlementDays));
    }

    HelperSpec helper(const json::value& v) {
        const std::string what = "a CDS helper";
        const json::object& o = object(v, what);
        onlyKeys(o, {"kind", "quote", "runningSpread", "tenor", "settlementDays", "calendar", "frequency",
                     "convention", "rule", "dayCounter", "recoveryRate", "discount", "upfrontSettlementDays",
                     "settlesAccrual", "paysAtDefaultTime", "startDate", "lastPeriodDayCounter", "rebatesAccrual",
                     "model", "tradeDate"},
                 what);
        HelperSpec s;
        const std::string kind = text(key(o, "kind", what));
        if (kind != "spread" && kind != "upfront")
            throw InvalidParams("unknown CDS helper kind " + kind);
        s.upfront = kind == "upfront";
        s.quote = real(key(o, "quote", what));
        if (s.upfront)
            s.runningSpread = real(key(o, "runningSpread", what));
        period(key(o, "tenor", what), s.tenorLength, s.tenorUnits);
        s.settlementDays = integer(key(o, "settlementDays", what));
        s.calendar = calendar(key(o, "calendar", what));
        s.frequency = enumeration<Frequency>(key(o, "frequency", what));
        s.convention = enumeration<BusinessDayConvention>(key(o, "convention", what));
        s.rule = enumeration<DateGeneration::Rule>(key(o, "rule", what));
        s.dayCounter = dayCounter(key(o, "dayCounter", what));
        s.recoveryRate = real(key(o, "recoveryRate", what));
        s.discount = discount(key(o, "discount", what));
        s.upfrontSettlementDays = natural(key(o, "upfrontSettlementDays", what), "the upfront settlement days");
        s.settlesAccrual = boolean(key(o, "settlesAccrual", what));
        s.paysAtDefaultTime = boolean(key(o, "paysAtDefaultTime", what));
        s.startDate = date(key(o, "startDate", what));
        s.lastPeriodDayCounter = dayCounter(key(o, "lastPeriodDayCounter", what));
        s.rebatesAccrual = boolean(key(o, "rebatesAccrual", what));
        s.model = enumeration<CreditDefaultSwap::PricingModel>(key(o, "model", what));
        s.tradeDate = date(key(o, "tradeDate", what));
        return s;
    }

    ext::shared_ptr<DefaultProbabilityHelper> makeHelper(const HelperSpec& s, Real quote) {
        Period tenor(s.tenorLength, s.tenorUnits);
        if (s.upfront)
            return ext::make_shared<UpfrontCdsHelper>(
                quote, s.runningSpread, tenor, s.settlementDays, makeCalendar(s.calendar), s.frequency, s.convention,
                s.rule, makeDayCounter(s.dayCounter), s.recoveryRate, makeDiscount(s.discount),
                static_cast<Natural>(s.upfrontSettlementDays), s.settlesAccrual, s.paysAtDefaultTime,
                makeDate(s.startDate), makeDayCounter(s.lastPeriodDayCounter), s.rebatesAccrual, s.model,
                makeDate(s.tradeDate));
        return ext::make_shared<SpreadCdsHelper>(
            quote, tenor, s.settlementDays, makeCalendar(s.calendar), s.frequency, s.convention, s.rule,
            makeDayCounter(s.dayCounter), s.recoveryRate, makeDiscount(s.discount), s.settlesAccrual,
            s.paysAtDefaultTime, makeDate(s.startDate), makeDayCounter(s.lastPeriodDayCounter), s.rebatesAccrual,
            s.model, makeDate(s.tradeDate));
    }

    json::value inspectors(const CreditDefaultSwap& cds) {
        json::array couponDates, accrualStartDates, accrualEndDates, couponAmounts;
        for (const auto& cf : cds.coupons()) {
            auto c = ext::dynamic_pointer_cast<Coupon>(cf);
            couponDates.push_back(result(c->date()));
            accrualStartDates.push_back(result(c->accrualStartDate()));
            accrualEndDates.push_back(result(c->accrualEndDate()));
            couponAmounts.push_back(json::value(c->amount()));
        }
        const ext::shared_ptr<SimpleCashFlow>& rebate = cds.accrualRebate();
        std::optional<Rate> upfront = cds.upfront();
        return json::object{
            {"protectionStartDate", result(cds.protectionStartDate())},
            {"protectionEndDate", result(cds.protectionEndDate())},
            {"tradeDate", result(cds.tradeDate())},
            {"cashSettlementDays", static_cast<std::int64_t>(cds.cashSettlementDays())},
            {"upfront", upfront ? json::value(*upfront) : json::value(nullptr)},
            {"upfrontDate", result(cds.upfrontPayment()->date())},
            {"upfrontAmount", cds.upfrontPayment()->amount()},
            {"rebateDate", rebate ? result(rebate->date()) : json::value(nullptr)},
            {"rebateAmount", rebate ? json::value(rebate->amount()) : json::value(nullptr)},
            {"couponDates", couponDates},
            {"accrualStartDates", accrualStartDates},
            {"accrualEndDates", accrualEndDates},
            {"couponAmounts", couponAmounts},
            {"isExpired", cds.isExpired()},
        };
    }

    json::value results(const CreditDefaultSwap& cds) {
        return json::object{
            {"NPV", available([&] { return cds.NPV(); })},
            {"couponLegNPV", available([&] { return cds.couponLegNPV(); })},
            {"defaultLegNPV", available([&] { return cds.defaultLegNPV(); })},
            {"upfrontNPV", available([&] { return cds.upfrontNPV(); })},
            {"accrualRebateNPV", available([&] { return cds.accrualRebateNPV(); })},
            {"fairSpread", available([&] { return cds.fairSpread(); })},
            {"fairUpfront", available([&] { return cds.fairUpfront(); })},
            {"couponLegBPS", available([&] { return cds.couponLegBPS(); })},
            {"upfrontBPS", available([&] { return cds.upfrontBPS(); })},
        };
    }

}
