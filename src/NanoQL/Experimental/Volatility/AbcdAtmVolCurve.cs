// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/abcdatmvolcurve.hpp ql/experimental/volatility/abcdatmvolcurve.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Experimental;

/// <summary>
/// The at-the-money volatility curve of the abcd function fitted to quoted volatilities at option tenors, each fitted
/// value scaled by the adjustment factor that matches the quotes, interpolated linearly in time.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's <c>LazyObject</c> computes at construction (the S-xeq plan's §2.0): QuantLib fits the abcd function when
/// its constructor builds the interpolation and again at the first calculation, from the first fit's coefficients
/// (<c>update()</c>, which checks no guess), so the curve fits twice, the second through <see cref="AbcdInterpolation"/>'s
/// internal refit. Quotes are values. QuantLib's moving reference date is the factory's, dated by the context. QuantLib's
/// acyclic visitor isn't ported.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class AbcdAtmVolCurve : BlackAtmVolCurve
{
    private readonly ImmutableArray<double> actualOptionTimes;
    private readonly AbcdInterpolation interpolation;

    private AbcdAtmVolCurve(
        Date referenceDate,
        Calendar calendar,
        int settlementDays,
        IReadOnlyList<Period> optionTenors,
        IReadOnlyList<double> vols,
        IReadOnlyList<bool>? inclusionInInterpolationFlag,
        BusinessDayConvention bdc,
        DayCounter dayCounter)
        : base(referenceDate, calendar, bdc, dayCounter, settlementDays)
    {
        ArgumentNullException.ThrowIfNull(optionTenors);
        ArgumentNullException.ThrowIfNull(vols);
        OptionTenors = [.. optionTenors];
        Volatilities = [.. vols];
        ImmutableArray<bool> inclusion = CheckInputs(OptionTenors, Volatilities, inclusionInInterpolationFlag ?? [true]);

        // QuantLib's initializeOptionDatesAndTimes and initializeVolatilities
        OptionDates = [.. OptionTenors.Select(OptionDateFromTenor)];
        OptionTimes = [.. OptionDates.Select(TimeFromReference)];
        var times = ImmutableArray.CreateBuilder<double>();
        var tenors = ImmutableArray.CreateBuilder<Period>();
        var actualVols = new List<double>();
        for (int i = 0; i < OptionTenors.Length; ++i)
        {
            if (inclusion[i])
            {
                times.Add(OptionTimes[i]);
                tenors.Add(OptionTenors[i]);
                actualVols.Add(Volatilities[i]);
            }
        }

        actualOptionTimes = times.ToImmutable();
        OptionTenorsInInterpolation = tenors.ToImmutable();

        // QuantLib's interpolate(), the constructor's fit, then performCalculations' update(), the warm refit
        interpolation = new AbcdInterpolation(actualOptionTimes.AsSpan(), actualVols.ToArray()).Refit();
    }

    /// <summary>Gets the option tenors.</summary>
    public ImmutableArray<Period> OptionTenors { get; }

    /// <summary>Gets the option tenors whose volatilities the fit includes.</summary>
    public ImmutableArray<Period> OptionTenorsInInterpolation { get; }

    /// <summary>Gets the option dates.</summary>
    public ImmutableArray<Date> OptionDates { get; }

    /// <summary>Gets the option times.</summary>
    public ImmutableArray<double> OptionTimes { get; }

    /// <summary>Gets the quoted volatilities, one per option tenor.</summary>
    public ImmutableArray<double> Volatilities { get; }

    /// <summary>Gets the fitted a.</summary>
    public double A => interpolation.A;

    /// <summary>Gets the fitted b.</summary>
    public double B => interpolation.B;

    /// <summary>Gets the fitted c.</summary>
    public double C => interpolation.C;

    /// <summary>Gets the fitted d.</summary>
    public double D => interpolation.D;

    /// <summary>Gets the fit's weighted root-mean-square error.</summary>
    public double RmsError => interpolation.RmsError;

    /// <summary>Gets the fit's largest absolute error.</summary>
    public double MaxError => interpolation.MaxError;

    /// <summary>Gets the end criterion the fit met.</summary>
    public EndCriteriaType EndCriteria => interpolation.EndCriteria;

    /// <inheritdoc/>
    public override Date MaxDate => OptionDateFromTenor(OptionTenors[^1]);

    /// <summary>
    /// Creates the curve on the reference date the settlement days give from the context's evaluation date.
    /// </summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="optionTenors">The option tenors, increasing from a positive first.</param>
    /// <param name="vols">The volatilities, one per option tenor.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="inclusionInInterpolationFlag">Whether each volatility is fitted, one flag for every tenor or one per
    /// tenor; <see langword="null"/> for QuantLib's default, every one.</param>
    /// <param name="bdc">The business-day convention of the option dates.</param>
    /// <param name="dayCounter">The day counter, or <see langword="null"/> for QuantLib's default, Actual/365
    /// (Fixed).</param>
    /// <returns>The curve.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    /// <exception cref="NanoQLException">The tenors are empty or not increasing from a positive first, the sizes
    /// differ, or the fit fails.</exception>
    public static AbcdAtmVolCurve Create(
        int settlementDays,
        Calendar calendar,
        IReadOnlyList<Period> optionTenors,
        IReadOnlyList<double> vols,
        EvaluationContext context,
        IReadOnlyList<bool>? inclusionInInterpolationFlag = null,
        BusinessDayConvention bdc = BusinessDayConvention.Following,
        DayCounter? dayCounter = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        ArgumentNullException.ThrowIfNull(calendar);
        Date referenceDate = calendar.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
        return new AbcdAtmVolCurve(
            referenceDate,
            calendar,
            settlementDays,
            optionTenors,
            vols,
            inclusionInInterpolationFlag,
            bdc,
            dayCounter ?? new Actual365Fixed());
    }

    /// <summary>The adjustment factors that match the fitted volatilities to the quotes.</summary>
    /// <returns>The factors.</returns>
    public ImmutableArray<double> K() => interpolation.K;

    /// <summary>The adjustment factor at a time, interpolated linearly between the fitted option times.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The factor.</returns>
    /// <exception cref="NanoQLException">The time is outside the fitted option times.</exception>
    public double K(double t) => interpolation.KAt(t, actualOptionTimes.AsSpan());

    /// <inheritdoc/>
    public override double MinStrike() => NumericLimits.QL_MIN_REAL;

    /// <inheritdoc/>
    public override double MaxStrike() => NumericLimits.QL_MAX_REAL;

    /// <inheritdoc/>
    protected override double AtmVarianceImpl(double t)
    {
        double vol = AtmVolImpl(t);
        return vol * vol * t;
    }

    /// <inheritdoc/>
    protected override double AtmVolImpl(double t) => K(t) * interpolation.Evaluate(t, true);

    // QuantLib's checkInputs: the tenors, the sizes and the inclusion flags, one for all broadcast
    private static ImmutableArray<bool> CheckInputs(
        ImmutableArray<Period> optionTenors, ImmutableArray<double> vols, IReadOnlyList<bool> inclusion)
    {
        int n = optionTenors.Length;
        if (n == 0)
        {
            throw new NanoQLException("empty option tenor vector");
        }

        if (!(n == vols.Length))
        {
            throw new NanoQLException(
                $"mismatch between number of option tenors ({n}) and number of volatilities ({vols.Length})");
        }

        if (!(optionTenors[0] > new Period(0, TimeUnit.Days)))
        {
            throw new NanoQLException($"negative first option tenor: {optionTenors[0]}");
        }

        for (int i = 1; i < n; ++i)
        {
            if (!(optionTenors[i] > optionTenors[i - 1]))
            {
                throw new NanoQLException(
                    $"non increasing option tenor: {DataFormattersFunctions.Ordinal(i)} is {optionTenors[i - 1]}, "
                    + $"{DataFormattersFunctions.Ordinal(i + 1)} is {optionTenors[i]}");
            }
        }

        if (inclusion.Count == 1)
        {
            return [.. Enumerable.Repeat(inclusion[0], n)];
        }

        if (!(inclusion.Count == n))
        {
            throw new NanoQLException(
                $"mismatch between number of option tenors ({n}) and number of inclusion's flags ({inclusion.Count})");
        }

        return [.. inclusion];
    }
}
