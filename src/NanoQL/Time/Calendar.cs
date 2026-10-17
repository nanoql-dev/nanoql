// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006 Piter Dias
// SPDX-FileCopyrightText: Copyright (C) 2020 Leonardo Arcari
// SPDX-FileCopyrightText: Copyright (C) 2020 Kline s.r.l.
// SPDX-FileCopyrightText: Copyright (C) 2004 Jeff Yu
// SPDX-FileCopyrightText: Copyright (C) 2014 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendar.hpp ql/time/calendar.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Time;

/// <summary>
/// A calendar: whether a date is a business day or a holiday in a given market, and moving a date by business days.
/// </summary>
/// <remarks>
/// <para>
/// A calendar is immutable. <see cref="AddHoliday"/>, <see cref="RemoveHoliday"/> and
/// <see cref="ResetAddedAndRemovedHolidays"/> return a modified copy and leave the receiver unchanged. In QuantLib the
/// change applies process-wide, because every instance of a calendar shares one implementation.
/// </para>
/// <para>
/// Two calendars are equal when they are of the same type and have the same name and the same added and removed
/// holidays, compared by content; the type keeps equality symmetric between a calendar and a subclass that compares
/// more, such as <see cref="JointCalendar"/>. QuantLib's empty calendar, <c>Calendar()</c>, is <see langword="null"/>.
/// </para>
/// </remarks>
public abstract class Calendar : IEquatable<Calendar>
{
    // Written only on a fresh copy (AddHoliday, RemoveHoliday, ResetAddedAndRemovedHolidays).
    private ImmutableSortedSet<Date> addedHolidays = ImmutableSortedSet<Date>.Empty;
    private ImmutableSortedSet<Date> removedHolidays = ImmutableSortedSet<Date>.Empty;

    /// <summary>Gets the calendar's name, used for output and comparison; not meant for switch-on-type code.</summary>
    public abstract string Name { get; }

    /// <summary>Gets the holidays added to the calendar's rules.</summary>
    public ImmutableSortedSet<Date> AddedHolidays => addedHolidays;

    /// <summary>Gets the holidays removed from the calendar's rules.</summary>
    public ImmutableSortedSet<Date> RemovedHolidays => removedHolidays;

    /// <summary>Whether two calendars are equal: the same name and the same added and removed holidays.</summary>
    /// <param name="c1">The first calendar.</param>
    /// <param name="c2">The second calendar.</param>
    /// <returns><see langword="true"/> when equal, or both <see langword="null"/>.</returns>
    public static bool operator ==(Calendar? c1, Calendar? c2) => c1 is null ? c2 is null : c1.Equals(c2);

    /// <summary>Whether two calendars differ.</summary>
    /// <param name="c1">The first calendar.</param>
    /// <param name="c2">The second calendar.</param>
    /// <returns><see langword="true"/> when not equal.</returns>
    public static bool operator !=(Calendar? c1, Calendar? c2) => !(c1 == c2);

    /// <summary>Whether the date is a business day for the given market.</summary>
    /// <param name="d">The date.</param>
    /// <returns><see langword="true"/> for a business day.</returns>
    public bool IsBusinessDay(Date d)
    {
        Date.ThrowIfDefault(d, nameof(d));
        if (!addedHolidays.IsEmpty && addedHolidays.Contains(d))
        {
            return false;
        }

        if (!removedHolidays.IsEmpty && removedHolidays.Contains(d))
        {
            return true;
        }

        return IsBusinessDayCore(d);
    }

    /// <summary>Whether the date is a holiday for the given market.</summary>
    /// <param name="d">The date.</param>
    /// <returns><see langword="true"/> for a holiday.</returns>
    public bool IsHoliday(Date d) => !IsBusinessDay(d);

    /// <summary>Whether the weekday is part of the weekend for the given market.</summary>
    /// <param name="w">The weekday.</param>
    /// <returns><see langword="true"/> for a weekend day.</returns>
    public abstract bool IsWeekend(Weekday w);

    /// <summary>Whether, in the given market, the date is on or before the month's first business day.</summary>
    /// <param name="d">The date.</param>
    /// <returns><see langword="true"/> on or before the first business day.</returns>
    public bool IsStartOfMonth(Date d) => d <= StartOfMonth(d);

    /// <summary>The first business day of the month to which the given date belongs.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The first business day.</returns>
    public Date StartOfMonth(Date d) => Adjust(Date.StartOfMonth(d), BusinessDayConvention.Following);

