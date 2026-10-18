// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2008, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/loginterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Log-linear interpolation between discrete points: the exponential of the linear interpolation of the logarithms of
/// the y values, which must be positive.
/// </summary>
/// <remarks>It copies its inputs, as every <see cref="Interpolation"/> does. It has no primitive.</remarks>
public sealed class LogLinearInterpolation : Interpolation
{
    private readonly LinearInterpolation interpolation;

    /// <summary>Initializes a new instance of the <see cref="LogLinearInterpolation"/> class.</summary>
    /// <param name="x">The x values, which must be sorted; at least 2.</param>
    /// <param name="y">The y values, one per x value, each positive.</param>
    /// <exception cref="ArgumentException">The sizes differ, there are fewer than 2 points, or a y value isn't positive.</exception>
    public LogLinearInterpolation(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
        : base(x, y, Linear.RequiredPoints)
    {
        interpolation = new LinearInterpolation(X, LogInterpolationImpl.Logs(Y));
    }

    /// <inheritdoc/>
    protected override double ValueCore(double x) => LogInterpolationImpl.Value(interpolation, x);

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always: the log interpolations have no primitive.</exception>
    protected override double PrimitiveCore(double x) => LogInterpolationImpl.Primitive();

    /// <inheritdoc/>
    protected override double DerivativeCore(double x) => LogInterpolationImpl.Derivative(interpolation, x);

    /// <inheritdoc/>
    protected override double SecondDerivativeCore(double x) => LogInterpolationImpl.SecondDerivative(interpolation, x);
}
