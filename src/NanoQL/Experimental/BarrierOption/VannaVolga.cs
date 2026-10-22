// SPDX-FileCopyrightText: Copyright (C) 2013 Yue Tian
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/barrieroption/vannavolgainterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>The Vanna-Volga interpolation's factory and traits: a <see cref="VannaVolgaInterpolation"/> on a
/// market.</summary>
/// <remarks>
/// QuantLib's <c>VannaVolga</c> has no default constructor, so the zero value is refused by every member with
/// <see cref="InvalidOperationException"/>. QuantLib's traits declare no
/// <c>global</c>; it is <see langword="true"/> here, as every value reads all three points.
/// </remarks>
[Experimental("NQL0001")]
public readonly struct VannaVolga : IInterpolator
{
    private readonly double spot;
    private readonly double dDiscount;
    private readonly double fDiscount;
    private readonly double t;
    private readonly bool constructed;

    /// <summary>Initializes a new instance of the <see cref="VannaVolga"/> struct.</summary>
    /// <param name="spot">The spot.</param>
    /// <param name="dDiscount">The domestic discount factor.</param>
    /// <param name="fDiscount">The foreign discount factor.</param>
    /// <param name="t">The time to maturity.</param>
    public VannaVolga(double spot, double dDiscount, double fDiscount, double t)
    {
        this.spot = spot;
        this.dDiscount = dDiscount;
        this.fDiscount = fDiscount;
        this.t = t;
        constructed = true;
    }

    /// <summary>Gets a value indicating whether the interpolation is global: <see langword="true"/>.</summary>
    public static bool Global => true;

    /// <summary>Gets the fewest points the interpolation takes: 3.</summary>
    public static int RequiredPoints => 3;

    /// <summary>Builds a <see cref="VannaVolgaInterpolation"/> over the given points, which it copies.</summary>
    /// <param name="x">The three strikes, sorted.</param>
    /// <param name="y">The three volatilities, the second at the money.</param>
    /// <returns>The interpolation.</returns>
    /// <exception cref="InvalidOperationException">This is the zero value, which QuantLib can't construct.</exception>
    /// <exception cref="ArgumentException">The sizes differ, or there aren't exactly three points.</exception>
    public Interpolation Interpolate(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
    {
        if (!constructed)
        {
            throw new InvalidOperationException("a VannaVolga factory has no default: build it with its market");
        }

        return new VannaVolgaInterpolation(x, y, spot, dDiscount, fDiscount, t);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">This is the zero value, which QuantLib can't construct.</exception>
    public void CheckPoints(int points)
    {
        if (!constructed)
        {
            throw new InvalidOperationException("a VannaVolga factory has no default: build it with its market");
        }

        Interpolation.CheckRequiredPoints(points, RequiredPoints);
    }
}