    /// <summary>Whether, in the given market, the date is on or after the month's last business day.</summary>
    /// <param name="d">The date.</param>
    /// <returns><see langword="true"/> on or after the last business day.</returns>
    public bool IsEndOfMonth(Date d) => d >= EndOfMonth(d);

    /// <summary>The last business day of the month to which the given date belongs.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The last business day.</returns>
    public Date EndOfMonth(Date d) => Adjust(Date.EndOfMonth(d), BusinessDayConvention.Preceding);

    /// <summary>A copy of the calendar with the date added to its holidays.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The modified copy; the receiver is unchanged.</returns>
    public Calendar AddHoliday(Date d)
    {
        Date.ThrowIfDefault(d, nameof(d));
        var copy = (Calendar)MemberwiseClone();

        // if d was a genuine holiday previously removed, revert the change
        copy.removedHolidays = removedHolidays.Remove(d);

        // if it's already a holiday, leave the calendar alone. Otherwise, add it.
        if (IsBusinessDayCore(d))
        {
            copy.addedHolidays = addedHolidays.Add(d);
        }

        return copy;
    }

    /// <summary>A copy of the calendar with the date removed from its holidays.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The modified copy; the receiver is unchanged.</returns>
    public Calendar RemoveHoliday(Date d)
    {
        Date.ThrowIfDefault(d, nameof(d));
        var copy = (Calendar)MemberwiseClone();

        // if d was an artificially-added holiday, revert the change
        copy.addedHolidays = addedHolidays.Remove(d);

        // if it's already a business day, leave the calendar alone. Otherwise, add it.
        if (!IsBusinessDayCore(d))
        {
            copy.removedHolidays = removedHolidays.Add(d);
        }

        return copy;
    }

    /// <summary>A copy of the calendar with no added or removed holidays.</summary>
    /// <returns>The modified copy; the receiver is unchanged.</returns>
    public Calendar ResetAddedAndRemovedHolidays()
    {
        var copy = (Calendar)MemberwiseClone();
        copy.addedHolidays = ImmutableSortedSet<Date>.Empty;
        copy.removedHolidays = ImmutableSortedSet<Date>.Empty;
        return copy;
    }

    /// <summary>The holidays between two dates, both included.</summary>
    /// <param name="from">The first date.</param>
    /// <param name="to">The last date.</param>
    /// <param name="includeWeekEnds">Whether weekend days are listed too.</param>
    /// <returns>The holidays, in order.</returns>
    public ImmutableArray<Date> HolidayList(Date from, Date to, bool includeWeekEnds = false)
    {
        Date.ThrowIfDefault(from, nameof(from));
        Date.ThrowIfDefault(to, nameof(to));
        var result = ImmutableArray.CreateBuilder<Date>();
        for (Date d = from; d <= to; ++d)
        {
            if (IsHoliday(d) && (includeWeekEnds || !IsWeekend(d.Weekday)))
            {
                result.Add(d);
            }
        }

        return result.ToImmutable();
    }

    /// <summary>The business days between two dates, both included.</summary>
    /// <param name="from">The first date.</param>
    /// <param name="to">The last date.</param>
    /// <returns>The business days, in order.</returns>
    public ImmutableArray<Date> BusinessDayList(Date from, Date to)
    {
        Date.ThrowIfDefault(from, nameof(from));
        Date.ThrowIfDefault(to, nameof(to));
        var result = ImmutableArray.CreateBuilder<Date>();
        for (Date d = from; d <= to; ++d)
        {
            if (IsBusinessDay(d))
            {
                result.Add(d);
            }
        }

        return result.ToImmutable();
    }

    /// <summary>Adjusts a non-business day to a nearby business day by the given convention.</summary>
    /// <param name="d">The date.</param>
    /// <param name="c">The business-day convention.</param>
    /// <returns>The adjusted date.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public Date Adjust(Date d, BusinessDayConvention c = BusinessDayConvention.Following)
    {
        RequireDate(d, nameof(d));
        if (c == BusinessDayConvention.Unadjusted)
        {
            return d;
        }

        Date d1 = d;
        if (c is BusinessDayConvention.Following or BusinessDayConvention.ModifiedFollowing
            or BusinessDayConvention.HalfMonthModifiedFollowing)
        {
            while (IsHoliday(d1))
            {
                ++d1;
            }

            if (c is BusinessDayConvention.ModifiedFollowing or BusinessDayConvention.HalfMonthModifiedFollowing)
            {
                if (d1.Month != d.Month)
                {
                    return Adjust(d, BusinessDayConvention.Preceding);
                }

                if (c == BusinessDayConvention.HalfMonthModifiedFollowing && d.DayOfMonth <= 15 && d1.DayOfMonth > 15)
                {
                    return Adjust(d, BusinessDayConvention.Preceding);
                }
            }
        }
        else if (c is BusinessDayConvention.Preceding or BusinessDayConvention.ModifiedPreceding)
        {
            while (IsHoliday(d1))
            {
                --d1;
            }

            if (c == BusinessDayConvention.ModifiedPreceding && d1.Month != d.Month)
            {
                return Adjust(d, BusinessDayConvention.Following);
            }
        }
        else if (c == BusinessDayConvention.Nearest)
        {
            Date d2 = d;
            while (IsHoliday(d1) && IsHoliday(d2))
            {
                ++d1;
                --d2;
            }

            return IsHoliday(d1) ? d2 : d1;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(c), c, "unknown business-day convention");
        }

