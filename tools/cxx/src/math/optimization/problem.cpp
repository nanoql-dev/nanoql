// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/optimization/problem.hpp: the members of Problem that count evaluations, and its reset and setters. A call
// builds the Problem over a named cost function (math/optimization/specs.hpp), NoConstraint and the point as its
// initial value, resets it (QuantLib leaves the counts and values uninitialized until reset) and calls the member at
// the point; it returns the member's result with the Problem's state after it (specs.hpp's Problem result).

#include "math/optimization/specs.hpp"

#include <ql/math/optimization/problem.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using namespace optimization;

        // A member of a Problem at a point.
        Call atPoint(const Args& a, json::value (*f)(Problem&, const Array&)) {
            arity(a, 2);
            CostFunctionSpec s = costFunction(a[0]);
            std::vector<Real> x = reals(a[1]);
            checkPoint(s, x);
            return [=] {
                std::unique_ptr<CostFunction> cost = makeCostFunction(s);
                NoConstraint none;
                Problem p(*cost, none, makeArray(x));
                p.reset();
                json::value result = f(p, makeArray(x));
                json::object out = problemResult(p);
                out["result"] = result;
                return json::value(std::move(out));
            };
        }

        const Register unit({
            {"Problem::value",
             [](const Args& a) { return atPoint(a, [](Problem& p, const Array& x) { return json::value(p.value(x)); }); }},
            {"Problem::values",
             [](const Args& a) { return atPoint(a, [](Problem& p, const Array& x) { return arrayResult(p.values(x)); }); }},
            {"Problem::gradient",
             [](const Args& a) {
                 return atPoint(a, [](Problem& p, const Array& x) {
                     Array grad(x.size());
                     p.gradient(grad, x);
                     return arrayResult(grad);
                 });
             }},
            {"Problem::valueAndGradient",
             [](const Args& a) {
                 return atPoint(a, [](Problem& p, const Array& x) {
                     Array grad(x.size());
                     Real value = p.valueAndGradient(grad, x);
                     return json::value(json::object{{"value", value}, {"gradient", arrayResult(grad)}});
                 });
             }},
            // After value and gradient at the point, setCurrentValue, setFunctionValue and setGradientNormValue:
            // the state before reset, then after it.
            {"Problem::reset",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 CostFunctionSpec s = costFunction(a[0]);
                 std::vector<Real> x = reals(a[1]), current = reals(a[2]);
                 Real functionValue = real(a[3]), gradientNormValue = real(a[4]);
                 checkPoint(s, x);
                 return [=] {
                     std::unique_ptr<CostFunction> cost = makeCostFunction(s);
                     NoConstraint none;
                     Problem p(*cost, none, makeArray(x));
                     p.reset();
                     Array grad(x.size());
                     p.value(makeArray(x));
                     p.gradient(grad, makeArray(x));
                     p.setCurrentValue(makeArray(current));
                     p.setFunctionValue(functionValue);
                     p.setGradientNormValue(gradientNormValue);
                     json::object before = problemResult(p);
                     p.reset();
                     return json::value(json::object{{"before", std::move(before)}, {"after", problemResult(p)}});
                 };
             }},
        });

    }

}
