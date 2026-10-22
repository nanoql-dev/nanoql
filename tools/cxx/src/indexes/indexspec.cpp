// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "indexes/indexspec.hpp"

#include <ql/currency.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/termstructures/yield/zerocurve.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/calendars/target.hpp>
#include <ql/time/calendars/unitedkingdom.hpp>
#include <ql/time/calendars/unitedstates.hpp>
#include <ql/utilities/null.hpp>

#include <initializer_list>
#include <utility>
#include <vector>

using namespace QuantLib;

namespace harness::indexes {

    namespace {

        // The harness's index whose fixings QuantLib refuses to store, as for an index whose fixings are those of the
        // indexes it is built from.
        class NoNativeFixingsIndex : public Index {
          public:
            explicit NoNativeFixingsIndex(std::string name) : name_(std::move(name)) {}
            std::string name() const override { return name_; }
            Calendar fixingCalendar() const override { return NullCalendar(); }
            bool isValidFixingDate(const Date&) const override { return true; }
            Real fixing(const Date&, bool) const override {
                QL_FAIL("the harness's index without native fixings forecasts nothing");
            }
            bool allowsNativeFixings() override { return false; }

          private:
            std::string name_;
        };

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

        // presetCase's forecast curve: a flat forward's reference date and rate, or a zero curve's nodes.
        struct ForecastCurveSpec {
            bool zero = false;
            std::vector<DateSpec> dates;
            std::vector<Real> rates;
            DayCounterSpec dayCounter;
        };

        ForecastCurveSpec forecastCurve(const json::value& v) {
            const json::object& o = object(v, "a forecast curve");
            ForecastCurveSpec s;
            const std::string type = text(key(o, "type", "a forecast curve"));
            if (type == "FlatForward") {
                onlyKeys(o, {"type", "referenceDate", "forward", "dayCounter"}, type);
                s.dates.push_back(date(key(o, "referenceDate", type)));
                s.rates.push_back(real(key(o, "forward", type)));
            } else if (type == "ZeroCurve") {
                onlyKeys(o, {"type", "dates", "rates", "dayCounter"}, type);
                const json::value& dates = key(o, "dates", type);
                if (!dates.is_array())
                    throw InvalidParams("expected the curve's dates as an array, got " + json::serialize(dates));
                for (const json::value& d : dates.get_array())
                    s.dates.push_back(date(d));
                s.rates = reals(key(o, "rates", type));
                s.zero = true;
            } else {
                throw InvalidParams("unknown forecast curve " + type);
            }
            s.dayCounter = dayCounter(key(o, "dayCounter", type));
            return s;
        }

        Handle<YieldTermStructure> makeForecastCurve(const ForecastCurveSpec& s) {
            if (!s.zero)
                return Handle<YieldTermStructure>(
                    ext::make_shared<FlatForward>(makeDate(s.dates[0]), s.rates[0], makeDayCounter(s.dayCounter)));
            std::vector<Date> dates;
            for (const DateSpec& d : s.dates)
                dates.push_back(makeDate(d));
            return Handle<YieldTermStructure>(
                ext::make_shared<ZeroCurve>(dates, s.rates, makeDayCounter(s.dayCounter)));
        }

        template <class F>
        json::value attempted(F f) {
            try {
                return f();
            } catch (const Error&) {
                return json::object{{"throws", true}};
            }
        }

    }

    CalendarSpec calendar(const json::value& v) {
        const json::object& o = object(v, "a calendar");
        CalendarSpec s;
        s.type = text(key(o, "type", "a calendar"));
        if (s.type == "TARGET" || s.type == "NullCalendar") {
            onlyKeys(o, {"type"}, s.type);
        } else if (s.type == "UnitedStates" || s.type == "UnitedKingdom") {
            onlyKeys(o, {"type", "market"}, s.type);
            s.market = enumeration<int>(key(o, "market", s.type));
        } else {
            throw InvalidParams("unknown calendar " + s.type);
        }
        return s;
    }

    Calendar makeCalendar(const CalendarSpec& s) {
        if (s.type == "TARGET")
            return TARGET();
        if (s.type == "NullCalendar")
            return NullCalendar();
        if (s.type == "UnitedStates")
            return UnitedStates(static_cast<UnitedStates::Market>(s.market));
        if (s.type == "UnitedKingdom")
            return UnitedKingdom(static_cast<UnitedKingdom::Market>(s.market));
        throw InvalidParams("the calendar " + s.type + " was not checked");
    }

    CurveSpec curve(const json::value& v) {
        CurveSpec s;
        if (v.is_null())
            return s;
        const json::object& o = object(v, "a curve");
        onlyKeys(o, {"referenceDate", "forward", "dayCounter", "compounding", "frequency"}, "a curve");
        s.null = false;
        s.referenceDate = date(key(o, "referenceDate", "a curve"));
        s.forward = real(key(o, "forward", "a curve"));
        s.dayCounter = dayCounter(key(o, "dayCounter", "a curve"));
        s.compounding = enumeration<Compounding>(key(o, "compounding", "a curve"));
        s.frequency = enumeration<Frequency>(key(o, "frequency", "a curve"));
        return s;
    }

    Handle<YieldTermStructure> makeCurve(const CurveSpec& s) {
        if (s.null)
            return {};
        return Handle<YieldTermStructure>(ext::make_shared<FlatForward>(
            makeDate(s.referenceDate), s.forward, makeDayCounter(s.dayCounter), s.compounding, s.frequency));
    }

