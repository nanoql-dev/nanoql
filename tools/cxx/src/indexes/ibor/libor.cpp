// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/indexes/ibor/libor.hpp: Libor::jointCalendar(), which the wheel doesn't bind. A Libor argument is the spec of
// tools/vectors/gen/indexes/ibor/libor.py, {"type": "Libor", "familyName", "tenor" ([length, units]),
// "settlementDays", "currency" (CAD, CHF, GBP or USD), "calendar" (Canada, Switzerland or TARGET), "dayCounter"
// (Actual365Fixed or Actual360)}, built inside the call. The calendar is returned as {"name", "isBusinessDay"}: its
// name, and whether each date of the second argument, a list of serials, is a business day on it.

#include "convert.hpp"

#include <ql/currencies/america.hpp>
#include <ql/currencies/europe.hpp>
#include <ql/indexes/ibor/libor.hpp>
#include <ql/time/calendars/canada.hpp>
#include <ql/time/calendars/switzerland.hpp>
#include <ql/time/calendars/target.hpp>
#include <ql/time/daycounters/actual360.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <initializer_list>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct LiborSpec {
            std::string familyName;
            Integer length;
            int units;
            Natural settlementDays;
            std::string currency, calendar, dayCounter;
        };

        const json::object& object(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a Libor spec, got " + json::serialize(v));
            return v.get_object();
        }

        const json::value& key(const json::object& o, const char* name) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string("a Libor spec needs ") + name);
            return *v;
        }

        std::string oneOf(const json::value& v, std::initializer_list<const char*> names, const std::string& what) {
            std::string s = text(v);
            for (const char* n : names)
                if (s == n)
                    return s;
            throw InvalidParams("unknown " + what + " " + s);
        }

        LiborSpec libor(const json::value& v) {
            const json::object& o = object(v);
            for (const auto& kv : o) {
                bool known = false;
                for (const char* k : {"type", "familyName", "tenor", "settlementDays", "currency", "calendar",
                                      "dayCounter"})
                    known = known || kv.key() == k;
                if (!known)
                    throw InvalidParams("a Libor spec takes no key " + std::string(kv.key()));
            }
            if (text(key(o, "type")) != "Libor")
                throw InvalidParams("expected a Libor spec, got " + json::serialize(v));
            LiborSpec s;
            s.familyName = text(key(o, "familyName"));
            const json::value& tenor = key(o, "tenor");
            if (!tenor.is_array() || tenor.get_array().size() != 2)
                throw InvalidParams("a tenor is [length, units], got " + json::serialize(tenor));
            s.length = integer(tenor.get_array()[0]);
            s.units = enumeration<int>(tenor.get_array()[1]);
            Integer days = integer(key(o, "settlementDays"));
            if (days == Null<Integer>() || days < 0)
                throw InvalidParams("expected a Natural, got " + json::serialize(key(o, "settlementDays")));
            s.settlementDays = static_cast<Natural>(days);
            s.currency = oneOf(key(o, "currency"), {"CAD", "CHF", "GBP", "USD"}, "currency");
            s.calendar = oneOf(key(o, "calendar"), {"Canada", "Switzerland", "TARGET"}, "calendar");
            s.dayCounter = oneOf(key(o, "dayCounter"), {"Actual365Fixed", "Actual360"}, "day counter");
            return s;
        }

        Currency makeCurrency(const std::string& code) {
            if (code == "CAD")
                return CADCurrency();
            if (code == "CHF")
                return CHFCurrency();
            if (code == "GBP")
                return GBPCurrency();
            return USDCurrency();
        }

        Calendar makeCalendar(const std::string& name) {
            if (name == "Canada")
                return Canada();
            if (name == "Switzerland")
                return Switzerland();
            return TARGET();
        }

        DayCounter makeDayCounter(const std::string& name) {
            if (name == "Actual365Fixed")
                return Actual365Fixed();
            return Actual360();
        }

        const Register unit({
            {"Libor::jointCalendar",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 LiborSpec s = libor(a[0]);
                 if (!a[1].is_array())
                     throw InvalidParams("expected an array of dates, got " + json::serialize(a[1]));
                 std::vector<DateSpec> dates;
                 for (const json::value& d : a[1].get_array())
                     dates.push_back(date(d));
                 return [=] {
                     Libor index(s.familyName, Period(s.length, static_cast<TimeUnit>(s.units)), s.settlementDays,
                                 makeCurrency(s.currency), makeCalendar(s.calendar), makeDayCounter(s.dayCounter));
                     Calendar joint = index.jointCalendar();
                     json::array businessDays;
                     for (const DateSpec& d : dates)
                         businessDays.emplace_back(joint.isBusinessDay(makeDate(d)));
                     return json::value(json::object{{"name", joint.name()}, {"isBusinessDay", businessDays}});
                 };
             }},
        });

    }

}
