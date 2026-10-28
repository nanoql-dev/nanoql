// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The specs of the optimization vectors (<c>tools/cxx/src/math/optimization/specs.hpp</c>): the harness's named cost
/// functions, written as the harness writes them so the arithmetic is the same, which override only
/// <see cref="CostFunction.Values"/>; the constraints; the end criteria; and a minimization's results as the vectors
/// hold them.
/// </summary>
internal static class OptimizationSpecs
{
    /// <summary>The output names of a minimization's results, which <see cref="Results"/> compares.</summary>
    internal static readonly string[] ResultNames =
        ["currentValue", "functionValue", "gradientNormValue", "functionEvaluation", "gradientEvaluation", "endCriteriaType"];

    /// <summary>
    /// A cost function from its spec: <c>{"type": name}</c>, a <see cref="SimpleCostFunction"/>, or one of QuantLib's
    /// test-suite functions with its parameters (<c>weightedQuadratic</c>'s center and weight, <c>quadraticForm</c>'s
    /// matrix).
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The cost function.</returns>
    internal static CostFunction CostFunction(JsonElement spec)
    {
        string type = spec.GetProperty("type").GetString()
            ?? throw new InvalidOperationException("a cost function spec without a type");
        return type switch
        {
            "weightedQuadratic" => new WeightedQuadratic(Numbers(spec.GetProperty("center")), Numbers(spec.GetProperty("weight"))),
            "weightedQuadraticValueOnly" =>
                new WeightedQuadraticValueOnly(Numbers(spec.GetProperty("center")), Numbers(spec.GetProperty("weight"))),
            "quadraticForm" => new QuadraticForm(SquareMatrix(spec.GetProperty("matrix"))),
            _ => Named(type, spec.TryGetProperty("values", out JsonElement values) ? values.GetString() : null),
        };
    }

    /// <summary>A named cost function, or a <see cref="SimpleCostFunction"/> over a named values function.</summary>
    /// <param name="type">The name, or <c>SimpleCostFunction</c>.</param>
    /// <param name="values">SimpleCostFunction's values function.</param>
    /// <returns>The cost function.</returns>
    internal static CostFunction Named(string type, string? values = null) => type switch
    {
        "rosenbrock" => new Rosenbrock(),
        "polynomialFit" => new PolynomialFit(),
        "beale" => new Beale(),
        "linear" => new Linear(),
        "rankDeficient" => new RankDeficient(),
        "quadratic2" => new Quadratic2(),
        "booth" => new Booth(),
        "extendedRosenbrock" => new ExtendedRosenbrock(),
        "firstDeJong" => new FirstDeJong(),
        "griewangk" => new Griewangk(),
        "rastrigin" => new Rastrigin(),
        "SimpleCostFunction" when values == "circle" => new SimpleCostFunction(Circle),
        _ => throw new InvalidOperationException($"unknown cost function {type}"),
    };

    /// <summary>A constraint from its spec.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The constraint.</returns>
    internal static Constraint Constraint(JsonElement spec) => spec.GetProperty("type").GetString() switch
    {
        "NoConstraint" => new NoConstraint(),
        "PositiveConstraint" => new PositiveConstraint(),
        "BoundaryConstraint" => new BoundaryConstraint(Number(spec.GetProperty("low")), Number(spec.GetProperty("high"))),
        "CompositeConstraint" => new CompositeConstraint(Constraint(spec.GetProperty("c1")), Constraint(spec.GetProperty("c2"))),
        "NonhomogeneousBoundaryConstraint" =>
            new NonhomogeneousBoundaryConstraint(Numbers(spec.GetProperty("low")), Numbers(spec.GetProperty("high"))),
        "HestonModel::FellerConstraint" => new HestonModelFellerConstraint(),
        string type => throw new InvalidOperationException($"unknown constraint {type}"),
        null => throw new InvalidOperationException("a constraint spec without a type"),
    };

    /// <summary>End criteria from their spec, the nulls QuantLib's <c>Null&lt;Size&gt;()</c> and <c>Null&lt;Real&gt;()</c>.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The end criteria.</returns>
    internal static EndCriteria EndCriteria(JsonElement spec)
    {
        JsonElement stationary = spec.GetProperty("maxStationaryStateIterations");
        JsonElement gradient = spec.GetProperty("gradientNormEpsilon");
        return new EndCriteria(
            spec.GetProperty("maxIterations").GetInt32(),
            stationary.ValueKind == JsonValueKind.Null ? null : stationary.GetInt32(),
            Number(spec.GetProperty("rootEpsilon")),
            Number(spec.GetProperty("functionEpsilon")),
            gradient.ValueKind == JsonValueKind.Null ? null : Number(gradient));
    }

