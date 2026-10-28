// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/credit/spreadedhazardratecurve.hpp and factorspreadedhazardratecurve.hpp, which the wheel doesn't
// expose, on a credit curve spec of termstructures/credit/creditspec.hpp:
//
//     SpreadedHazardRateCurve::values (factor, curve, spread, times): the curve's reference date, maximum date and
//         time, and its hazard rate at each time, extrapolated; additive, or multiplicative where factor is true. Their
//         survival probabilities and densities, which QuantLib integrates numerically where nanoql's are exact, aren't
//         written (the S-xcr plan's XR3 (b)).

#include "termstructures/credit/creditspec.hpp"

#include <ql/experimental/credit/factorspreadedhazardratecurve.hpp>
#include <ql/experimental/credit/spreadedhazardratecurve.hpp>
#include <ql/quotes/simplequote.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"SpreadedHazardRateCurve::values",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 bool factor = boolean(a[0]);
                 credit::CurveSpec s = credit::curve(a[1]);
                 Real spread = real(a[2]);
                 std::vector<Real> times = reals(a[3]);
                 return [=] {
                     Handle<DefaultProbabilityTermStructure> original(credit::makeCurve(s));
                     Handle<Quote> q(ext::make_shared<SimpleQuote>(spread));
                     ext::shared_ptr<DefaultProbabilityTermStructure> curve =
                         factor ? ext::shared_ptr<DefaultProbabilityTermStructure>(
                                      ext::make_shared<FactorSpreadedHazardRateCurve>(original, q)) :
                                  ext::shared_ptr<DefaultProbabilityTermStructure>(
                                      ext::make_shared<SpreadedHazardRateCurve>(original, q));
                     json::array hazard;
                     for (Time t : times)
                         hazard.emplace_back(curve->hazardRate(t, true));
                     return json::value(json::object{{"referenceDate", result(curve->referenceDate())},
                                                     {"maxDate", result(curve->maxDate())},
                                                     {"maxTime", curve->maxTime()},
                                                     {"hazardRate", hazard}});
                 };
             }},
        });

    }

}
