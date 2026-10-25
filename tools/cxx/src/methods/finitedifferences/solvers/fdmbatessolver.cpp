// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/finitedifferences/solvers/fdmbatessolver.hpp, .cpp: the Bates solver, which the wheel doesn't expose.
// "FdmBatesSolver::results" takes (process, optionType, strike, exercise, spots, variances, boundaries, tGrid,
// dampingSteps, scheme, integrationOrder, quanto, points): the process models/equity/hestonspec.hpp's Bates spec; the
// exercise ["European" | "American", date] (American from the process's reference date); a composite mesher of two
// predefined meshers, the log-spots and the variances given; the inner value FdmLogInnerValue(payoff, mesher, 0) of a
// plain vanilla payoff; FdmStepConditionComposite::vanillaComposite with no dividends on the risk-free curve's reference
// date and day counter; the boundaries [[valueOnBoundary, direction, side]] as FdmDirichletBoundary's; the maturity the
// risk-free curve's time to the exercise's last date; the scheme [type, theta, mu]; the quanto helper hestonspec.hpp's
// spec or null. It returns {"valueAt", "thetaAt", "deltaAt", "gammaAt"}, each at the points [s, v]. The evaluation
// date is the case's Settings.

#include "convert.hpp"
#include "models/equity/hestonspec.hpp"

#include <ql/exercise.hpp>
#include <ql/instruments/payoffs.hpp>
#include <ql/methods/finitedifferences/meshers/fdmmeshercomposite.hpp>
#include <ql/methods/finitedifferences/meshers/predefined1dmesher.hpp>
#include <ql/methods/finitedifferences/solvers/fdmbatessolver.hpp>
#include <ql/methods/finitedifferences/stepconditions/fdmstepconditioncomposite.hpp>
#include <ql/methods/finitedifferences/utilities/fdmdirichletboundary.hpp>
#include <ql/methods/finitedifferences/utilities/fdminnervaluecalculator.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct Boundary {
            Real value;
            Size direction;
            FdmDirichletBoundary::Side side;
        };

        Size size(const json::value& v) {
            Integer n = integer(v);
            if (n < 0)
                throw InvalidParams("a size must be non-negative");
            return static_cast<Size>(n);
        }

        const Register unit({
            {"FdmBatesSolver::results",
             [](const Args& a) -> Call {
                 arity(a, 13);
                 equity::ProcessSpec p = equity::process(a[0]);
                 auto type = enumeration<Option::Type>(a[1]);
                 Real strike = real(a[2]);
                 if (!a[3].is_array() || a[3].get_array().size() != 2)
                     throw InvalidParams("expected [type, date], got " + json::serialize(a[3]));
                 std::string exerciseType = text(a[3].get_array()[0]);
                 if (exerciseType != "European" && exerciseType != "American")
                     throw InvalidParams("an exercise type is European or American");
                 DateSpec exerciseDate = date(a[3].get_array()[1]);
                 std::vector<Real> spots = reals(a[4]), variances = reals(a[5]);
                 if (!a[6].is_array())
                     throw InvalidParams("expected a list of boundaries, got " + json::serialize(a[6]));
                 std::vector<Boundary> boundaries;
                 for (const json::value& b : a[6].get_array()) {
                     if (!b.is_array() || b.get_array().size() != 3)
                         throw InvalidParams("expected [value, direction, side], got " + json::serialize(b));
                     boundaries.push_back({real(b.get_array()[0]), size(b.get_array()[1]),
                                           enumeration<FdmDirichletBoundary::Side>(b.get_array()[2])});
                 }
                 Size tGrid = size(a[7]), dampingSteps = size(a[8]);
                 if (!a[9].is_array() || a[9].get_array().size() != 3)
                     throw InvalidParams("expected [type, theta, mu], got " + json::serialize(a[9]));
                 auto schemeType = enumeration<FdmSchemeDesc::FdmSchemeType>(a[9].get_array()[0]);
                 Real theta = real(a[9].get_array()[1]), mu = real(a[9].get_array()[2]);
                 Size order = size(a[10]);
                 std::optional<equity::QuantoSpec> quanto = equity::quanto(a[11]);
                 if (!a[12].is_array())
                     throw InvalidParams("expected a list of points, got " + json::serialize(a[12]));
                 std::vector<std::pair<Real, Real>> points;
                 for (const json::value& x : a[12].get_array()) {
                     std::vector<Real> sv = reals(x);
                     if (sv.size() != 2)
                         throw InvalidParams("a point is [s, v]");
                     points.emplace_back(sv[0], sv[1]);
                 }
                 return [=] {
                     ext::shared_ptr<BatesProcess> process = equity::makeBatesProcess(p);
                     auto mesher = ext::make_shared<FdmMesherComposite>(ext::make_shared<Predefined1dMesher>(spots),
                                                                         ext::make_shared<Predefined1dMesher>(variances));
                     Date reference = makeDate(p.referenceDate);
                     DayCounter dc = makeDayCounter(p.dayCounter);
                     ext::shared_ptr<Exercise> exercise;
                     if (exerciseType == "European")
                         exercise = ext::make_shared<EuropeanExercise>(makeDate(exerciseDate));
                     else
                         exercise = ext::make_shared<AmericanExercise>(reference, makeDate(exerciseDate));
                     auto calculator = ext::make_shared<FdmLogInnerValue>(
                         ext::make_shared<PlainVanillaPayoff>(type, strike), mesher, 0);
                     auto conditions = FdmStepConditionComposite::vanillaComposite(
                         DividendSchedule(), exercise, mesher, calculator, reference, dc);
                     FdmBoundaryConditionSet bcSet;
                     for (const Boundary& b : boundaries)
                         bcSet.push_back(ext::make_shared<FdmDirichletBoundary>(mesher, b.value, b.direction, b.side));
                     FdmSolverDesc desc = {mesher, bcSet, conditions, calculator,
                                           dc.yearFraction(reference, exercise->lastDate()), tGrid, dampingSteps};
                     FdmBatesSolver solver(Handle<BatesProcess>(process), desc, FdmSchemeDesc(schemeType, theta, mu),
                                           order, Handle<FdmQuantoHelper>(equity::makeQuanto(quanto, p)));
                     json::array value, thetaAt, delta, gamma;
                     for (const auto& [s, v] : points) {
                         value.push_back(json::value(solver.valueAt(s, v)));
                         thetaAt.push_back(json::value(solver.thetaAt(s, v)));
                         delta.push_back(json::value(solver.deltaAt(s, v)));
                         gamma.push_back(json::value(solver.gammaAt(s, v)));
                     }
                     json::object out;
                     out["valueAt"] = value;
                     out["thetaAt"] = thetaAt;
                     out["deltaAt"] = delta;
                     out["gammaAt"] = gamma;
                     return json::value(out);
                 };
             }},
        });

    }

}
