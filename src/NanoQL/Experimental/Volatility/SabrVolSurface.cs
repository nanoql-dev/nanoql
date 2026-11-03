// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/sabrvolsurface.hpp ql/experimental/volatility/sabrvolsurface.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Experimental;

/// <summary>
/// An interest-rate volatility surface of SABR smiles: at each time, the SABR section fitted to the at-the-money curve's
/// volatility plus volatility spreads at strike spreads around the index's fixing, the spreads interpolated linearly
/// between the option tenors.
/// </summary>
/// <remarks>
/// <para>
/// The surface's reference date, calendar, day counter, settlement days and maximum date are the at-the-money curve's,
/// as QuantLib's overrides give them. It keeps the context it was built with, which the index's fixings and forecasts
/// read and on which each smile section is dated; each section is fitted per query, uncached. Quotes are values.
/// QuantLib's <c>updateSabrGuesses</c>, private and never called, isn't ported (it writes past its guesses); its guess
/// search is kept, every guess equal. QuantLib's acyclic visitor isn't ported.
/// </para>
/// <para>
/// Documented divergence (<c>Divergences/SabrVolSurfaceFixingTests.cs</c>): the section at time t reads the index's
/// fixing on the date ⌊365t⌋ days after the reference date adjusted on the index's fixing calendar by Following, the
/// adjustment QuantLib's interest-rate surface makes to its reference date, where QuantLib reads the unadjusted date,
/// refused ("Fixing date … is not valid") two days in seven; the section keeps the unadjusted date. Rows of volatility
/// spreads fewer than the option tenors are refused, where QuantLib reads past them.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class SabrVolSurface : InterestRateVolSurface
{
    // QuantLib's constructor's settings: nothing fixed, vega weighted, each tenor's guesses 0.025, 0.5, 0.3, 0.0 in
    // the section's alpha, beta, nu, rho order
    private const bool IsAlphaFixed = false;
    private const bool IsBetaFixed = false;
    private const bool IsNuFixed = false;
    private const bool IsRhoFixed = false;
    private const bool VegaWeighted = true;

    private readonly EvaluationContext context;
    private readonly ImmutableArray<double> atmRateSpreads;
    private readonly ImmutableArray<ImmutableArray<double>> volSpreads;
    private readonly ImmutableArray<double> optionTimes;
    private readonly ImmutableArray<Date> optionDates;
    private readonly ImmutableArray<double[]> sabrGuesses;

    /// <summary>Initializes a new instance of the <see cref="SabrVolSurface"/> class.</summary>
    /// <param name="index">The interest-rate index.</param>
    /// <param name="atmCurve">The at-the-money volatility curve.</param>
    /// <param name="optionTenors">The option tenors.</param>
    /// <param name="atmRateSpreads">The strike spreads around the fixing, increasing, at least two.</param>
    /// <param name="volSpreads">The volatility spreads, one row per option tenor, one column per strike spread.</param>
    /// <param name="context">The evaluation context the fixings, the forecasts and the sections read.</param>
    /// <exception cref="ArgumentNullException">An argument or a row is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">There are fewer than two strike spreads, they aren't increasing, a row's size
    /// isn't theirs, or there are fewer rows than option tenors.</exception>
    public SabrVolSurface(
        InterestRateIndex index,
        BlackAtmVolCurve atmCurve,
        IReadOnlyList<Period> optionTenors,
        IReadOnlyList<double> atmRateSpreads,
        IReadOnlyList<IReadOnlyList<double>> volSpreads,
        EvaluationContext context)
        : base(
            index,
            NotNull(atmCurve).ReferenceDate,
            atmCurve.Calendar,
            BusinessDayConvention.Following,
            atmCurve.DayCounter,
            atmCurve.SettlementDays)
    {
        ArgumentNullException.ThrowIfNull(optionTenors);
        ArgumentNullException.ThrowIfNull(atmRateSpreads);
        ArgumentNullException.ThrowIfNull(volSpreads);
        ArgumentNullException.ThrowIfNull(context);
        AtmCurve = atmCurve;
        this.context = context;
        OptionTenors = [.. optionTenors];
        this.atmRateSpreads = [.. atmRateSpreads];
        var rows = ImmutableArray.CreateBuilder<ImmutableArray<double>>(volSpreads.Count);
        foreach (IReadOnlyList<double> row in volSpreads)
        {
            ArgumentNullException.ThrowIfNull(row, nameof(volSpreads));
            rows.Add([.. row]);
        }

        this.volSpreads = rows.ToImmutable();
        CheckInputs();
        optionDates = [.. OptionTenors.Select(OptionDateFromTenor)];
        optionTimes = [.. optionDates.Select(TimeFromReference)];
        sabrGuesses = [.. OptionTenors.Select(_ => new[] { 0.025, 0.5, 0.3, 0.0 })];
    }

    /// <summary>Gets the at-the-money volatility curve.</summary>
    public BlackAtmVolCurve AtmCurve { get; }

    /// <summary>Gets the option tenors.</summary>
    public ImmutableArray<Period> OptionTenors { get; }

    /// <inheritdoc/>
    public override Date MaxDate => AtmCurve.MaxDate;

    /// <inheritdoc/>
    public override double MaxTime => AtmCurve.MaxTime;

    /// <inheritdoc/>
    public override double MinStrike() => NumericLimits.QL_MIN_REAL;

    /// <inheritdoc/>
    public override double MaxStrike() => NumericLimits.QL_MAX_REAL;

    /// <summary>The volatility spreads at an option tenor's date, interpolated linearly between the option times.</summary>
    /// <param name="p">The option tenor.</param>
    /// <returns>One spread per strike spread.</returns>
    public ImmutableArray<double> VolatilitySpreads(Period p) => VolatilitySpreads(OptionDateFromTenor(p));

    /// <summary>The volatility spreads at a date, interpolated linearly between the option times.</summary>
    /// <param name="d">The date.</param>
    /// <returns>One spread per strike spread.</returns>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    public ImmutableArray<double> VolatilitySpreads(Date d)
    {
        Date.ThrowIfDefault(d, nameof(d));
        int nOptionsTimes = optionTimes.Length;
        int nAtmRateSpreads = atmRateSpreads.Length;
        var interpolatedVols = new double[nAtmRateSpreads];
        var vols = new double[nOptionsTimes];
        double t = TimeFromReference(d);
        for (int i = 0; i < nAtmRateSpreads; ++i)
        {
            for (int j = 0; j < nOptionsTimes; ++j)
            {
                vols[j] = volSpreads[j][i];
            }

            var interpolator = new LinearInterpolation(optionTimes.AsSpan(), vols);
            interpolatedVols[i] = interpolator.Evaluate(t, true);
        }

        return [.. interpolatedVols];
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The index has no fixing or forecast at the adjusted date, or the fit
    /// fails.</exception>
    protected override SmileSection SmileSectionImpl(double t)
    {
        long n = (long)(t * 365.0);
        Date d = ReferenceDate + (int)n;
        ImmutableArray<double> spreads = VolatilitySpreads(d);
        double[] sabrParameters1 = SabrGuesses(d);

        // the fixing on the date adjusted on the fixing calendar, as the interest-rate surface adjusts its reference
        // date
        Date fixingDate = Index.FixingCalendar.Adjust(d, BusinessDayConvention.Following);
        return SabrInterpolatedSmileSection.Create(
            d,
            Index.Fixing(fixingDate, context, true),
            atmRateSpreads.AsSpan(),
            true,
            AtmCurve.AtmVol(d),
            spreads.AsSpan(),
            sabrParameters1[0],
            sabrParameters1[1],
            sabrParameters1[2],
            sabrParameters1[3],
            context,
            IsAlphaFixed,
            IsBetaFixed,
            IsNuFixed,
            IsRhoFixed,
            VegaWeighted);
    }

    // QuantLib's search, kept: every guess equal, so the first is QuantLib's value
    private double[] SabrGuesses(Date d)
    {
        if (d <= optionDates[0])
        {
            return sabrGuesses[0];
        }

        int i = 0;
        while (i < optionDates.Length - 1 && d < optionDates[i])
        {
            ++i;
        }

        return sabrGuesses[i];
    }

    // QuantLib's checkInputs, and the rows' count, which QuantLib reads past
    private void CheckInputs()
    {
        int nStrikes = atmRateSpreads.Length;
        if (!(nStrikes > 1))
        {
            throw new NanoQLException($"too few strikes ({nStrikes})");
        }

        for (int i = 1; i < nStrikes; ++i)
        {
            if (!(atmRateSpreads[i - 1] < atmRateSpreads[i]))
            {
                throw new NanoQLException(
                    $"non increasing strike spreads: {DataFormattersFunctions.Ordinal(i)} is "
                    + $"{DataFormattersFunctions.General(atmRateSpreads[i - 1])}, {DataFormattersFunctions.Ordinal(i + 1)} "
                    + $"is {DataFormattersFunctions.General(atmRateSpreads[i])}");
            }
        }

        for (int i = 0; i < volSpreads.Length; i++)
        {
            if (!(nStrikes == volSpreads[i].Length))
            {
                throw new NanoQLException(
                    $"mismatch between number of strikes ({nStrikes}) and number of columns ({volSpreads[i].Length}) in "
                    + $"the {DataFormattersFunctions.Ordinal(i + 1)} row");
            }
        }

        if (!(volSpreads.Length >= OptionTenors.Length))
        {
            throw new NanoQLException(
                $"mismatch between number of option tenors ({OptionTenors.Length}) and number of rows ({volSpreads.Length})");
        }
    }

    private static BlackAtmVolCurve NotNull(BlackAtmVolCurve atmCurve)
    {
        ArgumentNullException.ThrowIfNull(atmCurve);
        return atmCurve;
    }
}
