// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/volatility/abcdatmvolcurve.hpp, .cpp, which the wheel doesn't expose. "AbcdAtmVolCurve" takes
// (settlementDays, calendar, tenors, vols, inclusion, times, queryTenors, queryDates): the curve on the calendar
// ("TARGET" or "Null") from the evaluation date, its option tenors each [length, TimeUnit], a quote per volatility, the
// inclusion flags (null for QuantLib's default, every one); it returns {"a", "b", "c", "d", "rmsError", "maxError",
// "endCriteria", "k", "optionTimes", "maxDate", "minStrike", "maxStrike", "atmVol", "atmVariance", "kAt",
// "atmVolAtTenor", "atmVarianceAtTenor", "atmVolAtDate", "atmVarianceAtDate"}: the fit after the curve's first
// calculation, its adjustment factors, the option times, its strike range, the volatility, variance and factor at the
// given times, and the volatility and variance at the query tenors (BlackAtmVolCurve's period overloads) and dates
// (serials).

#include "convert.hpp"

#include <ql/experimental/volatility/abcdatmvolcurve.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/calendars/target.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        Calendar calendarOf(const std::string& name) {
            if (name == "TARGET")
                return TARGET();
            if (name == "Null")
                return NullCalendar();
            throw InvalidParams("unknown calendar " + name);
        }

        std::vector<Period> tenorsOf(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected a list of [length, unit], got " + json::serialize(v));
            std::vector<Period> out;
            for (const json::value& p : v.get_array()) {
                if (!p.is_array() || p.get_array().size() != 2)
                    throw InvalidParams("expected [length, unit], got " + json::serialize(p));
                out.emplace_back(integer(p.get_array()[0]), enumeration<TimeUnit>(p.get_array()[1]));
            }
            return out;
        }

        json::array array(const std::vector<Real>& x) {
            json::array out;
            for (Real xi : x)
                out.emplace_back(xi);
            return out;
        }

        const Register unit({
            {"AbcdAtmVolCurve",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 Integer settlementDays = integer(a[0]);
                 Calendar calendar = calendarOf(text(a[1]));
                 std::vector<Period> tenors = tenorsOf(a[2]);
                 std::vector<Real> vols = reals(a[3]);
                 std::vector<bool> inclusion(1, true);
                 if (!a[4].is_null()) {
                     inclusion.clear();
                     for (const json::value& f : a[4].get_array())
                         inclusion.push_back(boolean(f));
                 }
                 std::vector<Real> times = reals(a[5]);
                 std::vector<Period> queryTenors = tenorsOf(a[6]);
                 std::vector<Integer> queryDates = integers(a[7]);
                 return [=] {
                     std::vector<Handle<Quote>> quotes;
                     for (Real vol : vols)
                         quotes.emplace_back(ext::make_shared<SimpleQuote>(vol));
                     AbcdAtmVolCurve curve(static_cast<Natural>(settlementDays), calendar, tenors, quotes, inclusion);
                     json::object o;
                     std::vector<Real> atmVol, atmVariance, kAt;
                     for (Real t : times) {
                         atmVol.push_back(curve.atmVol(t));
                         atmVariance.push_back(curve.atmVariance(t));
                         kAt.push_back(curve.k(t));
                     }
                     o["a"] = json::value(curve.a());
                     o["b"] = json::value(curve.b());
                     o["c"] = json::value(curve.c());
                     o["d"] = json::value(curve.d());
                     o["rmsError"] = json::value(curve.rmsError());
                     o["maxError"] = json::value(curve.maxError());
                     o["endCriteria"] = json::value(static_cast<int>(curve.endCriteria()));
                     o["k"] = json::value(array(curve.k()));
                     o["optionTimes"] = json::value(array(curve.optionTimes()));
                     o["maxDate"] = result(curve.maxDate());
                     o["minStrike"] = json::value(curve.minStrike());
                     o["maxStrike"] = json::value(curve.maxStrike());
                     std::vector<Real> volAtTenor, varianceAtTenor, volAtDate, varianceAtDate;
                     for (const Period& p : queryTenors) {
                         volAtTenor.push_back(curve.atmVol(p));
                         varianceAtTenor.push_back(curve.atmVariance(p));
                     }
                     for (Integer s : queryDates) {
                         Date d = Date(Date::serial_type(s));
                         volAtDate.push_back(curve.atmVol(d));
                         varianceAtDate.push_back(curve.atmVariance(d));
                     }
                     o["atmVolAtTenor"] = json::value(array(volAtTenor));
                     o["atmVarianceAtTenor"] = json::value(array(varianceAtTenor));
                     o["atmVolAtDate"] = json::value(array(volAtDate));
                     o["atmVarianceAtDate"] = json::value(array(varianceAtDate));
                     o["atmVol"] = json::value(array(atmVol));
                     o["atmVariance"] = json::value(array(atmVariance));
                     o["kAt"] = json::value(array(kAt));
                     return json::value(o);
                 };
             }},
        });

    }

}
