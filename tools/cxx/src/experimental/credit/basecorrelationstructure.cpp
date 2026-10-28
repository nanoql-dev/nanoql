// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/credit/basecorrelationstructure.hpp and correlationstructure.hpp, which the wheel doesn't expose,
// on experimental/credit/portfoliospec's bilinear surface:
//
//     BaseCorrelationTermStructure::reads (surface, dates, times, levels, tenors): [referenceDate, maxDate,
//         correlationSize, businessDayConvention, correlation(d, level) for each date and level, correlation(t, level)
//         for each time and level, dateFromTenor(p) for each tenor ([length, units])], dates as serials

#include "experimental/credit/portfoliospec.hpp"

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"BaseCorrelationTermStructure::reads",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 json::value surface = a[0], tenors = a[4];
                 std::vector<DateSpec> dates;
                 if (!a[1].is_array())
                     throw InvalidParams("expected an array of dates");
                 for (const json::value& d : a[1].get_array())
                     dates.push_back(date(d));
                 std::vector<Real> times = reals(a[2]), levels = reals(a[3]);
                 if (!tenors.is_array())
                     throw InvalidParams("expected an array of tenors");
                 return [=] {
                     auto s = credit::portfolio::makeBaseCorrelationSurface(surface);
                     json::array byDate, byTime, fromTenor;
                     for (const DateSpec& ds : dates)
                         for (Real l : levels)
                             byDate.emplace_back(s->correlation(makeDate(ds), l));
                     for (Real t : times)
                         for (Real l : levels)
                             byTime.emplace_back(s->correlation(t, l));
                     for (const json::value& p : tenors.get_array())
                         fromTenor.emplace_back(
                             static_cast<std::int64_t>(s->dateFromTenor(credit::portfolio::period(p)).serialNumber()));
                     json::array out;
                     out.emplace_back(static_cast<std::int64_t>(s->referenceDate().serialNumber()));
                     out.emplace_back(static_cast<std::int64_t>(s->maxDate().serialNumber()));
                     out.emplace_back(static_cast<std::int64_t>(s->correlationSize()));
                     out.emplace_back(static_cast<std::int64_t>(s->businessDayConvention()));
                     for (auto* x : {&byDate, &byTime, &fromTenor})
                         out.emplace_back(std::move(*x));
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
