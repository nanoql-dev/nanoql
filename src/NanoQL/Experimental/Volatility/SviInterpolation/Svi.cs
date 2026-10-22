// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/sviinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>The SVI interpolation's factory and traits: an <see cref="SviInterpolation"/> fitted with its
/// parameters.</summary>
/// <remarks>
/// As QuantLib's, the factory's vega weighting defaults to off, where <see cref="SviInterpolation"/>'s defaults to
/// on. QuantLib's <c>Svi</c> has no default constructor, so the zero value is refused by every member with
/// <see cref="InvalidOperationException"/>.
/// </remarks>
[Experimental("NQL0001")]
public readonly struct Svi : IInterpolator
{
    private readonly double t;
    private readonly double forward;
    private readonly double? a;
    private readonly double? b;
    private readonly double? sigma;
    private readonly double? rho;
    private readonly double? m;
    private readonly bool aIsFixed;
    private readonly bool bIsFixed;
    private readonly bool sigmaIsFixed;
    private readonly bool rhoIsFixed;
    private readonly bool mIsFixed;
    private readonly bool vegaWeighted;
    private readonly EndCriteria? endCriteria;
    private readonly OptimizationMethod? optMethod;
    private readonly double errorAccept;
    private readonly bool useMaxError;
    private readonly int maxGuesses;
    private readonly bool constructed;

    /// <summary>Initializes a new instance of the <see cref="Svi"/> struct.</summary>
    /// <param name="t">The time to expiry.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="a">a's guess, or <see langword="null"/> for SVI's default.</param>
    /// <param name="b">b's guess, or <see langword="null"/> for 2/(1+|ρ|).</param>
    /// <param name="sigma">σ's guess, or <see langword="null"/> for 0.1.</param>
    /// <param name="rho">ρ's guess, or <see langword="null"/> for −0.4.</param>
    /// <param name="m">m's guess, or <see langword="null"/> for 0.</param>
    /// <param name="aIsFixed">Whether a is fixed at its guess.</param>
    /// <param name="bIsFixed">Whether b is fixed at its guess.</param>
    /// <param name="sigmaIsFixed">Whether σ is fixed at its guess.</param>
    /// <param name="rhoIsFixed">Whether ρ is fixed at its guess.</param>
    /// <param name="mIsFixed">Whether m is fixed at its guess.</param>
    /// <param name="vegaWeighted">Whether the errors are vega-weighted.</param>
    /// <param name="endCriteria">The end criteria, or <see langword="null"/> for QuantLib's default.</param>
    /// <param name="optMethod">The optimizer, or <see langword="null"/> for QuantLib's default.</param>
    /// <param name="errorAccept">The error at or below which a fit is accepted.</param>
    /// <param name="useMaxError">Whether a fit is scored by its maximum error.</param>
    /// <param name="maxGuesses">The most minimizations.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxGuesses"/> is negative.</exception>
    public Svi(
        double t,
        double forward,
        double? a,
        double? b,
        double? sigma,
        double? rho,
        double? m,
        bool aIsFixed,
        bool bIsFixed,
        bool sigmaIsFixed,
        bool rhoIsFixed,
        bool mIsFixed,
        bool vegaWeighted = false,
        EndCriteria? endCriteria = null,
        OptimizationMethod? optMethod = null,
        double errorAccept = 0.0020,
        bool useMaxError = false,
        int maxGuesses = 50)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxGuesses);
        this.t = t;
        this.forward = forward;
        this.a = a;
        this.b = b;
        this.sigma = sigma;
        this.rho = rho;
        this.m = m;
        this.aIsFixed = aIsFixed;
        this.bIsFixed = bIsFixed;
        this.sigmaIsFixed = sigmaIsFixed;
        this.rhoIsFixed = rhoIsFixed;
        this.mIsFixed = mIsFixed;
        this.vegaWeighted = vegaWeighted;
        this.endCriteria = endCriteria;
        this.optMethod = optMethod;
        this.errorAccept = errorAccept;
        this.useMaxError = useMaxError;
        this.maxGuesses = maxGuesses;
        constructed = true;
    }

    /// <summary>Gets a value indicating whether the interpolation is global: <see langword="true"/>.</summary>
    public static bool Global => true;

    /// <summary>Gets the fewest points the interpolation takes: 1, XABR's.</summary>
    public static int RequiredPoints => 1;

    /// <summary>Builds and fits an <see cref="SviInterpolation"/> over the given points, with this factory's
    /// parameters.</summary>
    /// <param name="x">The strikes, sorted.</param>
    /// <param name="y">The volatilities, one per strike.</param>
    /// <returns>The interpolation.</returns>
    /// <exception cref="InvalidOperationException">This is the zero value, which QuantLib can't construct.</exception>
    /// <exception cref="ArgumentException">The interpolation refuses the points or the parameters.</exception>
    /// <exception cref="NanoQLException">The optimizer fails.</exception>
    public Interpolation Interpolate(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
    {
        if (!constructed)
        {
            throw new InvalidOperationException("an Svi factory has no default: build it with its parameters");
        }

        return new SviInterpolation(
            x,
            y,
            t,
            forward,
            a,
            b,
            sigma,
            rho,
            m,
            aIsFixed,
            bIsFixed,
            sigmaIsFixed,
            rhoIsFixed,
            mIsFixed,
            vegaWeighted,
            endCriteria,
            optMethod,
            errorAccept,
            useMaxError,
            maxGuesses);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">This is the zero value, which QuantLib can't construct.</exception>
    public void CheckPoints(int points)
    {
        if (!constructed)
        {
            throw new InvalidOperationException("an Svi factory has no default: build it with its parameters");
        }

        Interpolation.CheckRequiredPoints(points, RequiredPoints);
    }
}
