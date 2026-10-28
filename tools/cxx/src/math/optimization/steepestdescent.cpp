// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/optimization/steepestdescent.hpp, .cpp: SteepestDescent::minimize on its default Armijo line search, given
// explicitly so that its last point can be read, with what it leaves in the Problem. The cost function, constraint and
// end criteria are specs (math/optimization/specs.hpp); the initial value is a list of reals; the result is specs.hpp's
// line-search result.

#include "math/optimization/specs.hpp"

#include <ql/math/optimization/armijo.hpp>
#include <ql/math/optimization/steepestdescent.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using namespace optimization;

        const Register unit({
            {"SteepestDescent::minimize",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 CostFunctionSpec f = costFunction(a[0]);
                 ConstraintSpec c = constraint(a[1]);
                 std::vector<Real> x = reals(a[2]);
                 EndCriteriaSpec e = endCriteria(a[3]);
                 checkPoint(f, x);
                 return [=] {
                     std::unique_ptr<CostFunction> cost = makeCostFunction(f);
                     Constraint bounds = makeConstraint(c);
                     Problem p(*cost, bounds, makeArray(x));
                     auto search = ext::make_shared<ArmijoLineSearch>();
                     SteepestDescent method(search);
                     EndCriteria::Type type = method.minimize(p, makeEndCriteria(e));
                     return lineSearchResult(p, type, *search);
                 };
             }},
        });

    }

}
