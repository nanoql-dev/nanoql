// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Bernd Engelmann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/inflation/polynomial2Dspline.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A two-dimensional interpolation, parabolic in the y direction and a spline in the x direction: QuantLib's
/// <c>Polynomial2DSpline</c>.
/// </summary>
/// <remarks>
/// Each column of the data, one per x value, is a parabola in y, read with extrapolation; at a point, the parabolas'
/// values at y are interpolated in x by QuantLib's monotonic natural cubic spline (second derivatives 0 at both ends),
/// with extrapolation.
/// </remarks>
[Experimental("NQL0001")]
public sealed class Polynomial2DSpline : Interpolation2D
{
    private readonly ImmutableArray<Parabolic> polynomials;

    /// <summary>Initializes a new instance of the <see cref="Polynomial2DSpline"/> class.</summary>
    /// <param name="x">The x values, which must be sorted; at least 2.</param>
    /// <param name="y">The y values, which must be sorted; at least 2.</param>
    /// <param name="zData">The function values, a row per y value and a column per x value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="zData"/> is null.</exception>
    /// <exception cref="ArgumentException">There are fewer than 2 x or y values, or the matrix's rows aren't one per y
    /// value or its columns one per x value ("size mismatch of the interpolation data").</exception>
    public Polynomial2DSpline(ReadOnlySpan<double> x, ReadOnlySpan<double> y, Matrix zData)
        : base(x, y, zData)
    {
        if (zData.Rows != y.Length)
        {
            throw new ArgumentException("size mismatch of the interpolation data", nameof(zData));
        }

        // QuantLib checks the columns against the x values at each read; here at construction.
        if (zData.Columns != x.Length)
        {
            throw new ArgumentException("size mismatch of the interpolation data", nameof(zData));
        }

        var built = ImmutableArray.CreateBuilder<Parabolic>(zData.Columns);
        double[] column = new double[zData.Rows];
        for (int i = 0; i < zData.Columns; ++i)
        {
            for (int k = 0; k < zData.Rows; ++k)
            {
                column[k] = zData[k, i];
            }

            built.Add(new Parabolic(Y, column));
        }

        polynomials = built.MoveToImmutable();
    }

    /// <inheritdoc/>
    protected override double ValueCore(double x, double y)
    {
        double[] section = new double[polynomials.Length];
        for (int i = 0; i < polynomials.Length; ++i)
        {
            section[i] = polynomials[i].Evaluate(y, true);
        }

        return new MonotonicCubicNaturalSpline(X, section).Evaluate(x, true);
    }
}
