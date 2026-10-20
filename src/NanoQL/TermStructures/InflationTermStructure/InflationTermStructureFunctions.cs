// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/inflationtermstructure.hpp ql/termstructures/inflationtermstructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>QuantLib's free functions of <c>inflationtermstructure.hpp</c>: inflation periods and times.</summary>
public static class InflationTermStructureFunctions
{
    /// <summary>The inflation period a date falls in: QuantLib's <c>inflationPeriod</c>.</summary>
    /// <param name="d">The date.</param>
    /// <param name="frequency">The index's frequency: annual, semiannual, every fourth month, quarterly, bimonthly or
    /// monthly.</param>
    /// <returns>The period's first and last days, QuantLib's <c>std::pair</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frequency"/> isn't a member of its type, or is
    /// another frequency, with QuantLib's "Frequency not handled".</exception>
    public static (Date Start, Date End) InflationPeriod(Date d, Frequency frequency)
    {
        Date.ThrowIfDefault(d, nameof(d));
        EnumArguments.Check(frequency, nameof(frequency));
        int month = (int)d.Month;
        int year = d.Year;
        int startMonth, endMonth;
        switch (frequency)
        {
            case Frequency.Annual:
            case Frequency.Semiannual:
            case Frequency.EveryFourthMonth:
            case Frequency.Quarterly:
            case Frequency.Bimonthly:
                int nMonths = 12 / (int)frequency;
                startMonth = month - ((month - 1) % nMonths);
                endMonth = startMonth + nMonths - 1;
                break;
            case Frequency.Monthly:
                startMonth = endMonth = month;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(frequency), $"Frequency not handled: {FrequencyFunctions.ToString(frequency)}");
        }

        return (new Date(1, (Month)startMonth, year), Date.EndOfMonth(new Date(1, (Month)endMonth, year)));
    }

    /// <summary>
    /// The time between two dates, depending on the index's frequency and interpolation: QuantLib's
    /// <c>inflationYearFraction</c>.
    /// </summary>
    /// <param name="f">The index's frequency.</param>
    /// <param name="indexIsInterpolated">Whether the index is observed with linear interpolation.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="d1">The first date.</param>
    /// <param name="d2">The second date.</param>
    /// <returns>The year fraction between the dates when interpolated; otherwise between their periods' starts, the
    /// fixing being constant over a period.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dayCounter"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The index isn't interpolated and <paramref name="f"/> isn't a
    /// frequency <see cref="InflationPeriod"/> handles.</exception>
    public static double InflationYearFraction(
        Frequency f, bool indexIsInterpolated, DayCounter dayCounter, Date d1, Date d2)
    {
        ArgumentNullException.ThrowIfNull(dayCounter);
        if (indexIsInterpolated)
        {
            // N.B. we do not use linear interpolation between flat
            // fixing forecasts for forecasts.  This avoids awkwardnesses
            // when bootstrapping the inflation curve.
            return dayCounter.YearFraction(d1, d2);
        }

        // I.e. fixing is constant for the whole inflation period.
        // Use the value for half way along the period.
        // But the inflation time is the time between period starts
        (Date limD1, _) = InflationPeriod(d1, f);
        (Date limD2, _) = InflationPeriod(d2, f);
        return dayCounter.YearFraction(limD1, limD2);
    }
}
