// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007, 2008, 2014 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2014 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/period.hpp ql/time/period.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Time;

/// <summary>A period, a length and a <see cref="TimeUnit"/>, with a limited algebra.</summary>
/// <remarks>
/// Equality follows QuantLib's and never throws: every zero-length period equals every other; other periods compare
/// normalised (years as months, weeks as days); a pair whose order QuantLib can't decide (1M against 30D) compares
/// unequal, where QuantLib's <c>==</c> throws. Ordering follows QuantLib's and throws <see cref="NanoQLException"/> on
/// such a pair.
/// </remarks>
public readonly struct Period : IEquatable<Period>, IComparable<Period>, IComparable
{
    /// <summary>Initializes a new instance of the <see cref="Period"/> struct.</summary>
    /// <param name="n">The length.</param>
    /// <param name="units">The time unit.</param>
    /// <exception cref="ArgumentOutOfRangeException">The unit isn't a member of <see cref="TimeUnit"/>.</exception>
    public Period(int n, TimeUnit units)
    {
        EnumArguments.Check(units, nameof(units));
        Length = n;
        Units = units;
    }

    /// <summary>Initializes a new instance of the <see cref="Period"/> struct from a frequency.</summary>
    /// <param name="f">The frequency.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The frequency is <see cref="Time.Frequency.OtherFrequency"/> or not a member of <see cref="Time.Frequency"/>.
    /// </exception>
    public Period(Frequency f)
    {
        this = FromFrequency(f, nameof(f));
    }

    /// <summary>Gets the length.</summary>
    public int Length { get; }

    /// <summary>Gets the time unit.</summary>
    public TimeUnit Units { get; }

    /// <summary>Returns the period with the opposite sign.</summary>
    /// <param name="p">The period.</param>
    /// <returns>The negated period.</returns>
    public static Period operator -(Period p) => new(-p.Length, p.Units);

    /// <summary>Multiplies a period's length.</summary>
    /// <param name="n">The factor.</param>
    /// <param name="p">The period.</param>
    /// <returns>The product.</returns>
    public static Period operator *(int n, Period p) => new(n * p.Length, p.Units);

    /// <summary>Multiplies a period's length.</summary>
    /// <param name="p">The period.</param>
    /// <param name="n">The factor.</param>
    /// <returns>The product.</returns>
    public static Period operator *(Period p, int n) => new(n * p.Length, p.Units);

    /// <summary>Divides a period, changing years to months or weeks to days when the length doesn't divide.</summary>
    /// <param name="p">The period.</param>
    /// <param name="n">The divisor.</param>
    /// <returns>The quotient.</returns>
    /// <exception cref="ArgumentException"><paramref name="n"/> is zero, or doesn't divide the period.</exception>
    public static Period operator /(Period p, int n)
    {
        if (n == 0)
        {
            throw new ArgumentException("cannot be divided by zero", nameof(n));
        }

        if (p.Length % n == 0)
        {
            // keep the original units. If the user created a 24-months period, they'll probably want a 12-months
            // one when they halve it.
            return new Period(p.Length / n, p.Units);
        }

        TimeUnit units = p.Units;
        int length = p.Length;
        switch (units)
        {
            case TimeUnit.Years:
                length *= 12;
                units = TimeUnit.Months;
                break;
            case TimeUnit.Weeks:
                length *= 7;
                units = TimeUnit.Days;
                break;
        }

        if (length % n != 0)
        {
            throw new ArgumentException($"{p} cannot be divided by {n}", nameof(n));
        }

        return new Period(length / n, units);
    }

    /// <summary>Adds two periods, converting years to months or weeks to days where needed.</summary>
    /// <param name="p1">The first period.</param>
    /// <param name="p2">The second period.</param>
    /// <returns>The sum.</returns>
    /// <exception cref="ArgumentException">The units can't be combined (months and days).</exception>
    public static Period operator +(Period p1, Period p2)
    {
        int length = p1.Length;
        TimeUnit units = p1.Units;
        if (length == 0)
        {
            return new Period(p2.Length, p2.Units);
        }

        if (units == p2.Units)
        {
            // no conversion needed
            return new Period(length + p2.Length, units);
        }

        switch (units)
        {
            case TimeUnit.Years:
                switch (p2.Units)
                {
                    case TimeUnit.Months:
                        return new Period((length * 12) + p2.Length, TimeUnit.Months);
                    case TimeUnit.Weeks:
                    case TimeUnit.Days:
                        RequireZero(p1, p2);
                        return p1;
                    default:
                        throw new NanoQLException($"unknown time unit ({(int)p2.Units})");
                }

            case TimeUnit.Months:
                switch (p2.Units)
                {
                    case TimeUnit.Years:
                        return new Period(length + (p2.Length * 12), units);
                    case TimeUnit.Weeks:
                    case TimeUnit.Days:
                        RequireZero(p1, p2);
                        return p1;
                    default:
                        throw new NanoQLException($"unknown time unit ({(int)p2.Units})");
                }

            case TimeUnit.Weeks:
                switch (p2.Units)
                {
                    case TimeUnit.Days:
                        return new Period((length * 7) + p2.Length, TimeUnit.Days);
                    case TimeUnit.Years:
                    case TimeUnit.Months:
                        RequireZero(p1, p2);
                        return p1;
                    default:
                        throw new NanoQLException($"unknown time unit ({(int)p2.Units})");
                }

            case TimeUnit.Days:
                switch (p2.Units)
                {
                    case TimeUnit.Weeks:
                        return new Period(length + (p2.Length * 7), units);
                    case TimeUnit.Years:
                    case TimeUnit.Months:
                        RequireZero(p1, p2);
                        return p1;
                    default:
                        throw new NanoQLException($"unknown time unit ({(int)p2.Units})");
                }

            default:
                throw new NanoQLException($"unknown time unit ({(int)units})");
        }
    }

    /// <summary>Subtracts a period from another.</summary>
    /// <param name="p1">The first period.</param>
    /// <param name="p2">The period to subtract.</param>
    /// <returns>The difference.</returns>
    public static Period operator -(Period p1, Period p2) => p1 + (-p2);

    /// <summary>Whether two periods are equal, by QuantLib's equality (never throws).</summary>
    /// <param name="p1">The first period.</param>
    /// <param name="p2">The second period.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    public static bool operator ==(Period p1, Period p2) => p1.Equals(p2);

    /// <summary>Whether two periods differ, by QuantLib's equality (never throws).</summary>
    /// <param name="p1">The first period.</param>
    /// <param name="p2">The second period.</param>
    /// <returns><see langword="true"/> when not equal.</returns>
    public static bool operator !=(Period p1, Period p2) => !p1.Equals(p2);

    /// <summary>Whether a period is shorter than another.</summary>
    /// <param name="p1">The first period.</param>
    /// <param name="p2">The second period.</param>
    /// <returns><see langword="true"/> when <paramref name="p1"/> is shorter.</returns>
    /// <exception cref="NanoQLException">The comparison is undecidable (1M against 30D).</exception>
    public static bool operator <(Period p1, Period p2)
    {
        // special cases
        if (p1.Length == 0)
        {
            return p2.Length > 0;
        }

        if (p2.Length == 0)
        {
            return p1.Length < 0;
        }

        if (ExactLengths(p1, p2) is (long l1, long l2))
        {
            return l1 < l2;
        }

        // inexact comparisons (handled by converting to days and using limits)
        (long min1, long max1) = DaysMinMax(p1);
        (long min2, long max2) = DaysMinMax(p2);
        if (max1 < min2)
        {
            return true;
        }

        if (min1 > max2)
        {
            return false;
        }

        throw new NanoQLException($"undecidable comparison between {p1} and {p2}");
    }

    /// <summary>Whether a period is longer than another.</summary>
    /// <param name="p1">The first period.</param>
    /// <param name="p2">The second period.</param>
    /// <returns><see langword="true"/> when <paramref name="p1"/> is longer.</returns>
    /// <exception cref="NanoQLException">The comparison is undecidable.</exception>
    public static bool operator >(Period p1, Period p2) => p2 < p1;

    /// <summary>Whether a period is not longer than another.</summary>
    /// <param name="p1">The first period.</param>
    /// <param name="p2">The second period.</param>
    /// <returns><see langword="true"/> when <paramref name="p1"/> is not longer.</returns>
    /// <exception cref="NanoQLException">The comparison is undecidable.</exception>
    public static bool operator <=(Period p1, Period p2) => !(p1 > p2);

    /// <summary>Whether a period is not shorter than another.</summary>
    /// <param name="p1">The first period.</param>
    /// <param name="p2">The second period.</param>
    /// <returns><see langword="true"/> when <paramref name="p1"/> is not shorter.</returns>
    /// <exception cref="NanoQLException">The comparison is undecidable.</exception>
    public static bool operator >=(Period p1, Period p2) => !(p1 < p2);

    /// <summary>The frequency the period corresponds to.</summary>
    /// <returns>The frequency, or <see cref="Frequency.OtherFrequency"/>.</returns>
    /// <exception cref="NanoQLException">The time unit is not a calendar unit.</exception>
    public Frequency Frequency()
    {
        // unsigned version
        int length = Math.Abs(Length);
        if (length == 0)
        {
            return Units == TimeUnit.Years ? Time.Frequency.Once : Time.Frequency.NoFrequency;
        }

        switch (Units)
        {
            case TimeUnit.Years:
                return length == 1 ? Time.Frequency.Annual : Time.Frequency.OtherFrequency;
            case TimeUnit.Months:
                return 12 % length == 0 && length <= 12 ? (Time.Frequency)(12 / length) : Time.Frequency.OtherFrequency;
            case TimeUnit.Weeks:
                return length switch
                {
                    1 => Time.Frequency.Weekly,
                    2 => Time.Frequency.Biweekly,
                    4 => Time.Frequency.EveryFourthWeek,
                    _ => Time.Frequency.OtherFrequency,
                };
            case TimeUnit.Days:
                return length == 1 ? Time.Frequency.Daily : Time.Frequency.OtherFrequency;
            default:
                throw new NanoQLException($"unknown time unit ({(int)Units})");
        }
    }

    /// <summary>The period in its normal form: zero as zero days, months as years and days as weeks where whole.</summary>
    /// <returns>The normalised period.</returns>
    public Period Normalized()
    {
        if (Length == 0)
        {
            return new Period(0, TimeUnit.Days);
        }

        switch (Units)
        {
            case TimeUnit.Months:
                return Length % 12 == 0 ? new Period(Length / 12, TimeUnit.Years) : this;
            case TimeUnit.Days:
                return Length % 7 == 0 ? new Period(Length / 7, TimeUnit.Weeks) : this;
            case TimeUnit.Weeks:
            case TimeUnit.Years:
                return this;
            default:
                throw new NanoQLException($"unknown time unit ({(int)Units})");
        }
    }

    /// <inheritdoc/>
    public bool Equals(Period other)
    {
        if (Length == 0 || other.Length == 0)
        {
            return Length == other.Length;
        }

        return ExactLengths(this, other) is (long l1, long l2) && l1 == l2;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Period other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        if (Length == 0)
        {
            return 0;
        }

        return Units switch
        {
            TimeUnit.Years => HashCode.Combine(TimeUnit.Months, (long)Length * 12),
            TimeUnit.Weeks => HashCode.Combine(TimeUnit.Days, (long)Length * 7),
            _ => HashCode.Combine(Units, (long)Length),
        };
    }

    /// <summary>Compares by QuantLib's ordering.</summary>
    /// <param name="other">The other period.</param>
    /// <returns>Negative, zero or positive.</returns>
    /// <exception cref="NanoQLException">The comparison is undecidable.</exception>
    public int CompareTo(Period other) => this < other ? -1 : other < this ? 1 : 0;

    /// <inheritdoc/>
    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        Period other => CompareTo(other),
        _ => throw new ArgumentException($"Object must be of type {nameof(Period)}.", nameof(obj)),
    };

    /// <summary>The period of a frequency, refusing the frequencies QuantLib can't turn into one.</summary>
    /// <param name="f">The frequency.</param>
    /// <param name="paramName">The caller's parameter name.</param>
    /// <returns>The period.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The frequency is <see cref="Time.Frequency.OtherFrequency"/> or not a member of <see cref="Time.Frequency"/>.
    /// </exception>
    internal static Period FromFrequency(Frequency f, string paramName)
    {
        switch (f)
        {
            case Time.Frequency.NoFrequency:
                // same as Period()
                return new Period(0, TimeUnit.Days);
            case Time.Frequency.Once:
                return new Period(0, TimeUnit.Years);
            case Time.Frequency.Annual:
                return new Period(1, TimeUnit.Years);
            case Time.Frequency.Semiannual:
            case Time.Frequency.EveryFourthMonth:
            case Time.Frequency.Quarterly:
            case Time.Frequency.Bimonthly:
            case Time.Frequency.Monthly:
                return new Period(12 / (int)f, TimeUnit.Months);
            case Time.Frequency.EveryFourthWeek:
            case Time.Frequency.Biweekly:
            case Time.Frequency.Weekly:
                return new Period(52 / (int)f, TimeUnit.Weeks);
            case Time.Frequency.Daily:
                return new Period(1, TimeUnit.Days);
            case Time.Frequency.OtherFrequency:
                throw new ArgumentOutOfRangeException(paramName, f, "unknown frequency"); // no point in showing 999...
            default:
                throw new ArgumentOutOfRangeException(paramName, f, $"unknown frequency ({(int)f})");
        }
    }

    /// <summary>
    /// The period in QuantLib's short format, e.g. "6M". Milliseconds and microseconds, which that format can't print,
    /// are written "1ms" and "1us", so the text never throws; a unit outside <see cref="TimeUnit"/> can't be constructed.
    /// </summary>
    /// <returns>The short-format text.</returns>
    public override string ToString() => Units switch
    {
        TimeUnit.Milliseconds => Length.ToString(CultureInfo.InvariantCulture) + "ms",
        TimeUnit.Microseconds => Length.ToString(CultureInfo.InvariantCulture) + "us",
        _ => PeriodFunctions.ShortPeriod(this),
    };

    private static void RequireZero(Period p1, Period p2)
    {
        if (p2.Length != 0)
        {
            throw new ArgumentException($"impossible addition between {p1} and {p2}", nameof(p2));
        }
    }

    // The lengths in a common unit where QuantLib compares exactly (same units, months and years, days and weeks).
    private static (long, long)? ExactLengths(Period p1, Period p2) => (p1.Units, p2.Units) switch
    {
        var (u1, u2) when u1 == u2 => (p1.Length, p2.Length),
        (TimeUnit.Months, TimeUnit.Years) => (p1.Length, 12L * p2.Length),
        (TimeUnit.Years, TimeUnit.Months) => (12L * p1.Length, p2.Length),
        (TimeUnit.Days, TimeUnit.Weeks) => (p1.Length, 7L * p2.Length),
        (TimeUnit.Weeks, TimeUnit.Days) => (7L * p1.Length, p2.Length),
        _ => null,
    };

    private static (long Min, long Max) DaysMinMax(Period p) => p.Units switch
    {
        TimeUnit.Days => (p.Length, p.Length),
        TimeUnit.Weeks => (7L * p.Length, 7L * p.Length),

        // for negative lengths, the longer month or year gives the minimum
        TimeUnit.Months => p.Length >= 0 ? (28L * p.Length, 31L * p.Length) : (31L * p.Length, 28L * p.Length),
        TimeUnit.Years => p.Length >= 0 ? (365L * p.Length, 366L * p.Length) : (366L * p.Length, 365L * p.Length),
        _ => throw new NanoQLException($"unknown time unit ({(int)p.Units})"),
    };
}
