// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/finitedifferences/solvers/fdmndimsolver.hpp: the n-dimensional solver, which the wheel doesn't expose.
// "FdmNdimSolver::results" takes (today, meshers, lambdas, r, weights, payoff, exercise, maturity, tGrid,
// dampingSteps, scheme, points): today a serial; the meshers [start, end, size] each, uniform, 1 to 4 of them; the
// Wiener operator's variance rates; the risk-free rate of a flat Act/365 (Fixed) curve from today, or null for an
// undiscounted operator; the average basket payoff's weights over a plain vanilla [optionType, strike], its inner value
// FdmLogBasketInnerValue; the exercise ["European"] (no condition) or ["American", serial] (QuantLib's vanilla
// composite from today to the date, on Act/365 (Fixed)); the maturity, steps and damping steps; the scheme [type,
// theta, mu]; and returns {"interpolateAt", "thetaAt"} at the points, the theta null where QuantLib returns
// Null<Real>().

#include "convert.hpp"

// QuantLib's FdmNdimSolver sums the layout's Size coordinates into an unsigned long (fdmndimsolver.hpp:103),
// which MSVC warns of; its own build doesn't treat warnings as errors
#pragma warning(disable : 4267)

#include <ql/exercise.hpp>
#include <ql/instruments/basketoption.hpp>
#include <ql/instruments/payoffs.hpp>
#include <ql/methods/finitedifferences/meshers/fdmmeshercomposite.hpp>
#include <ql/methods/finitedifferences/meshers/uniform1dmesher.hpp>
#include <ql/methods/finitedifferences/operators/fdmwienerop.hpp>
#include <ql/methods/finitedifferences/solvers/fdmndimsolver.hpp>
#include <ql/methods/finitedifferences/stepconditions/fdmstepconditioncomposite.hpp>
#include <ql/methods/finitedifferences/utilities/fdminnervaluecalculator.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <list>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        template <Size N>
        json::value solve(const FdmSolverDesc& desc, const FdmSchemeDesc& scheme,
                          const ext::shared_ptr<FdmLinearOpComposite>& op,
                          const std::vector<std::vector<Real>>& points) {
            FdmNdimSolver<N> solver(desc, scheme, op);
            json::array values, thetas;
            for (const auto& x : points) {
                values.emplace_back(solver.interpolateAt(x));
                const Real theta = solver.thetaAt(x);
                thetas.emplace_back(theta == Null<Real>() ? json::value(nullptr) : json::value(theta));
            }
            return json::value(json::object{{"interpolateAt", values}, {"thetaAt", thetas}});
        }

        const Register unit({
            {"FdmNdimSolver::results",
             [](const Args& a) -> Call {
                 arity(a, 12);
                 const Date today = makeDate(date(a[0]));
                 if (!a[1].is_array())
                     throw InvalidParams("expected meshers, got " + json::serialize(a[1]));
                 std::vector<std::vector<Real>> meshers;
                 for (const json::value& m : a[1].get_array()) {
                     meshers.push_back(reals(m));
                     if (meshers.back().size() != 3)
                         throw InvalidParams("a mesher is [start, end, size]");
                 }
                 if (meshers.empty() || meshers.size() > 4)
                     throw InvalidParams("1 to 4 meshers");
                 const std::vector<Real> lambdas = reals(a[2]);
                 const bool discounted = !a[3].is_null();
                 const Real r = discounted ? real(a[3]) : 0.0;
                 const std::vector<Real> weights = reals(a[4]);
                 const std::vector<Real> payoff = reals(a[5]);
                 if (payoff.size() != 2)
                     throw InvalidParams("the payoff is [optionType, strike]");
                 if (!a[6].is_array() || a[6].get_array().empty())
                     throw InvalidParams("expected an exercise, got " + json::serialize(a[6]));
                 const std::string exercise = text(a[6].get_array()[0]);
                 const bool american = exercise == "American";
                 if (!american && exercise != "European")
                     throw InvalidParams("unknown exercise " + exercise);
                 const DateSpec exerciseDate = american ? date(a[6].get_array()[1]) : DateSpec();
                 const Real maturity = real(a[7]);
                 const Integer tGrid = integer(a[8]), dampingSteps = integer(a[9]);
                 const std::vector<Real> scheme = reals(a[10]);
                 if (scheme.size() != 3)
                     throw InvalidParams("the scheme is [type, theta, mu]");
                 if (!a[11].is_array())
                     throw InvalidParams("expected points, got " + json::serialize(a[11]));
                 std::vector<std::vector<Real>> points;
                 for (const json::value& p : a[11].get_array())
                     points.push_back(reals(p));
                 return [=] {
                     std::vector<ext::shared_ptr<Fdm1dMesher>> m1d;
                     for (const auto& m : meshers)
                         m1d.push_back(ext::make_shared<Uniform1dMesher>(m[0], m[1], Size(m[2])));
                     const auto mesher = ext::make_shared<FdmMesherComposite>(m1d);
                     const DayCounter dc = Actual365Fixed();
                     ext::shared_ptr<YieldTermStructure> rTS;
                     if (discounted)
                         rTS = ext::make_shared<FlatForward>(today, r, dc);
                     const auto op = ext::make_shared<FdmWienerOp>(mesher, rTS, Array(lambdas.begin(), lambdas.end()));
                     const auto basket = ext::make_shared<AverageBasketPayoff>(
                         ext::make_shared<PlainVanillaPayoff>(Option::Type(Integer(payoff[0])), payoff[1]),
                         Array(weights.begin(), weights.end()));
                     const auto calculator = ext::make_shared<FdmLogBasketInnerValue>(basket, mesher);
                     ext::shared_ptr<FdmStepConditionComposite> conditions;
                     if (american)
                         conditions = FdmStepConditionComposite::vanillaComposite(
                             DividendSchedule(), ext::make_shared<AmericanExercise>(today, makeDate(exerciseDate)),
                             mesher, calculator, today, dc);
                     else
                         conditions = ext::make_shared<FdmStepConditionComposite>(
                             std::list<std::vector<Time>>(), FdmStepConditionComposite::Conditions());
                     const FdmSolverDesc desc = {mesher, FdmBoundaryConditionSet(), conditions, calculator, maturity,
                                                 Size(tGrid), Size(dampingSteps)};
                     const FdmSchemeDesc schemeDesc(FdmSchemeDesc::FdmSchemeType(Integer(scheme[0])), scheme[1],
                                                    scheme[2]);
                     switch (meshers.size()) {
                       case 1: return solve<1>(desc, schemeDesc, op, points);
                       case 2: return solve<2>(desc, schemeDesc, op, points);
                       case 3: return solve<3>(desc, schemeDesc, op, points);
                       default: return solve<4>(desc, schemeDesc, op, points);
                     }
                 };
             }},
        });

    }

}
