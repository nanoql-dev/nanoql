// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The scalar cost functions of <c>tools/vectors/gen/math/optimization/simplex.py</c>, each operation written as the
/// generator writes it, so the arithmetic is the same; read for <see cref="SimplexVectorTests"/> and
/// <see cref="ConstraintVectorTests"/>.
/// </summary>
internal static class SimplexCostFunctions
{
    /// <summary>A cost function by its name, counting its calls.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The cost function.</returns>
    internal static Counted Create(string name) => new(name switch
    {
        "quadratic2" => x => ((x[0] - 1.0) * (x[0] - 1.0)) + ((x[1] + 0.5) * (x[1] + 0.5)),
        "rosenbrock" => x =>
        {
            double a = x[1] - (x[0] * x[0]);
            double b = 1.0 - x[0];
            return (100.0 * a * a) + (b * b);
        },
        "parabola" => x => (x[0] * x[0]) + x[0] + 1.0,
        "booth" => x =>
        {
            double a = x[0] + (2.0 * x[1]) - 7.0;
            double b = (2.0 * x[0]) + x[1] - 5.0;
            return (a * a) + (b * b);
        },
        "quadratic3" => x =>
            (x[0] * x[0]) + (2.0 * x[1] * x[1]) + (3.0 * x[2] * x[2]) + (x[0] * x[1]) - x[2],
        _ => throw new InvalidOperationException($"unknown cost function {name}"),
    });

    /// <summary>
    /// A scalar cost function, as the wheel's Python function is: its value the function's, counting its calls, and
    /// its values not implemented, as QuantLib-SWIG's <c>PyCostFunction</c>'s aren't.
    /// </summary>
    /// <param name="value">The function.</param>
    internal sealed class Counted(Func<Vector, double> value) : CostFunction
    {
        /// <summary>Gets the number of calls of the function.</summary>
        public int Calls { get; private set; }

        /// <inheritdoc/>
        public override double Value(Vector x)
        {
            Calls++;
            return value(x);
        }

        /// <inheritdoc/>
        public override Vector Values(Vector x) => throw new NanoQLException("Not implemented");
    }
}
