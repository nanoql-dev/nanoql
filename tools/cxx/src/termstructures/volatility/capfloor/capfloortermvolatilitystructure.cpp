// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/capfloor/capfloortermvolatilitystructure.hpp: the members of
// CapFloorTermVolatilityStructure and its bases, on a CapFloorTermVolSurface built inside each call from a surface
// spec of capfloorvolspec.hpp.
//
// each method named after this unit's class, through which the surface reaches its bases' members:
//
//     CapFloorTermVolatilityStructure::settlementDays      (surface): TermStructure's count, or QuantLib's refusal
//                                                          for a fixed reference date; the wheel doesn't bind it
//     CapFloorTermVolatilityStructure::referenceDate, maxDate, maxTime (surface)
//     CapFloorTermVolatilityStructure::minStrike, maxStrike, businessDayConvention (surface)
//     CapFloorTermVolatilityStructure::optionDateFromTenor (surface, [length, units])
//     CapFloorTermVolatilityStructure::volatility(Period)  (surface, [length, units], strike, extrapolate)
//     CapFloorTermVolatilityStructure::volatility(Date)    (surface, date, strike, extrapolate)
//     CapFloorTermVolatilityStructure::volatility(Time)    (surface, time, strike, extrapolate)

#include "convert.hpp"
#include "termstructures/volatility/capfloor/capfloorvolspec.hpp"

#include <ql/termstructures/volatility/capfloor/capfloortermvolsurface.hpp>
#include <ql/time/period.hpp>

#include <cstdint>
#include <utility>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        // A member of the surface alone: (surface).
        Call whole(const Args& a, json::value (*f)(const CapFloorTermVolSurface&)) {
            arity(a, 1);
            capfloorvols::SurfaceSpec s = capfloorvols::surface(a[0]);
            return [=] { return f(*capfloorvols::makeSurface(s)); };
        }

        // A volatility at a point: (surface, point, strike, extrapolate).
        template <class P, class F>
        Call atPoint(const Args& a, P (*point)(const json::value&), F f) {
            arity(a, 4);
            capfloorvols::SurfaceSpec s = capfloorvols::surface(a[0]);
            P p = point(a[1]);
            Rate strike = real(a[2]);
            bool extrapolate = boolean(a[3]);
            return [=] { return json::value(f(*capfloorvols::makeSurface(s), p, strike, extrapolate)); };
        }

        std::pair<Integer, TimeUnit> tenorOf(const json::value& v) { return capfloorvols::period(v); }
        DateSpec dateOf(const json::value& v) { return date(v); }
        Real timeOf(const json::value& v) { return real(v); }

        const Register unit({
            {"CapFloorTermVolatilityStructure::settlementDays",
             [](const Args& a) {
                 return whole(a, [](const CapFloorTermVolSurface& s) {
                     return json::value(static_cast<std::int64_t>(s.settlementDays()));
                 });
             }},
            {"CapFloorTermVolatilityStructure::referenceDate",
             [](const Args& a) {
                 return whole(a, [](const CapFloorTermVolSurface& s) { return result(s.referenceDate()); });
             }},
            {"CapFloorTermVolatilityStructure::maxDate",
             [](const Args& a) { return whole(a, [](const CapFloorTermVolSurface& s) { return result(s.maxDate()); }); }},
            {"CapFloorTermVolatilityStructure::maxTime",
             [](const Args& a) {
                 return whole(a, [](const CapFloorTermVolSurface& s) { return json::value(s.maxTime()); });
             }},
            {"CapFloorTermVolatilityStructure::minStrike",
             [](const Args& a) {
                 return whole(a, [](const CapFloorTermVolSurface& s) { return json::value(s.minStrike()); });
             }},
            {"CapFloorTermVolatilityStructure::maxStrike",
             [](const Args& a) {
                 return whole(a, [](const CapFloorTermVolSurface& s) { return json::value(s.maxStrike()); });
             }},
            {"CapFloorTermVolatilityStructure::businessDayConvention",
             [](const Args& a) {
                 return whole(a, [](const CapFloorTermVolSurface& s) {
                     return json::value(static_cast<std::int64_t>(s.businessDayConvention()));
                 });
             }},
            {"CapFloorTermVolatilityStructure::optionDateFromTenor",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 capfloorvols::SurfaceSpec s = capfloorvols::surface(a[0]);
                 std::pair<Integer, TimeUnit> t = capfloorvols::period(a[1]);
                 return [=] { return result(capfloorvols::makeSurface(s)->optionDateFromTenor(Period(t.first, t.second))); };
             }},
            {"CapFloorTermVolatilityStructure::volatility(Period)",
             [](const Args& a) {
                 return atPoint(a, tenorOf,
                                [](const CapFloorTermVolSurface& s, const std::pair<Integer, TimeUnit>& t, Rate k,
                                   bool e) { return s.volatility(Period(t.first, t.second), k, e); });
             }},
            {"CapFloorTermVolatilityStructure::volatility(Date)",
             [](const Args& a) {
                 return atPoint(a, dateOf, [](const CapFloorTermVolSurface& s, const DateSpec& d, Rate k, bool e) {
                     return s.volatility(makeDate(d), k, e);
                 });
             }},
            {"CapFloorTermVolatilityStructure::volatility(Time)",
             [](const Args& a) {
                 return atPoint(a, timeOf,
                                [](const CapFloorTermVolSurface& s, Real t, Rate k, bool e) { return s.volatility(t, k, e); });
             }},
        });

    }

}
