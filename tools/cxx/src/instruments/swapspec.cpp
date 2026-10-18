// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "instruments/swapspec.hpp"

#include <ql/indexes/iborindex.hpp>
#include <ql/time/calendar.hpp>
#include <ql/utilities/null.hpp>

#include <cstdint>
#include <initializer_list>
#include <set>

using namespace QuantLib;

namespace harness::swaps {

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

        std::pair<Integer, TimeUnit> period(const json::value& v) {
            if (!v.is_array() || v.get_array().size() != 2)
                throw InvalidParams("expected a period as [length, units], got " + json::serialize(v));
            Integer n = integer(v.get_array()[0]);
            if (n == Null<Integer>())
                throw InvalidParams("expected a period's length, got " + json::serialize(v));
            return {n, enumeration<TimeUnit>(v.get_array()[1])};
        }

        indexes::IndexSpec indexOfType(const json::value& v, const std::string& type) {
            indexes::IndexSpec s = indexes::iborIndex(v);
            if (s.type != type)
                throw InvalidParams("expected an " + type + " spec, got " + json::serialize(v));
            return s;
        }

        // The setters each builder takes, by the kind of value they hold.
        enum class Kind { Natural, Date, Period, Calendar, DayCounter, Flag };

        BuildSpec build(const json::value& v, const std::string& indexType,
                        std::initializer_list<std::pair<const char*, Kind>> known) {
            const std::string what = "a build";
            const json::object& o = object(v, what);
            onlyKeys(o, {"swapTenor", "index", "fixedRate", "forwardStart", "setters"}, what);
            BuildSpec s;
            s.swapTenor = period(key(o, "swapTenor", what));
            s.index = indexOfType(key(o, "index", what), indexType);
            s.fixedRate = real(key(o, "fixedRate", what));
            s.forwardStart = period(key(o, "forwardStart", what));
            const json::value& setters = key(o, "setters", what);
            if (!setters.is_array())
                throw InvalidParams("expected a list of setters, got " + json::serialize(setters));
            for (const json::value& x : setters.get_array()) {
                if (!x.is_array() || x.get_array().size() != 2)
                    throw InvalidParams("expected a setter as [name, value], got " + json::serialize(x));
                SetterSpec setter;
                setter.name = text(x.get_array()[0]);
                const json::value& value = x.get_array()[1];
                bool found = false;
                for (const auto& [name, kind] : known) {
                    if (setter.name != name)
                        continue;
                    found = true;
                    switch (kind) {
                      case Kind::Natural:
                        setter.natural = natural(value);
                        break;
                      case Kind::Date:
                        setter.date = date(value);
                        if (setter.date.null)
                            throw InvalidParams("expected a date, got null");
                        break;
                      case Kind::Period:
                        setter.period = period(value);
                        break;
                      case Kind::Calendar:
                        setter.calendar = indexes::calendar(value);
                        break;
                      case Kind::DayCounter:
                        setter.dayCounter = dayCounter(value);
                        break;
                      case Kind::Flag:
                        setter.flag = boolean(value);
                        break;
                    }
                }
                if (!found)
                    throw InvalidParams("the harness's builder takes no setter " + setter.name);
                s.setters.push_back(setter);
            }
            return s;
        }

