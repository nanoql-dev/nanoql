// SPDX-FileCopyrightText: Copyright (C) 2010 SunTrust Bank
// SPDX-FileCopyrightText: Copyright (C) 2010, 2014 Cavit Hafizoglu
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/shortrate/generalizedhullwhite.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// Linear interpolation between discrete points with flat extrapolation: QuantLib's <c>LinearFlatInterpolation</c>.
/// </summary>
/// <remarks>
/// It copies its inputs, as every <see cref="Interpolation"/> does. Outside its range the value is the nearest end's
/// and the derivative 0, as QuantLib's. The primitive there is the flat value's integral, where QuantLib's extends the
/// end intervals' lines past the ends, so that its slope outside the range isn't the value (the S-xrt run's R3-1;
/// <c>Divergences/LinearFlatPrimitiveTests.cs</c>); on a single point, where QuantLib's primitive and derivative read
/// before their arrays, they are the flat value's.
/// </remarks>
[Experimental("NQL0001")]
public sealed class LinearFlatInterpolation : Interpolation
{
    private readonly double[] primitiveConst;
    private readonly double[] s;

    /// <summary>Initializes a new instance of the <see cref="LinearFlatInterpolation"/> class.</summary>
    /// <param name="x">The x values, which must be sorted; at least 1.</param>
    /// <param name="y">The y values, one per x value.</param>
    /// <exception cref="ArgumentException">The sizes differ, or there are no points.</exception>
    public LinearFlatInterpolation(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
        : base(x, y, LinearFlat.RequiredPoints)
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
        if (x <= XMin)
        {
            return Y[0];
        }

        if (x >= XMax)
        {
            return Y[^1];
        }

        int i = Locate(x);
        return Y[i] + ((x - X[i]) * s[i]);
    }

    /// <inheritdoc/>
    /// <remarks>The flat value's integral outside the range (R3-1).</remarks>
    protected override double PrimitiveCore(double x)
    {
        if (x < XMin || X.Length == 1)
        {
            return (x - XMin) * Y[0];
        }

        if (x > XMax)
        {
            return primitiveConst[^1] + ((x - XMax) * Y[^1]);
        }

        int i = Locate(x);
        double dx = x - X[i];
        return primitiveConst[i] + (dx * (Y[i] + (0.5 * dx * s[i])));
    }

    /// <inheritdoc/>
    protected override double DerivativeCore(double x)
    {
        if (!IsInRange(x) || X.Length == 1)
        {
            return 0.0;
        }

        int i = Locate(x);
        return s[i];
    }

    /// <inheritdoc/>
    protected override double SecondDerivativeCore(double x) => 0.0;
}