    /// <summary>Compares a minimization's results with a case's outputs of <see cref="ResultNames"/>.</summary>
    /// <param name="check">The comparison.</param>
    /// <param name="c">The case.</param>
    /// <param name="r">The results.</param>
    internal static void Results(VectorAssert check, VectorCase c, ProblemResults r)
    {
        check.Output(c, "currentValue", r.CurrentValue);
        check.Output(c, "functionValue", r.FunctionValue);
        check.Output(c, "gradientNormValue", r.GradientNormValue);
        check.Output(c, "functionEvaluation", r.FunctionEvaluation);
        check.Output(c, "gradientEvaluation", r.GradientEvaluation);
        check.Output(c, "endCriteriaType", r.EndCriteriaType);
    }

    /// <summary>
    /// Compares a line-search-based minimization's results with a case's outputs, its current value with the point
    /// QuantLib's line search last accepted (<c>lastAcceptedPoint</c>): QuantLib's own current value is the point before
    /// where it returns on the function tolerance or the iteration limit (the S-sr plan's SSR6 (xvii)).
    /// </summary>
    /// <param name="check">The comparison.</param>
    /// <param name="c">The case.</param>
    /// <param name="r">The results.</param>
    internal static void LineSearchResults(VectorAssert check, VectorCase c, ProblemResults r)
    {
        check.Output(c, "lastAcceptedPoint", r.CurrentValue);
        check.Output(c, "functionValue", r.FunctionValue);
        check.Output(c, "gradientNormValue", r.GradientNormValue);
        check.Output(c, "functionEvaluation", r.FunctionEvaluation);
        check.Output(c, "gradientEvaluation", r.GradientEvaluation);
        check.Output(c, "endCriteriaType", r.EndCriteriaType);
    }

    /// <summary>A matrix as the vectors hold it: the list of its rows.</summary>
    /// <param name="m">The matrix.</param>
    /// <returns>The rows.</returns>
    internal static object?[] Rows(Matrix m) => [.. Enumerable.Range(0, m.Rows).Select(i => (object?)m.Row(i).ToArray())];

    // The harness's named values function of SimpleCostFunction.
    private static Vector Circle(Vector x)
    {
        var v = new Vector(2);
        v[0] = (x[0] * x[0]) + (x[1] * x[1]) - 1.0;
        v[1] = x[0] - x[1];
        return v;
    }

    // 2 variables, 2 values: 10 (x1 - x0 x0), 1 - x0.
    private sealed class Rosenbrock : CostFunction
    {
        public override Vector Values(Vector x)
        {
            var v = new Vector(2);
            v[0] = 10.0 * (x[1] - (x[0] * x[0]));
            v[1] = 1.0 - x[0];
            return v;
        }
    }

    // 3 variables, 4 values: x0 + x1 t + x2 t t - y.
    private sealed class PolynomialFit : CostFunction
    {
        private static readonly double[] T = [-1.0, 0.0, 1.0, 2.0];
        private static readonly double[] Y = [2.5, 1.0, 1.5, 3.5];

        public override Vector Values(Vector x)
        {
            var v = new Vector(4);
            for (int i = 0; i < 4; ++i)
            {
                v[i] = x[0] + (x[1] * T[i]) + (x[2] * T[i] * T[i]) - Y[i];
            }

            return v;
        }
    }

    // 2 variables, 3 values: y - x0 (1 - x1^k), the powers as products.
    private sealed class Beale : CostFunction
    {
        public override Vector Values(Vector x)
        {
            var v = new Vector(3);
            v[0] = 1.5 - (x[0] * (1.0 - x[1]));
            v[1] = 2.25 - (x[0] * (1.0 - (x[1] * x[1])));
            v[2] = 2.625 - (x[0] * (1.0 - (x[1] * x[1] * x[1])));
            return v;
        }
    }

    // Any number of variables, 1 value: sum of x_i (i + 1), minus 1.
    private sealed class Linear : CostFunction
    {
        public override Vector Values(Vector x)
        {
            double s = 0.0;
            for (int i = 0; i < x.Size; ++i)
            {
                s += x[i] * (i + 1.0);
            }

            return new Vector(1, s - 1.0);
        }
    }

    // 2 variables, 2 values of s = x0 + x1 only, so the Jacobian has rank 1: 10 (s s s - 2), s - 3.
    private sealed class RankDeficient : CostFunction
    {
        public override Vector Values(Vector x)
        {
            double s = x[0] + x[1];
            var v = new Vector(2);
            v[0] = 10.0 * ((s * s * s) - 2.0);
            v[1] = s - 3.0;
            return v;
        }
    }

    // A scalar cost function: its value only, its values not implemented (QuantLib-SWIG's PyCostFunction's aren't).
    private sealed class Quadratic2 : CostFunction
    {
        public override double Value(Vector x) => ((x[0] - 1.0) * (x[0] - 1.0)) + ((x[1] + 0.5) * (x[1] + 0.5));

        public override Vector Values(Vector x) => throw new NanoQLException("Not implemented");
    }