        return d1;
    }

    /// <summary>Advances a date by a number of time units, moving by business days for <see cref="TimeUnit.Days"/>.</summary>
    /// <param name="d">The date.</param>
    /// <param name="n">The number of units.</param>
    /// <param name="unit">The time unit.</param>
    /// <param name="c">The business-day convention applied to the result.</param>
    /// <param name="endOfMonth">Whether a month-end date stays at month end.</param>
    /// <returns>The advanced date; the input is not modified.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The unit or the convention isn't a member of its enum.</exception>
    public Date Advance(Date d, int n, TimeUnit unit, BusinessDayConvention c = BusinessDayConvention.Following, bool endOfMonth = false)
    {
        RequireDate(d, nameof(d));
        EnumArguments.Check(unit, nameof(unit));
        EnumArguments.Check(c, nameof(c));
        if (n == 0)
        {
            return Adjust(d, c);
        }

        if (unit == TimeUnit.Days)
        {
            Date d1 = d;
            if (n > 0)
            {
                while (n > 0)
                {
                    ++d1;
                    while (IsHoliday(d1))
                    {
                        ++d1;
                    }

                    --n;
                }
            }
            else
            {
                while (n < 0)
                {
                    --d1;
                    while (IsHoliday(d1))
                    {
                        --d1;
                    }

                    ++n;
                }
            }

            return d1;
        }

        if (unit == TimeUnit.Weeks)
        {
            return Adjust(d + new Period(n, unit), c);
        }

        Date result = d + new Period(n, unit);

        // we are sure the unit is Months or Years
        if (endOfMonth)
        {
            if (c == BusinessDayConvention.Unadjusted)
            {
                // move to the last calendar day if d is the last calendar day
                if (Date.IsEndOfMonth(d))
                {
                    return Date.EndOfMonth(result);
                }
            }
            else if (IsEndOfMonth(d))
            {
                // move to the last business day if d is the last business day
                return EndOfMonth(result);
            }
        }

        return Adjust(result, c);
    }

    /// <summary>Advances a date by a period, moving by business days for a period in days.</summary>
    /// <param name="date">The date.</param>
    /// <param name="period">The period.</param>
    /// <param name="c">The business-day convention applied to the result.</param>
    /// <param name="endOfMonth">Whether a month-end date stays at month end.</param>
    /// <returns>The advanced date; the input is not modified.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public Date Advance(Date date, Period period, BusinessDayConvention c = BusinessDayConvention.Following, bool endOfMonth = false) =>
        Advance(date, period.Length, period.Units, c, endOfMonth);

    /// <summary>The number of business days between two dates.</summary>
    /// <param name="from">The first date.</param>
    /// <param name="to">The second date.</param>
    /// <param name="includeFirst">Whether <paramref name="from"/> counts.</param>
    /// <param name="includeLast">Whether <paramref name="to"/> counts.</param>
    /// <returns>The count, negative when <paramref name="to"/> is earlier.</returns>
    public int BusinessDaysBetween(Date from, Date to, bool includeFirst = true, bool includeLast = false)
    {
        Date.ThrowIfDefault(from, nameof(from));
        Date.ThrowIfDefault(to, nameof(to));
        if (from < to)
        {
            return DaysBetweenImpl(from, to, includeFirst, includeLast);
        }

        if (from > to)
        {
            return -DaysBetweenImpl(to, from, includeLast, includeFirst);
        }

        return includeFirst && includeLast && IsBusinessDay(from) ? 1 : 0;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as Calendar);

    /// <summary>
    /// Whether the calendar is of the same kind and has the same name and the same added and removed holidays as
    /// another.
    /// </summary>
    /// <param name="other">The other calendar.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    /// <remarks>
    /// Comparing the runtime types keeps equality symmetric when a derived calendar compares more than this does
    /// (a joint calendar also compares its rule and its components).
    /// </remarks>
    public virtual bool Equals(Calendar? other) =>
        other is not null
        && (ReferenceEquals(this, other)
            || (GetType() == other.GetType()
                && Name == other.Name
                && addedHolidays.SetEquals(other.addedHolidays)
                && removedHolidays.SetEquals(other.removedHolidays)));

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Name);
        foreach (Date d in addedHolidays)
        {
            hash.Add(d);
        }

        hash.Add(-1);
        foreach (Date d in removedHolidays)
        {
            hash.Add(d);
        }

        return hash.ToHashCode();
    }

    /// <summary>The calendar's name.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => Name;

    /// <summary>Whether the date is a business day by the calendar's own rules, before added and removed holidays.</summary>
    /// <param name="d">The date.</param>
    /// <returns><see langword="true"/> for a business day.</returns>
    protected abstract bool IsBusinessDayCore(Date d);

    // QuantLib's QL_REQUIRE(d != Date(), "null date") in adjust and advance, with its wording.
    private static void RequireDate(Date d, string paramName)
    {
        if (d.SerialNumber == 0)
        {
            throw new ArgumentException("null date", paramName);
        }
    }

    // Requires: from < to.
    private int DaysBetweenImpl(Date from, Date to, bool includeFirst, bool includeLast)
    {
        int res = includeLast && IsBusinessDay(to) ? 1 : 0;
        for (Date d = includeFirst ? from : from + 1; d < to; ++d)
        {
            res += IsBusinessDay(d) ? 1 : 0;
        }

        return res;
    }

    /// <summary>The Western calendars' weekend (Saturday and Sunday) and Easter Monday.</summary>
    protected static class WesternImpl
    {
        private static readonly byte[] EasterMondays =
        [
                 98,  90, 103,  95, 114, 106,  91, 111, 102, // 1901-1909
            87, 107,  99,  83, 103,  95, 115,  99,  91, 111, // 1910-1919
            96,  87, 107,  92, 112, 103,  95, 108, 100,  91, // 1920-1929
            111,  96,  88, 107,  92, 112, 104,  88, 108, 100, // 1930-1939
            85, 104,  96, 116, 101,  92, 112,  97,  89, 108, // 1940-1949
            100,  85, 105,  96, 109, 101,  93, 112,  97,  89, // 1950-1959
            109,  93, 113, 105,  90, 109, 101,  86, 106,  97, // 1960-1969
            89, 102,  94, 113, 105,  90, 110, 101,  86, 106, // 1970-1979
            98, 110, 102,  94, 114,  98,  90, 110,  95,  86, // 1980-1989
            106,  91, 111, 102,  94, 107,  99,  90, 103,  95, // 1990-1999
            115, 106,  91, 111, 103,  87, 107,  99,  84, 103, // 2000-2009
            95, 115, 100,  91, 111,  96,  88, 107,  92, 112, // 2010-2019
            104,  95, 108, 100,  92, 111,  96,  88, 108,  92, // 2020-2029
            112, 104,  89, 108, 100,  85, 105,  96, 116, 101, // 2030-2039
            93, 112,  97,  89, 109, 100,  85, 105,  97, 109, // 2040-2049
            101,  93, 113,  97,  89, 109,  94, 113, 105,  90, // 2050-2059
            110, 101,  86, 106,  98,  89, 102,  94, 114, 105, // 2060-2069
            90, 110, 102,  86, 106,  98, 111, 102,  94, 114, // 2070-2079
            99,  90, 110,  95,  87, 106,  91, 111, 103,  94, // 2080-2089
            107,  99,  91, 103,  95, 115, 107,  91, 111, 103, // 2090-2099
            88, 108, 100,  85, 105,  96, 109, 101,  93, 112, // 2100-2109
            97,  89, 109,  93, 113, 105,  90, 109, 101,  86, // 2110-2119
            106,  97,  89, 102,  94, 113, 105,  90, 110, 101, // 2120-2129
            86, 106,  98, 110, 102,  94, 114,  98,  90, 110, // 2130-2139
            95,  86, 106,  91, 111, 102,  94, 107,  99,  90, // 2140-2149
            103,  95, 115, 106,  91, 111, 103,  87, 107,  99, // 2150-2159
            84, 103,  95, 115, 100,  91, 111,  96,  88, 107, // 2160-2169
            92, 112, 104,  95, 108, 100,  92, 111,  96,  88, // 2170-2179
            108,  92, 112, 104,  89, 108, 100,  85, 105,  96, // 2180-2189
            116, 101,  93, 112,  97,  89, 109, 100,  85, 105, // 2190-2199
        ];

        /// <summary>Whether the weekday is a Western weekend day: Saturday or Sunday.</summary>
        /// <param name="w">The weekday.</param>
        /// <returns><see langword="true"/> on Saturday and Sunday.</returns>
        public static bool IsWeekend(Weekday w) => w is Weekday.Saturday or Weekday.Sunday;

        /// <summary>Easter Monday, as a day of the year.</summary>
        /// <param name="y">The year, 1901 to 2199.</param>
        /// <returns>The day of the year, one-based.</returns>
        public static int EasterMonday(int y) => EasterMondays[y - 1901];
    }

    /// <summary>The Orthodox calendars' weekend (Saturday and Sunday) and Orthodox Easter Monday.</summary>
    protected static class OrthodoxImpl
    {
        private static readonly byte[] EasterMondays =
        [
                 105, 118, 110, 102, 121, 106, 126, 118, 102, // 1901-1909
            122, 114,  99, 118, 110,  95, 115, 106, 126, 111, // 1910-1919
            103, 122, 107,  99, 119, 110, 123, 115, 107, 126, // 1920-1929
            111, 103, 123, 107,  99, 119, 104, 123, 115, 100, // 1930-1939
            120, 111,  96, 116, 108, 127, 112, 104, 124, 115, // 1940-1949
            100, 120, 112,  96, 116, 108, 128, 112, 104, 124, // 1950-1959
            109, 100, 120, 105, 125, 116, 101, 121, 113, 104, // 1960-1969
            117, 109, 101, 120, 105, 125, 117, 101, 121, 113, // 1970-1979
            98, 117, 109, 129, 114, 105, 125, 110, 102, 121, // 1980-1989
            106,  98, 118, 109, 122, 114, 106, 118, 110, 102, // 1990-1999
            122, 106, 126, 118, 103, 122, 114,  99, 119, 110, // 2000-2009
            95, 115, 107, 126, 111, 103, 123, 107,  99, 119, // 2010-2019
            111, 123, 115, 107, 127, 111, 103, 123, 108,  99, // 2020-2029
            119, 104, 124, 115, 100, 120, 112,  96, 116, 108, // 2030-2039
            128, 112, 104, 124, 116, 100, 120, 112,  97, 116, // 2040-2049
            108, 128, 113, 104, 124, 109, 101, 120, 105, 125, // 2050-2059
            117, 101, 121, 113, 105, 117, 109, 101, 121, 105, // 2060-2069
            125, 110, 102, 121, 113,  98, 118, 109, 129, 114, // 2070-2079
            106, 125, 110, 102, 122, 106,  98, 118, 110, 122, // 2080-2089
            114,  99, 119, 110, 102, 115, 107, 126, 118, 103, // 2090-2099
            123, 115, 100, 120, 112,  96, 116, 108, 128, 112, // 2100-2109
            104, 124, 109, 100, 120, 105, 125, 116, 108, 121, // 2110-2119
            113, 104, 124, 109, 101, 120, 105, 125, 117, 101, // 2120-2129
            121, 113,  98, 117, 109, 129, 114, 105, 125, 110, // 2130-2139
            102, 121, 113,  98, 118, 109, 129, 114, 106, 125, // 2140-2149
            110, 102, 122, 106, 126, 118, 103, 122, 114,  99, // 2150-2159
            119, 110, 102, 115, 107, 126, 111, 103, 123, 114, // 2160-2169
            99, 119, 111, 130, 115, 107, 127, 111, 103, 123, // 2170-2179
            108,  99, 119, 104, 124, 115, 100, 120, 112, 103, // 2180-2189
            116, 108, 128, 119, 104, 124, 116, 100, 120, 112, // 2190-2199
        ];

        /// <summary>Whether the weekday is a weekend day: Saturday or Sunday.</summary>
        /// <param name="w">The weekday.</param>
        /// <returns><see langword="true"/> on Saturday and Sunday.</returns>
        public static bool IsWeekend(Weekday w) => w is Weekday.Saturday or Weekday.Sunday;

        /// <summary>Orthodox Easter Monday, as a day of the year.</summary>
        /// <param name="y">The year, 1901 to 2199.</param>
        /// <returns>The day of the year, one-based.</returns>
        public static int EasterMonday(int y) => EasterMondays[y - 1901];
    }
}
