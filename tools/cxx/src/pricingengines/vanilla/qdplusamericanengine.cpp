// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/vanilla/qdplusamericanengine.hpp, .cpp: the QD+ engine's exercise boundary members, which the
// wheel doesn't expose. The engine is built inside the call from (interpolationPoints, solverType, eps, maxIter or null
// for QuantLib's default) on a placeholder process the members don't read:
//
//     QdPlusAmericanEngine::putExerciseBoundaryAtTau  (S, K, r, q, vol, T, tau, interpolationPoints, solverType, eps,
//                                                     maxIter): [the evaluations, the boundary]
//     QdPlusAmericanEngine::getPutExerciseBoundary    (S, K, r, q, vol, T, interpolationPoints, solverType, eps,
//                                                     maxIter, points): the interpolation's value at each point of
//                                                     [-1, 1], with no extrapolation
//     QdPlusAmericanEngine::xMax                      (K, r, q)

#include "convert.hpp"

#include <ql/math/interpolations/chebyshevinterpolation.hpp>
#include <ql/pricingengines/vanilla/qdplusamericanengine.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        struct EngineSpec {
            Integer interpolationPoints;
            QdPlusAmericanEngine::SolverType solverType;
            Real eps;
            Integer maxIter;
        };

        EngineSpec engineSpec(const Args& a, std::size_t first) {
            EngineSpec s{integer(a[first]), enumeration<QdPlusAmericanEngine::SolverType>(a[first + 1]),
                         real(a[first + 2]), integer(a[first + 3])};
            if (s.interpolationPoints < 0 || (s.maxIter != Null<Integer>() && s.maxIter < 0))
                throw InvalidParams("a size must be non-negative");
            return s;
        }

        QdPlusAmericanEngine engine(const EngineSpec& s) {
            Date today = Settings::instance().evaluationDate();
            auto p = ext::make_shared<BlackScholesMertonProcess>(
                Handle<Quote>(ext::make_shared<SimpleQuote>(100.0)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, 0.0, Actual365Fixed())),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, 0.0, Actual365Fixed())),
                Handle<BlackVolTermStructure>(
                    ext::make_shared<BlackConstantVol>(today, NullCalendar(), 0.2, Actual365Fixed())));
            Size maxIter = s.maxIter == Null<Integer>() ? Null<Size>() : static_cast<Size>(s.maxIter);
            return QdPlusAmericanEngine(p, static_cast<Size>(s.interpolationPoints), s.solverType, s.eps, maxIter);
        }

        const Register unit({
            {"QdPlusAmericanEngine::putExerciseBoundaryAtTau",
             [](const Args& a) -> Call {
                 arity(a, 11);
                 Real S = real(a[0]), K = real(a[1]), r = real(a[2]), q = real(a[3]), vol = real(a[4]),
                      T = real(a[5]), tau = real(a[6]);
                 EngineSpec s = engineSpec(a, 7);
                 return [=] {
                     std::pair<Size, Real> b = engine(s).putExerciseBoundaryAtTau(S, K, r, q, vol, T, tau);
                     json::array out{static_cast<std::int64_t>(b.first), b.second};
                     return json::value(out);
                 };
             }},
            {"QdPlusAmericanEngine::getPutExerciseBoundary",
             [](const Args& a) -> Call {
                 arity(a, 11);
                 Real S = real(a[0]), K = real(a[1]), r = real(a[2]), q = real(a[3]), vol = real(a[4]),
                      T = real(a[5]);
                 EngineSpec s = engineSpec(a, 6);
                 std::vector<Real> points = reals(a[10]);
                 return [=] {
                     ext::shared_ptr<ChebyshevInterpolation> boundary =
                         engine(s).getPutExerciseBoundary(S, K, r, q, vol, T);
                     json::array out;
                     for (Real z : points)
                         out.push_back(json::value((*boundary)(z)));
                     return json::value(out);
                 };
             }},
            {"QdPlusAmericanEngine::xMax",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 Real K = real(a[0]), r = real(a[1]), q = real(a[2]);
                 return [=] { return json::value(QdPlusAmericanEngine::xMax(K, r, q)); };
             }},
        });

    }

}
