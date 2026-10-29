// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/barrieroption/perturbativebarrieroptionengine.hpp, .cpp, which the wheel doesn't expose.
// "PerturbativeBarrierOptionEngine" takes (referenceDate, spot, dividendYield, riskFreeRate, varianceDays, vols,
// barrierType, barrier, rebate, optionType, strike, expiryDays, order, zeroGamma, npoint, npoint2): a Black-Scholes-
// Merton process on flat forwards of the two rates and a Black variance curve at the given vols on the dates that many
// days after the reference date, all from the reference date on Act/365; the barrier option on the plain vanilla
// payoff, European that many days after it; the engine at that order, gamma choice and point counts; its NPV.

#include "convert.hpp"

#include <ql/exercise.hpp>
#include <ql/experimental/barrieroption/perturbativebarrieroptionengine.hpp>
#include <ql/instruments/barrieroption.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackvariancecurve.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"PerturbativeBarrierOptionEngine",
             [](const Args& a) -> Call {
                 arity(a, 16);
                 DateSpec reference = date(a[0]);
                 Real spot = real(a[1]);
                 Real q = real(a[2]);
                 Real r = real(a[3]);
                 std::vector<Integer> varianceDays = integers(a[4]);
                 std::vector<Real> vols = reals(a[5]);
                 auto barrierType = enumeration<Barrier::Type>(a[6]);
                 Real barrier = real(a[7]);
                 Real rebate = real(a[8]);
                 auto optionType = enumeration<Option::Type>(a[9]);
                 Real strike = real(a[10]);
                 Integer expiryDays = integer(a[11]);
                 Integer order = integer(a[12]);
                 bool zeroGamma = boolean(a[13]);
                 Integer npoint = integer(a[14]);
                 Integer npoint2 = integer(a[15]);
                 if (varianceDays.size() != vols.size() || vols.empty())
                     throw InvalidParams("expected as many vols as variance days");
                 return [=] {
                     Date d = makeDate(reference);
                     DayCounter c = Actual365Fixed();
                     std::vector<Date> dates;
                     for (Integer n : varianceDays)
                         dates.push_back(d + n);
                     auto process = ext::make_shared<BlackScholesMertonProcess>(
                         Handle<Quote>(ext::make_shared<SimpleQuote>(spot)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, q, c)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, r, c)),
                         Handle<BlackVolTermStructure>(ext::make_shared<BlackVarianceCurve>(d, dates, vols, c)));
                     BarrierOption option(barrierType, barrier, rebate,
                                          ext::make_shared<PlainVanillaPayoff>(optionType, strike),
                                          ext::make_shared<EuropeanExercise>(d + expiryDays));
                     option.setPricingEngine(ext::make_shared<PerturbativeBarrierOptionEngine>(
                         process, static_cast<Natural>(order), zeroGamma, static_cast<Size>(npoint),
                         static_cast<Size>(npoint2)));
                     return json::value(option.NPV());
                 };
             }},
        });

    }

}
