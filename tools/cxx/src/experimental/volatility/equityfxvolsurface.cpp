// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/volatility/equityfxvolsurface.hpp, .cpp, an abstract surface QuantLib derives nothing from, on the
// harness's surface below. "EquityFXVolSurface::values" takes (referenceDate, v0, slope, dates, times): the surface from
// referenceDate (a serial) on a null calendar, Following and Actual/365 (Fixed), its smile at time t a flat section of
// volatility v0 + slope·t at the level 100; it returns {"atmForwardVolAtDates", "atmForwardVarianceAtDates",
// "atmForwardVolAtTimes", "atmForwardVarianceAtTimes"} between each consecutive pair of dates (serials) and of times,
// each {"throws": true} where QuantLib refuses (pairs out of order, a variance that doesn't increase).

#include "convert.hpp"

#include <ql/experimental/volatility/equityfxvolsurface.hpp>
#include <ql/termstructures/volatility/flatsmilesection.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        // The harness's surface: a flat section at each time, its volatility linear in time.
        class LinearFlatSurface : public EquityFXVolSurface {
          public:
            LinearFlatSurface(const Date& referenceDate, Real v0, Real slope)
            : EquityFXVolSurface(referenceDate, NullCalendar(), Following, Actual365Fixed()), v0_(v0),
              slope_(slope) {}
            Date maxDate() const override { return Date::maxDate(); }
            Real minStrike() const override { return QL_MIN_REAL; }
            Real maxStrike() const override { return QL_MAX_REAL; }

          protected:
            ext::shared_ptr<SmileSection> smileSectionImpl(Time t) const override {
                return ext::make_shared<FlatSmileSection>(t, v0_ + slope_ * t, Actual365Fixed(), 100.0);
            }

          private:
            Real v0_, slope_;
        };

        template <class F>
        json::value attempted(F f) {
            try {
                return json::value(f());
            } catch (const Error&) {
                return json::object{{"throws", true}};
            }
        }

        const Register unit({
            {"EquityFXVolSurface::values",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 const DateSpec referenceDate = date(a[0]);
                 const Real v0 = real(a[1]), slope = real(a[2]);
                 const std::vector<Integer> serials = integers(a[3]);
                 const std::vector<Real> times = reals(a[4]);
                 return [=] {
                     LinearFlatSurface surface(makeDate(referenceDate), v0, slope);
                     json::array volDates, varianceDates, volTimes, varianceTimes;
                     for (Size i = 0; i + 1 < serials.size(); ++i) {
                         Date d1 = Date(Date::serial_type(serials[i])), d2 = Date(Date::serial_type(serials[i + 1]));
                         volDates.push_back(attempted([&] { return surface.atmForwardVol(d1, d2, true); }));
                         varianceDates.push_back(attempted([&] { return surface.atmForwardVariance(d1, d2, true); }));
                     }
                     for (Size i = 0; i + 1 < times.size(); ++i) {
                         Time t1 = times[i], t2 = times[i + 1];
                         volTimes.push_back(attempted([&] { return surface.atmForwardVol(t1, t2, true); }));
                         varianceTimes.push_back(attempted([&] { return surface.atmForwardVariance(t1, t2, true); }));
                     }
                     json::object out;
                     out["atmForwardVolAtDates"] = volDates;
                     out["atmForwardVarianceAtDates"] = varianceDates;
                     out["atmForwardVolAtTimes"] = volTimes;
                     out["atmForwardVarianceAtTimes"] = varianceTimes;
                     return json::value(out);
                 };
             }},
        });

    }

}
