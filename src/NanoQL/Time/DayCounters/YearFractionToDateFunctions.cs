// SPDX-FileCopyrightText: Copyright (C) 2023 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2023, 2026 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/daycounters/yearfractiontodate.hpp ql/time/daycounters/yearfractiontodate.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Time;

/// <summary>The "inverse" of a day counter: the date a year fraction reaches.</summary>
public static class YearFractionToDateFunctions
{
    /// <summary>The date whose year fraction from the reference date, by the day counter, is closest to a
    /// time.</summary>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="t">The year fraction.</param>
    /// <returns>The date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dayCounter"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="t"/> is NaN, or its day count overflows an integer (an infinite time among them); or
    /// <paramref name="referenceDate"/> is <c>default(Date)</c>.
    /// </exception>
    /// <remarks>
    /// QuantLib casts a NaN time's day count to an integer unchecked, which is undefined
    /// (<c>yearfractiontodate.cpp:40-41</c>): here a NaN is refused on entry, a documented divergence. An infinite or
    /// overflowing day count is refused where QuantLib's <c>boost::numeric_cast</c> throws.
    /// </remarks>
    public static Date YearFractionToDate(DayCounter dayCounter, Date referenceDate, double t)
    {
        ArgumentNullException.ThrowIfNull(dayCounter);
        Date.ThrowIfDefault(referenceDate, nameof(referenceDate));
        if (double.IsNaN(t))
        {
            throw new ArgumentException("the time is NaN", nameof(t));
        }

        Date guessDate = referenceDate + new Period(DaysOf(t * 365.25, t), TimeUnit.Days);
        double guessTime = dayCounter.YearFraction(referenceDate, guessDate);

        guessDate += new Period(DaysOf((t - guessTime) * 365.25, t), TimeUnit.Days);
        guessTime = dayCounter.YearFraction(referenceDate, guessDate);

        if (ComparisonFunctions.CloseEnough(guessTime, t))
        {
            return guessDate;
        }

        int searchDirection = (int)Math.CopySign(1.0, t - guessTime);

        double compT = t + (searchDirection * 500 * NumericLimits.QL_EPSILON);

        Date nextDate;
        foreach (TimeUnit u in (ReadOnlySpan<TimeUnit>)[TimeUnit.Years, TimeUnit.Months, TimeUnit.Days])
        {
            while (searchDirection
                * (dayCounter.YearFraction(referenceDate, nextDate = guessDate + new Period(searchDirection, u))
                    - compT) < 0.0)
            {
                guessDate = nextDate;
            }
        }

        guessTime = dayCounter.YearFraction(referenceDate, guessDate);

        if (ComparisonFunctions.CloseEnough(guessTime, t)
            || Math.Abs(dayCounter.YearFraction(referenceDate, guessDate + new Period(searchDirection, TimeUnit.Days))
                - compT) > Math.Abs(guessTime - compT))
        {
            return guessDate;
        }
        else
        {
            return guessDate + new Period(searchDirection, TimeUnit.Days);
        }
    }

    /// <summary>The date rounded to the start of its day: the date itself.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The date.</returns>
    /// <remarks>
    /// A date has no time of day here, as in QuantLib's default build, whose <c>roundToDayStart</c> returns the date
    /// unchanged; its high-resolution build rounds a date at or past noon to the next day.
    /// </remarks>
    public static Date RoundToDayStart(Date d) => d;

    // QuantLib's boost::numeric_cast<Integer>(round(x)): C's round, half away from zero, then refused where the result
    // is outside an integer's range, as the cast throws "bad numeric conversion".
    private static int DaysOf(double x, double t)
    {
        double r = Math.Round(x, MidpointRounding.AwayFromZero);
        if (!(r >= int.MinValue && r <= int.MaxValue))
        {
            throw new ArgumentException(
                $"the time ({t.ToString("R", CultureInfo.InvariantCulture)}) is too large: its day count, "
                + $"{r.ToString("R", CultureInfo.InvariantCulture)}, overflows an integer",
                nameof(t));
        }

        return (int)r;
    }
}
