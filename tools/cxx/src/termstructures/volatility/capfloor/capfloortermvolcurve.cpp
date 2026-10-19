// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/capfloor/capfloortermvolcurve.hpp: the members of CapFloorTermVolCurve the wheel doesn't
// bind, on a curve built inside each call from a curve spec of capfloorvolspec.hpp:
//
//     CapFloorTermVolCurve::settlementDays       (curve): TermStructure's count, or QuantLib's refusal for a fixed
//                                                reference date
//     CapFloorTermVolCurve::optionTenors         (curve): [[length, units], ...]
//     CapFloorTermVolCurve::optionDates          (curve)
//     CapFloorTermVolCurve::optionTimes          (curve)
//     CapFloorTermVolCurve::optionDateFromTenor  (curve, [length, units])

#include "convert.hpp"
#include "termstructures/volatility/capfloor/capfloorvolspec.hpp"

#include <ql/time/period.hpp>

#include <cstdint>
#include <utility>

using namespace QuantLib;

namespace harness {

    namespace {

        Call whole(const Args& a, json::value (*f)(const CapFloorTermVolCurve&)) {
            arity(a, 1);
            capfloorvols::SurfaceSpec s = capfloorvols::curve(a[0]);
            return [=] { return f(*capfloorvols::makeCurve(s)); };
        }

        const Register unit({
            {"CapFloorTermVolCurve::settlementDays",
             [](const Args& a) {
                 return whole(a, [](const CapFloorTermVolCurve& c) {
                     return json::value(static_cast<std::int64_t>(c.settlementDays()));
                 });
             }},
            {"CapFloorTermVolCurve::optionTenors",
             [](const Args& a) {
                 return whole(a, [](const CapFloorTermVolCurve& c) {
                     json::array out;
                     for (const Period& p : c.optionTenors())
                         out.push_back(json::array{static_cast<std::int64_t>(p.length()),
                                                   static_cast<std::int64_t>(p.units())});
                     return json::value(out);
                 });
             }},
            {"CapFloorTermVolCurve::optionDates",
             [](const Args& a) {
                 return whole(a, [](const CapFloorTermVolCurve& c) {
                     json::array out;
                     for (const Date& d : c.optionDates())
                         out.push_back(result(d));
                     return json::value(out);
                 });
             }},
            {"CapFloorTermVolCurve::optionTimes",
             [](const Args& a) {
                 return whole(a, [](const CapFloorTermVolCurve& c) {
                     json::array out;
                     for (Time t : c.optionTimes())
                         out.push_back(json::value(t));
                     return json::value(out);
                 });
             }},
            {"CapFloorTermVolCurve::optionDateFromTenor",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 capfloorvols::SurfaceSpec s = capfloorvols::curve(a[0]);
                 std::pair<Integer, TimeUnit> t = capfloorvols::period(a[1]);
                 return [=] {
                     return result(capfloorvols::makeCurve(s)->optionDateFromTenor(Period(t.first, t.second)));
                 };
             }},
        });

    }

}
