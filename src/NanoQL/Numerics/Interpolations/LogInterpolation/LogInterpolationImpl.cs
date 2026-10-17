// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2008, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/loginterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib's <c>detail::LogInterpolationImpl</c>, shared by the log interpolations: the logarithms of the y values,
/// and the value and derivatives of the exponential of an interpolation of them.
/// </summary>
internal static class LogInterpolationImpl
{
    /// <summary>The logarithms of the y values, each refused unless positive: the first step of QuantLib's <c>update()</c>.</summary>
    /// <param name="y">The y values.</param>
    /// <returns>A new array of their logarithms.</returns>
    /// <exception cref="ArgumentException">A y value isn't positive.</exception>
    internal static double[] Logs(ReadOnlySpan<double> y)
    {
        var logY = new double[y.Length];
        for (int i = 0; i < logY.Length; ++i)
        {
            if (!(y[i] > 0.0))
            {
                throw new ArgumentException(
                    $"invalid value ({y[i].ToString(CultureInfo.InvariantCulture)}) at index {i}", nameof(y));
            }

            logY[i] = Math.Log(y[i]);
        }

        return logY;
    }

    /// <summary>The value: the exponential of the interpolated logarithm, extrapolated where needed.</summary>
    /// <param name="interpolation">The interpolation of the logarithms.</param>
    /// <param name="x">The point.</param>
    /// <returns>The value.</returns>
    internal static double Value(Interpolation interpolation, double x) => Math.Exp(interpolation.Evaluate(x, true));

    /// <summary>The primitive, which QuantLib doesn't implement.</summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="NanoQLException">Always.</exception>
    internal static double Primitive() => throw new NanoQLException("LogInterpolation primitive not implemented");

    /// <summary>The first derivative: the value times the interpolated logarithm's derivative.</summary>
    /// <param name="interpolation">The interpolation of the logarithms.</param>
    /// <param name="x">The point.</param>
    /// <returns>The derivative.</returns>
    internal static double Derivative(Interpolation interpolation, double x) =>
        Value(interpolation, x) * interpolation.Derivative(x, true);

    /// <summary>The second derivative, from the value and the interpolated logarithm's two derivatives.</summary>
    /// <param name="interpolation">The interpolation of the logarithms.</param>
    /// <param name="x">The point.</param>
    /// <returns>The second derivative.</returns>
    internal static double SecondDerivative(Interpolation interpolation, double x) =>
        (Derivative(interpolation, x) * interpolation.Derivative(x, true))
        + (Value(interpolation, x) * interpolation.SecondDerivative(x, true));
}