    // The same, Booth's function.
    private sealed class Booth : CostFunction
    {
        public override double Value(Vector x)
        {
            double a = x[0] + (2.0 * x[1]) - 7.0;
            double b = (2.0 * x[0]) + x[1] - 5.0;
            return (a * a) + (b * b);
        }

        public override Vector Values(Vector x) => throw new NanoQLException("Not implemented");
    }

    // A square matrix from the list of its rows.
    private static Matrix SquareMatrix(JsonElement rows)
    {
        double[][] r = [.. rows.EnumerateArray().Select(row => Numbers(row))];
        var m = new Matrix(r.Length, r.Length);
        for (int i = 0; i < r.Length; ++i)
        {
            for (int j = 0; j < r.Length; ++j)
            {
                m[i, j] = r[i][j];
            }
        }

        return m;
    }

    // QuantLib's test-suite cost functions (test-suite/optimizers.cpp), as the harness writes them.

    // Any number of variables: sum of 100 (x_{i+1} - x_i x_i)^2 + (1 - x_i)^2, with its analytic gradient.
    private sealed class ExtendedRosenbrock : CostFunction
    {
        public override double Value(Vector x)
        {
            double f = 0.0;
            for (int i = 0; i + 1 < x.Size; ++i)
            {
                f += (100.0 * Math.Pow(x[i + 1] - (x[i] * x[i]), 2)) + Math.Pow(1.0 - x[i], 2);
            }

            return f;
        }

        public override Vector Values(Vector x) => new(1, Value(x));

        public override void Gradient(Vector grad, Vector x)
        {
            grad.AsSpan().Fill(0.0);
            for (int i = 0; i + 1 < x.Size; ++i)
            {
                grad[i] += (-400.0 * x[i] * (x[i + 1] - (x[i] * x[i]))) - (2.0 * (1.0 - x[i]));
                grad[i + 1] += 200.0 * (x[i + 1] - (x[i] * x[i]));
            }
        }

        public override double ValueAndGradient(Vector grad, Vector x)
        {
            Gradient(grad, x);
            return Value(x);
        }
    }

    // Sum of w_i (x_i - c_i)^2, with no analytic gradient.
    private class WeightedQuadraticValueOnly(double[] center, double[] weight) : CostFunction
    {
        protected double[] Center { get; } = center;

        protected double[] Weight { get; } = weight;

        public override double Value(Vector x)
        {
            double f = 0.0;
            for (int i = 0; i < x.Size; ++i)
            {
                f += Weight[i] * Math.Pow(x[i] - Center[i], 2);
            }

            return f;
        }

        public override Vector Values(Vector x) => new(1, Value(x));
    }

    // The same with its analytic gradient.
    private sealed class WeightedQuadratic(double[] center, double[] weight) : WeightedQuadraticValueOnly(center, weight)
    {
        public override void Gradient(Vector grad, Vector x)
        {
            for (int i = 0; i < x.Size; ++i)
            {
                grad[i] = 2.0 * Weight[i] * (x[i] - Center[i]);
            }
        }

        public override double ValueAndGradient(Vector grad, Vector x)
        {
            Gradient(grad, x);
            return Value(x);
        }
    }

    // x . x
    private sealed class FirstDeJong : CostFunction
    {
        public override Vector Values(Vector x) => new(x.Size, Value(x));

        public override double Value(Vector x) => ArrayFunctions.DotProduct(x, x);
    }

    // Sum of x_i x_i / 4000, minus prod cos(x_i / sqrt(i + 1)), plus 1.
    private sealed class Griewangk : CostFunction
    {
        public override Vector Values(Vector x) => new(x.Size, Value(x));

        public override double Value(Vector x)
        {
            double fx = 0.0;
            for (int i = 0; i < x.Size; ++i)
            {
                fx += x[i] * x[i] / 4000.0;
            }

            double p = 1.0;
            for (int i = 0; i < x.Size; ++i)
            {
                p *= Math.Cos(x[i] / Math.Sqrt(i + 1.0));
            }

            return fx - p + 1.0;
        }
    }

    // 10 n + sum of x_i x_i - 10 cos(2 pi x_i).
    private sealed class Rastrigin : CostFunction
    {
        public override double Value(Vector x)
        {
            double fx = 10.0 * x.Size;
            for (int i = 0; i < x.Size; ++i)
            {
                fx += (x[i] * x[i]) - (10.0 * Math.Cos(2.0 * Math.PI * x[i]));
            }

            return fx;
        }

        public override Vector Values(Vector x) => new(x.Size, Value(x));
    }

    // x . (M x)
    private sealed class QuadraticForm(Matrix m) : CostFunction
    {
        public override double Value(Vector x) => ArrayFunctions.DotProduct(x, m * x);

        public override Vector Values(Vector x) => new(x.Size, Value(x));
    }
}
