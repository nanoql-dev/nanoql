// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "indexes/indexspec.hpp"

#include <ql/currency.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/calendars/target.hpp>
#include <ql/time/calendars/unitedkingdom.hpp>
#include <ql/time/calendars/unitedstates.hpp>
#include <ql/utilities/null.hpp>

#include <initializer_list>
#include <utility>

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

}
