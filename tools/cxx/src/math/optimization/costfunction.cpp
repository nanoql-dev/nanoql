// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/optimization/costfunction.hpp: CostFunction's default members (value as the root mean square of values,
// the central-difference gradient and Jacobian, valueAndGradient, valuesAndJacobian, finiteDifferenceEpsilon) over
// the harness's named cost functions, and SimpleCostFunction over a named values function
// (math/optimization/specs.hpp). A point is a list of reals; a gradient a list; a Jacobian the list of its rows, one
// per value.

#include "math/optimization/specs.hpp"

using namespace QuantLib;

namespace harness {

    namespace {

        using namespace optimization;

        // A function of a cost function and a point.
        Call atPoint(const Args& a, json::value (*f)(const CostFunction&, const Array&)) {
            arity(a, 2);
            CostFunctionSpec s = costFunction(a[0]);
            std::vector<Real> x = reals(a[1]);
            checkPoint(s, x);
            return [=] { return f(*makeCostFunction(s), makeArray(x)); };
        }

        // The Jacobian's rows: one per value at x.
        Matrix jacobianFor(const CostFunction& f, const Array& x) { return Matrix(f.values(x).size(), x.size()); }

        const Register unit({
            {"CostFunction::value",
             [](const Args& a) {
                 return atPoint(a, [](const CostFunction& f, const Array& x) { return json::value(f.value(x)); });
             }},
            {"CostFunction::values",
             [](const Args& a) {
                 return atPoint(a, [](const CostFunction& f, const Array& x) { return arrayResult(f.values(x)); });
             }},
            {"CostFunction::gradient",
             [](const Args& a) {
                 return atPoint(a, [](const CostFunction& f, const Array& x) {
                     Array grad(x.size());
                     f.gradient(grad, x);
                     return arrayResult(grad);
                 });
             }},
            {"CostFunction::valueAndGradient",
             [](const Args& a) {
                 return atPoint(a, [](const CostFunction& f, const Array& x) {
                     Array grad(x.size());
                     Real value = f.valueAndGradient(grad, x);
                     return json::value(json::object{{"value", value}, {"gradient", arrayResult(grad)}});
                 });
             }},
            {"CostFunction::jacobian",
             [](const Args& a) {
                 return atPoint(a, [](const CostFunction& f, const Array& x) {
                     Matrix jac = jacobianFor(f, x);
                     f.jacobian(jac, x);
                     return matrixResult(jac);
                 });
             }},
            {"CostFunction::valuesAndJacobian",
             [](const Args& a) {
                 return atPoint(a, [](const CostFunction& f, const Array& x) {
                     Matrix jac = jacobianFor(f, x);
                     Array values = f.valuesAndJacobian(jac, x);
                     return json::value(json::object{{"values", arrayResult(values)}, {"jacobian", matrixResult(jac)}});
                 });
             }},
            {"CostFunction::finiteDifferenceEpsilon",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 CostFunctionSpec s = costFunction(a[0]);
                 return [=] { return json::value(makeCostFunction(s)->finiteDifferenceEpsilon()); };
             }},
        });

    }

}
