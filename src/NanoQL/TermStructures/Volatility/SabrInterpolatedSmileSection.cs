// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/sabrinterpolatedsmilesection.hpp ql/termstructures/volatility/sabrinterpolatedsmilesection.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A shifted lognormal smile section whose volatilities are a SABR fit to market volatilities at given strikes.
/// </summary>
/// <remarks>
/// <para>
/// The fit (<see cref="SABRInterpolation"/>) runs when the section is built: QuantLib's lazy calculation. QuantLib's
/// quote forms are the value forms here, a quote being its value, and a volatility quote that isn't valid is a
/// <see langword="null"/> value, its strike left out of the fit. With floating strikes, the strikes are offsets from the
/// forward and the volatilities spreads over the at-the-money volatility. QuantLib's sections float with the evaluation
/// date, having no reference date; here the <c>Create</c> factories take the evaluation context and fix the date.
/// </para>
/// <para>
/// QuantLib reads one strike per volatility, past the strikes' end where there are fewer strikes than volatilities;
/// they are refused here.
/// </para>
/// </remarks>
public sealed class SabrInterpolatedSmileSection : SmileSection
{
    private readonly double forwardValue;
    private readonly SABRInterpolation sabrInterpolation;

    private SabrInterpolatedSmileSection(
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
        Date referenceDate,
        bool isAlphaFixed,
        bool isBetaFixed,
        bool isNuFixed,
        bool isRhoFixed,
        bool vegaWeighted,
        EndCriteria? endCriteria,
        OptimizationMethod? method,
        DayCounter? dc,
        double shift)
        : base(optionDate, dc ?? new Actual365Fixed(), referenceDate, VolatilityType.ShiftedLognormal, shift)
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

