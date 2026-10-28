// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/optimization/goldstein.hpp, .cpp: GoldsteinLineSearch, through the minimization of a line-search-based
// method that runs on it. "GoldsteinLineSearch::minimize" takes (method, cost function, constraint, initial value,
// end criteria, eps, alpha, beta, extrapolation), the method "ConjugateGradient", "SteepestDescent" or "BFGS", the
// specs as math/optimization/specs.hpp's; it returns specs.hpp's line-search result.

#include "math/optimization/specs.hpp"

#include <ql/math/optimization/bfgs.hpp>
#include <ql/math/optimization/conjugategradient.hpp>
#include <ql/math/optimization/goldstein.hpp>
#include <ql/math/optimization/steepestdescent.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using namespace optimization;

        const Register unit({
            {"GoldsteinLineSearch::minimize",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 std::string method = text(a[0]);
                 CostFunctionSpec f = costFunction(a[1]);
                 ConstraintSpec c = constraint(a[2]);
                 std::vector<Real> x = reals(a[3]);
                 EndCriteriaSpec e = endCriteria(a[4]);
                 Real eps = real(a[5]), alpha = real(a[6]), beta = real(a[7]), extrapolation = real(a[8]);
                 checkPoint(f, x);
                 if (method != "ConjugateGradient" && method != "SteepestDescent" && method != "BFGS")
                     throw InvalidParams("unknown line-search method " + method);
                 return [=] {
                     std::unique_ptr<CostFunction> cost = makeCostFunction(f);
                     Constraint bounds = makeConstraint(c);
                     Problem p(*cost, bounds, makeArray(x));
                     auto search = ext::make_shared<GoldsteinLineSearch>(eps, alpha, beta, extrapolation);
                     std::unique_ptr<OptimizationMethod> m;
                     if (method == "ConjugateGradient")
                         m = std::make_unique<ConjugateGradient>(search);
                     else if (method == "SteepestDescent")
                         m = std::make_unique<SteepestDescent>(search);
                     else
                         m = std::make_unique<BFGS>(search);
                     EndCriteria::Type type = m->minimize(p, makeEndCriteria(e));
                     return lineSearchResult(p, type, *search);
                 };
             }},
        });

    }

}
