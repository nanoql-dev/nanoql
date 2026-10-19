// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/optimization/projectedcostfunction.hpp, .cpp: ProjectedCostFunction, which no wheel class exposes, over
// one of the harness's named cost functions (math/optimization/specs.hpp), built inside each call by either of
// QuantLib's constructors: "values" from the parameters' values and the fixed-parameter flags (an empty list for
// QuantLib's default, all free), "projection" from a Projection built on them, whose state it copies.
//
//     ProjectedCostFunction::value    (constructor, costFunction, parameterValues, fixParameters, freeParameters)
//     ProjectedCostFunction::values   (constructor, costFunction, parameterValues, fixParameters, freeParameters)
//     ProjectedCostFunction::project  (constructor, costFunction, parameterValues, fixParameters, parameters)
//     ProjectedCostFunction::include  (constructor, costFunction, parameterValues, fixParameters, projectedParameters)
//     ProjectedCostFunction::finiteDifferenceEpsilon  (constructor, costFunction, parameterValues, fixParameters)
//
// the value a real, the others an Array as a list of reals, or QuantLib's refusal of the projection or of the call's
// argument. The parameters' values are a point of the cost function, which the harness checks.

#include "math/optimization/specs.hpp"

#include <ql/math/optimization/projectedcostfunction.hpp>
#include <ql/math/optimization/projection.hpp>

#include <memory>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using namespace optimization;

        struct ProjectedSpec {
            std::string constructor;  // values or projection
            CostFunctionSpec costFunction;
            std::vector<Real> parameterValues;
            std::vector<bool> fixParameters;
        };

        std::vector<bool> flags(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected an array of booleans, got " + json::serialize(v));
            std::vector<bool> out;
            for (const json::value& x : v.get_array())
                out.push_back(boolean(x));
            return out;
        }

        // The check of the first four arguments.
        ProjectedSpec projected(const Args& a) {
            ProjectedSpec s;
            s.constructor = text(a[0]);
            if (s.constructor != "values" && s.constructor != "projection")
                throw InvalidParams("a ProjectedCostFunction is built from values or a projection, not " +
                                    s.constructor);
            s.costFunction = costFunction(a[1]);
            s.parameterValues = reals(a[2]);
            s.fixParameters = flags(a[3]);
            checkPoint(s.costFunction, s.parameterValues);
            return s;
        }

        // The call: the cost function, which the projected one refers to, and the projected one.
        struct Built {
            std::unique_ptr<CostFunction> costFunction;
            std::unique_ptr<ProjectedCostFunction> projected;
        };

        Built build(const ProjectedSpec& s) {
            Built b;
            b.costFunction = makeCostFunction(s.costFunction);
            if (s.constructor == "values") {
                b.projected = std::make_unique<ProjectedCostFunction>(*b.costFunction, makeArray(s.parameterValues),
                                                                      s.fixParameters);
            } else {
                Projection projection(makeArray(s.parameterValues), s.fixParameters);
                b.projected = std::make_unique<ProjectedCostFunction>(*b.costFunction, projection);
            }
            return b;
        }

        // A member of a list of reals: (constructor, costFunction, parameterValues, fixParameters, x).
        Call withArray(const Args& a, json::value (*f)(const ProjectedCostFunction&, const Array&)) {
            arity(a, 5);
            ProjectedSpec s = projected(a);
            std::vector<Real> x = reals(a[4]);
            return [=] {
                Built b = build(s);
                return f(*b.projected, makeArray(x));
            };
        }

        const Register unit({
            {"ProjectedCostFunction::value",
             [](const Args& a) {
                 return withArray(a, [](const ProjectedCostFunction& p, const Array& x) {
                     return json::value(p.value(x));
                 });
             }},
            {"ProjectedCostFunction::values",
             [](const Args& a) {
                 return withArray(a, [](const ProjectedCostFunction& p, const Array& x) {
                     return arrayResult(p.values(x));
                 });
             }},
            {"ProjectedCostFunction::project",
             [](const Args& a) {
                 return withArray(a, [](const ProjectedCostFunction& p, const Array& x) {
                     return arrayResult(p.project(x));
                 });
             }},
            {"ProjectedCostFunction::include",
             [](const Args& a) {
                 return withArray(a, [](const ProjectedCostFunction& p, const Array& x) {
                     return arrayResult(p.include(x));
                 });
             }},
            {"ProjectedCostFunction::finiteDifferenceEpsilon",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 ProjectedSpec s = projected(a);
                 return [=] { return json::value(build(s).projected->finiteDifferenceEpsilon()); };
             }},
        });

    }

}