        std::vector<Real> nominals(const json::value& v) {
            std::vector<Real> out = reals(v);
            for (Real x : out) {
                if (x == Null<Real>())
                    throw InvalidParams("expected nominals, got " + json::serialize(v));
            }
            return out;
        }

    }

    OisSpec ois(const json::value& v) {
        const std::string what = "an overnight-indexed swap";
        const json::object& o = object(v, what);
        onlyKeys(o, {"kind", "form", "type", "fixedNominals", "fixedSchedule", "fixedRate", "fixedDC",
                     "overnightNominals", "overnightSchedule", "index", "spread", "paymentLag", "paymentAdjustment",
                     "paymentCalendar", "telescopicValueDates", "averagingMethod", "lookbackDays", "lockoutDays",
                     "applyObservationShift", "roundingPrecision"},
                 what);
        if (text(key(o, "kind", what)) != "OvernightIndexedSwap")
            throw InvalidParams("expected an OvernightIndexedSwap spec, got " + json::serialize(v));
        OisSpec s;
        s.form = text(key(o, "form", what));
        static const std::set<std::string> forms = {"nominal", "nominals", "nominal, two schedules",
                                                    "nominals, two schedules"};
        if (forms.count(s.form) == 0)
            throw InvalidParams("unknown form " + s.form);
        s.type = enumeration<Swap::Type>(key(o, "type", what));
        s.fixedNominals = nominals(key(o, "fixedNominals", what));
        s.overnightNominals = nominals(key(o, "overnightNominals", what));
        if (s.fixedNominals.empty() || s.overnightNominals.empty())
            throw InvalidParams("expected at least one nominal on each leg");
        s.fixedSchedule = cashflows::schedule(key(o, "fixedSchedule", what));
        s.overnightSchedule = cashflows::schedule(key(o, "overnightSchedule", what));
        s.fixedRate = real(key(o, "fixedRate", what));
        const json::value& dc = key(o, "fixedDC", what);
        if (!dc.is_null())
            s.fixedDC = dayCounter(dc);
        s.index = indexOfType(key(o, "index", what), "OvernightIndex");
        s.spread = real(key(o, "spread", what));
        s.paymentLag = integer(key(o, "paymentLag", what));
        s.paymentAdjustment = enumeration<BusinessDayConvention>(key(o, "paymentAdjustment", what));
        const json::value& cal = key(o, "paymentCalendar", what);
        if (!cal.is_null())
            s.paymentCalendar = indexes::calendar(cal);
        s.telescopicValueDates = boolean(key(o, "telescopicValueDates", what));
        s.averagingMethod = enumeration<RateAveraging::Type>(key(o, "averagingMethod", what));
        const json::value& lookback = key(o, "lookbackDays", what);
        if (!lookback.is_null())
            s.lookbackDays = natural(lookback);
        s.lockoutDays = natural(key(o, "lockoutDays", what));
        s.applyObservationShift = boolean(key(o, "applyObservationShift", what));
        const json::value& precision = key(o, "roundingPrecision", what);
        if (!precision.is_null()) {
            Integer p = integer(precision);
            if (p == Null<Integer>())
                throw InvalidParams("expected a rounding precision, got " + json::serialize(precision));
            s.roundingPrecision = p;
        }
        if (s.paymentLag == Null<Integer>() || s.fixedRate == Null<Real>() || s.spread == Null<Real>())
            throw InvalidParams("expected a fixed rate, a spread and a payment lag: " + json::serialize(v));
        return s;
    }

    BuildSpec vanillaBuild(const json::value& v) {
        return build(v, "IborIndex",
                     {{"settlementDays", Kind::Natural}, {"settlementCalendar", Kind::Calendar},
                      {"effectiveDate", Kind::Date}, {"terminationDate", Kind::Date},
                      {"fixedLegTenor", Kind::Period}, {"fixedLegCalendar", Kind::Calendar},
                      {"fixedLegDayCount", Kind::DayCounter}, {"floatingLegCalendar", Kind::Calendar}});
    }

    BuildSpec oisBuild(const json::value& v) {
        return build(v, "OvernightIndex",
                     {{"settlementDays", Kind::Natural}, {"settlementCalendar", Kind::Calendar},
                      {"effectiveDate", Kind::Date}, {"terminationDate", Kind::Date},
                      {"calendar", Kind::Calendar}, {"fixedLegCalendar", Kind::Calendar},
                      {"overnightLegCalendar", Kind::Calendar}, {"endOfMonth", Kind::Flag}});
    }

    ext::shared_ptr<OvernightIndexedSwap> makeOis(const OisSpec& s) {
        auto index = ext::dynamic_pointer_cast<OvernightIndex>(indexes::makeIborIndex(s.index));
        if (!index)
            throw InvalidParams("the index is no overnight index");
        Schedule fixed = cashflows::makeSchedule(s.fixedSchedule);
        DayCounter dc = s.fixedDC ? makeDayCounter(*s.fixedDC) : DayCounter();
        Calendar cal = s.paymentCalendar ? indexes::makeCalendar(*s.paymentCalendar) : Calendar();
        Natural lookback = s.lookbackDays ? *s.lookbackDays : Null<Natural>();
        if (s.form == "nominal")
            return ext::make_shared<OvernightIndexedSwap>(
                s.type, s.fixedNominals[0], fixed, s.fixedRate, dc, index, s.spread, s.paymentLag,
                s.paymentAdjustment, cal, s.telescopicValueDates, s.averagingMethod, lookback, s.lockoutDays,
                s.applyObservationShift, s.roundingPrecision);
        if (s.form == "nominals")
            return ext::make_shared<OvernightIndexedSwap>(
                s.type, s.fixedNominals, fixed, s.fixedRate, dc, index, s.spread, s.paymentLag, s.paymentAdjustment,
                cal, s.telescopicValueDates, s.averagingMethod, lookback, s.lockoutDays, s.applyObservationShift,
                s.roundingPrecision);
        Schedule overnight = cashflows::makeSchedule(s.overnightSchedule);
        if (s.form == "nominal, two schedules")
            return ext::make_shared<OvernightIndexedSwap>(
                s.type, s.fixedNominals[0], fixed, s.fixedRate, dc, overnight, index, s.spread, s.paymentLag,
                s.paymentAdjustment, cal, s.telescopicValueDates, s.averagingMethod, lookback, s.lockoutDays,
                s.applyObservationShift, s.roundingPrecision);
        if (s.form == "nominals, two schedules")
            return ext::make_shared<OvernightIndexedSwap>(
                s.type, s.fixedNominals, fixed, s.fixedRate, dc, s.overnightNominals, overnight, index, s.spread,
                s.paymentLag, s.paymentAdjustment, cal, s.telescopicValueDates, s.averagingMethod, lookback,
                s.lockoutDays, s.applyObservationShift, s.roundingPrecision);
        throw InvalidParams("the form " + s.form + " was not checked");
    }

    ext::shared_ptr<VanillaSwap> makeVanillaSwap(const BuildSpec& s) {
        MakeVanillaSwap b(Period(s.swapTenor.first, s.swapTenor.second), indexes::makeIborIndex(s.index));
        b.withFixedRate(s.fixedRate).withForwardStart(Period(s.forwardStart.first, s.forwardStart.second));
        for (const SetterSpec& x : s.setters) {
            if (x.name == "settlementDays")
                b.withSettlementDays(x.natural);
            else if (x.name == "settlementCalendar")
                b.withSettlementCalendar(indexes::makeCalendar(x.calendar));
            else if (x.name == "effectiveDate")
                b.withEffectiveDate(makeDate(x.date));
            else if (x.name == "terminationDate")
                b.withTerminationDate(makeDate(x.date));
            else if (x.name == "fixedLegTenor")
                b.withFixedLegTenor(Period(x.period.first, x.period.second));
            else if (x.name == "fixedLegCalendar")
                b.withFixedLegCalendar(indexes::makeCalendar(x.calendar));
            else if (x.name == "fixedLegDayCount")
                b.withFixedLegDayCount(makeDayCounter(x.dayCounter));
            else if (x.name == "floatingLegCalendar")
                b.withFloatingLegCalendar(indexes::makeCalendar(x.calendar));
            else
                throw InvalidParams("the setter " + x.name + " was not checked");
        }
        return b;
    }

    ext::shared_ptr<OvernightIndexedSwap> makeOisFromBuild(const BuildSpec& s) {
        auto index = ext::dynamic_pointer_cast<OvernightIndex>(indexes::makeIborIndex(s.index));
        if (!index)
            throw InvalidParams("the index is no overnight index");
        MakeOIS b(Period(s.swapTenor.first, s.swapTenor.second), index);
        b.withFixedRate(s.fixedRate).withForwardStart(Period(s.forwardStart.first, s.forwardStart.second));
        for (const SetterSpec& x : s.setters) {
            if (x.name == "settlementDays")
                b.withSettlementDays(x.natural);
            else if (x.name == "settlementCalendar")
                b.withSettlementCalendar(indexes::makeCalendar(x.calendar));
            else if (x.name == "effectiveDate")
                b.withEffectiveDate(makeDate(x.date));
            else if (x.name == "terminationDate")
                b.withTerminationDate(makeDate(x.date));
            else if (x.name == "calendar")
                b.withCalendar(indexes::makeCalendar(x.calendar));
            else if (x.name == "fixedLegCalendar")
                b.withFixedLegCalendar(indexes::makeCalendar(x.calendar));
            else if (x.name == "overnightLegCalendar")
                b.withOvernightLegCalendar(indexes::makeCalendar(x.calendar));
            else if (x.name == "endOfMonth")
                b.withEndOfMonth(x.flag);
            else
                throw InvalidParams("the setter " + x.name + " was not checked");
        }
        return b;
    }

    json::value scheduleDates(const Schedule& s) {
        json::array out;
        for (const Date& d : s.dates())
            out.emplace_back(result(d));
        return out;
    }

    json::value describe(const FixedVsFloatingSwap& swap, const char* floatingSchedule) {
        json::object o;
        o["startDate"] = result(swap.startDate());
        o["maturityDate"] = result(swap.maturityDate());
        o["fixedSchedule"] = scheduleDates(swap.fixedSchedule());
        o[floatingSchedule] = scheduleDates(swap.floatingSchedule());
        o["fixedRate"] = swap.fixedRate();
        return o;
    }

}
