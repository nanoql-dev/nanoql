// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/indexes/inflation/aucpi.hpp: YYAUCPI, which the wheel doesn't bind, read as
// tools/vectors/gen/indexes/inflationindex.py's case families read an index, each method one family's outputs. Every
// method takes the preset's frequency (an integer) and revised flag first; the fixings are [[serial, value], ...],
// added in order through addFixing, the curves cashflows/inflationspec.hpp's year-on-year curve specs (null for an
// empty handle), and a lag [length, units].
//
// - "YYAUCPI::inspectors" (frequency, revised, weekend): the index's inspectors.
// - "YYAUCPI::reads" (frequency, revised, fixings, dates): lastFixingDate, and needsForecast, fixing and pastFixing at
//   each date.
// - "YYAUCPI::forecasts" (frequency, revised, curve, fixings, dates, clone curve): needsForecast and fixing at each
//   date, and the name and fixings of clone() on the clone curve.
// - "YYAUCPI::lagged" (frequency, revised, curve, fixings, dates, lags, interpolations): CPI::laggedYoYRate as
//   [date][lag][interpolation].
//
// Each value QuantLib refuses is {"throws": true}.

#include "cashflows/inflationspec.hpp"
#include "convert.hpp"

#include <ql/indexes/inflation/aucpi.hpp>

#include <utility>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        template <class F>
        json::value attempted(F f) {
            try {
                return f();
            } catch (const Error&) {
                return json::object{{"throws", true}};
            }
        }

        struct Preset {
            Frequency frequency;
            bool revised;
        };

        Preset preset(const Args& a) { return {enumeration<Frequency>(a[0]), boolean(a[1])}; }

        std::vector<DateSpec> dates(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected an array of dates, got " + json::serialize(v));
            std::vector<DateSpec> out;
            for (const json::value& d : v.get_array())
                out.push_back(date(d));
            return out;
        }

        std::vector<std::pair<DateSpec, Real>> fixings(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected an array of fixings, got " + json::serialize(v));
            std::vector<std::pair<DateSpec, Real>> out;
            for (const json::value& f : v.get_array()) {
                if (!f.is_array() || f.get_array().size() != 2)
                    throw InvalidParams("a fixing is [serial, value], got " + json::serialize(f));
                out.emplace_back(date(f.get_array()[0]), real(f.get_array()[1]));
            }
            return out;
        }

        ext::shared_ptr<YoYInflationIndex> make(const Preset& p,
                                                const cashflows::InflationCurveSpec& curve,
                                                const std::vector<std::pair<DateSpec, Real>>& stored) {
            auto index = ext::make_shared<YYAUCPI>(p.frequency, p.revised, cashflows::makeYoYCurve(curve));
            for (const auto& [d, value] : stored)
                index->addFixing(makeDate(d), value);
            return index;
        }

        template <class F>
        json::array each(const std::vector<DateSpec>& days, F f) {
            json::array out;
            for (const DateSpec& d : days) {
                Date day = makeDate(d);
                out.push_back(attempted([&] { return f(day); }));
            }
            return out;
        }

        const Register unit({
            {"YYAUCPI::inspectors",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 Preset p = preset(a);
                 DateSpec weekend = date(a[2]);
                 return [=] {
                     YYAUCPI index(p.frequency, p.revised);
                     Period lag = index.availabilityLag();
                     return json::value(json::object{
                         {"name", index.name()},
                         {"familyName", index.familyName()},
                         {"regionName", index.region().name()},
                         {"regionCode", index.region().code()},
                         {"revised", index.revised()},
                         {"frequency", static_cast<int>(index.frequency())},
                         {"availabilityLagLength", lag.length()},
                         {"availabilityLagUnits", static_cast<int>(lag.units())},
                         {"currency", index.currency().code()},
                         {"fixingCalendar", index.fixingCalendar().name()},
                         {"isValidFixingDate", index.isValidFixingDate(makeDate(weekend))},
                         {"ratio", index.ratio()},
                         {"underlying", index.ratio() ? json::value(index.underlyingIndex()->name()) : nullptr}});
                 };
             }},
            {"YYAUCPI::reads",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Preset p = preset(a);
                 auto stored = fixings(a[2]);
                 auto days = dates(a[3]);
                 return [=] {
                     auto index = make(p, cashflows::InflationCurveSpec(), stored);
                     return json::value(json::object{
                         {"lastFixingDate", attempted([&] { return result(index->lastFixingDate()); })},
                         {"needsForecast",
                          each(days, [&](const Date& d) { return json::value(index->needsForecast(d)); })},
                         {"fixing", each(days, [&](const Date& d) { return json::value(index->fixing(d)); })},
                         {"pastFixing", each(days, [&](const Date& d) { return json::value(index->pastFixing(d)); })}});
                 };
             }},
            {"YYAUCPI::forecasts",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 Preset p = preset(a);
                 cashflows::InflationCurveSpec curve = cashflows::inflationCurve(a[2]);
                 auto stored = fixings(a[3]);
                 auto days = dates(a[4]);
                 cashflows::InflationCurveSpec cloneCurve = cashflows::inflationCurve(a[5]);
                 return [=] {
                     auto index = make(p, curve, stored);
                     auto clone = index->clone(cashflows::makeYoYCurve(cloneCurve));
                     return json::value(json::object{
                         {"needsForecast",
                          each(days, [&](const Date& d) { return json::value(index->needsForecast(d)); })},
                         {"fixing", each(days, [&](const Date& d) { return json::value(index->fixing(d)); })},
                         {"cloneName", clone->name()},
                         {"cloneFixing", each(days, [&](const Date& d) { return json::value(clone->fixing(d)); })}});
                 };
             }},
            {"YYAUCPI::lagged",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 Preset p = preset(a);
                 cashflows::InflationCurveSpec curve = cashflows::inflationCurve(a[2]);
                 auto stored = fixings(a[3]);
                 auto days = dates(a[4]);
                 if (!a[5].is_array())
                     throw InvalidParams("expected an array of lags, got " + json::serialize(a[5]));
                 std::vector<cashflows::LagPeriod> lags;
                 for (const json::value& l : a[5].get_array())
                     lags.push_back(cashflows::lagPeriod(l));
                 std::vector<CPI::InterpolationType> interpolations;
                 for (Integer i : integers(a[6]))
                     interpolations.push_back(static_cast<CPI::InterpolationType>(i));
                 return [=] {
                     auto index = make(p, curve, stored);
                     json::array lagged;
                     for (const DateSpec& d : days) {
                         json::array byLag;
                         for (const cashflows::LagPeriod& l : lags) {
                             json::array byInterpolation;
                             for (CPI::InterpolationType i : interpolations)
                                 byInterpolation.push_back(attempted([&] {
                                     return json::value(
                                         CPI::laggedYoYRate(index, makeDate(d), cashflows::makePeriod(l), i));
                                 }));
                             byLag.push_back(std::move(byInterpolation));
                         }
                         lagged.push_back(std::move(byLag));
                     }
                     return json::value(json::object{{"lagged", std::move(lagged)}});
                 };
             }},
        });

    }

}
