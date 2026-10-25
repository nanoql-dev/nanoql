// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/finitedifferences/solvers/fdmsimple2dbssolver.hpp, .cpp: the two-dimensional Black-Scholes solver, which
// the wheel doesn't expose. "FdmSimple2dBSSolver::results" takes (today, spot, r, q, vol, payoff, exerciseTimes,
// minExercises, maxExercises, tGrid, xGrid, points, eps): flat Act/365 (Fixed) curves and a constant volatility from
// today (a serial); the payoff ["Forward" | "PlainVanilla", optionType, strike]; the problem FdSimpleBSSwingEngine
// builds on them (fdsimplebsswingengine.cpp:47-90), its exercises at the given times, the maturity the last of them;
// and returns {"valueAt", "deltaAt", "gammaAt", "thetaAt"} at each point [s, a], the delta and gamma bumped by eps.

#include "convert.hpp"

#include <ql/instruments/payoffs.hpp>
#include <ql/instruments/vanillaswingoption.hpp>
#include <ql/methods/finitedifferences/meshers/fdmblackscholesmesher.hpp>
#include <ql/methods/finitedifferences/meshers/fdmmeshercomposite.hpp>
#include <ql/methods/finitedifferences/meshers/uniform1dmesher.hpp>
#include <ql/methods/finitedifferences/solvers/fdmsimple2dbssolver.hpp>
#include <ql/methods/finitedifferences/stepconditions/fdmsimpleswingcondition.hpp>
#include <ql/methods/finitedifferences/stepconditions/fdmstepconditioncomposite.hpp>
#include <ql/methods/finitedifferences/utilities/fdminnervaluecalculator.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <list>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        ext::shared_ptr<StrikedTypePayoff> payoffOf(const json::value& v) {
            if (!v.is_array() || v.get_array().size() != 3)
                throw InvalidParams("expected [type, optionType, strike], got " + json::serialize(v));
            const json::array& p = v.get_array();
            const std::string type = text(p[0]);
            const auto optionType = Option::Type(integer(p[1]));
            const Real strike = real(p[2]);
            if (type == "Forward")
                return ext::make_shared<VanillaForwardPayoff>(optionType, strike);
            if (type == "PlainVanilla")
                return ext::make_shared<PlainVanillaPayoff>(optionType, strike);
            throw InvalidParams("unknown payoff " + type);
        }

        const Register unit({
            {"FdmSimple2dBSSolver::results",
             [](const Args& a) -> Call {
                 arity(a, 13);
                 const Date today = makeDate(date(a[0]));
                 const Real spot = real(a[1]), r = real(a[2]), q = real(a[3]), vol = real(a[4]);
                 const auto payoff = payoffOf(a[5]);
                 const std::vector<Real> exerciseTimes = reals(a[6]);
                 const Integer minExercises = integer(a[7]), maxExercises = integer(a[8]);
                 const Integer tGrid = integer(a[9]), xGrid = integer(a[10]);
                 std::vector<std::pair<Real, Real>> points;
                 if (!a[11].is_array())
                     throw InvalidParams("expected points, got " + json::serialize(a[11]));
                 for (const json::value& p : a[11].get_array()) {
                     const std::vector<Real> xy = reals(p);
                     if (xy.size() != 2)
                         throw InvalidParams("a point is [s, a]");
                     points.emplace_back(xy[0], xy[1]);
                 }
                 const Real eps = real(a[12]);
                 return [=] {
                     const DayCounter dc = Actual365Fixed();
                     const auto process = ext::make_shared<BlackScholesMertonProcess>(
                         Handle<Quote>(ext::make_shared<SimpleQuote>(spot)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, q, dc)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, r, dc)),
                         Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(today, NullCalendar(), vol, dc)));
                     const Time maturity = exerciseTimes.back();
                     const auto equityMesher =
                         ext::make_shared<FdmBlackScholesMesher>(xGrid, process, maturity, payoff->strike());
                     const auto exerciseMesher = ext::make_shared<Uniform1dMesher>(
                         0, static_cast<Real>(maxExercises), maxExercises + 1);
                     const auto mesher = ext::make_shared<FdmMesherComposite>(equityMesher, exerciseMesher);
                     const auto calculator = ext::make_shared<FdmZeroInnerValue>();
                     const auto exerciseCalculator = ext::make_shared<FdmLogInnerValue>(payoff, mesher, 0);
                     std::list<std::vector<Time>> stoppingTimes(1, exerciseTimes);
                     std::list<ext::shared_ptr<StepCondition<Array>>> stepConditions(
                         1, ext::make_shared<FdmSimpleSwingCondition>(exerciseTimes, mesher, exerciseCalculator, 1,
                                                                      minExercises));
                     const auto conditions = ext::make_shared<FdmStepConditionComposite>(stoppingTimes, stepConditions);
                     const FdmSolverDesc solverDesc = {mesher, FdmBoundaryConditionSet(), conditions, calculator,
                                                       maturity, Size(tGrid), 0};
                     FdmSimple2dBSSolver solver(Handle<GeneralizedBlackScholesProcess>(process), payoff->strike(),
                                                solverDesc);
                     json::array value, delta, gamma, theta;
                     for (const auto& [s, x] : points) {
                         value.emplace_back(solver.valueAt(s, x));
                         delta.emplace_back(solver.deltaAt(s, x, eps));
                         gamma.emplace_back(solver.gammaAt(s, x, eps));
                         theta.emplace_back(solver.thetaAt(s, x));
                     }
                     return json::value(json::object{
                         {"valueAt", value}, {"deltaAt", delta}, {"gammaAt", gamma}, {"thetaAt", theta}});
                 };
             }},
        });

    }

}