        sabrInterpolation = new SABRInterpolation(
            [.. actualStrikes],
            [.. actualVols],
            ExerciseTime,
            forwardValue,
            alpha,
            beta,
            nu,
            rho,
            isAlphaFixed,
            isBetaFixed,
            isNuFixed,
            isRhoFixed,
            vegaWeighted,
            endCriteria,
            method,
            0.0020,
            false,
            50,
            Shift);
    }

    /// <summary>Gets the fitted alpha.</summary>
    public double Alpha => sabrInterpolation.Alpha;

    /// <summary>Gets the fitted beta.</summary>
    public double Beta => sabrInterpolation.Beta;

    /// <summary>Gets the fitted nu.</summary>
    public double Nu => sabrInterpolation.Nu;

    /// <summary>Gets the fitted rho.</summary>
    public double Rho => sabrInterpolation.Rho;

    /// <summary>Gets the fit's root-mean-square error.</summary>
    public double RmsError => sabrInterpolation.RmsError;

    /// <summary>Gets the fit's largest error.</summary>
    public double MaxError => sabrInterpolation.MaxError;

    /// <summary>Gets the fit's end criteria.</summary>
    public EndCriteriaType EndCriteria => sabrInterpolation.EndCriteria;

    /// <summary>
    /// A SABR-interpolated smile section for an option date, whose reference date is the evaluation date, from
    /// market volatilities: QuantLib's form with values.
    /// </summary>
    /// <param name="optionDate">The option's exercise date.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="strikes">The strikes, sorted; with floating strikes, the offsets from the forward. At least one
    /// per volatility.</param>
    /// <param name="hasFloatingStrikes">Whether the strikes and the volatilities are offsets from the forward and
    /// spreads over the at-the-money volatility.</param>
    /// <param name="atmVolatility">The at-the-money volatility, read with floating strikes.</param>
    /// <param name="vols">The market volatilities, or with floating strikes their spreads.</param>
    /// <param name="alpha">The guess or fixed value of alpha; <see langword="null"/> for SABR's default guess.</param>
    /// <param name="beta">The guess or fixed value of beta; <see langword="null"/> for SABR's default guess.</param>
    /// <param name="nu">The guess or fixed value of nu; <see langword="null"/> for SABR's default guess.</param>
    /// <param name="rho">The guess or fixed value of rho; <see langword="null"/> for SABR's default guess.</param>
    /// <param name="context">The evaluation context, whose evaluation date is the reference date.</param>
    /// <param name="isAlphaFixed">Whether alpha is fixed.</param>
    /// <param name="isBetaFixed">Whether beta is fixed.</param>
    /// <param name="isNuFixed">Whether nu is fixed.</param>
    /// <param name="isRhoFixed">Whether rho is fixed.</param>
    /// <param name="vegaWeighted">Whether the fit's errors are weighted by vega.</param>
    /// <param name="endCriteria">The fit's end criteria, or <see langword="null"/> for SABR's default.</param>
    /// <param name="method">The fit's optimization method, or <see langword="null"/> for SABR's default.</param>
    /// <param name="dc">The day counter, or <see langword="null"/> for QuantLib's default, Actual/365 (Fixed).</param>
    /// <param name="shift">The shift.</param>
    /// <returns>The section, with a fixed reference date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">The option date is <c>default(Date)</c> or before the evaluation date,
    /// there are fewer strikes than volatilities, or the fit refuses its points or its parameters.</exception>
    public static SabrInterpolatedSmileSection Create(
        Date optionDate,
        double forward,
        ReadOnlySpan<double> strikes,
        bool hasFloatingStrikes,
        double atmVolatility,
        ReadOnlySpan<double> vols,
        double? alpha,
        double? beta,
        double? nu,
        double? rho,
        EvaluationContext context,
        bool isAlphaFixed = false,
        bool isBetaFixed = false,
        bool isNuFixed = false,
        bool isRhoFixed = false,
        bool vegaWeighted = true,
        EndCriteria? endCriteria = null,
        OptimizationMethod? method = null,
        DayCounter? dc = null,
        double shift = 0.0)
    {
        var valid = new double?[vols.Length];
        for (int i = 0; i < vols.Length; i++)
        {
            valid[i] = vols[i];
        }

        return Create(
            optionDate, forward, strikes, hasFloatingStrikes, atmVolatility, valid, alpha, beta, nu, rho, context,
            isAlphaFixed, isBetaFixed, isNuFixed, isRhoFixed, vegaWeighted, endCriteria, method, dc, shift);
    }

    /// <summary>
    /// A SABR-interpolated smile section for an option date, whose reference date is the evaluation date, from
    /// market volatilities some of which may be missing: QuantLib's form with quotes.
    /// </summary>
    /// <param name="optionDate">The option's exercise date.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="strikes">The strikes, sorted; with floating strikes, the offsets from the forward. At least one
    /// per volatility.</param>
    /// <param name="hasFloatingStrikes">Whether the strikes and the volatilities are offsets from the forward and
    /// spreads over the at-the-money volatility.</param>
    /// <param name="atmVolatility">The at-the-money volatility, read with floating strikes.</param>
    /// <param name="vols">The market volatilities, or with floating strikes their spreads; <see langword="null"/>,
    /// a quote that isn't valid, leaves its strike out of the fit.</param>
    /// <param name="alpha">The guess or fixed value of alpha; <see langword="null"/> for SABR's default guess.</param>
    /// <param name="beta">The guess or fixed value of beta; <see langword="null"/> for SABR's default guess.</param>
    /// <param name="nu">The guess or fixed value of nu; <see langword="null"/> for SABR's default guess.</param>
    /// <param name="rho">The guess or fixed value of rho; <see langword="null"/> for SABR's default guess.</param>
    /// <param name="context">The evaluation context, whose evaluation date is the reference date.</param>
    /// <param name="isAlphaFixed">Whether alpha is fixed.</param>
    /// <param name="isBetaFixed">Whether beta is fixed.</param>
    /// <param name="isNuFixed">Whether nu is fixed.</param>
    /// <param name="isRhoFixed">Whether rho is fixed.</param>
    /// <param name="vegaWeighted">Whether the fit's errors are weighted by vega.</param>
    /// <param name="endCriteria">The fit's end criteria, or <see langword="null"/> for SABR's default.</param>
    /// <param name="method">The fit's optimization method, or <see langword="null"/> for SABR's default.</param>
    /// <param name="dc">The day counter, or <see langword="null"/> for QuantLib's default, Actual/365 (Fixed).</param>
    /// <param name="shift">The shift.</param>
    /// <returns>The section, with a fixed reference date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">The option date is <c>default(Date)</c> or before the evaluation date,
    /// there are fewer strikes than volatilities, or the fit refuses its points or its parameters.</exception>
    public static SabrInterpolatedSmileSection Create(
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
        EvaluationContext context,
        bool isAlphaFixed = false,
        bool isBetaFixed = false,
        bool isNuFixed = false,
        bool isRhoFixed = false,
        bool vegaWeighted = true,
        EndCriteria? endCriteria = null,
        OptimizationMethod? method = null,
        DayCounter? dc = null,
        double shift = 0.0)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SabrInterpolatedSmileSection(
            optionDate, forward, strikes, hasFloatingStrikes, atmVolatility, vols, alpha, beta, nu, rho,
            context.EvaluationDate, isAlphaFixed, isBetaFixed, isNuFixed, isRhoFixed, vegaWeighted, endCriteria, method,
            dc, shift);
    }

    /// <inheritdoc/>
    public override double MinStrike() => sabrInterpolation.XMin;

    /// <inheritdoc/>
    public override double MaxStrike() => sabrInterpolation.XMax;

    /// <inheritdoc/>
    public override double? AtmLevel() => forwardValue;

    /// <inheritdoc/>
    protected override double VarianceImpl(double strike)
    {
        double v = sabrInterpolation.Evaluate(strike, true);
        return v * v * ExerciseTime;
    }

    /// <inheritdoc/>
    protected override double VolatilityImpl(double strike) => sabrInterpolation.Evaluate(strike, true);
}