    IndexSpec iborIndex(const json::value& v) {
        const json::object& o = object(v, "an index");
        IndexSpec s;
        s.type = text(key(o, "type", "an index"));
        if (s.type == "IborIndex") {
            onlyKeys(o, {"type", "familyName", "tenor", "settlementDays", "fixingCalendar", "convention", "endOfMonth",
                         "dayCounter", "curve"},
                     s.type);
            const json::value& tenor = key(o, "tenor", s.type);
            if (!tenor.is_array() || tenor.get_array().size() != 2)
                throw InvalidParams("expected a tenor as [length, units], got " + json::serialize(tenor));
            s.tenorLength = integer(tenor.get_array()[0]);
            s.tenorUnits = enumeration<TimeUnit>(tenor.get_array()[1]);
            s.convention = enumeration<BusinessDayConvention>(key(o, "convention", s.type));
            s.endOfMonth = boolean(key(o, "endOfMonth", s.type));
        } else if (s.type == "OvernightIndex") {
            onlyKeys(o, {"type", "familyName", "settlementDays", "fixingCalendar", "dayCounter", "curve"}, s.type);
        } else {
            throw InvalidParams("unknown Ibor index " + s.type);
        }
        s.name = text(key(o, "familyName", s.type));
        s.settlementDays = natural(key(o, "settlementDays", s.type));
        s.fixingCalendar = calendar(key(o, "fixingCalendar", s.type));
        s.dayCounter = dayCounter(key(o, "dayCounter", s.type));
        s.curve = curve(key(o, "curve", s.type));
        return s;
    }

    IndexSpec index(const json::value& v) {
        const json::object& o = object(v, "an index");
        const json::value* type = o.if_contains("type");
        if (type == nullptr || !type->is_string() || type->get_string() != "NoNativeFixingsIndex")
            return iborIndex(v);
        onlyKeys(o, {"type", "name"}, "NoNativeFixingsIndex");
        IndexSpec s;
        s.type = "NoNativeFixingsIndex";
        s.name = text(key(o, "name", s.type));
        return s;
    }

    ext::shared_ptr<IborIndex> makeIborIndex(const IndexSpec& s) {
        if (s.type == "IborIndex")
            return ext::make_shared<IborIndex>(s.name, Period(s.tenorLength, s.tenorUnits), s.settlementDays,
                                               Currency(), makeCalendar(s.fixingCalendar), s.convention,
                                               s.endOfMonth, makeDayCounter(s.dayCounter), makeCurve(s.curve));
        if (s.type == "OvernightIndex")
            return ext::make_shared<OvernightIndex>(s.name, s.settlementDays, Currency(),
                                                    makeCalendar(s.fixingCalendar), makeDayCounter(s.dayCounter),
                                                    makeCurve(s.curve));
        throw InvalidParams("the index " + s.type + " is no Ibor index");
    }

    ext::shared_ptr<Index> makeIndex(const IndexSpec& s) {
        if (s.type == "NoNativeFixingsIndex")
            return ext::make_shared<NoNativeFixingsIndex>(s.name);
        return makeIborIndex(s);
    }

    Call presetCase(IborPreset make, const json::value& dates, const json::value& curves) {
        if (!dates.is_array())
            throw InvalidParams("expected an array of dates, got " + json::serialize(dates));
        std::vector<DateSpec> days;
        for (const json::value& d : dates.get_array())
            days.push_back(date(d));
        if (!curves.is_array())
            throw InvalidParams("expected an array of forecast curves, got " + json::serialize(curves));
        std::vector<ForecastCurveSpec> forecastCurves;
        for (const json::value& c : curves.get_array())
            forecastCurves.push_back(forecastCurve(c));
        return [make = std::move(make), days, forecastCurves] {
            ext::shared_ptr<IborIndex> index = make(Handle<YieldTermStructure>());
            json::array fixingDates, valueDates, maturityDates;
            for (const DateSpec& d : days) {
                Date day = makeDate(d);
                fixingDates.push_back(attempted([&] { return result(index->fixingDate(day)); }));
                valueDates.push_back(attempted([&] { return result(index->valueDate(day)); }));
                maturityDates.push_back(attempted([&] { return result(index->maturityDate(day)); }));
            }
            json::array forecasts;
            for (const ForecastCurveSpec& c : forecastCurves) {
                ext::shared_ptr<IborIndex> onCurve = make(makeForecastCurve(c));
                json::array fixings;
                for (const DateSpec& d : days) {
                    Date day = makeDate(d);
                    fixings.push_back(attempted([&] { return json::value(onCurve->fixing(day)); }));
                }
                forecasts.push_back(std::move(fixings));
            }
            Period tenor = index->tenor();
            return json::value(json::object{
                {"name", index->name()},
                {"familyName", index->familyName()},
                {"tenor", json::array{tenor.length(), static_cast<int>(tenor.units())}},
                {"fixingDays", index->fixingDays()},
                {"currency", index->currency().code()},
                {"fixingCalendar", index->fixingCalendar().name()},
                {"dayCounter", index->dayCounter().name()},
                {"businessDayConvention", static_cast<int>(index->businessDayConvention())},
                {"endOfMonth", index->endOfMonth()},
                {"fixingDate", std::move(fixingDates)},
                {"valueDate", std::move(valueDates)},
                {"maturityDate", std::move(maturityDates)},
                {"forecast", std::move(forecasts)}});
        };
    }

}
