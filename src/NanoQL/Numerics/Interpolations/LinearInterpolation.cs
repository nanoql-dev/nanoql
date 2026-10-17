// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/linearinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>Linear interpolation between discrete points.</summary>
/// <remarks>It copies its inputs, as every <see cref="Interpolation"/> does.</remarks>
public sealed class LinearInterpolation : Interpolation
{
    private readonly double[] primitiveConst;
    private readonly double[] s;

    /// <summary>Initializes a new instance of the <see cref="LinearInterpolation"/> class.</summary>
    /// <param name="x">The x values, which must be sorted; at least 2.</param>
    /// <param name="y">The y values, one per x value.</param>
    /// <exception cref="ArgumentException">The sizes differ, or there are fewer than 2 points.</exception>
    public LinearInterpolation(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
        : base(x, y, Linear.RequiredPoints)
    {
        ReadOnlySpan<double> xs = X, ys = Y;
        int n = xs.Length;
        primitiveConst = new double[n];
        s = new double[n];

        // QuantLib's update().
        primitiveConst[0] = 0.0;
        for (int i = 1; i < n; ++i)
        {
            double dx = xs[i] - xs[i - 1];
            s[i - 1] = (ys[i] - ys[i - 1]) / dx;
            primitiveConst[i] = primitiveConst[i - 1] + (dx * (ys[i - 1] + (0.5 * dx * s[i - 1])));
        }
    }

    /// <inheritdoc/>
    protected override double ValueCore(double x)
    {
        int i = Locate(x);
        return Y[i] + ((x - X[i]) * s[i]);
    }

    /// <inheritdoc/>
    protected override double PrimitiveCore(double x)
    {
        int i = Locate(x);
        double dx = x - X[i];
        return primitiveConst[i] + (dx * (Y[i] + (0.5 * dx * s[i])));
    }

    /// <inheritdoc/>
    protected override double DerivativeCore(double x)
    {
        int i = Locate(x);
        return s[i];
    }

    /// <inheritdoc/>
    protected override double SecondDerivativeCore(double x) => 0.0;
}
