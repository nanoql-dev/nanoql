// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/credit/piecewisedefaultcurve.hpp on the default-density and survival-probability traits, which
// the wheel doesn't bind (it binds the hazard-rate traits on backward-flat interpolation alone).
// "PiecewiseDefaultCurve::build" takes the traits ("HazardRate", "DefaultDensity" or "SurvivalProbability"), the
// interpolator ("BackwardFlat", "Linear" or "LogLinear"), the CDS helpers (termstructures/credit/creditspec.hpp's),
// their quotes, the curve's reference date and day counter, and sample dates; the result is the curve's dates, times
// and data, and its survival probability and hazard rate at each sample date, read with extrapolation.

#include "termstructures/credit/creditspec.hpp"

#include <ql/math/interpolations/backwardflatinterpolation.hpp>
#include <ql/math/interpolations/linearinterpolation.hpp>
#include <ql/math/interpolations/loginterpolation.hpp>
#include <ql/termstructures/credit/piecewisedefaultcurve.hpp>
#include <ql/termstructures/credit/probabilitytraits.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct CurveSpec {
            std::string traits, interpolator;
            std::vector<credit::HelperSpec> helpers;
            std::vector<Real> quotes;
            DateSpec referenceDate{true, 0};
            credit::DayCounterSpec dayCounter;
            std::vector<DateSpec> sample;
        };

        template <class T, class I>
        json::value values(const CurveSpec& s) {
            std::vector<ext::shared_ptr<DefaultProbabilityHelper>> helpers;
            for (std::size_t i = 0; i < s.helpers.size(); ++i)
                helpers.push_back(credit::makeHelper(s.helpers[i], s.quotes[i]));
            PiecewiseDefaultCurve<T, I> curve(makeDate(s.referenceDate), helpers, credit::makeDayCounter(s.dayCounter));
            json::array dates, times, data, survival, hazard;
            for (const Date& d : curve.dates())
                dates.push_back(result(d));
            for (Time x : curve.times())
                times.push_back(json::value(x));
            for (Real x : curve.data())
                data.push_back(json::value(x));
            for (const DateSpec& ds : s.sample) {
                Date d = makeDate(ds);
                survival.push_back(json::value(curve.survivalProbability(d, true)));
                hazard.push_back(json::value(curve.hazardRate(d, true)));
            }
            return json::object{{"dates", dates},
                                {"times", times},
                                {"data", data},
                                {"survivalProbability", survival},
                                {"hazardRate", hazard}};
        }

        template <class T>
        json::value valuesOn(const CurveSpec& s) {
            if (s.interpolator == "BackwardFlat")
                return values<T, BackwardFlat>(s);
            if (s.interpolator == "Linear")
                return values<T, Linear>(s);
            return values<T, LogLinear>(s);
        }

        json::value build(const CurveSpec& s) {
            if (s.traits == "HazardRate")
                return valuesOn<HazardRate>(s);
            if (s.traits == "DefaultDensity")
                return valuesOn<DefaultDensity>(s);
            return valuesOn<SurvivalProbability>(s);
        }

        const Register unit({
            // (traits, interpolator, helpers, quotes, reference date, day counter, sample dates)
            {"PiecewiseDefaultCurve::build",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 CurveSpec s;
                 s.traits = text(a[0]);
                 if (s.traits != "HazardRate" && s.traits != "DefaultDensity" && s.traits != "SurvivalProbability")
                     throw InvalidParams("unknown traits " + s.traits);
                 s.interpolator = text(a[1]);
                 if (s.interpolator != "BackwardFlat" && s.interpolator != "Linear" && s.interpolator != "LogLinear")
                     throw InvalidParams("unknown interpolator " + s.interpolator);
                 if (!a[2].is_array())
                     throw InvalidParams("expected the helpers as an array");
                 for (const json::value& h : a[2].get_array())
                     s.helpers.push_back(credit::helper(h));
                 s.quotes = reals(a[3]);
                 if (s.quotes.size() != s.helpers.size())
                     throw InvalidParams("expected one quote per helper");
                 s.referenceDate = date(a[4]);
                 s.dayCounter = credit::dayCounter(a[5]);
                 if (!a[6].is_array())
                     throw InvalidParams("expected the sample dates as an array");
                 for (const json::value& d : a[6].get_array())
                     s.sample.push_back(date(d));
                 return [=] { return build(s); };
             }},
        });

    }

}
