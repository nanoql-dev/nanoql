// SPDX-FileCopyrightText: Copyright (C) 2005, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/forwardflatinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Forward-flat interpolation between discrete points: on each interval [x<sub>i</sub>, x<sub>i+1</sub>) the value is
/// y<sub>i</sub>, and from the last x on it is the last y.
/// </summary>
/// <remarks>
/// <para>It copies its inputs, as every <see cref="Interpolation"/> does.</para>
/// <para>
/// Past the last node the primitive grows at the value there, y<sub>n-1</sub>, from its value at the last node, as the
/// interpolation's own value says. QuantLib's adds <c>(x - x[n-2]) y[n-2]</c> to the primitive at the
/// second-to-last node, so its integral past the last node disagrees with its value there
/// (<c>forwardflatinterpolation.hpp:93-97</c>), a documented divergence; at and before the last node it is QuantLib's
/// expression.
/// </para>
/// </remarks>
public sealed class ForwardFlatInterpolation : Interpolation
{
    private readonly double[] primitive;

    /// <summary>Initializes a new instance of the <see cref="ForwardFlatInterpolation"/> class.</summary>
    /// <param name="x">The x values, which must be sorted; at least 2.</param>
    /// <param name="y">The y values, one per x value.</param>
    /// <exception cref="ArgumentException">The sizes differ, or there are fewer than two points.</exception>
    public ForwardFlatInterpolation(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
        : base(x, y, ForwardFlat.RequiredPoints)
    {
        ReadOnlySpan<double> xs = X, ys = Y;
        int n = xs.Length;
        primitive = new double[n];

        // QuantLib's update().
        primitive[0] = 0.0;
        for (int i = 1; i < n; i++)
        {
            double dx = xs[i] - xs[i - 1];
            primitive[i] = primitive[i - 1] + (dx * ys[i - 1]);
        }
    }

    /// <inheritdoc/>
    protected override double ValueCore(double x)
    {
        int n = X.Length;
        if (x >= X[n - 1])
        {
            return Y[n - 1];
        }

        int i = Locate(x);
        return Y[i];
    }

    /// <inheritdoc/>
    protected override double PrimitiveCore(double x)
    {
        int n = X.Length;
        if (x > X[n - 1])
        {
            // past the last node, at the value there; QuantLib's continues the second-to-last segment
            return primitive[n - 1] + ((x - X[n - 1]) * Y[n - 1]);
        }

        int i = Locate(x);
        double dx = x - X[i];
        return primitive[i] + (dx * Y[i]);
    }

    /// <inheritdoc/>
    protected override double DerivativeCore(double x) => 0.0;

    /// <inheritdoc/>
    protected override double SecondDerivativeCore(double x) => 0.0;
}
