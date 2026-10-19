// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/sabrinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.TermStructures;

namespace NanoQL.Numerics;

/// <summary>
/// The SABR smile fitted to discrete volatilities: SABR's four parameters fitted to the volatilities at the strikes,
/// interpolated by the SABR volatility at the fitted parameters.
/// </summary>
/// <remarks>
/// <para>
/// The fit runs when the interpolation is built: QuantLib's construction followed by its <c>update()</c>, which every
/// QuantLib user calls at once. A new fit is a new interpolation, so <c>update()</c> isn't ported. The fit minimizes
/// the (vega-weighted, by default) squared errors from the given guesses, then, while the error stays above
/// <c>errorAccept</c>, from up to <c>maxGuesses</c> − 1 more guesses drawn from a Halton sequence seeded with
/// QuantLib's 42, keeping the best; its results are QuantLib's at every step.
/// </para>
/// <para>
/// As QuantLib's, a restart is kept by the error the cost function's last evaluation gives: the result's under
/// <see cref="LevenbergMarquardt"/>, the default, which evaluates its result last, but a later trial's under
/// <see cref="Simplex"/>, which returns its best vertex without evaluating it again. Fixed parameters go through the
/// parameter transforms at every evaluation, as QuantLib's do, so a fixed β of 0.5 is reported as
/// 0.5000000000000001. A fixed or guessed ρ beyond ±0.9999, or α or ν below 1e-7, is taken as the nearest value the
/// transform reaches, where QuantLib's transform flips ρ's sign or fails; and a smile of zero volatilities with vega
/// weights is refused, where QuantLib fits on NaN weights.
/// </para>
/// <para>Immutable and thread-safe once built: it copies its inputs, and nothing writes the fit after it.</para>
/// </remarks>
public sealed class SABRInterpolation : Interpolation, IXabrInterpolation
{
    private readonly XabrFit<SABRSpecs, SABRWrapper> fit;

    /// <summary>Initializes a new instance of the <see cref="SABRInterpolation"/> class, and fits it.</summary>
    /// <param name="x">The strikes, sorted.</param>
    /// <param name="y">The volatilities, one per strike.</param>
    /// <param name="t">The time to expiry, positive.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="alpha">α's guess, or <see langword="null"/> for SABR's default, 0.2·(F + shift)<sup>1−β</sup>.</param>
    /// <param name="beta">β's guess, or <see langword="null"/> for 0.5.</param>
    /// <param name="nu">ν's guess, or <see langword="null"/> for √0.4.</param>
    /// <param name="rho">ρ's guess, or <see langword="null"/> for 0.</param>
    /// <param name="alphaIsFixed">Whether α is fixed at its guess; a parameter not given is free.</param>
    /// <param name="betaIsFixed">Whether β is fixed at its guess.</param>
    /// <param name="nuIsFixed">Whether ν is fixed at its guess.</param>
    /// <param name="rhoIsFixed">Whether ρ is fixed at its guess.</param>
    /// <param name="vegaWeighted">Whether the errors are weighted by the strikes' Black vegas.</param>
    /// <param name="endCriteria">The end criteria, or <see langword="null"/> for QuantLib's
    /// <c>EndCriteria(60000, 100, 1e-8, 1e-8, 1e-8)</c>.</param>
    /// <param name="optMethod">The optimizer, or <see langword="null"/> for QuantLib's
    /// <c>LevenbergMarquardt(1e-8, 1e-8, 1e-8)</c>.</param>
    /// <param name="errorAccept">The error at or below which a fit is accepted and the restarts stop.</param>
    /// <param name="useMaxError">Whether a fit is scored by its maximum error, not its root-mean-square error.</param>
    /// <param name="maxGuesses">The most minimizations; 0 runs one, as 1 does.</param>
    /// <param name="shift">The shift of a shifted SABR model.</param>
    /// <param name="volatilityType">The volatilities' type: shifted lognormal or normal.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxGuesses"/> is negative, or
    /// <paramref name="volatilityType"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">The sizes differ, there is no strike, the time isn't positive, the forward
    /// plus the shift isn't positive, a guess is out of its range, a strike plus the shift isn't positive, the
    /// volatilities are all zero with vega weights, or the optimizer refuses the problem.</exception>
    /// <exception cref="NanoQLException">The optimizer fails.</exception>
    public SABRInterpolation(
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
        double shift = 0.0,
        VolatilityType volatilityType = VolatilityType.ShiftedLognormal)
        : base(x, y, 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxGuesses);
        EnumArguments.Check(volatilityType, nameof(volatilityType));
        fit = new XabrFit<SABRSpecs, SABRWrapper>(
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
            [shift],
            volatilityType);
    }

    /// <summary>Gets the time to expiry.</summary>
    public double Expiry => fit.T;

    /// <summary>Gets the forward.</summary>
    public double Forward => fit.Forward;

    /// <inheritdoc/>
    public double Alpha => fit.Parameter(0);

    /// <inheritdoc/>
    public double Beta => fit.Parameter(1);

    /// <inheritdoc/>
    public double Nu => fit.Parameter(2);

    /// <inheritdoc/>
    public double Rho => fit.Parameter(3);

    /// <inheritdoc/>
    public double RmsError => fit.Error;

    /// <inheritdoc/>
    public double MaxError => fit.MaxError;

    /// <inheritdoc/>
    public EndCriteriaType EndCriteria => fit.XabrEndCriteria;

    /// <inheritdoc/>
    public ImmutableArray<double> InterpolationWeights() => fit.Weights;

    /// <inheritdoc/>
    protected override double ValueCore(double x) => fit.Value(x);

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always: an XABR interpolation has no primitive.</exception>
    protected override double PrimitiveCore(double x) => throw new NanoQLException("XABR primitive not implemented");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always: an XABR interpolation has no derivative.</exception>
    protected override double DerivativeCore(double x) => throw new NanoQLException("XABR derivative not implemented");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always: an XABR interpolation has no second derivative.</exception>
    protected override double SecondDerivativeCore(double x) =>
        throw new NanoQLException("XABR secondDerivative not implemented");
}
