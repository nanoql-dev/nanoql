// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/optimization/lbfgsb.hpp, .cpp: LBFGSB::minimize, with what it leaves in the Problem. "LBFGSB::minimize"
// takes (cost function, constraint, initial value, end criteria, memory, pgTol, fTol, lower bound, upper bound), the
// specs as math/optimization/specs.hpp's, the bounds lists of reals or both null for the constructor without them;
// it returns specs.hpp's Problem result with the end criterion.

#include "math/optimization/specs.hpp"

#include <ql/math/optimization/lbfgsb.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using namespace optimization;

        const Register unit({
            {"LBFGSB::minimize",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 CostFunctionSpec f = costFunction(a[0]);
                 ConstraintSpec c = constraint(a[1]);
                 std::vector<Real> x = reals(a[2]);
                 EndCriteriaSpec e = endCriteria(a[3]);
                 Size memory = size(a[4]);
                 Real pgTol = real(a[5]), fTol = real(a[6]);
                 bool bounded = !a[7].is_null();
                 if (bounded == a[8].is_null())
                     throw InvalidParams("the bounds are both lists or both null");
                 std::vector<Real> lower = bounded ? reals(a[7]) : std::vector<Real>();
                 std::vector<Real> upper = bounded ? reals(a[8]) : std::vector<Real>();
                 checkPoint(f, x);
                 return [=] {
                     std::unique_ptr<CostFunction> cost = makeCostFunction(f);
                     Constraint bounds = makeConstraint(c);
                     Problem p(*cost, bounds, makeArray(x));
                     std::unique_ptr<LBFGSB> method =
                         bounded ? std::make_unique<LBFGSB>(makeArray(lower), makeArray(upper), memory, pgTol, fTol)
                                 : std::make_unique<LBFGSB>(memory, pgTol, fTol);
                     EndCriteria::Type type = method->minimize(p, makeEndCriteria(e));
                     return minimizationResult(p, type);
                 };
             }},
        });

    }

}
