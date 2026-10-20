// SPDX-FileCopyrightText: Copyright (C) 2008 Piero Del Boca
// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2015 Bernd Lewerenz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/inflation/seasonality.hpp ql/termstructures/inflation/seasonality.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>Multiplicative seasonality in the price index (CPI, RPI, HICP).</summary>
/// <remarks>
/// <para>
/// Stationary multiplicative seasonality in the price implies that zero-coupon inflation swap rates are affected and
/// year-on-year rates aren't; a non-stationary one affects both. The factors are a multiple of those one year needs at
/// the frequency (12 for monthly), reused for as long as needed: 24 factors repeat every two years.
/// </para>
/// <para>
/// A multi-year seasonality is fragile: its corrections at whole years before and after the term structure's base date
/// must be the same, which <see cref="IsConsistent"/> enforces for frequencies lower than daily. The factors are
/// normalized to the term structure's base date for zero inflation and to one year earlier for year-on-year inflation,
/// and the seasonality is piecewise constant.
/// </para>
/// <para>
/// Built whole: QuantLib's default constructor, which leaves the frequency uninitialized until <c>set</c>, and
/// <c>set</c> itself, whose only caller is the constructor, aren't ported. The constructor's checks are QuantLib's
/// <c>validate</c>, which isn't virtual here; nor are the three inspectors, two of which it reads (master §2.2's
/// constructor-read row), and no QuantLib class overrides them. An extension point: <see cref="KerkhofSeasonality"/>
/// derives from it.
/// </para>
/// </remarks>
public class MultiplicativePriceSeasonality : Seasonality
{
    private readonly ImmutableArray<double> seasonalityFactors;

    /// <summary>Initializes a new instance of the <see cref="MultiplicativePriceSeasonality"/> class.</summary>
    /// <param name="seasonalityBaseDate">The date the factors start from.</param>
    /// <param name="frequency">The factors' frequency: semiannual to daily.</param>
    /// <param name="seasonalityFactors">The factors, a multiple of the frequency's count a year.</param>
    /// <exception cref="ArgumentNullException"><paramref name="seasonalityFactors"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="seasonalityBaseDate"/> is <c>default(Date)</c>, no factor
    /// is given, or their number isn't a multiple of the frequency's.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frequency"/> isn't semiannual, every fourth month,
    /// quarterly, bimonthly, monthly, biweekly, weekly or daily.</exception>
    public MultiplicativePriceSeasonality(
        Date seasonalityBaseDate, Frequency frequency, IEnumerable<double> seasonalityFactors)
    {
        ArgumentNullException.ThrowIfNull(seasonalityFactors);
        Date.ThrowIfDefault(seasonalityBaseDate, nameof(seasonalityBaseDate));
        EnumArguments.Check(frequency, nameof(frequency));
        ImmutableArray<double> factors = [.. seasonalityFactors];
        Validate(frequency, factors);
        Frequency = frequency;
        this.seasonalityFactors = factors;
        SeasonalityBaseDate = seasonalityBaseDate;
    }

    /// <summary>Gets the date the factors start from.</summary>
    public Date SeasonalityBaseDate { get; }

    /// <summary>Gets the factors' frequency.</summary>
    public Frequency Frequency { get; }

    /// <summary>The seasonality factors.</summary>
    /// <returns>The factors.</returns>
    public ImmutableArray<double> SeasonalityFactors() => seasonalityFactors;

    /// <summary>The factor at a date, not normalized relative to anything.</summary>
    /// <param name="to">The date.</param>
    /// <returns>The factor of the date's period, counted from the base date and wrapped around the factors.</returns>
    public virtual double SeasonalityFactor(Date to)
    {
        Date.ThrowIfDefault(to, nameof(to));
        Date from = SeasonalityBaseDate;
        Frequency factorFrequency = Frequency;
        int nFactors = seasonalityFactors.Length;
        var factorPeriod = new Period(factorFrequency);
        int which;
        if (from == to)
        {
            which = 0;
        }
        else
        {
            // days, weeks, months, years are the only time unit possibilities
            int diffDays = Math.Abs(to - from);
            int dir = 1;
            if (from > to)
            {
                dir = -1;
            }

            int diff;
            switch (factorPeriod.Units)
            {
                case TimeUnit.Days:
                    diff = dir * diffDays;
                    break;
                case TimeUnit.Weeks:
                    diff = dir * (diffDays / 7);
                    break;
                case TimeUnit.Months:
                    (Date limStart, Date limEnd) = InflationTermStructureFunctions.InflationPeriod(to, factorFrequency);
                    diff = diffDays / (31 * factorPeriod.Length);
                    Date go = from + (dir * diff * factorPeriod);
                    while (!(limStart <= go && go <= limEnd))
                    {
                        go += dir * factorPeriod;
                        diff++;
                    }

                    diff = dir * diff;
                    break;
                default:
                    // Validate refuses every frequency whose period is in years.
                    throw new NanoQLException($"seasonality period time unit is not allowed to be : {factorPeriod.Units}");
            }

            // now adjust to the available number of factors, direction dependent
            which = dir == 1 ? diff % nFactors : (nFactors - (-diff % nFactors)) % nFactors;
        }

        return seasonalityFactors[which];
    }

