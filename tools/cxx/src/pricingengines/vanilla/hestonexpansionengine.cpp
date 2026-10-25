// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/vanilla/hestonexpansionengine.hpp, .cpp: the three expansions and the engine, which the wheel
// doesn't expose.
//
//     HestonExpansion::impliedVolatility   (formula, kappa, theta, sigma, v0, rho, term, strikes, forward)
//     HestonExpansionEngine::results       (process, formula, optionType, strike, exerciseDate)
//
// The formula is HestonExpansionEngine::HestonExpansionFormula's integer: LPP2HestonExpansion, LPP3HestonExpansion or
// FordeHestonExpansion at the term, the implied volatility at each strike; the engine on the HestonModel of
// models/equity/hestonspec.cpp's process prices a European plain vanilla and returns its NPV. The evaluation date is
// the case's Settings.

#include "convert.hpp"
#include "models/equity/hestonspec.hpp"

#include <ql/exercise.hpp>
#include <ql/instruments/vanillaoption.hpp>
#include <ql/models/equity/hestonmodel.hpp>
#include <ql/pricingengines/vanilla/hestonexpansionengine.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        ext::shared_ptr<HestonExpansion> expansion(HestonExpansionEngine::HestonExpansionFormula f, Real kappa,
                                                   Real theta, Real sigma, Real v0, Real rho, Real term) {
            switch (f) {
                case HestonExpansionEngine::LPP2:
                    return ext::make_shared<LPP2HestonExpansion>(kappa, theta, sigma, v0, rho, term);
                case HestonExpansionEngine::LPP3:
                    return ext::make_shared<LPP3HestonExpansion>(kappa, theta, sigma, v0, rho, term);
                case HestonExpansionEngine::Forde:
                    return ext::make_shared<FordeHestonExpansion>(kappa, theta, sigma, v0, rho, term);
                default:
                    throw InvalidParams("unknown expansion formula");
            }
        }

        const Register unit({
            {"HestonExpansion::impliedVolatility",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 auto f = enumeration<HestonExpansionEngine::HestonExpansionFormula>(a[0]);
                 Real kappa = real(a[1]), theta = real(a[2]), sigma = real(a[3]), v0 = real(a[4]), rho = real(a[5]),
                      term = real(a[6]);
                 std::vector<Real> strikes = reals(a[7]);
                 Real forward = real(a[8]);
                 return [=] {
                     ext::shared_ptr<HestonExpansion> e = expansion(f, kappa, theta, sigma, v0, rho, term);
                     json::array vols;
                     for (Real k : strikes)
                         vols.push_back(e->impliedVolatility(k, forward));
                     return json::value(vols);
                 };
             }},
            {"HestonExpansionEngine::results",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 equity::ProcessSpec p = equity::process(a[0]);
                 auto f = enumeration<HestonExpansionEngine::HestonExpansionFormula>(a[1]);
                 auto optionType = enumeration<Option::Type>(a[2]);
                 Real strike = real(a[3]);
                 DateSpec exercise = date(a[4]);
                 return [=] {
                     VanillaOption option(ext::make_shared<PlainVanillaPayoff>(optionType, strike),
                                          ext::make_shared<EuropeanExercise>(makeDate(exercise)));
                     option.setPricingEngine(
                         ext::make_shared<HestonExpansionEngine>(ext::make_shared<HestonModel>(equity::makeProcess(p)), f));
                     return json::value(option.NPV());
                 };
             }},
        });

    }

}
