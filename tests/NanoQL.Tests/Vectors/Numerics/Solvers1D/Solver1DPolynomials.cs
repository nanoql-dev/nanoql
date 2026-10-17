// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The six polynomials and their derivatives of <c>tools/vectors/gen/math/solvers1d/brent.py</c> and
/// <c>newtonsafe.py</c>, each operation written as the generators write it, so the arithmetic is the same; and the
/// solve cases' inputs, read for <see cref="BrentVectorTests"/> and <see cref="NewtonSafeVectorTests"/>.
/// </summary>
internal static class Solver1DPolynomials
{
    /// <summary>A polynomial by its name.</summary>
    /// <param name="name">The name, p1 to p6.</param>
    /// <returns>The polynomial.</returns>
    internal static Func<double, double> Function(string name) => name switch
    {
        "p1" => x => (x * x) - 1.0,
        "p2" => x => 1.0 - (x * x),
        "p3" => x => (x * x * x) - (2.0 * x) - 5.0,
        "p4" => x => (x - 0.3) * (x + 2.0) * (x - 4.0),
        "p5" => x => (x * x * x * x) - 10.0,
        "p6" => x => (0.5 * x * x * x) - (x * x) + 0.25,
        _ => throw new InvalidOperationException($"unknown polynomial {name}"),
    };

    /// <summary>A polynomial's derivative by its name.</summary>
    /// <param name="name">The name, p1 to p6.</param>
    /// <returns>The derivative.</returns>
    internal static Func<double, double> Derivative(string name) => name switch
    {
        "p1" => x => 2.0 * x,
        "p2" => x => -2.0 * x,
        "p3" => x => (3.0 * x * x) - 2.0,
        "p4" => x => ((x + 2.0) * (x - 4.0)) + ((x - 0.3) * (x - 4.0)) + ((x - 0.3) * (x + 2.0)),
        "p5" => x => 4.0 * x * x * x,
        "p6" => x => (1.5 * x * x) - (2.0 * x),
        _ => throw new InvalidOperationException($"unknown polynomial {name}"),
    };

    /// <summary>The solver with the case's maximum evaluations and bounds set, through the copy-returning setters.</summary>
    /// <typeparam name="TSelf">The solver's type.</typeparam>
    /// <typeparam name="TFunction">The solver's function concept.</typeparam>
    /// <param name="solver">The solver.</param>
    /// <param name="c">The case.</param>
    /// <returns>The configured solver.</returns>
    internal static TSelf Configured<TSelf, TFunction>(Solver1D<TSelf, TFunction> solver, VectorCase c)
        where TSelf : Solver1D<TSelf, TFunction>
        where TFunction : ISolver1DFunction
    {
        var s = (TSelf)solver;
        if (c.NullableInt("maxEvaluations") is int evaluations)
        {
            s = s.SetMaxEvaluations(evaluations);
        }

        if (NullableNumber(c.Input("lowerBound")) is double lower)
        {
            s = s.SetLowerBound(lower);
        }

        if (NullableNumber(c.Input("upperBound")) is double upper)
        {
            s = s.SetUpperBound(upper);
        }

        return s;
    }

    /// <summary>Whether the case solves from a guess and a step, rather than within a bracket.</summary>
    /// <param name="c">The case.</param>
    /// <returns><see langword="true"/> for the step form.</returns>
    internal static bool StepForm(VectorCase c) => c.String("form") == "step";

    private static double? NullableNumber(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : Number(e);

    /// <summary>A polynomial and its derivative, counting their calls and keeping the function's last argument.</summary>
    /// <param name="name">The polynomial's name.</param>
    internal sealed class Counted(string name)
    {
        private readonly Func<double, double> f = Function(name);
        private readonly Func<double, double> df = Solver1DPolynomials.Derivative(name);

        /// <summary>Gets the number of calls of the function.</summary>
        public int Calls { get; private set; }

        /// <summary>Gets the number of calls of the derivative.</summary>
        public int DerivativeCalls { get; private set; }

        /// <summary>Gets the argument of the function's last call, or <see langword="null"/> before any.</summary>
        public double? Last { get; private set; }

        /// <summary>The function, counted.</summary>
        /// <param name="x">The point.</param>
        /// <returns>f(x).</returns>
        public double Evaluate(double x)
        {
            Calls++;
            Last = x;
            return f(x);
        }

        /// <summary>The derivative, counted.</summary>
        /// <param name="x">The point.</param>
        /// <returns>f′(x).</returns>
        public double Derivative(double x)
        {
            DerivativeCalls++;
            return df(x);
        }
    }
}
