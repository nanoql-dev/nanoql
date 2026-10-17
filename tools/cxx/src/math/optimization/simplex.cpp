// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/optimization/simplex.hpp, .cpp: Simplex::minimize, with what it leaves in the Problem, which the wheel's
// Optimizer.solve doesn't return. The cost function, constraint and end criteria are specs
// (math/optimization/specs.hpp); the initial value is a list of reals; the result is specs.hpp's Problem result with
// the end criterion.

#include "math/optimization/specs.hpp"

#include <ql/math/optimization/simplex.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using namespace optimization;

        const Register unit({
            {"Simplex::minimize",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 CostFunctionSpec f = costFunction(a[0]);
                 ConstraintSpec c = constraint(a[1]);
                 std::vector<Real> x = reals(a[2]);
                 EndCriteriaSpec e = endCriteria(a[3]);
                 Real lambda = real(a[4]);
                 checkPoint(f, x);
                 return [=] {
                     std::unique_ptr<CostFunction> cost = makeCostFunction(f);
                     Constraint bounds = makeConstraint(c);
                     Problem p(*cost, bounds, makeArray(x));
                     Simplex simplex(lambda);
                     EndCriteria::Type type = simplex.minimize(p, makeEndCriteria(e));
                     return minimizationResult(p, type);
                 };
             }},
        });

    }

}
