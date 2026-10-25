// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/equityfx/impliedvoltermstructure.hpp: the implied Black-volatility structure, which the
// wheel doesn't expose. "ImpliedVolTermStructure::values" takes (originalReference, dayCounter, dates, strikes, vols,
// referenceDate, queryDates, queryTimes, queryStrikes): the original a BlackVarianceSurface from originalReference (a
// serial) on a null calendar and the day counter (a spec), its dates serials, its volatilities [strike][date], with
// QuantLib's default interpolation and extrapolation; the implied structure at referenceDate (a serial); and returns
// {"maxDate", "minStrike", "maxStrike", "varianceAtDate", "volAtDate", "forwardVariance", "varianceAtTime",
// "volAtTime"}: at each query date (or time) the values at every query strike, extrapolating; the forward variances
// between consecutive query dates.

#include "convert.hpp"

#include <ql/math/matrix.hpp>
#include <ql/termstructures/volatility/equityfx/blackvariancesurface.hpp>
#include <ql/termstructures/volatility/equityfx/impliedvoltermstructure.hpp>
#include <ql/time/calendars/nullcalendar.hpp>

#include <functional>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        json::array dates(const std::vector<Date>& ds, const std::vector<Real>& strikes,
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
            {"ImpliedVolTermStructure::values",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 const DateSpec originalReference = date(a[0]);
                 const DayCounterSpec dc = dayCounter(a[1]);
                 const std::vector<Integer> dateSerials = integers(a[2]);
                 const std::vector<Real> strikes = reals(a[3]);
                 if (!a[4].is_array())
                     throw InvalidParams("expected vols [strike][date], got " + json::serialize(a[4]));
                 std::vector<std::vector<Real>> vols;
                 for (const json::value& row : a[4].get_array())
                     vols.push_back(reals(row));
                 const DateSpec referenceDate = date(a[5]);
                 const std::vector<Integer> querySerials = integers(a[6]);
                 const std::vector<Real> queryTimes = reals(a[7]);
                 const std::vector<Real> queryStrikes = reals(a[8]);
                 for (const std::vector<Real>& row : vols) {
                     if (row.size() != dateSerials.size())
                         throw InvalidParams("a strike's vols are one per date");
                 }
                 if (vols.size() != strikes.size())
                     throw InvalidParams("the vols are one row per strike");
                 return [=] {
                     std::vector<Date> ds, queries;
                     for (Integer s : dateSerials)
                         ds.emplace_back(Date::serial_type(s));
                     for (Integer s : querySerials)
                         queries.emplace_back(Date::serial_type(s));
                     Matrix m(strikes.size(), ds.size());
                     for (Size i = 0; i < strikes.size(); ++i)
                         for (Size j = 0; j < ds.size(); ++j)
                             m[i][j] = vols[i][j];
                     const auto original = ext::make_shared<BlackVarianceSurface>(
                         makeDate(originalReference), NullCalendar(), ds, strikes, m, makeDayCounter(dc));
                     ImpliedVolTermStructure implied(Handle<BlackVolTermStructure>(original), makeDate(referenceDate));

                     json::object out;
                     out["maxDate"] = result(implied.maxDate());
                     out["minStrike"] = json::value(implied.minStrike());
                     out["maxStrike"] = json::value(implied.maxStrike());
                     out["varianceAtDate"] = dates(queries, queryStrikes, [&](const Date& d, Real k) {
                         return implied.blackVariance(d, k, true);
                     });
                     out["volAtDate"] = dates(queries, queryStrikes, [&](const Date& d, Real k) {
                         return implied.blackVol(d, k, true);
                     });
                     json::array forward;
                     for (Size i = 0; i + 1 < queries.size(); ++i) {
                         json::array row;
                         for (Real k : queryStrikes)
                             row.push_back(json::value(implied.blackForwardVariance(queries[i], queries[i + 1], k, true)));
                         forward.push_back(row);
                     }
                     out["forwardVariance"] = forward;
                     json::array variances, volatilities;
                     for (Time t : queryTimes) {
                         json::array v1, v2;
                         for (Real k : queryStrikes) {
                             v1.push_back(json::value(implied.blackVariance(t, k, true)));
                             v2.push_back(json::value(implied.blackVol(t, k, true)));
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
