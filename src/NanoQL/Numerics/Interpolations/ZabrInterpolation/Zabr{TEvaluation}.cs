// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/zabrinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;

namespace NanoQL.Numerics;

/// <summary>
/// The ZABR interpolation's factory and traits: a <see cref="ZabrInterpolation{TEvaluation}"/> fitted with its
/// parameters.
/// </summary>
/// <typeparam name="TEvaluation">The kernel of the interpolation's smile section.</typeparam>
/// <remarks>
/// As QuantLib's, the factory's vega weighting defaults to off, where <see cref="ZabrInterpolation{TEvaluation}"/>'s
/// defaults to on. QuantLib's <c>Zabr</c> has no default constructor, so the zero value is refused by every member with
/// <see cref="InvalidOperationException"/>.
/// </remarks>
public readonly struct Zabr<TEvaluation> : IInterpolator
    where TEvaluation : struct, IZabrEvaluation
{
    private readonly double t;
    private readonly double forward;
    private readonly double? alpha;
    private readonly double? beta;
    private readonly double? nu;
    private readonly double? rho;
    private readonly double? gamma;
    private readonly bool alphaIsFixed;
    private readonly bool betaIsFixed;
    private readonly bool nuIsFixed;
    private readonly bool rhoIsFixed;
    private readonly bool gammaIsFixed;
    private readonly bool vegaWeighted;
    private readonly EndCriteria? endCriteria;
    private readonly OptimizationMethod? optMethod;
    private readonly double errorAccept;
    private readonly bool useMaxError;
    private readonly int maxGuesses;
    private readonly bool constructed;

    /// <summary>Initializes a new instance of the <see cref="Zabr{TEvaluation}"/> struct.</summary>
    /// <param name="t">The time to expiry.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="alpha">α's guess, or <see langword="null"/> for ZABR's default.</param>
    /// <param name="beta">β's guess, or <see langword="null"/> for 0.5.</param>
    /// <param name="nu">ν's guess, or <see langword="null"/> for √0.4.</param>
    /// <param name="rho">ρ's guess, or <see langword="null"/> for 0.</param>
    /// <param name="gamma">γ's guess, or <see langword="null"/> for 1.</param>
    /// <param name="alphaIsFixed">Whether α is fixed at its guess.</param>
    /// <param name="betaIsFixed">Whether β is fixed at its guess.</param>
    /// <param name="nuIsFixed">Whether ν is fixed at its guess.</param>
    /// <param name="rhoIsFixed">Whether ρ is fixed at its guess.</param>
    /// <param name="gammaIsFixed">Whether γ is fixed at its guess.</param>
    /// <param name="vegaWeighted">Whether the errors are vega-weighted.</param>
    /// <param name="endCriteria">The end criteria, or <see langword="null"/> for QuantLib's default.</param>
    /// <param name="optMethod">The optimizer, or <see langword="null"/> for QuantLib's default.</param>
    /// <param name="errorAccept">The error at or below which a fit is accepted.</param>
    /// <param name="useMaxError">Whether a fit is scored by its maximum error.</param>
    /// <param name="maxGuesses">The most minimizations.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxGuesses"/> is negative.</exception>
    public Zabr(
        double t,
        double forward,
        double? alpha,
        double? beta,
        double? nu,
        double? rho,
        double? gamma,
        bool alphaIsFixed,
        bool betaIsFixed,
        bool nuIsFixed,
        bool rhoIsFixed,
        bool gammaIsFixed,
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
        this.alpha = alpha;
        this.beta = beta;
        this.nu = nu;
        this.rho = rho;
        this.gamma = gamma;
        this.alphaIsFixed = alphaIsFixed;
        this.betaIsFixed = betaIsFixed;
        this.nuIsFixed = nuIsFixed;
        this.rhoIsFixed = rhoIsFixed;
        this.gammaIsFixed = gammaIsFixed;
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

    /// <summary>Builds and fits a <see cref="ZabrInterpolation{TEvaluation}"/> over the given points, with this
    /// factory's parameters.</summary>
    /// <param name="x">The strikes, sorted.</param>
    /// <param name="y">The volatilities, one per strike.</param>
    /// <returns>The interpolation.</returns>
    /// <exception cref="InvalidOperationException">This is the zero value, which QuantLib can't construct.</exception>
    /// <exception cref="ArgumentException">The interpolation refuses the points or the parameters.</exception>
    /// <exception cref="NanoQLException">The optimizer or the model's integration fails.</exception>
    public Interpolation Interpolate(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
    {
        if (!constructed)
        {
            throw new InvalidOperationException("a Zabr factory has no default: build it with its parameters");
        }

        return new ZabrInterpolation<TEvaluation>(
            x,
            y,
            t,
            forward,
            alpha,
            beta,
            nu,
            rho,
            gamma,
            alphaIsFixed,
            betaIsFixed,
            nuIsFixed,
            rhoIsFixed,
            gammaIsFixed,
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
            throw new InvalidOperationException("a Zabr factory has no default: build it with its parameters");
        }

        Interpolation.CheckRequiredPoints(points, RequiredPoints);
    }
}
