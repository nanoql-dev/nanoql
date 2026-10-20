// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/credit/interpolatedhazardratecurve.hpp, interpolateddefaultdensitycurve.hpp and
// interpolatedsurvivalprobabilitycurve.hpp on the interpolators and with the jumps the wheel doesn't bind.
// "InterpolatedCreditCurve::values" takes a credit curve spec (termstructures/credit/creditspec.hpp's, with an
// interpolator and jumps) and sample dates; the result is the curve's dates, times, data, maximum date, jump dates and
// jump times, and at each sample date, read with extrapolation, its survival probability, default density and hazard
// rate, each {"throws": true} where QuantLib refuses it (a jump outside (0, 1], a log-linear primitive).

#include "termstructures/credit/creditspec.hpp"

#include <ql/termstructures/credit/interpolateddefaultdensitycurve.hpp>
#include <ql/termstructures/credit/interpolatedhazardratecurve.hpp>
#include <ql/termstructures/credit/interpolatedsurvivalprobabilitycurve.hpp>
#include <ql/math/interpolations/backwardflatinterpolation.hpp>
#include <ql/math/interpolations/linearinterpolation.hpp>
#include <ql/math/interpolations/loginterpolation.hpp>

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

        template <template <class> class C, class I>
        bool nodes(const DefaultProbabilityTermStructure& t, json::object& out) {
            const auto* c = dynamic_cast<const C<I>*>(&t);
            if (c == nullptr)
                return false;
            json::array dates, times, data;
            for (const Date& d : c->dates())
                dates.push_back(result(d));
            for (Time x : c->times())
                times.push_back(json::value(x));
            for (Real x : c->data())
                data.push_back(json::value(x));
            out["dates"] = dates;
            out["times"] = times;
            out["data"] = data;
            return true;
        }

        template <template <class> class C>
        bool nodesOn(const DefaultProbabilityTermStructure& t, json::object& out) {
            return nodes<C, BackwardFlat>(t, out) || nodes<C, Linear>(t, out) || nodes<C, LogLinear>(t, out);
        }

        json::value values(const credit::CurveSpec& s, const std::vector<DateSpec>& sample) {
            ext::shared_ptr<DefaultProbabilityTermStructure> curve = credit::makeCurve(s);
            json::object out;
            if (!nodesOn<InterpolatedHazardRateCurve>(*curve, out) &&
                !nodesOn<InterpolatedDefaultDensityCurve>(*curve, out) &&
                !nodesOn<InterpolatedSurvivalProbabilityCurve>(*curve, out))
                throw InvalidParams("expected an interpolated credit curve");
            out["maxDate"] = result(curve->maxDate());
            json::array jumpDates, jumpTimes, survival, density, hazard;
            for (const Date& d : curve->jumpDates())
                jumpDates.push_back(result(d));
            for (Time x : curve->jumpTimes())
                jumpTimes.push_back(json::value(x));
            for (const DateSpec& ds : sample) {
                Date d = makeDate(ds);
                survival.push_back(attempted([&] { return curve->survivalProbability(d, true); }));
                density.push_back(attempted([&] { return curve->defaultDensity(d, true); }));
                hazard.push_back(attempted([&] { return curve->hazardRate(d, true); }));
            }
            out["jumpDates"] = jumpDates;
            out["jumpTimes"] = jumpTimes;
            out["survivalProbability"] = survival;
            out["defaultDensity"] = density;
            out["hazardRate"] = hazard;
            return out;
        }

        const Register unit({
            // (curve, sample dates)
            {"InterpolatedCreditCurve::values",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 credit::CurveSpec s = credit::curve(a[0]);
                 if (!a[1].is_array())
                     throw InvalidParams("expected the sample dates as an array");
                 std::vector<DateSpec> sample;
                 for (const json::value& d : a[1].get_array())
                     sample.push_back(date(d));
                 return [=] { return values(s, sample); };
             }},
        });

    }

}
