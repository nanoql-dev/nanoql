// SPDX-FileCopyrightText: Copyright (C) 2006 Piter Dias
// SPDX-FileCopyrightText: Copyright (C) 2011 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/daycounters/business252.hpp ql/time/daycounters/business252.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>Business/252 day count convention.</summary>
/// <remarks>
/// <para>
/// The day count is the number of business days between two dates on the calendar, by QuantLib's walk over months and
/// years. QuantLib keeps each month's count in a cache keyed by the calendar's name and shared by the whole process,
/// filled once and never invalidated, so a count goes stale after a holiday edit and two calendars of one name share
/// their counts (<c>business252.cpp:31-68</c>). Here the constructor counts each month of 1901 to 2199 once, on its
/// own calendar, into a table no other object shares; the counts are QuantLib's wherever its cache isn't stale, a
/// documented divergence where it is.
/// </para>
/// <para>
/// A month the calendar refuses (a calendar with no data for a year) is left out of the table, with the rest of its
/// year, and counted when a count reads it, as QuantLib's walk counts it, so the calendar's refusal comes where
/// QuantLib's does.
/// </para>
/// <para>
/// Two Business/252 day counters are equal when their calendars are (by type, name, and added and removed holidays),
/// where QuantLib compares their names alone.
/// </para>
/// </remarks>
public sealed class Business252 : DayCounter
{
    private const int FirstYear = 1901;

    // The months 1901 to November 2199: December 2199 is never a whole month of a count, and its end is past Date's
    // range.
    private const int TableMonths = ((2199 - FirstYear + 1) * 12) - 1;

    private readonly Calendar calendar;

    // QuantLib's name, built once: the calendar is immutable, and the equality and the hash read it.
    private readonly string name;

    // cumulative[k]: the business days of the table's months before month k, January 1901 being month 0, a month left
    // to the count adding 0; leftBefore[k]: how many of those months were left to the count, or null where none was.
    private readonly int[] cumulative;
    private readonly int[]? leftBefore;

    /// <summary>
    /// Initializes a new instance of the <see cref="Business252"/> class on the calendar
    /// <see cref="ConventionProvider.ResolveBusiness252Calendar"/> returns: QuantLib's default, the Brazilian settlement
    /// calendar, unless the provider configures another.
    /// </summary>
    public Business252()
        : this(ConventionProvider.ReadBusiness252Calendar(Reference))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Business252"/> class on the given calendar.</summary>
    /// <param name="c">The calendar whose business days are counted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="c"/> is <see langword="null"/>.</exception>
    public Business252(Calendar c)
    {
        ArgumentNullException.ThrowIfNull(c);
        calendar = c;
        name = "Business/252(" + c.Name + ")";
        cumulative = new int[TableMonths + 1];
        List<int>? left = null;
        int total = 0, k = 0;
        for (int y = FirstYear; k < TableMonths; ++y)
        {
            bool yearLeft = false;
            for (int m = 1; m <= 12 && k < TableMonths; ++m, ++k)
            {
                cumulative[k] = total;
                if (!yearLeft)
                {
                    try
                    {
                        total += MonthDays((Month)m, y);
                    }
                    catch (Exception e) when (e is NanoQLException or ArgumentException)
                    {
                        yearLeft = true;
                    }
                }

                if (yearLeft)
                {
                    (left ??= []).Add(k);
                }
            }
        }

        cumulative[TableMonths] = total;
        if (left is not null)
        {
            leftBefore = new int[TableMonths + 1];
            int n = 0;
            for (int i = 0; i < TableMonths; ++i)
            {
                leftBefore[i] = n;
                if (n < left.Count && left[n] == i)
                {
                    ++n;
                }
            }

            leftBefore[TableMonths] = n;
        }
    }

    /// <inheritdoc/>
    public override string Name => name;

    /// <summary>Gets the calendar whose business days are counted.</summary>
    /// <remarks>A nanoql addition: QuantLib keeps the calendar private.</remarks>
    public Calendar Calendar => calendar;

