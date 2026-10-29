// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/volatility/sabrvoltermstructure.hpp, which the wheel doesn't expose. "SABRVolTermStructure::values"
// takes (alpha, beta, gamma, rho, s0, r, referenceDate, dayCounter, queryTimes, queryDates, queryStrikes): the structure
// on its SABR parameters, the underlying's value and growth rate, from referenceDate (a serial) on the day counter (a
// spec); it returns {"maxDate", "minStrike", "maxStrike", "volAtTime", "varianceAtTime", "volAtDate", "varianceAtDate"}:
// at each query time (or date) the values at every query strike.

#include "convert.hpp"

#include <ql/experimental/volatility/sabrvoltermstructure.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"SABRVolTermStructure::values",
             [](const Args& a) -> Call {
                 arity(a, 11);
                 const Real alpha = real(a[0]), beta = real(a[1]), gamma = real(a[2]), rho = real(a[3]);
                 const Real s0 = real(a[4]), r = real(a[5]);
                 const DateSpec referenceDate = date(a[6]);
                 const DayCounterSpec dc = dayCounter(a[7]);
                 const std::vector<Real> queryTimes = reals(a[8]);
                 const std::vector<Integer> querySerials = integers(a[9]);
                 const std::vector<Real> queryStrikes = reals(a[10]);
                 return [=] {
                     SABRVolTermStructure ts(alpha, beta, gamma, rho, s0, r, makeDate(referenceDate),
                                             makeDayCounter(dc));
                     json::object out;
                     out["maxDate"] = result(ts.maxDate());
                     out["minStrike"] = json::value(ts.minStrike());
                     out["maxStrike"] = json::value(ts.maxStrike());
                     json::array volAtTime, varianceAtTime, volAtDate, varianceAtDate;
                     for (Time t : queryTimes) {
                         json::array v1, v2;
                         for (Real k : queryStrikes) {
                             v1.push_back(json::value(ts.blackVol(t, k, true)));
                             v2.push_back(json::value(ts.blackVariance(t, k, true)));
                         }
                         volAtTime.push_back(v1);
                         varianceAtTime.push_back(v2);
                     }
                     for (Integer s : querySerials) {
                         Date d = Date(Date::serial_type(s));
                         json::array v1, v2;
                         for (Real k : queryStrikes) {
                             v1.push_back(json::value(ts.blackVol(d, k, true)));
                             v2.push_back(json::value(ts.blackVariance(d, k, true)));
                         }
                         volAtDate.push_back(v1);
                         varianceAtDate.push_back(v2);
                     }
                     out["volAtTime"] = volAtTime;
                     out["varianceAtTime"] = varianceAtTime;
                     out["volAtDate"] = volAtDate;
                     out["varianceAtDate"] = varianceAtDate;
                     return json::value(out);
                 };
             }},
        });

    }

}
