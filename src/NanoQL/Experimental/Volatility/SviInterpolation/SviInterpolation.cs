// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/sviinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// The SVI smile fitted to discrete volatilities: SVI's five parameters a, b, σ, ρ and m fitted to the lognormal
/// volatilities at the strikes, interpolated by the volatility of the SVI smile section at the fitted parameters.
/// </summary>
/// <remarks>
/// <para>
/// The fit is <see cref="SABRInterpolation"/>'s, XABR's, over SVI's parameters: it runs when the interpolation is
/// built, from the guesses and then, while the error stays above <c>errorAccept</c>, from up to <c>maxGuesses</c> − 1
/// Halton guesses seeded with QuantLib's 42, keeping the best; its values are QuantLib's at every step but where
/// <see cref="SviSpecs"/>' transforms depart from QuantLib's (a guess at an edge of the transforms' ranges, and a
/// trial the parameters' check would refuse with a or b fixed).
/// </para>
/// <para>
/// As an <see cref="IXabrInterpolation"/>, its first four parameters, a, b, σ and ρ, are the slots QuantLib's
/// coefficient holder keeps in α's, β's, ν's and ρ's places.
/// </para>
/// <para>Immutable and thread-safe once built: it copies its inputs, and nothing writes the fit after it.</para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class SviInterpolation : Interpolation, IXabrInterpolation
{
    private readonly XabrFit<SviSpecs, SviWrapper> fit;

    /// <summary>Initializes a new instance of the <see cref="SviInterpolation"/> class, and fits it.</summary>
    /// <param name="x">The strikes, sorted.</param>
    /// <param name="y">The lognormal volatilities, one per strike.</param>
    /// <param name="t">The time to expiry, positive.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="a">a's guess, or <see langword="null"/> for the default, which keeps the total variance at the
    /// forward at 0.2²·t where the parameters' check allows it.</param>
    /// <param name="b">b's guess, or <see langword="null"/> for 2/(1+|ρ|).</param>
    /// <param name="sigma">σ's guess, or <see langword="null"/> for 0.1.</param>
    /// <param name="rho">ρ's guess, or <see langword="null"/> for −0.4.</param>
    /// <param name="m">m's guess, or <see langword="null"/> for 0.</param>
    /// <param name="aIsFixed">Whether a is fixed at its guess; a parameter not given is free.</param>
    /// <param name="bIsFixed">Whether b is fixed at its guess.</param>
    /// <param name="sigmaIsFixed">Whether σ is fixed at its guess.</param>
    /// <param name="rhoIsFixed">Whether ρ is fixed at its guess.</param>
    /// <param name="mIsFixed">Whether m is fixed at its guess.</param>
    /// <param name="vegaWeighted">Whether the errors are weighted by the strikes' Black vegas.</param>
    /// <param name="endCriteria">The end criteria, or <see langword="null"/> for QuantLib's
    /// <c>EndCriteria(60000, 100, 1e-8, 1e-8, 1e-8)</c>.</param>
    /// <param name="optMethod">The optimizer, or <see langword="null"/> for QuantLib's
    /// <c>LevenbergMarquardt(1e-8, 1e-8, 1e-8)</c>.</param>
    /// <param name="errorAccept">The error at or below which a fit is accepted and the restarts stop.</param>
    /// <param name="useMaxError">Whether a fit is scored by its maximum error, not its root-mean-square error.</param>
    /// <param name="maxGuesses">The most minimizations; 0 runs one, as 1 does.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxGuesses"/> is negative.</exception>
    /// <exception cref="ArgumentException">The sizes differ, there is no strike, the time isn't positive, the guesses
    /// fail <see cref="SviInterpolationFunctions.CheckSviParameters"/>, the volatilities are all zero with vega
    /// weights, or the optimizer refuses the problem.</exception>
    /// <exception cref="NanoQLException">The optimizer fails.</exception>
    public SviInterpolation(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
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
        bool vegaWeighted = true,
        EndCriteria? endCriteria = null,
        OptimizationMethod? optMethod = null,
        double errorAccept = 0.0020,
        bool useMaxError = false,
        int maxGuesses = 50)
        : base(x, y, 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxGuesses);
        fit = new XabrFit<SviSpecs, SviWrapper>(
            X,
            Y,
            t,
            forward,
            [a, b, sigma, rho, m],
            [aIsFixed, bIsFixed, sigmaIsFixed, rhoIsFixed, mIsFixed],
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

    /// <summary>Gets the fitted a.</summary>
    public double A => fit.Parameter(0);

    /// <summary>Gets the fitted b.</summary>
    public double B => fit.Parameter(1);

    /// <summary>Gets the fitted σ.</summary>
    public double Sigma => fit.Parameter(2);

    /// <summary>Gets the fitted ρ.</summary>
    public double Rho => fit.Parameter(3);

    /// <summary>Gets the fitted m.</summary>
    public double M => fit.Parameter(4);

    /// <inheritdoc/>
    public double RmsError => fit.Error;

    /// <inheritdoc/>
    public double MaxError => fit.MaxError;

    /// <inheritdoc/>
    public EndCriteriaType EndCriteria => fit.XabrEndCriteria;

    /// <summary>Gets a, in α's slot.</summary>
    double IXabrInterpolation.Alpha => A;

    /// <summary>Gets b, in β's slot.</summary>
    double IXabrInterpolation.Beta => B;

    /// <summary>Gets σ, in ν's slot.</summary>
    double IXabrInterpolation.Nu => Sigma;

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
