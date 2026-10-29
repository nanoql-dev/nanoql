// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/varianceoption/varianceoption.hpp, .cpp and integralhestonvarianceoptionengine.hpp, .cpp, which the
// wheel doesn't expose. "IntegralHestonVarianceOptionEngine" takes (r, v0, kappa, theta, sigma, rho, optionType,
// strike, notional, maturityDays): a Heston process on a flat Act/360 risk-free forward from the evaluation date, an
// empty dividend handle (the engine refuses any other) and a spot of 1; the variance option on the plain vanilla payoff
// starting on the evaluation date and maturing that many days after it. It returns the NPV; the option's or the
// engine's refusal is QuantLib's.

#include "convert.hpp"

#include <ql/experimental/varianceoption/integralhestonvarianceoptionengine.hpp>
#include <ql/experimental/varianceoption/varianceoption.hpp>
#include <ql/instruments/payoffs.hpp>
#include <ql/processes/hestonprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/daycounters/actual360.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"IntegralHestonVarianceOptionEngine",
             [](const Args& a) -> Call {
                 arity(a, 10);
                 Real r = real(a[0]);
                 Real v0 = real(a[1]);
                 Real kappa = real(a[2]);
                 Real theta = real(a[3]);
                 Real sigma = real(a[4]);
                 Real rho = real(a[5]);
                 auto optionType = enumeration<Option::Type>(a[6]);
                 Real strike = real(a[7]);
                 Real notional = real(a[8]);
                 Integer maturityDays = integer(a[9]);
                 return [=] {
                     const Date today = Settings::instance().evaluationDate();
                     auto process = ext::make_shared<HestonProcess>(
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, r, Actual360())),
                         Handle<YieldTermStructure>(), Handle<Quote>(ext::make_shared<SimpleQuote>(1.0)), v0, kappa,
                         theta, sigma, rho);
                     VarianceOption option(ext::make_shared<PlainVanillaPayoff>(optionType, strike), notional, today,
                                           today + maturityDays);
                     option.setPricingEngine(ext::make_shared<IntegralHestonVarianceOptionEngine>(process));
                     return json::value(option.NPV());
                 };
             }},
        });

    }

}
