// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/zabrinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.TermStructures;

namespace NanoQL.Numerics;

/// <summary>
/// The ZABR smile fitted to discrete volatilities: ZABR's five parameters fitted to the lognormal volatilities at the
/// strikes, interpolated by the volatility of the ZABR smile section at the fitted parameters.
/// </summary>
/// <typeparam name="TEvaluation">The section's kernel: <see cref="ZabrShortMaturityLognormal"/> or
/// <see cref="ZabrShortMaturityNormal"/>.</typeparam>
/// <remarks>
/// <para>
/// The fit is <see cref="SABRInterpolation"/>'s, XABR's, over ZABR's parameters: it runs when the interpolation is
/// built, from the guesses and then, while the error stays above <c>errorAccept</c>, from up to <c>maxGuesses</c> − 1
/// Halton guesses seeded with QuantLib's 42, keeping the best; its values are QuantLib's at every step. ν is fitted in
/// [0, 5] and γ in [0, 1.9], the ranges QuantLib's transform maps them into.
/// </para>
/// <para>
/// The model the interpolation prices with is a section built from a copy of the parameters, as QuantLib's is. QuantLib
/// doesn't rebuild it after writing the best restart's parameters back, so where the restarts run out without an
/// accepted fit, or the last trial wasn't the best one, its values and errors are its last trial's while its parameters
/// are the best restart's. Here the section is rebuilt from the best parameters, so the values, the errors and the
/// parameters agree. A fixed or guessed ν above 5, or γ above 1.9, is taken as 5 or 1.9, where QuantLib's transform
/// wraps it to another value inside the range; ρ and α as <see cref="SABRInterpolation"/> takes them.
/// </para>
/// <para>Immutable and thread-safe once built: it copies its inputs, and nothing writes the fit after it.</para>
/// </remarks>
public sealed class ZabrInterpolation<TEvaluation> : Interpolation, IXabrInterpolation
    where TEvaluation : struct, IZabrEvaluation
{
    private readonly XabrFit<ZabrSpecs<TEvaluation>, ZabrSmileSection<TEvaluation>> fit;

    /// <summary>Initializes a new instance of the <see cref="ZabrInterpolation{TEvaluation}"/> class, and fits it.</summary>
    /// <param name="x">The strikes, sorted.</param>
    /// <param name="y">The lognormal volatilities, one per strike.</param>
    /// <param name="t">The time to expiry, positive.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="alpha">α's guess, or <see langword="null"/> for the default, 0.2·F<sup>1−β</sup>.</param>
    /// <param name="beta">β's guess, or <see langword="null"/> for 0.5.</param>
    /// <param name="nu">ν's guess, or <see langword="null"/> for √0.4.</param>
    /// <param name="rho">ρ's guess, or <see langword="null"/> for 0.</param>
    /// <param name="gamma">γ's guess, or <see langword="null"/> for 1.</param>
    /// <param name="alphaIsFixed">Whether α is fixed at its guess; a parameter not given is free.</param>
    /// <param name="betaIsFixed">Whether β is fixed at its guess.</param>
    /// <param name="nuIsFixed">Whether ν is fixed at its guess.</param>
    /// <param name="rhoIsFixed">Whether ρ is fixed at its guess.</param>
    /// <param name="gammaIsFixed">Whether γ is fixed at its guess.</param>
    /// <param name="vegaWeighted">Whether the errors are weighted by the strikes' Black vegas.</param>
    /// <param name="endCriteria">The end criteria, or <see langword="null"/> for QuantLib's
    /// <c>EndCriteria(60000, 100, 1e-8, 1e-8, 1e-8)</c>.</param>
    /// <param name="optMethod">The optimizer, or <see langword="null"/> for QuantLib's
    /// <c>LevenbergMarquardt(1e-8, 1e-8, 1e-8)</c>.</param>
    /// <param name="errorAccept">The error at or below which a fit is accepted and the restarts stop.</param>
    /// <param name="useMaxError">Whether a fit is scored by its maximum error, not its root-mean-square error.</param>
    /// <param name="maxGuesses">The most minimizations; 0 runs one, as 1 does.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxGuesses"/> is negative.</exception>
    /// <exception cref="ArgumentException">The sizes differ, there is no strike, the time isn't positive, the model
    /// refuses a guess or the forward, the volatilities are all zero with vega weights, or the optimizer refuses the
    /// problem.</exception>
    /// <exception cref="NanoQLException">The optimizer or the model's integration fails.</exception>
    public ZabrInterpolation(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
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
        bool vegaWeighted = true,
        EndCriteria? endCriteria = null,
        OptimizationMethod? optMethod = null,
        double errorAccept = 0.0020,
        bool useMaxError = false,
        int maxGuesses = 50)
        : base(x, y, 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxGuesses);
        fit = new XabrFit<ZabrSpecs<TEvaluation>, ZabrSmileSection<TEvaluation>>(
            X,
            Y,
            t,
            forward,
            [alpha, beta, nu, rho, gamma],
            [alphaIsFixed, betaIsFixed, nuIsFixed, rhoIsFixed, gammaIsFixed],
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

    /// <inheritdoc/>
    public double Alpha => fit.Parameter(0);

    /// <inheritdoc/>
    public double Beta => fit.Parameter(1);

    /// <inheritdoc/>
    public double Nu => fit.Parameter(2);

    /// <inheritdoc/>
    public double Rho => fit.Parameter(3);

    /// <summary>Gets the fitted γ.</summary>
    public double Gamma => fit.Parameter(4);

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
