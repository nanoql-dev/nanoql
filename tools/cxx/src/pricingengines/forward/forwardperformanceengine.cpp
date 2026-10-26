// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/forward/forwardperformanceengine.hpp: ForwardPerformanceVanillaEngine over AnalyticEuropeanEngine,
// which the wheel doesn't expose.
//
// "ForwardPerformanceVanillaEngine::results" takes (process, option): the process [spot, r, q, volatility], a
// Black-Scholes-Merton process on flat Act/365 (Fixed) forwards and a constant volatility from the evaluation date;
// the option [moneyness, resetDate, optionType, exerciseDate], a forward-start plain vanilla (its payoff's strike 0,
// which the engine ignores) with a European exercise. It returns {"NPV", "delta", "gamma", "theta", "vega", "rho",
// "dividendRho"}; the engine's refusals are QuantLib's, their messages the errors'. The evaluation date is the case's
// Settings.

#include "convert.hpp"

#include <ql/exercise.hpp>
#include <ql/instruments/forwardvanillaoption.hpp>
#include <ql/instruments/payoffs.hpp>
#include <ql/pricingengines/forward/forwardperformanceengine.hpp>
#include <ql/pricingengines/vanilla/analyticeuropeanengine.hpp>
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
            {"ForwardPerformanceVanillaEngine::results",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 if (!a[0].is_array() || a[0].get_array().size() != 4)
                     throw InvalidParams("expected [spot, r, q, volatility], got " + json::serialize(a[0]));
                 const json::array& p = a[0].get_array();
                 const Real spot = real(p[0]), r = real(p[1]), q = real(p[2]), vol = real(p[3]);
                 if (!a[1].is_array() || a[1].get_array().size() != 4)
                     throw InvalidParams("expected [moneyness, resetDate, optionType, exerciseDate], got " +
                                         json::serialize(a[1]));
                 const json::array& o = a[1].get_array();
                 const Real moneyness = real(o[0]);
                 const DateSpec reset = date(o[1]);
                 const auto type = enumeration<Option::Type>(o[2]);
                 const DateSpec expiry = date(o[3]);
                 return [=] {
                     const Date today = Settings::instance().evaluationDate();
                     const DayCounter dc = Actual365Fixed();
                     auto process = ext::make_shared<BlackScholesMertonProcess>(
                         Handle<Quote>(ext::make_shared<SimpleQuote>(spot)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, q, dc)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, r, dc)),
                         Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(today, NullCalendar(), vol, dc)));
                     ForwardVanillaOption option(moneyness, makeDate(reset), ext::make_shared<PlainVanillaPayoff>(type, 0.0),
                                                 ext::make_shared<EuropeanExercise>(makeDate(expiry)));
                     option.setPricingEngine(
                         ext::make_shared<ForwardPerformanceVanillaEngine<AnalyticEuropeanEngine>>(process));
                     return json::value(json::object{{"NPV", option.NPV()}, {"delta", option.delta()},
                                                     {"gamma", option.gamma()}, {"theta", option.theta()},
                                                     {"vega", option.vega()}, {"rho", option.rho()},
                                                     {"dividendRho", option.dividendRho()}});
                 };
             }},
        });

    }

}
