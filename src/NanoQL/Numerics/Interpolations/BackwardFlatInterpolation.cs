// SPDX-FileCopyrightText: Copyright (C) 2005, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/backwardflatinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Backward-flat interpolation between discrete points: on each interval (x<sub>i</sub>, x<sub>i+1</sub>] the value is
/// y<sub>i+1</sub>, and below x<sub>0</sub> it is y<sub>0</sub>.
/// </summary>
/// <remarks>It copies its inputs, as every <see cref="Interpolation"/> does.</remarks>
public sealed class BackwardFlatInterpolation : Interpolation
{
    private readonly double[] primitive;

    /// <summary>Initializes a new instance of the <see cref="BackwardFlatInterpolation"/> class.</summary>
    /// <param name="x">The x values, which must be sorted; at least 1.</param>
    /// <param name="y">The y values, one per x value.</param>
    /// <exception cref="ArgumentException">The sizes differ, or there are no points.</exception>
    public BackwardFlatInterpolation(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
        : base(x, y, BackwardFlat.RequiredPoints)
    {
        ReadOnlySpan<double> xs = X, ys = Y;
        int n = xs.Length;
        primitive = new double[n];

        // QuantLib's update().
        primitive[0] = 0.0;
        for (int i = 1; i < n; i++)
        {
            double dx = xs[i] - xs[i - 1];
            primitive[i] = primitive[i - 1] + (dx * ys[i]);
        }
    }

    /// <inheritdoc/>
    protected override double ValueCore(double x)
    {
        if (x <= X[0] || X.Length == 1)
        {
            return Y[0];
        }

        int i = Locate(x);
        if (x == X[i])
        {
            return Y[i];
        }
        else
        {
            return Y[i + 1];
        }
    }

    /// <inheritdoc/>
    protected override double PrimitiveCore(double x)
    {
        if (X.Length == 1)
        {
            return (x - X[0]) * Y[0];
        }

        int i = Locate(x);
        double dx = x - X[i];
        return primitive[i] + (dx * Y[i + 1]);
    }

    /// <inheritdoc/>
    protected override double DerivativeCore(double x) => 0.0;

    /// <inheritdoc/>
    protected override double SecondDerivativeCore(double x) => 0.0;
}
