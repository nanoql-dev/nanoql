// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/sviinterpolatedsmilesection.hpp ql/experimental/volatility/sviinterpolatedsmilesection.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A smile section whose volatilities are an SVI fit to market volatilities at given strikes.
/// </summary>
/// <remarks>
/// <para>
/// The fit (<see cref="SviInterpolation"/>) runs when the section is built: QuantLib's lazy calculation (the S-xeq plan's
/// §2.0, S-g1d's <see cref="SabrInterpolatedSmileSection"/>). QuantLib's quote forms are the value forms here, a quote
/// being its value, and a volatility quote that isn't valid is a <see langword="null"/> value, its strike left out of the
/// fit. With floating strikes, the strikes are offsets from the forward and the volatilities spreads over the
/// at-the-money volatility. QuantLib's sections float with the evaluation date, having no reference date; here the
/// <c>Create</c> factories take the evaluation context and fix the date.
/// </para>
/// <para>QuantLib reads one strike per volatility, past the strikes' end where there are fewer strikes; they are refused
/// here.</para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class SviInterpolatedSmileSection : SmileSection
{
    private readonly double forwardValue;
    private readonly double minStrike;
    private readonly double maxStrike;
    private readonly SviInterpolation sviInterpolation;

    private SviInterpolatedSmileSection(
        Date optionDate,
        double forward,
        ReadOnlySpan<double> strikes,
        bool hasFloatingStrikes,
        double atmVolatility,
        ReadOnlySpan<double?> vols,
        double? a,
        double? b,
        double? sigma,
        double? rho,
        double? m,
        Date referenceDate,
        bool isAFixed,
        bool isBFixed,
        bool isSigmaFixed,
        bool isRhoFixed,
        bool isMFixed,
        bool vegaWeighted,
        EndCriteria? endCriteria,
        OptimizationMethod? method,
        DayCounter? dc)
        : base(optionDate, dc ?? new Actual365Fixed(), referenceDate)
    {
        if (!(strikes.Length >= vols.Length))
        {
            throw new ArgumentException($"{strikes.Length} strikes given for {vols.Length} volatilities", nameof(strikes));
        }

        // QuantLib's performCalculations: the volatilities, skipping the invalid ones, and the fit
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

        sviInterpolation = new SviInterpolation(
            [.. actualStrikes],
            [.. actualVols],
            ExerciseTime,
            forwardValue,
            a,
            b,
            sigma,
            rho,
            m,
            isAFixed,
            isBFixed,
            isSigmaFixed,
            isRhoFixed,
            isMFixed,
            vegaWeighted,
            endCriteria,
            method);
        minStrike = actualStrikes[0];
        maxStrike = actualStrikes[^1];
    }

    /// <summary>Gets the fitted a.</summary>
    public double A => sviInterpolation.A;

    /// <summary>Gets the fitted b.</summary>
    public double B => sviInterpolation.B;

    /// <summary>Gets the fitted σ.</summary>
    public double Sigma => sviInterpolation.Sigma;

    /// <summary>Gets the fitted ρ.</summary>
    public double Rho => sviInterpolation.Rho;

    /// <summary>Gets the fitted m.</summary>
    public double M => sviInterpolation.M;

    /// <summary>Gets the fit's root-mean-square error.</summary>
    public double RmsError => sviInterpolation.RmsError;

    /// <summary>Gets the fit's largest error.</summary>
    public double MaxError => sviInterpolation.MaxError;

    /// <summary>Gets the end criterion the fit met.</summary>
    public EndCriteriaType EndCriteria => sviInterpolation.EndCriteria;

    /// <summary>Creates the section on market volatilities, a volatility given as <see langword="null"/> left
    /// out.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="strikes">The strikes, or with floating strikes the offsets from the forward.</param>
    /// <param name="hasFloatingStrikes">Whether the strikes are offsets and the volatilities spreads.</param>
    /// <param name="atmVolatility">The at-the-money volatility, read with floating strikes.</param>
    /// <param name="vols">The volatilities or spreads, one per strike; <see langword="null"/> where not valid.</param>
    /// <param name="a">a's guess, or <see langword="null"/> for the fit's default.</param>
    /// <param name="b">b's guess, or <see langword="null"/> for the fit's default.</param>
    /// <param name="sigma">σ's guess, or <see langword="null"/> for the fit's default.</param>
    /// <param name="rho">ρ's guess, or <see langword="null"/> for the fit's default.</param>
    /// <param name="m">m's guess, or <see langword="null"/> for the fit's default.</param>
    /// <param name="context">The evaluation context, whose date is the section's reference date.</param>
    /// <param name="isAFixed">Whether a is fixed at its guess.</param>
    /// <param name="isBFixed">Whether b is fixed at its guess.</param>
    /// <param name="isSigmaFixed">Whether σ is fixed at its guess.</param>
    /// <param name="isRhoFixed">Whether ρ is fixed at its guess.</param>
    /// <param name="isMFixed">Whether m is fixed at its guess.</param>
    /// <param name="vegaWeighted">Whether the errors are weighted by vega.</param>
    /// <param name="endCriteria">The end criteria, or <see langword="null"/> for the fit's.</param>
    /// <param name="method">The optimizer, or <see langword="null"/> for the fit's.</param>
    /// <param name="dc">The day counter, or <see langword="null"/> for QuantLib's default, Actual/365 (Fixed).</param>
    /// <returns>The section.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There are fewer strikes than volatilities, or the fit's inputs are out of
    /// range.</exception>
    /// <exception cref="NanoQLException">The fit fails.</exception>
    public static SviInterpolatedSmileSection Create(
        Date optionDate,
        double forward,
        ReadOnlySpan<double> strikes,
        bool hasFloatingStrikes,
        double atmVolatility,
        ReadOnlySpan<double?> vols,
        double? a,
        double? b,
        double? sigma,
        double? rho,
        double? m,
        EvaluationContext context,
        bool isAFixed,
        bool isBFixed,
        bool isSigmaFixed,
        bool isRhoFixed,
        bool isMFixed,
        bool vegaWeighted = true,
        EndCriteria? endCriteria = null,
        OptimizationMethod? method = null,
        DayCounter? dc = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SviInterpolatedSmileSection(
            optionDate, forward, strikes, hasFloatingStrikes, atmVolatility, vols, a, b, sigma, rho, m,
            context.EvaluationDate, isAFixed, isBFixed, isSigmaFixed, isRhoFixed, isMFixed, vegaWeighted, endCriteria,
            method, dc);
    }

    /// <summary>Creates the section on market volatilities, every one valid.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="strikes">The strikes, or with floating strikes the offsets from the forward.</param>
    /// <param name="hasFloatingStrikes">Whether the strikes are offsets and the volatilities spreads.</param>
    /// <param name="atmVolatility">The at-the-money volatility, read with floating strikes.</param>
    /// <param name="vols">The volatilities or spreads, one per strike.</param>
    /// <param name="a">a's guess, or <see langword="null"/> for the fit's default.</param>
    /// <param name="b">b's guess, or <see langword="null"/> for the fit's default.</param>
    /// <param name="sigma">σ's guess, or <see langword="null"/> for the fit's default.</param>
    /// <param name="rho">ρ's guess, or <see langword="null"/> for the fit's default.</param>
    /// <param name="m">m's guess, or <see langword="null"/> for the fit's default.</param>
    /// <param name="context">The evaluation context, whose date is the section's reference date.</param>
    /// <param name="isAFixed">Whether a is fixed at its guess.</param>
    /// <param name="isBFixed">Whether b is fixed at its guess.</param>
    /// <param name="isSigmaFixed">Whether σ is fixed at its guess.</param>
    /// <param name="isRhoFixed">Whether ρ is fixed at its guess.</param>
    /// <param name="isMFixed">Whether m is fixed at its guess.</param>
    /// <param name="vegaWeighted">Whether the errors are weighted by vega.</param>
    /// <param name="endCriteria">The end criteria, or <see langword="null"/> for the fit's.</param>
    /// <param name="method">The optimizer, or <see langword="null"/> for the fit's.</param>
    /// <param name="dc">The day counter, or <see langword="null"/> for QuantLib's default, Actual/365 (Fixed).</param>
    /// <returns>The section.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There are fewer strikes than volatilities, or the fit's inputs are out of
    /// range.</exception>
    /// <exception cref="NanoQLException">The fit fails.</exception>
    public static SviInterpolatedSmileSection Create(
        Date optionDate,
        double forward,
        ReadOnlySpan<double> strikes,
        bool hasFloatingStrikes,
        double atmVolatility,
        ReadOnlySpan<double> vols,
        double? a,
        double? b,
        double? sigma,
        double? rho,
        double? m,
        EvaluationContext context,
        bool isAFixed,
        bool isBFixed,
        bool isSigmaFixed,
        bool isRhoFixed,
        bool isMFixed,
        bool vegaWeighted = true,
        EndCriteria? endCriteria = null,
        OptimizationMethod? method = null,
        DayCounter? dc = null)
    {
        var valid = new double?[vols.Length];
        for (int i = 0; i < vols.Length; i++)
        {
            valid[i] = vols[i];
        }

        return Create(
            optionDate, forward, strikes, hasFloatingStrikes, atmVolatility, valid, a, b, sigma, rho, m, context, isAFixed,
            isBFixed, isSigmaFixed, isRhoFixed, isMFixed, vegaWeighted, endCriteria, method, dc);
    }

    /// <inheritdoc/>
    public override double MinStrike() => minStrike;

    /// <inheritdoc/>
    public override double MaxStrike() => maxStrike;

    /// <inheritdoc/>
    public override double? AtmLevel() => forwardValue;

    /// <inheritdoc/>
    protected override double VarianceImpl(double strike)
    {
        double v = sviInterpolation.Evaluate(strike, true);
        return v * v * ExerciseTime;
    }

    /// <inheritdoc/>
    protected override double VolatilityImpl(double strike) => sviInterpolation.Evaluate(strike, true);
}
