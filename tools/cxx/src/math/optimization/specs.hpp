// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The specs the optimization methods read (ql/math/optimization), shared by the units' files.
//
// A cost function is named, and tests/NanoQL.Tests defines each the same way
// (Vectors/Cxx/Numerics/Optimization/OptimizationSpecs.cs): {"type": <name>} for one of the harness's own cost
// functions below, which override only values() and so run CostFunction's defaults for every other member, or
// {"type": "SimpleCostFunction", "values": <name>} for QuantLib's SimpleCostFunction over a named values function.
// The named functions, x being the variables:
//   rosenbrock     2 variables, 2 values: 10 (x1 - x0 x0), 1 - x0
//   polynomialFit  3 variables, 4 values: x0 + x1 t + x2 t t - y at (t, y) = (-1, 2.5), (0, 1), (1, 1.5), (2, 3.5)
//   beale          2 variables, 3 values: y - x0 (1 - x1^k) for k = 1, 2, 3 and y = 1.5, 2.25, 2.625, the powers as
//                  products
//   linear         any number of variables, 1 value: sum of x_i (i + 1), minus 1
//   rankDeficient  2 variables, 2 values of s = x0 + x1 only, so the Jacobian has rank 1: 10 (s s s - 2), s - 3
//   quadratic2     2 variables, a scalar function, value() only (values() not implemented, as QuantLib-SWIG's
//                  PyCostFunction's isn't): (x0 - 1)(x0 - 1) + (x1 + 0.5)(x1 + 0.5)
//   booth          the same, with a = x0 + 2 x1 - 7, b = 2 x0 + x1 - 5: a a + b b
//   circle         (a values function) 2 variables, 2 values: x0 x0 + x1 x1 - 1, x0 - x1
// quadratic2 and booth are tools/vectors/gen/math/optimization/simplex.py's, for Simplex's constrained runs.
// QuantLib's test-suite cost functions (test-suite/optimizers.cpp), as it writes them, each value() with values() the
// one value repeated, the first three with QuantLib's analytic gradient() and valueAndGradient():
//   extendedRosenbrock          any number of variables: sum of 100 (x_{i+1} - x_i x_i)^2 + (1 - x_i)^2
//   weightedQuadratic           {"type", "center", "weight"}: sum of w_i (x_i - c_i)^2, as many variables as c
//   weightedQuadraticValueOnly  the same with no analytic gradient, so CostFunction's central differences
//   firstDeJong                 any number of variables: x . x
//   griewangk                   any number of variables: sum of x_i x_i / 4000, minus prod cos(x_i / sqrt(i + 1)),
//                               plus 1
//   rastrigin                   any number of variables: 10 n + sum of x_i x_i - 10 cos(2 pi x_i)
//   quadraticForm               {"type", "matrix"} with a square matrix's rows: x . (M x)
// A call refuses a point whose size isn't its function's number of variables, which the functions don't check.
//
// A constraint is {"type": "NoConstraint"}, {"type": "PositiveConstraint"}, {"type": "BoundaryConstraint", "low",
// "high"}, {"type": "CompositeConstraint", "c1", "c2"} with two constraint specs, {"type":
// "NonhomogeneousBoundaryConstraint", "low", "high"} with two lists of reals, or {"type":
// "HestonModel::FellerConstraint"}. End criteria are {"maxIterations",
// "maxStationaryStateIterations", "rootEpsilon", "functionEpsilon", "gradientNormEpsilon"}, the second and last null
// for Null<Size>() and Null<Real>(). A Problem result is {"currentValue", "functionValue", "gradientNormValue",
// "functionEvaluation", "gradientEvaluation"}, with "endCriteriaType" after a minimization, null for Null<Real>().

#ifndef NANOQL_CXX_MATH_OPTIMIZATION_SPECS_HPP
#define NANOQL_CXX_MATH_OPTIMIZATION_SPECS_HPP

#include "convert.hpp"

#include <ql/math/array.hpp>
#include <ql/math/matrix.hpp>
#include <ql/math/optimization/constraint.hpp>
#include <ql/math/optimization/costfunction.hpp>
#include <ql/math/optimization/endcriteria.hpp>
#include <ql/math/optimization/linesearch.hpp>
#include <ql/math/optimization/problem.hpp>

#include <memory>
#include <optional>
#include <string>
#include <vector>

namespace harness::optimization {

    struct CostFunctionSpec {
        std::string type;    // a named cost function, or "SimpleCostFunction"
        std::string values;  // SimpleCostFunction's named values function
        std::vector<QuantLib::Real> center, weight;          // weightedQuadratic's
        std::vector<std::vector<QuantLib::Real>> matrix;     // quadraticForm's rows
    };
    CostFunctionSpec costFunction(const json::value& v);
    // The function's number of variables, or none for a function of any number.
    std::optional<QuantLib::Size> variables(const CostFunctionSpec& s);
    // Refuses a point of another size than the function's variables.
    void checkPoint(const CostFunctionSpec& s, const std::vector<QuantLib::Real>& x);
    std::unique_ptr<QuantLib::CostFunction> makeCostFunction(const CostFunctionSpec& s);

    struct ConstraintSpec {
        std::string type;
        QuantLib::Real low = 0.0, high = 0.0;
        std::vector<QuantLib::Real> lows, highs;
        std::shared_ptr<ConstraintSpec> c1, c2;
    };
    ConstraintSpec constraint(const json::value& v);
    QuantLib::Constraint makeConstraint(const ConstraintSpec& s);

    struct EndCriteriaSpec {
        QuantLib::Size maxIterations, maxStationaryStateIterations;
        QuantLib::Real rootEpsilon, functionEpsilon, gradientNormEpsilon;
    };
    EndCriteriaSpec endCriteria(const json::value& v);
    QuantLib::EndCriteria makeEndCriteria(const EndCriteriaSpec& s);

    // A non-negative integer in Size, as the harness passes it.
    QuantLib::Size size(const json::value& v);

    QuantLib::Array makeArray(const std::vector<QuantLib::Real>& x);
    json::value arrayResult(const QuantLib::Array& a);
    json::value matrixResult(const QuantLib::Matrix& m);
    json::value realOrNull(QuantLib::Real x);
    json::value integerResult(QuantLib::Integer n);
    // A Problem's state: its current value, function value, gradient norm and counts.
    json::object problemResult(const QuantLib::Problem& p);
    // The state after a minimization, with the end criterion it returned.
    json::value minimizationResult(const QuantLib::Problem& p, QuantLib::EndCriteria::Type type);
    // The same after a line-search-based method's, with "lastAcceptedPoint": the point its line search last accepted,
    // the search's lastX() where its last search succeeded (the method then returns on the function tolerance or the
    // iteration limit before setting the problem's current value, which stays the point before), the current value
    // where it failed.
    json::value lineSearchResult(const QuantLib::Problem& p, QuantLib::EndCriteria::Type type,
                                 QuantLib::LineSearch& search);

}

#endif
