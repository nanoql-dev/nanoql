// SPDX-FileCopyrightText: Copyright (C) 2008 Piero Del Boca
// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2015 Bernd Lewerenz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/inflation/seasonality.hpp ql/termstructures/inflation/seasonality.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// Kerkhof's seasonality: twelve monthly factors, the correction between two months the product of the factors of the
/// months between them.
/// </summary>
/// <remarks>Defined on zero rates only: a year-on-year correction refuses.</remarks>
public sealed class KerkhofSeasonality : MultiplicativePriceSeasonality
{
    /// <summary>Initializes a new instance of the <see cref="KerkhofSeasonality"/> class, with monthly factors.</summary>
    /// <param name="seasonalityBaseDate">The date the factors start from.</param>
    /// <param name="seasonalityFactors">The factors, a multiple of 12; only 12 are used, any other number is refused
    /// when a factor is read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="seasonalityFactors"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="seasonalityBaseDate"/> is <c>default(Date)</c>, no factor
    /// is given, or their number isn't a multiple of 12.</exception>
    public KerkhofSeasonality(Date seasonalityBaseDate, IEnumerable<double> seasonalityFactors)
        : base(seasonalityBaseDate, Frequency.Monthly, seasonalityFactors)
    {
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The product of the factors indexed by the months from the base date's month up to the date's month, excluded;
    /// its inverse when the date's month is earlier in the year.
    /// </remarks>
    /// <exception cref="NanoQLException">The seasonality doesn't hold 12 factors.</exception>
    public override double SeasonalityFactor(Date to)
    {
        Date.ThrowIfDefault(to, nameof(to));
        int dir = 1;
        Date from = SeasonalityBaseDate;
        int fromMonth = (int)from.Month;
        int toMonth = (int)to.Month;
        var factorPeriod = new Period(Frequency);
        if (toMonth < fromMonth)
        {
            (fromMonth, toMonth) = (toMonth, fromMonth);
            dir = 0; // We calculate invers Factor in loop
        }

        if (!(SeasonalityFactors().Length == 12 && factorPeriod.Units == TimeUnit.Months))
        {
            throw new NanoQLException(
                $"12 monthly seasonal factors needed for Kerkhof Seasonality: got {SeasonalityFactors().Length}");
        }

        double seasonalCorrection = 1.0;
        for (int i = fromMonth; i < toMonth; i++)
        {
            seasonalCorrection *= SeasonalityFactors()[i];
        }

        if (dir == 0)
        {
            // invers Factor required
            seasonalCorrection = 1 / seasonalCorrection;
        }

        return seasonalCorrection;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The rate is a year-on-year rate.</exception>
    protected override double SeasonalityCorrection(
        double rate, Date atDate, DayCounter dc, Date curveBaseDate, bool isZeroRate)
    {
        ArgumentNullException.ThrowIfNull(dc);
        double indexFactor = SeasonalityFactor(atDate);

        // Getting seasonality correction
        if (!isZeroRate)
        {
            throw new NanoQLException("Seasonal Kerkhof model is not defined on YoY rates");
        }

        (Date limStart, _) = InflationTermStructureFunctions.InflationPeriod(curveBaseDate, Frequency.Monthly);
        double timeFromCurveBase = dc.YearFraction(limStart, atDate);
        double f = Math.Pow(indexFactor, 1 / timeFromCurveBase);
        return ((rate + 1) * f) - 1;
    }
}
