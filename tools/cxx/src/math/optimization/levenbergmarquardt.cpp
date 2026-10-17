// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/optimization/levenbergmarquardt.hpp, .cpp: LevenbergMarquardt::minimize, which runs MINPACK's lmdif
// (ql/math/optimization/lmdif.cpp), with what it leaves in the Problem; the wheel can't drive it, as its cost
// functions have no values. The cost function, constraint and end criteria are specs (math/optimization/specs.hpp);
// the initial value is a list of reals, then epsfcn, xtol, gtol and useCostFunctionsJacobian; the result is specs.hpp's
// Problem result with the end criterion.

#include "math/optimization/specs.hpp"

#include <ql/math/optimization/levenbergmarquardt.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using namespace optimization;

        const Register unit({
            {"LevenbergMarquardt::minimize",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 CostFunctionSpec f = costFunction(a[0]);
                 ConstraintSpec c = constraint(a[1]);
                 std::vector<Real> x = reals(a[2]);
                 EndCriteriaSpec e = endCriteria(a[3]);
                 Real epsfcn = real(a[4]), xtol = real(a[5]), gtol = real(a[6]);
                 bool useCostFunctionsJacobian = boolean(a[7]);
                 checkPoint(f, x);
                 return [=] {
                     std::unique_ptr<CostFunction> cost = makeCostFunction(f);
                     Constraint bounds = makeConstraint(c);
                     Problem p(*cost, bounds, makeArray(x));
                     LevenbergMarquardt method(epsfcn, xtol, gtol, useCostFunctionsJacobian);
                     EndCriteria::Type type = method.minimize(p, makeEndCriteria(e));
                     return minimizationResult(p, type);
                 };
             }},
        });

    }

}
