// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/noarbsabrinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// The no-arbitrage SABR smile fitted to discrete volatilities: α, β, ν and ρ fitted to the lognormal volatilities at
/// the strikes, interpolated by the volatility of the no-arbitrage SABR section at the fitted parameters.
/// </summary>
/// <remarks>
/// The fit is <see cref="SABRInterpolation"/>'s, XABR's, over the no-arbitrage SABR's parameters mapped onto the model's
/// domain: it runs when the interpolation is built, from the guesses and then, while the error stays above
/// <c>errorAccept</c>, from up to <c>maxGuesses</c> − 1 Halton guesses seeded with QuantLib's 42, keeping the best.
/// Immutable and thread-safe once built.
/// </remarks>
[Experimental("NQL0001")]
public sealed class NoArbSabrInterpolation : Interpolation, IXabrInterpolation
{
    private readonly XabrFit<NoArbSabrSpecs, NoArbSabrWrapper> fit;

    /// <summary>Initializes a new instance of the <see cref="NoArbSabrInterpolation"/> class, and fits it.</summary>
    /// <param name="x">The strikes, sorted.</param>
    /// <param name="y">The lognormal volatilities, one per strike.</param>
    /// <param name="t">The time to expiry.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="alpha">α's guess, or <see langword="null"/> for SABR's default moved into the model's domain.</param>
    /// <param name="beta">β's guess, or <see langword="null"/> for 0.5.</param>
    /// <param name="nu">ν's guess, or <see langword="null"/> for √0.4.</param>
    /// <param name="rho">ρ's guess, or <see langword="null"/> for 0.</param>
    /// <param name="alphaIsFixed">Whether α is fixed at its guess; a parameter not given is free.</param>
    /// <param name="betaIsFixed">Whether β is fixed at its guess.</param>
    /// <param name="nuIsFixed">Whether ν is fixed at its guess.</param>
    /// <param name="rhoIsFixed">Whether ρ is fixed at its guess.</param>
    /// <param name="vegaWeighted">Whether the errors are weighted by the strikes' Black vegas.</param>
    /// <param name="endCriteria">The end criteria, or <see langword="null"/> for the fit's default.</param>
    /// <param name="optMethod">The optimizer, or <see langword="null"/> for the fit's default.</param>
    /// <param name="errorAccept">The error at or below which a fit is accepted and the restarts stop.</param>
    /// <param name="useMaxError">Whether a fit is scored by its maximum error, not its root-mean-square error.</param>
    /// <param name="maxGuesses">The most minimizations; 0 runs one, as 1 does.</param>
    /// <param name="shift">The shift, which must be 0.</param>
    /// <exception cref="ArgumentException">The shift isn't 0 ("NoArbSabrInterpolation for non zero shift not
    /// implemented"), or the points or the parameters are refused.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxGuesses"/> is negative.</exception>
    /// <exception cref="NanoQLException">The optimizer or the model fails.</exception>
    public NoArbSabrInterpolation(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        double t,
        double forward,
        double? alpha,
        double? beta,
        double? nu,
        double? rho,
        bool alphaIsFixed,
        bool betaIsFixed,
        bool nuIsFixed,
        bool rhoIsFixed,
        bool vegaWeighted = true,
        EndCriteria? endCriteria = null,
        OptimizationMethod? optMethod = null,
        double errorAccept = 0.0020,
        bool useMaxError = false,
        int maxGuesses = 50,
        double shift = 0.0)
        : base(x, y, 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxGuesses);
        if (!(shift == 0.0))
        {
            throw new ArgumentException("NoArbSabrInterpolation for non zero shift not implemented", nameof(shift));
        }

        fit = new XabrFit<NoArbSabrSpecs, NoArbSabrWrapper>(
            X,
            Y,
            t,
            forward,
            [alpha, beta, nu, rho],
            [alphaIsFixed, betaIsFixed, nuIsFixed, rhoIsFixed],
            vegaWeighted,
            endCriteria,
            optMethod,
            errorAccept,
            useMaxError,
            maxGuesses,
            [],
            VolatilityType.ShiftedLognormal);
    }

    /// <summary>Gets the time to expiry.</summary>
    public double Expiry => fit.T;

    /// <summary>Gets the forward.</summary>
    public double Forward => fit.Forward;

    /// <summary>Gets the fitted α.</summary>
    public double Alpha => fit.Parameter(0);

    /// <summary>Gets the fitted β.</summary>
    public double Beta => fit.Parameter(1);

    /// <summary>Gets the fitted ν.</summary>
    public double Nu => fit.Parameter(2);

    /// <summary>Gets the fitted ρ.</summary>
    public double Rho => fit.Parameter(3);

    /// <summary>Gets the fit's root-mean-square error.</summary>
    public double RmsError => fit.Error;

    /// <summary>Gets the fit's largest error.</summary>
    public double MaxError => fit.MaxError;

    /// <summary>Gets the end criterion the fit met.</summary>
    public EndCriteriaType EndCriteria => fit.XabrEndCriteria;

    /// <summary>The fit's weights, one per strike.</summary>
    /// <returns>The weights.</returns>
    public ImmutableArray<double> InterpolationWeights() => fit.Weights;

    /// <inheritdoc/>
    protected override double ValueCore(double x) => fit.Value(x);

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always: QuantLib's XABR interpolation has no primitive.</exception>
    protected override double PrimitiveCore(double x) => throw new NanoQLException("XABR primitive not implemented");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always: QuantLib's XABR interpolation has no derivative.</exception>
    protected override double DerivativeCore(double x) => throw new NanoQLException("XABR derivative not implemented");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always: QuantLib's XABR interpolation has no second derivative.</exception>
    protected override double SecondDerivativeCore(double x) =>
        throw new NanoQLException("XABR secondDerivative not implemented");
}
