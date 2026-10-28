// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/credit/onefactoraffinesurvival.hpp and interpolatedaffinehazardratecurve.hpp, which the wheel
// doesn't expose, on a one-factor affine model given as ["Vasicek", r0, a, b, sigma] or ["CIR", r0, theta, k, sigma]
// (Feller's constraint on), and termstructures/credit/creditspec.hpp's CDS helpers:
//
//     OneFactorAffineSurvivalStructure::values (model, reference date, times, conditionals): the structure on the
//         reference date (no calendar, Actual/365 Fixed): its survival probability, default density and hazard rate at
//         each time, and its conditional survival probability at each [tFwd, tTarget, y]
//     InterpolatedAffineHazardRateCurve::values (model, dates, hazard rates, interpolator, times, conditionals): the
//         curve's nodes and the same reads, each extrapolated, {"throws": true} where QuantLib refuses
//     PiecewiseAffineHazardRateCurve::build (model, interpolator, helpers, quotes, reference date, times): the piecewise
//         curve on the affine traits, its dates and data, its survival probability at each time
//
// The curve's survival squares the process's x0 where nanoql takes the model's own initial intensity (the S-xcr
// plan's XR8 (xiii)), so its generator reads it on models where the two coincide.

#include "termstructures/credit/creditspec.hpp"

