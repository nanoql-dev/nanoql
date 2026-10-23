// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/zabrinterpolatedsmilesection.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A smile section whose volatilities are a ZABR fit to market volatilities at given strikes, by the kernel
/// <typeparamref name="TEvaluation"/> names.
/// </summary>
/// <typeparam name="TEvaluation">The kernel: <see cref="ZabrShortMaturityLognormal"/>,
/// <see cref="ZabrShortMaturityNormal"/>, <see cref="ZabrLocalVolatility"/> or <see cref="ZabrFullFd"/>.</typeparam>
/// <remarks>
/// <para>
/// The fit (<see cref="ZabrInterpolation{TEvaluation}"/>) runs when the section is built, QuantLib's lazy calculation
/// (the S-g1d plan's G1D10). QuantLib's quote forms are the value forms here, a quote being its value, and a volatility
/// quote that isn't valid is a <see langword="null"/> value, its strike left out of the fit. With floating strikes, the
/// strikes are offsets from the forward and the volatilities spreads over the at-the-money volatility. QuantLib's
/// sections float with the evaluation date, having no reference date; here the <c>Create</c> factories take the
/// evaluation context and fix the date.
/// </para>
/// <para>
/// QuantLib reads one strike per volatility, past the strikes' end where there are fewer strikes than volatilities;
/// they are refused here. Unsealed for its named classes, QuantLib-Python's
/// (<see cref="ZabrShortMaturityLognormalInterpolatedSmileSection"/>,
/// <see cref="ZabrShortMaturityNormalInterpolatedSmileSection"/>,
/// <see cref="ZabrLocalVolatilityInterpolatedSmileSection"/> and <see cref="ZabrFullFdInterpolatedSmileSection"/>). Its
/// constructor reads the exercise time, so
/// <see cref="ExerciseTime"/> is sealed here.
/// </para>
/// </remarks>
public class ZabrInterpolatedSmileSection<TEvaluation> : SmileSection
    where TEvaluation : struct, IZabrEvaluation
{
    private readonly double forwardValue;
    private readonly ZabrInterpolation<TEvaluation> zabrInterpolation;

    /// <summary>Initializes a new instance of the <see cref="ZabrInterpolatedSmileSection{TEvaluation}"/> class, and
    /// fits it.</summary>
    /// <param name="optionDate">The option's exercise date.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="strikes">The strikes, sorted; with floating strikes, the offsets from the forward. At least one per
    /// volatility.</param>
    /// <param name="hasFloatingStrikes">Whether the strikes and the volatilities are offsets from the forward and
    /// spreads over the at-the-money volatility.</param>
    /// <param name="atmVolatility">The at-the-money volatility, read with floating strikes.</param>
    /// <param name="vols">The market volatilities, or with floating strikes their spreads; <see langword="null"/>
    /// leaves its strike out of the fit.</param>
    /// <param name="alpha">The guess or fixed value of α; <see langword="null"/> for ZABR's default guess.</param>
    /// <param name="beta">The guess or fixed value of β; <see langword="null"/> for ZABR's default guess.</param>
    /// <param name="nu">The guess or fixed value of ν; <see langword="null"/> for ZABR's default guess.</param>
    /// <param name="rho">The guess or fixed value of ρ; <see langword="null"/> for ZABR's default guess.</param>
    /// <param name="gamma">The guess or fixed value of γ; <see langword="null"/> for ZABR's default guess.</param>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="isAlphaFixed">Whether α is fixed.</param>
    /// <param name="isBetaFixed">Whether β is fixed.</param>
    /// <param name="isNuFixed">Whether ν is fixed.</param>
    /// <param name="isRhoFixed">Whether ρ is fixed.</param>
    /// <param name="isGammaFixed">Whether γ is fixed.</param>
    /// <param name="vegaWeighted">Whether the fit's errors are weighted by vega.</param>
    /// <param name="endCriteria">The fit's end criteria, or <see langword="null"/> for ZABR's default.</param>
    /// <param name="method">The fit's optimization method, or <see langword="null"/> for ZABR's default.</param>
    /// <param name="dc">The day counter, or <see langword="null"/> for QuantLib's default, Actual/365 (Fixed).</param>
    /// <exception cref="ArgumentException">The option date is <c>default(Date)</c> or before the reference date, there
    /// are fewer strikes than volatilities, or the fit refuses its points or its parameters.</exception>
    /// <exception cref="NanoQLException">The fit fails.</exception>
    protected ZabrInterpolatedSmileSection(
        Date optionDate,
        double forward,
        ReadOnlySpan<double> strikes,
        bool hasFloatingStrikes,
        double atmVolatility,
        ReadOnlySpan<double?> vols,
        double? alpha,
        double? beta,
        double? nu,
        double? rho,
        double? gamma,
        Date referenceDate,
        bool isAlphaFixed = false,
        bool isBetaFixed = false,
        bool isNuFixed = false,
        bool isRhoFixed = false,
        bool isGammaFixed = false,
        bool vegaWeighted = true,
        EndCriteria? endCriteria = null,
        OptimizationMethod? method = null,
        DayCounter? dc = null)
        : base(optionDate, dc ?? new Actual365Fixed(), referenceDate)
    {
        if (!(strikes.Length >= vols.Length))
        {
            throw new ArgumentException($"{strikes.Length} strikes given for {vols.Length} volatilities", nameof(strikes));
        }

        // QuantLib's performCalculations: the volatilities, skipping the invalid ones, and the fit.
        forwardValue = forward;
        var actualStrikes = new List<double>();
        var actualVols = new List<double>();
        for (int i = 0; i < vols.Length; ++i)
        {
            if (vols[i] is double vol)
            {
                if (hasFloatingStrikes)
                {
                    actualStrikes.Add(forwardValue + strikes[i]);
                    actualVols.Add(atmVolatility + vol);
                }
                else
                {
                    actualStrikes.Add(strikes[i]);
                    actualVols.Add(vol);
                }
            }
        }

        zabrInterpolation = new ZabrInterpolation<TEvaluation>(
            [.. actualStrikes],
            [.. actualVols],
            ExerciseTime,
            forwardValue,
            alpha,
            beta,
            nu,
            rho,
            gamma,
            isAlphaFixed,
            isBetaFixed,
            isNuFixed,
            isRhoFixed,
            isGammaFixed,
            vegaWeighted,
            endCriteria,
            method);
    }

    /// <summary>Gets the exercise time.</summary>
    /// <remarks>Sealed: the constructor reads it.</remarks>
    public sealed override double ExerciseTime => base.ExerciseTime;

    /// <summary>Gets the fitted α.</summary>
    public double Alpha => zabrInterpolation.Alpha;

    /// <summary>Gets the fitted β.</summary>
    public double Beta => zabrInterpolation.Beta;

    /// <summary>Gets the fitted ν.</summary>
    public double Nu => zabrInterpolation.Nu;

    /// <summary>Gets the fitted ρ.</summary>
    public double Rho => zabrInterpolation.Rho;

    /// <summary>Gets the fitted γ.</summary>
    public double Gamma => zabrInterpolation.Gamma;

    /// <summary>Gets the fit's root-mean-square error.</summary>
    public double RmsError => zabrInterpolation.RmsError;

    /// <summary>Gets the fit's largest error.</summary>
    public double MaxError => zabrInterpolation.MaxError;

    /// <summary>Gets the fit's end criteria.</summary>
    public EndCriteriaType EndCriteria => zabrInterpolation.EndCriteria;

    /// <summary>
    /// A ZABR-interpolated smile section for an option date, whose reference date is the evaluation date, from market
    /// volatilities: QuantLib's form with values.
    /// </summary>
    /// <param name="optionDate">The option's exercise date.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="strikes">The strikes, sorted; with floating strikes, the offsets from the forward.</param>
    /// <param name="hasFloatingStrikes">Whether the strikes and the volatilities are offsets and spreads.</param>
    /// <param name="atmVolatility">The at-the-money volatility, read with floating strikes.</param>
    /// <param name="vols">The market volatilities, or with floating strikes their spreads.</param>
    /// <param name="alpha">The guess or fixed value of α; <see langword="null"/> for ZABR's default guess.</param>
    /// <param name="beta">The guess or fixed value of β; <see langword="null"/> for ZABR's default guess.</param>
    /// <param name="nu">The guess or fixed value of ν; <see langword="null"/> for ZABR's default guess.</param>
    /// <param name="rho">The guess or fixed value of ρ; <see langword="null"/> for ZABR's default guess.</param>
    /// <param name="gamma">The guess or fixed value of γ; <see langword="null"/> for ZABR's default guess.</param>
    /// <param name="context">The evaluation context, whose evaluation date is the reference date.</param>
    /// <param name="isAlphaFixed">Whether α is fixed.</param>
    /// <param name="isBetaFixed">Whether β is fixed.</param>
    /// <param name="isNuFixed">Whether ν is fixed.</param>
    /// <param name="isRhoFixed">Whether ρ is fixed.</param>
    /// <param name="isGammaFixed">Whether γ is fixed.</param>
    /// <param name="vegaWeighted">Whether the fit's errors are weighted by vega.</param>
    /// <param name="endCriteria">The fit's end criteria, or <see langword="null"/> for ZABR's default.</param>
    /// <param name="method">The fit's optimization method, or <see langword="null"/> for ZABR's default.</param>
    /// <param name="dc">The day counter, or <see langword="null"/> for Actual/365 (Fixed).</param>
    /// <returns>The section, with a fixed reference date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">The section refuses its arguments.</exception>
    /// <exception cref="NanoQLException">The fit fails.</exception>
    public static ZabrInterpolatedSmileSection<TEvaluation> Create(
        Date optionDate,
        double forward,
        ReadOnlySpan<double> strikes,
        bool hasFloatingStrikes,
        double atmVolatility,
        ReadOnlySpan<double?> vols,
        double? alpha,
        double? beta,
        double? nu,
        double? rho,
        double? gamma,
        EvaluationContext context,
        bool isAlphaFixed = false,
        bool isBetaFixed = false,
        bool isNuFixed = false,
        bool isRhoFixed = false,
        bool isGammaFixed = false,
        bool vegaWeighted = true,
        EndCriteria? endCriteria = null,
        OptimizationMethod? method = null,
        DayCounter? dc = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new ZabrInterpolatedSmileSection<TEvaluation>(
            optionDate, forward, strikes, hasFloatingStrikes, atmVolatility, vols, alpha, beta, nu, rho, gamma,
            context.EvaluationDate, isAlphaFixed, isBetaFixed, isNuFixed, isRhoFixed, isGammaFixed, vegaWeighted, endCriteria,
            method, dc);
    }

    /// <inheritdoc/>
    public override double MinStrike() => zabrInterpolation.XMin;

    /// <inheritdoc/>
    public override double MaxStrike() => zabrInterpolation.XMax;

    /// <inheritdoc/>
    public override double? AtmLevel() => forwardValue;

    /// <inheritdoc/>
    protected override double VarianceImpl(double strike)
    {
        double v = zabrInterpolation.Evaluate(strike, true);
        return v * v * ExerciseTime;
    }

    /// <inheritdoc/>
    protected override double VolatilityImpl(double strike) => zabrInterpolation.Evaluate(strike, true);
}
