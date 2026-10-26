// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/basket/singlefactorbsmbasketengine.hpp, .cpp: the single-factor basket engine and its root solver,
// which the wheel doesn't expose. "SingleFactorBsmBasketEngine::results" takes (today, maturity, spots, dividends, r,
// vols, weights, payoff, xTol): Black-Scholes-Merton processes on flat Act/365 (Fixed) curves and constant volatilities
// from today, one per spot (a volatility may be negative, the engine's signed deviation); a European average basket
// option on a plain vanilla [optionType, strike] to the maturity (a serial); and returns {"NPV", "d"}, d null where
// QuantLib sets none. "SumExponentialsRootSolver::values" takes (a, sig, K, x) and returns {"value", "derivative",
// "secondDerivative"}; "SumExponentialsRootSolver::getRoot" takes (a, sig, K, xTol, strategy) and returns {"root",
// "fCtr", "derivativeCtr", "secondDerivativeCtr"}.

#include "convert.hpp"

#include <ql/exercise.hpp>
#include <ql/instruments/basketoption.hpp>
#include <ql/instruments/payoffs.hpp>
#include <ql/pricingengines/basket/singlefactorbsmbasketengine.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <any>
#include <cstdint>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        Array arrayOf(const json::value& v) {
            const std::vector<Real> x = reals(v);
            return Array(x.begin(), x.end());
        }

        const Register unit({
            {"SingleFactorBsmBasketEngine::results",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 const Date today = makeDate(date(a[0])), maturity = makeDate(date(a[1]));
                 const std::vector<Real> spots = reals(a[2]), dividends = reals(a[3]), vols = reals(a[5]);
                 const Real r = real(a[4]);
                 const Array weights = arrayOf(a[6]);
                 const std::vector<Real> payoff = reals(a[7]);
                 if (payoff.size() != 2)
                     throw InvalidParams("the payoff is [optionType, strike]");
                 if (dividends.size() != spots.size() || vols.size() != spots.size())
                     throw InvalidParams("one dividend and one volatility per spot");
                 const Real xTol = real(a[8]);
                 return [=] {
                     const DayCounter dc = Actual365Fixed();
                     const Handle<YieldTermStructure> rTS(ext::make_shared<FlatForward>(today, r, dc));
                     std::vector<ext::shared_ptr<GeneralizedBlackScholesProcess>> processes;
                     for (Size i = 0; i < spots.size(); ++i)
                         processes.push_back(ext::make_shared<BlackScholesMertonProcess>(
                             Handle<Quote>(ext::make_shared<SimpleQuote>(spots[i])),
                             Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, dividends[i], dc)), rTS,
                             Handle<BlackVolTermStructure>(
                                 ext::make_shared<BlackConstantVol>(today, NullCalendar(), vols[i], dc))));
                     BasketOption option(
                         ext::make_shared<AverageBasketPayoff>(
                             ext::make_shared<PlainVanillaPayoff>(Option::Type(Integer(payoff[0])), payoff[1]),
                             weights),
                         ext::make_shared<EuropeanExercise>(maturity));
                     option.setPricingEngine(ext::make_shared<SingleFactorBsmBasketEngine>(processes, xTol));
                     const Real npv = option.NPV();
                     const auto& additional = option.additionalResults();
                     const auto d = additional.find("d");
                     return json::value(json::object{
                         {"NPV", npv},
                         {"d", d == additional.end() ? json::value(nullptr) : json::value(std::any_cast<Real>(d->second))}});
                 };
             }},
            {"SumExponentialsRootSolver::values",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 const Array as = arrayOf(a[0]), sig = arrayOf(a[1]);
                 const Real k = real(a[2]), x = real(a[3]);
                 return [=] {
                     const detail::SumExponentialsRootSolver solver(as, sig, k);
                     return json::value(json::object{{"value", solver(x)}, {"derivative", solver.derivative(x)},
                                                     {"secondDerivative", solver.secondDerivative(x)}});
                 };
             }},
            {"SumExponentialsRootSolver::getRoot",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 const Array as = arrayOf(a[0]), sig = arrayOf(a[1]);
                 const Real k = real(a[2]), xTol = real(a[3]);
                 const auto strategy = detail::SumExponentialsRootSolver::Strategy(integer(a[4]));
                 return [=] {
                     const detail::SumExponentialsRootSolver solver(as, sig, k);
                     const Real root = solver.getRoot(xTol, strategy);
                     return json::value(json::object{
                         {"root", root}, {"fCtr", std::int64_t(solver.getFCtr())},
                         {"derivativeCtr", std::int64_t(solver.getDerivativeCtr())},
                         {"secondDerivativeCtr", std::int64_t(solver.getSecondDerivativeCtr())}});
                 };
             }},
        });

    }

}
