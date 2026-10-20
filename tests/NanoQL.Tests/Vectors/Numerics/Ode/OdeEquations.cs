// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Numerics;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The named equations f'(x) = F(x, f(x)) of the Runge-Kutta vectors, each operation written as its generator writes
/// it, so the arithmetic is the same: the real ones of <c>tools/vectors/gen/math/ode/adaptiverungekutta.py</c>, read
/// for <see cref="AdaptiveRungeKuttaVectorTests"/>, and the complex ones of the C++ harness's
/// <c>tools/cxx/src/math/ode/adaptiverungekutta.cpp</c>, read for <see cref="AdaptiveRungeKuttaCxxVectorTests"/>.
/// </summary>
internal static class OdeEquations
{
    /// <summary>Whether a real equation is a vector one.</summary>
    /// <param name="name">The equation's name.</param>
    /// <returns><see langword="true"/> for a vector equation.</returns>
    internal static bool IsVector(string name) => name is "oscillator" or "predatorPrey" or "chain" or "expVector";

    /// <summary>A scalar real equation by its name.</summary>
    /// <param name="name">The name.</param>
    /// <returns>F.</returns>
    internal static Func<double, double, double> Scalar(string name) => name switch
    {
        "exp" => (x, y) => y,
        "decay" => (x, y) => -0.5 * y,
        "logistic" => (x, y) => y * (1.0 - y),
        "gaussian" => (x, y) => -2.0 * x * y,
        "linear" => (x, y) => x + y,
        "riccati" => (x, y) => y * y,
        "stiff" => (x, y) => (-1000.0 * y) + 1000.0,
        "singular" => (x, y) => 1.0 / (1.0 - x),
        "nan" => (x, y) => double.NaN,
        "infinity" => (x, y) => double.PositiveInfinity,
        _ => throw new InvalidOperationException($"unknown scalar equation {name}"),
    };

    /// <summary>A vector real equation by its name.</summary>
    /// <param name="name">The name.</param>
    /// <returns>F.</returns>
    internal static Func<double, ImmutableArray<double>, ImmutableArray<double>> Vector(string name) => name switch
    {
        "oscillator" => (x, y) => [y[1], -y[0]],
        "predatorPrey" => (x, y) => [y[0] - (y[0] * y[1]), (y[0] * y[1]) - y[1]],
        "chain" => (x, y) => [y[1], y[2], -y[0]],
        "expVector" => (x, y) => [y[0]],
        _ => throw new InvalidOperationException($"unknown vector equation {name}"),
    };

    /// <summary>Whether a complex equation is a vector one.</summary>
    /// <param name="name">The equation's name.</param>
    /// <returns><see langword="true"/> for a vector equation.</returns>
    internal static bool IsComplexVector(string name) => name is "oscillator" or "coupled";

    /// <summary>A scalar complex equation by its name.</summary>
    /// <param name="name">The name.</param>
    /// <returns>F.</returns>
    internal static Func<double, Complex, Complex> ComplexScalar(string name) => name switch
    {
        "rotation" => (x, y) => new Complex(0.0, 1.0) * y,
        "dampedRotation" => (x, y) => new Complex(-0.5, 2.0) * y,
        "chirp" => (x, y) => new Complex(0.0, x) * y,
        "decay" => (x, y) => -y + new Complex(0.25, 0.5),
        _ => throw new InvalidOperationException($"unknown scalar complex equation {name}"),
    };

    /// <summary>A complex equation by its name in the vector form, a scalar one on vectors of one value.</summary>
    /// <param name="name">The name.</param>
    /// <returns>F.</returns>
    internal static Func<double, ImmutableArray<Complex>, ImmutableArray<Complex>> ComplexVector(string name) => name switch
    {
        "oscillator" => (x, y) => [y[1], -y[0]],
        "coupled" => (x, y) => [new Complex(0.0, 1.0) * y[1], (new Complex(0.0, 1.0) * y[0]) - (0.1 * y[1])],
        _ => (x, y) => [ComplexScalar(name)(x, y[0])],
    };
}
