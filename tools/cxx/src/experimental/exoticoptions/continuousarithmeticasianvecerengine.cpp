// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/exoticoptions/continuousarithmeticasianvecerengine.hpp, .cpp, which the wheel doesn't expose.
// "ContinuousArithmeticAsianVecerEngine" takes (spot, r, q, volatility, optionType, strike, expiryDays, startDays,
// timeSteps, assetSteps, zMin, zMax): a Black-Scholes-Merton process on flat Act/365 (Fixed) forwards and a constant
// volatility from the evaluation date; a continuous arithmetic average-price option on the plain vanilla payoff,
// European that many days after the evaluation date; the engine averaging from startDays after it, on a current average
// of 0, with those steps and grid ends. It returns the NPV; the engine's refusal is QuantLib's.

#include "convert.hpp"

#include <ql/exercise.hpp>
#include <ql/experimental/exoticoptions/continuousarithmeticasianvecerengine.hpp>
#include <ql/instruments/asianoption.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"ContinuousArithmeticAsianVecerEngine",
             [](const Args& a) -> Call {
                 arity(a, 12);
                 Real spot = real(a[0]);
                 Real r = real(a[1]);
                 Real q = real(a[2]);
                 Real vol = real(a[3]);
                 auto optionType = enumeration<Option::Type>(a[4]);
                 Real strike = real(a[5]);
                 Integer expiryDays = integer(a[6]);
                 Integer startDays = integer(a[7]);
                 Integer timeSteps = integer(a[8]);
                 Integer assetSteps = integer(a[9]);
                 Real zMin = real(a[10]);
                 Real zMax = real(a[11]);
                 return [=] {
                     const Date today = Settings::instance().evaluationDate();
                     const DayCounter dc = Actual365Fixed();
                     auto process = ext::make_shared<BlackScholesMertonProcess>(
                         Handle<Quote>(ext::make_shared<SimpleQuote>(spot)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, q, dc)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, r, dc)),
                         Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(today, NullCalendar(), vol, dc)));
                     ContinuousAveragingAsianOption option(Average::Arithmetic,
                                                           ext::make_shared<PlainVanillaPayoff>(optionType, strike),
                                                           ext::make_shared<EuropeanExercise>(today + expiryDays));
                     option.setPricingEngine(ext::make_shared<ContinuousArithmeticAsianVecerEngine>(
                         process, Handle<Quote>(ext::make_shared<SimpleQuote>(0.0)), today + startDays,
                         static_cast<Size>(timeSteps), static_cast<Size>(assetSteps), zMin, zMax));
                     return json::value(option.NPV());
                 };
             }},
        });

    }

}