    /// <inheritdoc/>
    /// <remarks>
    /// As <c>ZeroInflationIndex::forecastFixing</c> chooses them, the curve's base date and the start of the date's
    /// inflation period, so that a seasonally adjusted fixing divided by an unadjusted one gives back the input
    /// factors.
    /// </remarks>
    public override double CorrectZeroRate(Date d, double r, InflationTermStructure iTS)
    {
        ArgumentNullException.ThrowIfNull(iTS);
        Date curveBaseDate = iTS.BaseDate;
        (Date effectiveFixingDate, _) = InflationTermStructureFunctions.InflationPeriod(d, iTS.Frequency);
        return SeasonalityCorrection(r, effectiveFixingDate, iTS.DayCounter, curveBaseDate, true);
    }

    /// <inheritdoc/>
    public override double CorrectYoYRate(Date d, double r, InflationTermStructure iTS)
    {
        ArgumentNullException.ThrowIfNull(iTS);
        (_, Date curveBaseDate) = InflationTermStructureFunctions.InflationPeriod(iTS.BaseDate, iTS.Frequency);
        return SeasonalityCorrection(r, d, iTS.DayCounter, curveBaseDate, false);
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">A multi-year seasonality whose factor at a whole number of years after the
    /// end of the curve's base period differs from the factor there by 1e-5 or more.</exception>
    /// <remarks>A daily seasonality isn't tested: it is inconsistent in general, given weekends, holidays and leap
    /// years.</remarks>
    public override bool IsConsistent(InflationTermStructure iTS)
    {
        ArgumentNullException.ThrowIfNull(iTS);

        // If multi-year is the specification consistent with the term structure start date?
        // We do NOT test daily seasonality because this will, in general, never be consistent
        // given weekends, holidays, leap years, etc.
        if (Frequency == Frequency.Daily)
        {
            return true;
        }

        if ((int)Frequency == seasonalityFactors.Length)
        {
            return true;
        }

        // how many years do you need to test?
        int nTest = seasonalityFactors.Length / (int)Frequency;

        // ... relative to the start of the inflation curve
        (_, Date curveBaseDate) = InflationTermStructureFunctions.InflationPeriod(iTS.BaseDate, iTS.Frequency);
        double factorBase = SeasonalityFactor(curveBaseDate);
        const double eps = 0.00001;
        for (int i = 1; i < nTest; i++)
        {
            double factorAt = SeasonalityFactor(curveBaseDate + new Period(i, TimeUnit.Years));
            if (!(Math.Abs(factorAt - factorBase) < eps))
            {
                throw new NanoQLException(
                    $"seasonality is inconsistent with inflation term structure, factors {Show(factorBase)} and later "
                    + $"factor {Show(factorAt)}, {i} years later from inflation curve  with base date at {curveBaseDate}");
            }
        }

        return true;
    }

    /// <summary>
    /// A rate corrected by the factor at a date relative to its reference: the curve's base date for a zero rate,
    /// whose factor must be one since the base fixing is known, and one year earlier for a year-on-year rate.
    /// </summary>
    /// <param name="rate">The uncorrected rate.</param>
    /// <param name="atDate">The date the rate is for.</param>
    /// <param name="dc">The term structure's day counter.</param>
    /// <param name="curveBaseDate">The curve's base date.</param>
    /// <param name="isZeroRate">Whether the rate is a zero rate, rather than a year-on-year one.</param>
    /// <returns>The corrected rate.</returns>
    protected virtual double SeasonalityCorrection(
        double rate, Date atDate, DayCounter dc, Date curveBaseDate, bool isZeroRate)
    {
        ArgumentNullException.ThrowIfNull(dc);

        // need _two_ corrections in order to get: seasonality = factor[atDate-seasonalityBase] / factor[reference-seasonalityBase]
        // i.e. for ZERO inflation rates you have the true fixing at the curve base so this factor must be normalized to one
        //      for YoY inflation rates your reference point is the year before
        double factorAt = SeasonalityFactor(atDate);

        // Getting seasonality correction for either ZC or YoY
        double f;
        if (isZeroRate)
        {
            double factorBase = SeasonalityFactor(curveBaseDate);
            double seasonalityAt = factorAt / factorBase;
            (Date start, _) = InflationTermStructureFunctions.InflationPeriod(atDate, Frequency);
            double timeFromCurveBase = dc.YearFraction(curveBaseDate, start);
            f = Math.Pow(seasonalityAt, 1 / timeFromCurveBase);
        }
        else
        {
            double factor1Ybefore = SeasonalityFactor(atDate - new Period(1, TimeUnit.Years));
            f = factorAt / factor1Ybefore;
        }

        return ((rate + 1) * f) - 1;
    }

    private static string Show(double x) => x.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // QuantLib's validate, read by the constructor only.
    private static void Validate(Frequency frequency, ImmutableArray<double> seasonalityFactors)
    {
        switch (frequency)
        {
            case Frequency.Semiannual:
            case Frequency.EveryFourthMonth:
            case Frequency.Quarterly:
            case Frequency.Bimonthly:
            case Frequency.Monthly:
            case Frequency.Biweekly:
            case Frequency.Weekly:
            case Frequency.Daily:
                if (seasonalityFactors.IsEmpty)
                {
                    throw new ArgumentException("no seasonality factors given", nameof(seasonalityFactors));
                }

                if (seasonalityFactors.Length % (int)frequency != 0)
                {
                    throw new ArgumentException(
                        $"For frequency {FrequencyFunctions.ToString(frequency)} require multiple of {(int)frequency} "
                        + $"factors {seasonalityFactors.Length} were given.",
                        nameof(seasonalityFactors));
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(frequency),
                    $"bad frequency specified: {FrequencyFunctions.ToString(frequency)}, only semi-annual through daily "
                    + "permitted.");
        }
    }
}