#include <ql/experimental/credit/interpolatedaffinehazardratecurve.hpp>
#include <ql/experimental/credit/onefactoraffinesurvival.hpp>
#include <ql/math/interpolations/backwardflatinterpolation.hpp>
#include <ql/math/interpolations/linearinterpolation.hpp>
#include <ql/models/shortrate/onefactormodels/coxingersollross.hpp>
#include <ql/models/shortrate/onefactormodels/vasicek.hpp>
#include <ql/termstructures/credit/piecewisedefaultcurve.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

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

        struct ModelSpec {
            std::string type;
            std::vector<Real> p;
        };

        ModelSpec model(const json::value& v) {
            if (!v.is_array() || v.get_array().size() != 5)
                throw InvalidParams("expected a model as [type, four parameters]");
            ModelSpec s;
            s.type = text(v.get_array()[0]);
            if (s.type != "Vasicek" && s.type != "CIR")
                throw InvalidParams("unknown model " + s.type);
            for (std::size_t i = 1; i < 5; ++i)
                s.p.push_back(real(v.get_array()[i]));
            return s;
        }

        ext::shared_ptr<OneFactorAffineModel> makeModel(const ModelSpec& s) {
            if (s.type == "Vasicek")
                return ext::make_shared<Vasicek>(s.p[0], s.p[1], s.p[2], s.p[3]);
            return ext::make_shared<CoxIngersollRoss>(s.p[0], s.p[1], s.p[2], s.p[3]);
        }

        std::vector<std::vector<Real>> triples(const json::value& v) {
            std::vector<std::vector<Real>> out;
            if (!v.is_array())
                throw InvalidParams("expected the conditionals as an array");
            for (const json::value& t : v.get_array())
                out.push_back(reals(t));
            return out;
        }

        json::value reads(const OneFactorAffineSurvivalStructure& s, const std::vector<Real>& times,
                          const std::vector<std::vector<Real>>& conditionals) {
            json::array survival, density, hazard, conditional;
            for (Time t : times) {
                survival.push_back(attempted([&] { return s.survivalProbability(t, true); }));
                density.push_back(attempted([&] { return s.defaultDensity(t, true); }));
                hazard.push_back(attempted([&] { return s.hazardRate(t, true); }));
            }
            for (const std::vector<Real>& c : conditionals)
                conditional.push_back(attempted([&] { return s.conditionalSurvivalProbability(c[0], c[1], c[2], true); }));
            return json::object{{"survivalProbability", survival},
                                {"defaultDensity", density},
                                {"hazardRate", hazard},
                                {"conditionalSurvivalProbability", conditional}};
        }

        template <class I>
        json::value curveValues(const ModelSpec& m, const std::vector<DateSpec>& dates, const std::vector<Real>& rates,
                                const std::vector<Real>& times, const std::vector<std::vector<Real>>& conditionals) {
            std::vector<Date> ds;
            for (const DateSpec& d : dates)
                ds.push_back(makeDate(d));
            InterpolatedAffineHazardRateCurve<I> curve(ds, rates, Actual365Fixed(), makeModel(m));
            json::value out = reads(curve, times, conditionals);
            out.get_object()["maxDate"] = result(curve.maxDate());
            return out;
        }

        template <class I>
        json::value piecewise(const ModelSpec& m, const std::vector<credit::HelperSpec>& specs,
                              const std::vector<Real>& quotes, const DateSpec& reference,
                              const std::vector<Real>& times) {
            std::vector<ext::shared_ptr<DefaultProbabilityHelper>> helpers;
            for (std::size_t i = 0; i < specs.size(); ++i)
                helpers.push_back(credit::makeHelper(specs[i], quotes[i]));
            PiecewiseDefaultCurve<AffineHazardRate, I> curve(makeDate(reference), helpers, Actual365Fixed(),
                                                             makeModel(m));
            json::array dates, data, survival;
            for (const Date& d : curve.dates())
                dates.push_back(result(d));
            for (Real x : curve.data())
                data.push_back(json::value(x));
            for (Time t : times)
                survival.push_back(json::value(curve.survivalProbability(t, true)));
            return json::object{{"dates", dates}, {"data", data}, {"survivalProbability", survival}};
        }

        const Register unit({
            {"OneFactorAffineSurvivalStructure::values",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 ModelSpec m = model(a[0]);
                 DateSpec reference = date(a[1]);
                 std::vector<Real> times = reals(a[2]);
                 std::vector<std::vector<Real>> conditionals = triples(a[3]);
                 return [=] {
                     OneFactorAffineSurvivalStructure s(makeModel(m), makeDate(reference), Calendar(),
                                                        Actual365Fixed());
                     return reads(s, times, conditionals);
                 };
             }},
            {"InterpolatedAffineHazardRateCurve::values",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 ModelSpec m = model(a[0]);
                 std::vector<DateSpec> dates;
                 if (!a[1].is_array())
                     throw InvalidParams("expected the dates as an array");
                 for (const json::value& d : a[1].get_array())
                     dates.push_back(date(d));
                 std::vector<Real> rates = reals(a[2]);
                 std::string interpolator = text(a[3]);
                 if (interpolator != "BackwardFlat" && interpolator != "Linear")
                     throw InvalidParams("unknown interpolator " + interpolator);
                 std::vector<Real> times = reals(a[4]);
                 std::vector<std::vector<Real>> conditionals = triples(a[5]);
                 return [=] {
                     return interpolator == "Linear" ? curveValues<Linear>(m, dates, rates, times, conditionals) :
                                                       curveValues<BackwardFlat>(m, dates, rates, times, conditionals);
                 };
             }},
            {"PiecewiseAffineHazardRateCurve::build",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 ModelSpec m = model(a[0]);
                 std::string interpolator = text(a[1]);
                 if (interpolator != "BackwardFlat" && interpolator != "Linear")
                     throw InvalidParams("unknown interpolator " + interpolator);
                 std::vector<credit::HelperSpec> helpers;
                 if (!a[2].is_array())
                     throw InvalidParams("expected the helpers as an array");
                 for (const json::value& h : a[2].get_array())
                     helpers.push_back(credit::helper(h));
                 std::vector<Real> quotes = reals(a[3]);
                 if (quotes.size() != helpers.size())
                     throw InvalidParams("expected one quote per helper");
                 DateSpec reference = date(a[4]);
                 std::vector<Real> times = reals(a[5]);
                 return [=] {
                     return interpolator == "Linear" ? piecewise<Linear>(m, helpers, quotes, reference, times) :
                                                       piecewise<BackwardFlat>(m, helpers, quotes, reference, times);
                 };
             }},
        });

    }

}