    /// <inheritdoc/>
    public override int DayCount(Date d1, Date d2)
    {
        if (SameMonth(d1, d2) || d1 >= d2)
        {
            // we treat the case of d1 > d2 here, since we'd need a
            // second cache to get it right (our cached figures are
            // for first included, last excluded and might have to be
            // changed going the other way.)
            return calendar.BusinessDaysBetween(d1, d2);
        }
        else if (d1.Year == d2.Year)
        {
            int total = 0;

            // first, we get to the beginning of next month.
            Date d = new Date(1, d1.Month, d1.Year) + new Period(1, TimeUnit.Months);
            total += calendar.BusinessDaysBetween(d1, d);

            // then, we add any whole months in the middle of our period.
            total += WholeMonths(MonthIndex(d.Month, d.Year), MonthIndex(d2.Month, d2.Year));

            // finally, we get to the end of the period.
            d = new Date(1, d2.Month, d2.Year);
            total += calendar.BusinessDaysBetween(d, d2);
            return total;
        }
        else
        {
            int total = 0;

            // first, we get to the beginning of next year.
            // The first bit gets us to the end of this month...
            Date d = new Date(1, d1.Month, d1.Year) + new Period(1, TimeUnit.Months);
            total += calendar.BusinessDaysBetween(d1, d);

            // ...then we add any remaining months
            if (d1.Month != Month.December)
            {
                total += WholeMonths(MonthIndex(d1.Month + 1, d.Year), MonthIndex(Month.January, d.Year + 1));
            }

            // then, we add any whole year in the middle of our period.
            total += WholeMonths(MonthIndex(Month.January, d1.Year + 1), MonthIndex(Month.January, d2.Year));

            // finally, we get to the end of the period.
            // First, we add whole months...
            total += WholeMonths(MonthIndex(Month.January, d2.Year), MonthIndex(d2.Month, d2.Year));

            // ...then the last bit.
            d = new Date(1, d2.Month, d2.Year);
            total += calendar.BusinessDaysBetween(d, d2);
            return total;
        }
    }

    /// <inheritdoc/>
    public override double YearFraction(Date d1, Date d2, Date? refPeriodStart = null, Date? refPeriodEnd = null)
    {
        RequireDates(d1, d2, refPeriodStart, refPeriodEnd);
        return DayCount(d1, d2) / 252.0;
    }

    /// <summary>Whether the day counter equals another: a Business/252 day counter on an equal calendar.</summary>
    /// <param name="other">The other day counter.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    public override bool Equals(DayCounter? other) =>
        other is Business252 b && base.Equals(b) && (ReferenceEquals(this, b) || calendar == b.calendar);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), calendar);

    // QuantLib's default calendar, the reference the provider resolves.
    private static Calendar Reference() => new Brazil();

    private static bool SameMonth(Date d1, Date d2) => d1.Year == d2.Year && d1.Month == d2.Month;

    private static int MonthIndex(Month m, int y) => ((y - FirstYear) * 12) + ((int)m - 1);

    // QuantLib's businessDays(cache, calendar, month, year), counted once, at construction.
    private int MonthDays(Month month, int year)
    {
        var d1 = new Date(1, month, year);
        Date d2 = d1 + new Period(1, TimeUnit.Months);
        return calendar.BusinessDaysBetween(d1, d2);
    }

    // The business days of the whole months from month i to month j - 1, counted from January 1901: one difference of
    // the table's sums, unless the span holds a month left to the count, when each month is read in order and the one
    // left counted as QuantLib's walk counts it.
    private int WholeMonths(int i, int j)
    {
        if (j <= i)
        {
            return 0;
        }

        if (leftBefore is null || leftBefore[j] == leftBefore[i])
        {
            return cumulative[j] - cumulative[i];
        }

        int total = 0;
        for (int k = i; k < j; ++k)
        {
            total += leftBefore[k + 1] > leftBefore[k]
                ? MonthDays((Month)((k % 12) + 1), FirstYear + (k / 12))
                : cumulative[k + 1] - cumulative[k];
        }

        return total;
    }
}
