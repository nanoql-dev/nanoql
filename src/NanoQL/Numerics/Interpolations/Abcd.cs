// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/abcdinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The abcd interpolation's factory and traits: an <see cref="AbcdInterpolation"/> fitted with its
/// parameters.</summary>
/// <remarks>
/// QuantLib's <c>Abcd</c> has no default constructor, so the zero value is refused by every member with
/// <see cref="InvalidOperationException"/>.
/// </remarks>
public readonly struct Abcd : IInterpolator
{
    private readonly double? a;
    private readonly double? b;
    private readonly double? c;
    private readonly double? d;
    private readonly bool aIsFixed;
    private readonly bool bIsFixed;
    private readonly bool cIsFixed;
    private readonly bool dIsFixed;
    private readonly bool vegaWeighted;
    private readonly EndCriteria? endCriteria;
    private readonly OptimizationMethod? optMethod;
    private readonly bool constructed;

    /// <summary>Initializes a new instance of the <see cref="Abcd"/> struct.</summary>
    /// <param name="a">a's guess, or <see langword="null"/> for the default, free.</param>
    /// <param name="b">b's guess, or <see langword="null"/> for the default, free.</param>
    /// <param name="c">c's guess, or <see langword="null"/> for the default, free.</param>
    /// <param name="d">d's guess, or <see langword="null"/> for the default, free.</param>
    /// <param name="aIsFixed">Whether a is fixed at its guess.</param>
    /// <param name="bIsFixed">Whether b is fixed at its guess.</param>
    /// <param name="cIsFixed">Whether c is fixed at its guess.</param>
    /// <param name="dIsFixed">Whether d is fixed at its guess.</param>
    /// <param name="vegaWeighted">Whether the errors are vega-weighted.</param>
    /// <param name="endCriteria">The end criteria, or <see langword="null"/> for QuantLib's default.</param>
    /// <param name="optMethod">The optimizer, or <see langword="null"/> for QuantLib's default.</param>
    public Abcd(
        double? a,
        double? b,
        double? c,
        double? d,
        bool aIsFixed,
        bool bIsFixed,
        bool cIsFixed,
        bool dIsFixed,
        bool vegaWeighted = false,
        EndCriteria? endCriteria = null,
        OptimizationMethod? optMethod = null)
    {
        this.a = a;
        this.b = b;
        this.c = c;
        this.d = d;
        this.aIsFixed = aIsFixed;
        this.bIsFixed = bIsFixed;
        this.cIsFixed = cIsFixed;
        this.dIsFixed = dIsFixed;
        this.vegaWeighted = vegaWeighted;
        this.endCriteria = endCriteria;
        this.optMethod = optMethod;
        constructed = true;
    }

    /// <summary>Gets a value indicating whether the interpolation is global: <see langword="true"/>.</summary>
    public static bool Global => true;

    /// <summary>Gets the fewest points the interpolation takes: 2.</summary>
    public static int RequiredPoints => 2;

    /// <summary>Builds and fits an <see cref="AbcdInterpolation"/> over the given points, with this factory's
    /// parameters.</summary>
    /// <param name="x">The times, sorted.</param>
    /// <param name="y">The volatilities, one per time.</param>
    /// <returns>The interpolation.</returns>
    /// <exception cref="InvalidOperationException">This is the zero value, which QuantLib can't construct.</exception>
    /// <exception cref="ArgumentException">The interpolation refuses the points or the guesses.</exception>
    /// <exception cref="NanoQLException">The fit fails.</exception>
    public Interpolation Interpolate(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
    {
        if (!constructed)
        {
            throw new InvalidOperationException("an Abcd factory has no default: build it with its parameters");
        }

        return new AbcdInterpolation(x, y, a, b, c, d, aIsFixed, bIsFixed, cIsFixed, dIsFixed, vegaWeighted, endCriteria, optMethod);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">This is the zero value, which QuantLib can't construct.</exception>
    public void CheckPoints(int points)
    {
        if (!constructed)
        {
            throw new InvalidOperationException("an Abcd factory has no default: build it with its parameters");
        }

        Interpolation.CheckRequiredPoints(points, RequiredPoints);
    }
}
