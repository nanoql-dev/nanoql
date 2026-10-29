// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/volatility/extendedblackvariancecurve.hpp, .cpp, which the wheel doesn't expose.
// "ExtendedBlackVarianceCurve::values" takes (referenceDate, dayCounter, dates, vols, forceMonotoneVariance,
// interpolation, queryDates, queryTimes, strike): the curve from referenceDate (a serial) on the day counter (a spec), its
// dates serials and a quote per volatility, on QuantLib's Linear variance ("Linear") or after setInterpolation with
// "Cubic" (QuantLib's Cubic()) or "BackwardFlat"; it returns {"maxDate", "minStrike", "maxStrike", "varianceAtDate",
// "volAtDate", "forwardVariance", "varianceAtTime", "volAtTime"}: at each query date (or time) the values at the strike,
// extrapolating; the forward variances between consecutive query dates, each {"throws": true} where QuantLib refuses a
// falling variance.

#include "convert.hpp"

#include <ql/experimental/volatility/extendedblackvariancecurve.hpp>
#include <ql/math/interpolations/backwardflatinterpolation.hpp>
#include <ql/math/interpolations/cubicinterpolation.hpp>
#include <ql/quotes/simplequote.hpp>

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

        const Register unit({
            {"ExtendedBlackVarianceCurve::values",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 const DateSpec referenceDate = date(a[0]);
                 const DayCounterSpec dc = dayCounter(a[1]);
                 const std::vector<Integer> dateSerials = integers(a[2]);
                 const std::vector<Real> vols = reals(a[3]);
                 const bool forceMonotone = boolean(a[4]);
                 const std::string interpolation = text(a[5]);
                 if (interpolation != "Linear" && interpolation != "Cubic" && interpolation != "BackwardFlat")
                     throw InvalidParams("unknown interpolation " + interpolation);
                 const std::vector<Integer> querySerials = integers(a[6]);
                 const std::vector<Real> queryTimes = reals(a[7]);
                 const Real strike = real(a[8]);
                 return [=] {
                     std::vector<Date> ds, queries;
                     for (Integer s : dateSerials)
                         ds.emplace_back(Date::serial_type(s));
                     for (Integer s : querySerials)
                         queries.emplace_back(Date::serial_type(s));
                     std::vector<Handle<Quote>> quotes;
                     for (Real vol : vols)
                         quotes.emplace_back(ext::make_shared<SimpleQuote>(vol));
                     ExtendedBlackVarianceCurve curve(makeDate(referenceDate), ds, quotes, makeDayCounter(dc),
                                                      forceMonotone);
                     if (interpolation == "Cubic")
                         curve.setInterpolation<Cubic>();
                     else if (interpolation == "BackwardFlat")
                         curve.setInterpolation<BackwardFlat>();

                     json::object out;
                     out["maxDate"] = result(curve.maxDate());
                     out["minStrike"] = json::value(curve.minStrike());
                     out["maxStrike"] = json::value(curve.maxStrike());
                     json::array varianceAtDate, volAtDate, forward, varianceAtTime, volAtTime;
                     for (const Date& d : queries) {
                         varianceAtDate.push_back(json::value(curve.blackVariance(d, strike, true)));
                         volAtDate.push_back(json::value(curve.blackVol(d, strike, true)));
                     }
                     for (Size i = 0; i + 1 < queries.size(); ++i)
                         forward.push_back(attempted([&] {
                             return curve.blackForwardVariance(queries[i], queries[i + 1], strike, true);
                         }));
                     for (Time t : queryTimes) {
                         varianceAtTime.push_back(json::value(curve.blackVariance(t, strike, true)));
                         volAtTime.push_back(json::value(curve.blackVol(t, strike, true)));
                     }
                     out["varianceAtDate"] = varianceAtDate;
                     out["volAtDate"] = volAtDate;
                     out["forwardVariance"] = forward;
                     out["varianceAtTime"] = varianceAtTime;
                     out["volAtTime"] = volAtTime;
                     return json::value(out);
                 };
             }},
        });

    }

}
