// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/finitedifferences/schemes/trbdf2scheme.hpp, which the wheel doesn't expose: the scheme stepped on its
// own, and numberOfIterations. "TrBDF2Scheme::step" takes (spot, riskFreeRate, dividendYield, volatility, xMin, xMax,
// size, alpha, dt, from, steps, initial): the Black-Scholes operator of a Black-Scholes-Merton process on flat
// forwards and a constant volatility from the evaluation date on Act/365, struck at the spot, on a uniform log mesher
// of size points over [xMin, xMax]; TrBDF2Scheme<CrankNicolsonScheme> at alpha with a Crank-Nicolson trapezoid at
// theta 0.5 (FdmBackwardSolver's is over a Craig-Sneyd trapezoid); steps of dt back from the given time. It returns
// {"values" (after each step), "numberOfIterations" (after the last)}.

#include "convert.hpp"

#include <ql/methods/finitedifferences/meshers/fdmmeshercomposite.hpp>
#include <ql/methods/finitedifferences/meshers/uniform1dmesher.hpp>
#include <ql/methods/finitedifferences/operators/fdmblackscholesop.hpp>
#include <ql/methods/finitedifferences/schemes/cranknicolsonscheme.hpp>
#include <ql/methods/finitedifferences/schemes/trbdf2scheme.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"TrBDF2Scheme::step",
             [](const Args& a) -> Call {
                 arity(a, 12);
                 Real spot = real(a[0]), r = real(a[1]), q = real(a[2]), vol = real(a[3]);
                 Real xMin = real(a[4]), xMax = real(a[5]);
                 Integer size = integer(a[6]);
                 Real alpha = real(a[7]), dt = real(a[8]), from = real(a[9]);
                 Integer steps = integer(a[10]);
                 std::vector<Real> initial = reals(a[11]);
                 if (size < 2 || steps < 0 || initial.size() != static_cast<std::size_t>(size))
                     throw InvalidParams("a mesher of at least 2 points, as many initial values, steps not negative");
                 return [=] {
                     Date today = Settings::instance().evaluationDate();
                     DayCounter dc = Actual365Fixed();
                     auto process = ext::make_shared<BlackScholesMertonProcess>(
                         Handle<Quote>(ext::make_shared<SimpleQuote>(spot)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, q, dc)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, r, dc)),
                         Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(today, NullCalendar(), vol, dc)));
                     auto mesher = ext::make_shared<FdmMesherComposite>(
                         ext::make_shared<Uniform1dMesher>(xMin, xMax, static_cast<Size>(size)));
                     auto op = ext::make_shared<FdmBlackScholesOp>(mesher, process, spot);
                     auto trapezoid = ext::make_shared<CrankNicolsonScheme>(0.5, op);
                     TrBDF2Scheme<CrankNicolsonScheme> scheme(alpha, op, trapezoid);
                     scheme.setStep(dt);
                     Array x(initial.begin(), initial.end());
                     json::array values;
                     Time t = from;
                     for (Integer k = 0; k < steps; ++k) {
                         scheme.step(x, t);
                         t -= dt;
                         json::array v;
                         for (Real y : x)
                             v.emplace_back(y);
                         values.emplace_back(v);
                     }
                     return json::value(json::object{
                         {"values", values},
                         {"numberOfIterations", static_cast<std::int64_t>(scheme.numberOfIterations())}});
                 };
             }},
        });

    }

}
