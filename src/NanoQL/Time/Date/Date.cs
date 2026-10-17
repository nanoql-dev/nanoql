// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2006 Toyin Akin
// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2020 Leonardo Arcari
// SPDX-FileCopyrightText: Copyright (C) 2020 Kline s.r.l.
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/date.hpp ql/time/date.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: io::formatted_date (not ported: a Date converts to DateOnly, whose ToString(format) does its job)

namespace NanoQL.Time;

/// <summary>
/// A date, held as QuantLib's serial number: 367 (January 1st, 1901) to 109574 (December 31st, 2199), Excel's
/// numbering. It provides inspectors, a limited date algebra, and conversion to and from <see cref="DateOnly"/>.
/// </summary>
/// <remarks>
/// QuantLib's null date, <c>Date()</c>, is <see cref="Nullable{T}"/> null in nanoql. <c>default(Date)</c> (serial 0) is
/// never a valid date: no constructor produces it, and members that read it throw <see cref="ArgumentException"/>.
/// Equality, ordering, hashing and <see cref="SerialNumber"/> never throw.
/// </remarks>
public readonly struct Date : IEquatable<Date>, IComparable<Date>, IComparable
{
    private const int MinimumSerialNumber = 367;
    private const int MaximumSerialNumber = 109574;

    private static readonly int[] MonthLengths = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
    private static readonly int[] MonthLeapLengths = [31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];

    // Offsets of each month's first day, with a thirteenth entry used in dayOfMonth to bracket the day.
    private static readonly int[] MonthOffsets = [0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334, 365];
    private static readonly int[] MonthLeapOffsets = [0, 31, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335, 366];

    // QuantLib's YearOffset table, 1900 to 2200: the serial number of December 31st of the year before.
    private static readonly int[] YearOffsets = BuildYearOffsets();

    // Excel's (and QuantLib's) serial 0, so that serial n is this date plus n days from March 1st, 1900 on.
    private static readonly DateOnly SerialEpoch = new(1899, 12, 30);

    private readonly int serialNumber;

    /// <summary>Initializes a new instance of the <see cref="Date"/> struct from a serial number, as Excel numbers dates.</summary>
    /// <param name="serialNumber">The serial number, 367 to 109574.</param>
    /// <exception cref="ArgumentOutOfRangeException">The serial number is outside the allowed range.</exception>
    public Date(int serialNumber)
    {
        CheckSerialNumber(serialNumber, nameof(serialNumber));
        this.serialNumber = serialNumber;
    }

    /// <summary>Initializes a new instance of the <see cref="Date"/> struct from a day, a month and a year.</summary>
    /// <param name="d">The day of the month.</param>
    /// <param name="m">The month.</param>
    /// <param name="y">The year, 1901 to 2199.</param>
    /// <exception cref="ArgumentOutOfRangeException">The year, month or day is out of range.</exception>
    public Date(int d, Month m, int y)
    {
        if (!(y > 1900 && y < 2200))
        {
            throw new ArgumentOutOfRangeException(nameof(y), $"year {y} out of bound. It must be in [1901,2199]");
        }

        if (!((int)m > 0 && (int)m < 13))
        {
            throw new ArgumentOutOfRangeException(nameof(m), $"month {(int)m} outside January-December range [1,12]");
        }

        bool leap = IsLeap(y);
        int len = MonthLength(m, leap), offset = MonthOffset(m, leap);
        if (!(d <= len && d > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(d), $"day outside month ({(int)m}) day-range [1,{len}]");
        }

        serialNumber = d + offset + YearOffset(y);
    }

    /// <summary>Gets the earliest allowed date, January 1st, 1901.</summary>
    public static Date MinDate => new(MinimumSerialNumber);

    /// <summary>Gets the latest allowed date, December 31st, 2199.</summary>
    public static Date MaxDate => new(MaximumSerialNumber);

    /// <summary>Gets the serial number; 0 for <c>default(Date)</c>.</summary>
    public int SerialNumber => serialNumber;

    /// <summary>Gets the day of the week.</summary>
    public Weekday Weekday
    {
        get
        {
            int w = Serial % 7;
            return (Weekday)(w == 0 ? 7 : w);
        }
    }

    /// <summary>Gets the day of the month.</summary>
    public int DayOfMonth => DayOfYear - MonthOffset(Month, IsLeap(Year));

    /// <summary>Gets the day of the year, one-based (January 1st = 1).</summary>
    public int DayOfYear => Serial - YearOffset(Year);

    /// <summary>Gets the month.</summary>
    public Month Month
    {
        get
        {
            int d = DayOfYear; // one-based
            int m = (d / 30) + 1;
            bool leap = IsLeap(Year);
            while (d <= MonthOffset((Month)m, leap))
            {
                --m;
            }

            while (d > MonthOffset((Month)(m + 1), leap))
            {
                ++m;
            }

            return (Month)m;
        }
    }

    /// <summary>Gets the year.</summary>
    public int Year
    {
        get
        {
            int y = (Serial / 365) + 1900;
            if (serialNumber <= YearOffset(y))
            {
                --y;
            }

            return y;
        }
    }

    private const string DefaultDateMessage = "default(Date) is not a valid date; a missing date is Date? null";

    private int Serial => serialNumber != 0 ? serialNumber : throw new ArgumentException(DefaultDateMessage);

    /// <summary>Returns a new date incremented by the given number of days.</summary>
    /// <param name="d">The date.</param>
    /// <param name="days">The number of days.</param>
    /// <returns>The new date.</returns>
    public static Date operator +(Date d, int days) => FromSerial((long)d.Serial + days, nameof(days));

    /// <summary>Returns a new date decremented by the given number of days.</summary>
    /// <param name="d">The date.</param>
    /// <param name="days">The number of days.</param>
    /// <returns>The new date.</returns>
    public static Date operator -(Date d, int days) => FromSerial((long)d.Serial - days, nameof(days));

    /// <summary>Returns a new date incremented by the given period.</summary>
    /// <param name="d">The date.</param>
    /// <param name="p">The period.</param>
    /// <returns>The new date.</returns>
    public static Date operator +(Date d, Period p) => Advance(d, p.Length, p.Units);

    /// <summary>Returns a new date decremented by the given period.</summary>
    /// <param name="d">The date.</param>
    /// <param name="p">The period.</param>
    /// <returns>The new date.</returns>
    public static Date operator -(Date d, Period p) => Advance(d, -p.Length, p.Units);

    /// <summary>Returns the difference in days between two dates.</summary>
    /// <param name="d1">The later date.</param>
    /// <param name="d2">The earlier date.</param>
    /// <returns>The difference in days.</returns>
    public static int operator -(Date d1, Date d2) => d1.Serial - d2.Serial;

    /// <summary>Returns the next day.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The date one day later.</returns>
    public static Date operator ++(Date d) => d + 1;

    /// <summary>Returns the previous day.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The date one day earlier.</returns>
    public static Date operator --(Date d) => d - 1;

    /// <summary>Whether two dates are the same.</summary>
    /// <param name="d1">The first date.</param>
    /// <param name="d2">The second date.</param>
    /// <returns><see langword="true"/> when the serial numbers are equal.</returns>
    public static bool operator ==(Date d1, Date d2) => d1.serialNumber == d2.serialNumber;

    /// <summary>Whether two dates differ.</summary>
    /// <param name="d1">The first date.</param>
    /// <param name="d2">The second date.</param>
    /// <returns><see langword="true"/> when the serial numbers differ.</returns>
    public static bool operator !=(Date d1, Date d2) => d1.serialNumber != d2.serialNumber;

    /// <summary>Whether a date is earlier than another.</summary>
    /// <param name="d1">The first date.</param>
    /// <param name="d2">The second date.</param>
    /// <returns><see langword="true"/> when <paramref name="d1"/> is earlier.</returns>
    public static bool operator <(Date d1, Date d2) => d1.serialNumber < d2.serialNumber;

    /// <summary>Whether a date is earlier than or equal to another.</summary>
    /// <param name="d1">The first date.</param>
    /// <param name="d2">The second date.</param>
    /// <returns><see langword="true"/> when <paramref name="d1"/> is not later.</returns>
    public static bool operator <=(Date d1, Date d2) => d1.serialNumber <= d2.serialNumber;

    /// <summary>Whether a date is later than another.</summary>
    /// <param name="d1">The first date.</param>
    /// <param name="d2">The second date.</param>
    /// <returns><see langword="true"/> when <paramref name="d1"/> is later.</returns>
    public static bool operator >(Date d1, Date d2) => d1.serialNumber > d2.serialNumber;

    /// <summary>Whether a date is later than or equal to another.</summary>
    /// <param name="d1">The first date.</param>
    /// <param name="d2">The second date.</param>
    /// <returns><see langword="true"/> when <paramref name="d1"/> is not earlier.</returns>
    public static bool operator >=(Date d1, Date d2) => d1.serialNumber >= d2.serialNumber;

    /// <summary>Today's date, from the clock's local time.</summary>
    /// <param name="timeProvider">The clock; <see cref="TimeProvider.System"/> when <see langword="null"/>.</param>
    /// <returns>Today's date.</returns>
    /// <remarks>No library code calls it: the evaluation date is an explicit input.</remarks>
    public static Date TodaysDate(TimeProvider? timeProvider = null)
    {
        DateTimeOffset now = (timeProvider ?? TimeProvider.System).GetLocalNow();
        return new Date(now.Day, (Month)now.Month, now.Year);
    }

    /// <summary>Whether the given year is a leap one.</summary>
    /// <param name="y">The year, 1900 to 2200.</param>
    /// <returns><see langword="true"/> for a leap year.</returns>
    /// <remarks>As in QuantLib (and Excel), 1900 counts as a leap year.</remarks>
    public static bool IsLeap(int y)
    {
        if (!(y >= 1900 && y <= 2200))
        {
            throw new ArgumentOutOfRangeException(nameof(y), "year outside valid range");
        }

        return y == 1900 || (y % 4 == 0 && (y % 100 != 0 || y % 400 == 0));
    }

    /// <summary>The first day of the month to which the given date belongs.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The first day of its month.</returns>
    public static Date StartOfMonth(Date d) => new(1, d.Month, d.Year);

    /// <summary>Whether a date is the first day of its month.</summary>
    /// <param name="d">The date.</param>
    /// <returns><see langword="true"/> on the first day.</returns>
    public static bool IsStartOfMonth(Date d) => d.DayOfMonth == 1;

    /// <summary>The last day of the month to which the given date belongs.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The last day of its month.</returns>
    public static Date EndOfMonth(Date d)
    {
        Month m = d.Month;
        int y = d.Year;
        return new Date(MonthLength(m, IsLeap(y)), m, y);
    }

    /// <summary>Whether a date is the last day of its month.</summary>
    /// <param name="d">The date.</param>
    /// <returns><see langword="true"/> on the last day.</returns>
    public static bool IsEndOfMonth(Date d) => d.DayOfMonth == MonthLength(d.Month, IsLeap(d.Year));

    /// <summary>The next given weekday following or equal to the given date.</summary>
    /// <param name="d">The date.</param>
    /// <param name="w">The weekday.</param>
    /// <returns>The date; e.g., the Friday following Tuesday, January 15th, 2002 was January 18th, 2002.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The weekday isn't a member of the enum.</exception>
    public static Date NextWeekday(Date d, Weekday w)
    {
        EnumArguments.Check(w, nameof(w));
        Weekday wd = d.Weekday;
        return d + ((wd > w ? 7 : 0) - (int)wd + (int)w);
    }

    /// <summary>The n-th given weekday in the given month and year.</summary>
    /// <param name="n">Which occurrence, 1 to 5.</param>
    /// <param name="w">The weekday.</param>
    /// <param name="m">The month.</param>
    /// <param name="y">The year.</param>
    /// <returns>The date; e.g., the 4th Thursday of March, 1998 was March 26th, 1998.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="n"/> isn't 1 to 5, or the weekday or the month isn't a member of its enum.
    /// </exception>
    public static Date NthWeekday(int n, Weekday w, Month m, int y)
    {
        if (!(n > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(n), "zeroth day of week in a given (month, year) is undefined");
        }

        if (!(n < 6))
        {
            throw new ArgumentOutOfRangeException(nameof(n), "no more than 5 weekday in a given (month, year)");
        }

        EnumArguments.Check(w, nameof(w));
        Weekday first = new Date(1, m, y).Weekday;
        int skip = n - (w >= first ? 1 : 0);
        return new Date((1 + (int)w + (skip * 7)) - (int)first, m, y);
    }

    /// <summary>Converts a <see cref="DateOnly"/> to a date.</summary>
    /// <param name="date">The date, January 1st, 1901 to December 31st, 2199.</param>
    /// <returns>The same day as a <see cref="Date"/>.</returns>
    public static Date FromDateOnly(DateOnly date) => FromSerial((long)date.DayNumber - SerialEpoch.DayNumber, nameof(date));

    /// <summary>Converts the date to a <see cref="DateOnly"/>.</summary>
    /// <returns>The same day as a <see cref="DateOnly"/>.</returns>
    public DateOnly ToDateOnly() => DateOnly.FromDayNumber(SerialEpoch.DayNumber + Serial);

    /// <summary>
    /// Refuses <c>default(Date)</c> where a member takes a date without reading its fields, which would otherwise throw.
    /// </summary>
    /// <param name="d">The date.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentException">The date is <c>default(Date)</c>.</exception>
    internal static void ThrowIfDefault(Date d, string paramName)
    {
        if (d.serialNumber == 0)
        {
            throw new ArgumentException(DefaultDateMessage, paramName);
        }
    }

    /// <summary>Refuses <c>default(Date)</c> in an optional date; <see langword="null"/> passes.</summary>
    /// <param name="d">The date, or <see langword="null"/>.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentException">The date is <c>default(Date)</c>.</exception>
    internal static void ThrowIfDefault(Date? d, string paramName)
    {
        if (d is Date date)
        {
            ThrowIfDefault(date, paramName);
        }
    }

    /// <inheritdoc/>
    public bool Equals(Date other) => serialNumber == other.serialNumber;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Date other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => serialNumber.GetHashCode();

    /// <inheritdoc/>
    public int CompareTo(Date other) => serialNumber.CompareTo(other.serialNumber);

    /// <inheritdoc/>
    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        Date other => CompareTo(other),
        _ => throw new ArgumentException($"Object must be of type {nameof(Date)}.", nameof(obj)),
    };

    /// <summary>The date in QuantLib's long format, e.g. "October 15th, 2026".</summary>
    /// <returns>The long-format text.</returns>
    public override string ToString() => serialNumber == 0 ? "null date" : DateFunctions.LongDate(this);

    private static Date FromSerial(long serial, string paramName)
    {
        CheckSerialNumber(serial, paramName);
        return new Date((int)serial);
    }

    private static void CheckSerialNumber(long serial, string paramName)
    {
        if (!(serial >= MinimumSerialNumber && serial <= MaximumSerialNumber))
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                $"Date's serial number ({serial}) outside allowed range [{MinimumSerialNumber}-{MaximumSerialNumber}], "
                + $"i.e. [{MinDate}-{MaxDate}]");
        }
    }

    private static Date Advance(Date date, int n, TimeUnit units)
    {
        switch (units)
        {
            case TimeUnit.Days:
                return FromSerial((long)date.Serial + n, nameof(n));
            case TimeUnit.Weeks:
                return FromSerial(date.Serial + (7L * n), nameof(n));
            case TimeUnit.Months:
            {
                int d = date.DayOfMonth;
                long m = (long)date.Month + n;
                long y = date.Year;

                // QuantLib's loops of +/-12, as a floor division.
                long years = m > 12 ? (m - 1) / 12 : m < 1 ? -((12 - m) / 12) : 0;
                m -= 12 * years;
                y += years;
                if (!(y >= 1900 && y <= 2199))
                {
                    throw new NanoQLException($"year {y} out of bounds. It must be in [1901,2199]");
                }

                int length = MonthLength((Month)m, IsLeap((int)y));
                if (d > length)
                {
                    d = length;
                }

                return new Date(d, (Month)m, (int)y);
            }

            case TimeUnit.Years:
            {
                int d = date.DayOfMonth;
                Month m = date.Month;
                long y = (long)date.Year + n;
                if (!(y >= 1900 && y <= 2199))
                {
                    throw new NanoQLException($"year {y} out of bounds. It must be in [1901,2199]");
                }

                if (d == 29 && m == Month.February && !IsLeap((int)y))
                {
                    d = 28;
                }

                return new Date(d, m, (int)y);
            }

            default:
                throw new NanoQLException("undefined time units");
        }
    }

    private static int MonthLength(Month m, bool leapYear) =>
        leapYear ? MonthLeapLengths[(int)m - 1] : MonthLengths[(int)m - 1];

    private static int MonthOffset(Month m, bool leapYear) =>
        leapYear ? MonthLeapOffsets[(int)m - 1] : MonthOffsets[(int)m - 1];

    private static int YearOffset(int y) => YearOffsets[y - 1900];

    private static int[] BuildYearOffsets()
    {
        var offsets = new int[301];
        for (int y = 1901; y <= 2200; ++y)
        {
            offsets[y - 1900] = offsets[y - 1901] + (IsLeap(y - 1) ? 366 : 365);
        }

        return offsets;
    }
}
