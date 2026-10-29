// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/volatility/extendedblackvariancesurface.hpp, .cpp, which the wheel doesn't expose.
// "ExtendedBlackVarianceSurface::values" takes (referenceDate, dayCounter, dates, strikes, vols, lowerExtrapolation,
// upperExtrapolation, interpolation, queryDates, queryTimes, queryStrikes): the surface from referenceDate (a serial) on a
// null calendar and the day counter (a spec), its dates serials, its strikes, a quote per volatility in QuantLib's order
// (strike by strike, each strike's at every date), each extrapolation an ExtendedBlackVarianceSurface::Extrapolation, on
// QuantLib's Bilinear variance ("Bilinear") or after setInterpolation with "Bicubic" (QuantLib's Bicubic()); it returns
// {"maxDate", "minStrike", "maxStrike", "varianceAtDate", "volAtDate", "forwardVariance", "varianceAtTime",
// "volAtTime"}: at each query date (or time) the values at every query strike, extrapolating; the forward variances
// between consecutive query dates, each {"throws": true} where QuantLib refuses a falling variance.

#include "convert.hpp"

#include <ql/experimental/volatility/extendedblackvariancesurface.hpp>
#include <ql/math/interpolations/bicubicsplineinterpolation.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/time/calendars/nullcalendar.hpp>

#include <functional>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        template <class F>
        json::value attempted(F f) {
            try {
                return json::value(f());
            } catch (const Error&) {
                return json::object{{"throws", true}};
            }
        }

        json::array grid(const std::vector<Date>& ds, const std::vector<Real>& strikes,
                         const std::function<Real(const Date&, Real)>& f) {
            json::array out;
            for (const Date& d : ds) {
                json::array row;
                for (Real k : strikes)
                    row.push_back(json::value(f(d, k)));
                out.push_back(row);
            }
            return out;
        }

        const Register unit({
            {"ExtendedBlackVarianceSurface::values",
             [](const Args& a) -> Call {
                 arity(a, 11);
                 const DateSpec referenceDate = date(a[0]);
                 const DayCounterSpec dc = dayCounter(a[1]);
                 const std::vector<Integer> dateSerials = integers(a[2]);
                 const std::vector<Real> strikes = reals(a[3]);
                 const std::vector<Real> vols = reals(a[4]);
                 const auto lower = enumeration<ExtendedBlackVarianceSurface::Extrapolation>(a[5]);
                 const auto upper = enumeration<ExtendedBlackVarianceSurface::Extrapolation>(a[6]);
                 const std::string interpolation = text(a[7]);
                 if (interpolation != "Bilinear" && interpolation != "Bicubic")
                     throw InvalidParams("unknown interpolation " + interpolation);
                 const std::vector<Integer> querySerials = integers(a[8]);
                 const std::vector<Real> queryTimes = reals(a[9]);
                 const std::vector<Real> queryStrikes = reals(a[10]);
                 return [=] {
                     std::vector<Date> ds, queries;
                     for (Integer s : dateSerials)
                         ds.emplace_back(Date::serial_type(s));
                     for (Integer s : querySerials)
                         queries.emplace_back(Date::serial_type(s));
                     std::vector<Handle<Quote>> quotes;
                     for (Real vol : vols)
                         quotes.emplace_back(ext::make_shared<SimpleQuote>(vol));
                     ExtendedBlackVarianceSurface surface(makeDate(referenceDate), NullCalendar(), ds, strikes, quotes,
                                                          makeDayCounter(dc), lower, upper);
                     if (interpolation == "Bicubic")
                         surface.setInterpolation<Bicubic>();

                     json::object out;
                     out["maxDate"] = result(surface.maxDate());
                     out["minStrike"] = json::value(surface.minStrike());
                     out["maxStrike"] = json::value(surface.maxStrike());
                     out["varianceAtDate"] = grid(queries, queryStrikes, [&](const Date& d, Real k) {
                         return surface.blackVariance(d, k, true);
                     });
                     out["volAtDate"] = grid(queries, queryStrikes, [&](const Date& d, Real k) {
                         return surface.blackVol(d, k, true);
                     });
                     json::array forward;
                     for (Size i = 0; i + 1 < queries.size(); ++i) {
                         json::array row;
                         for (Real k : queryStrikes)
                             row.push_back(attempted([&] {
                                 return surface.blackForwardVariance(queries[i], queries[i + 1], k, true);
                             }));
                         forward.push_back(row);
                     }
                     out["forwardVariance"] = forward;
                     json::array variances, volatilities;
                     for (Time t : queryTimes) {
                         json::array v1, v2;
                         for (Real k : queryStrikes) {
                             v1.push_back(json::value(surface.blackVariance(t, k, true)));
                             v2.push_back(json::value(surface.blackVol(t, k, true)));
                         }
                         variances.push_back(v1);
                         volatilities.push_back(v2);
                     }
                     out["varianceAtTime"] = variances;
                     out["volAtTime"] = volatilities;
                     return json::value(out);
                 };
             }},
        });

    }

}
