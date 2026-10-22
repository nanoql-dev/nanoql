// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2009, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/ecb.hpp ql/time/ecb.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;

namespace NanoQL.Time;

/// <summary>European Central Bank reserve maintenance dates.</summary>
/// <remarks>
/// <para>
/// The known dates are a value: <see cref="Default"/> holds QuantLib's, from January 2005 to December 2024, and
/// <see cref="AddDate"/> and <see cref="RemoveDate"/> return a new value, so two calculations can use different sets
/// and neither changes the other's. In QuantLib they are one static set that <c>addDate</c> and <c>removeDate</c>
/// change for the whole process. Two values are equal when their known dates are.
/// </para>
/// <para>
/// QuantLib's functions whose date defaults to <c>Date()</c>, read as the global evaluation date, take the context
/// after QuantLib's parameters without a default, and the date as a nullable after it: the context's evaluation date is
/// read only when the date is <see langword="null"/>.
/// </para>
/// <para>
/// At the start of Date's range QuantLib steps one day before the month's first day, or before the date, and so throws
/// on 1 January 1901 (<c>ecb.hpp:42</c>, <c>:81-84</c>; <c>ecb.cpp:200</c>): here a month's date is the first known
/// date on or after its first day, and <see cref="IsECBdate"/> is membership in the known dates up to the last one,
/// QuantLib's answers everywhere else; a documented divergence.
/// </para>
/// <para>
/// A method of this class is named <c>Date</c>, which hides the type inside it, so the class writes the type
/// <c>NanoQL.Time.Date</c>.
/// </para>
/// </remarks>
public sealed class ECB : IEquatable<ECB>
{
    private static readonly string[] MonthCodes =
        ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];

    // Start of maintenance period
    // source:
    // https://web.archive.org/web/20230610050642/https://www.ecb.europa.eu/press/calendars/reserve/html/index.en.html
    private static readonly int[] KnownDateSerials =
    [
        // 2005
        38371, 38391, 38420, 38455, 38483, 38511,
        38546, 38574, 38602, 38637, 38665, 38692,

        // 2006
        38735, 38756, 38784, 38819, 38847, 38883,
        38910, 38938, 38966, 39001, 39029, 39064,

        // 2007
        39099, 39127, 39155, 39190, 39217, 39246,
        39274, 39302, 39337, 39365, 39400, 39428,

        // 2008
        39463, 39491, 39519, 39554, 39582, 39610,
        39638, 39673, 39701, 39729, 39764, 39792,

        // 2009
        39834, 39855, 39883, 39911, 39946, 39974,
        40002, 40037, 40065, 40100, 40128, 40155,

        // 2010
        40198, 40219, 40247, 40282, 40310, 40345,
        40373, 40401, 40429, 40464, 40492, 40520,

        // 2011
        40562, 40583, 40611, 40646, 40674, 40709,
        40737, 40765, 40800, 40828, 40856, 40891,

        // 2012
        40926, 40954, 40982, 41010, 41038, 41073,
        41101, 41129, 41164, 41192, 41227, 41255,

        // 2013
        41290, 41318, 41346, 41374, 41402, 41437,
        41465, 41493, 41528, 41556, 41591, 41619,

        // 2014
        41654, 41682, 41710, 41738, 41773, 41801,
        41829, 41864, 41892, 41920, 41955, 41983,

        // 2015
        42032, 42074, 42116, 42165, 42207, 42256,
        42305, 42347,

        // 2016
        42396, 42445, 42487, 42529, 42578, 42627,
        42669, 42718,

        // 2017
        42760, 42809, 42858, 42900, 42942, 42991,
        43040, 43089,

        // 2018
        43131, 43167, 43216, 43265, 43307, 43356,
        43398, 43447,

        // 2019
        43495, 43537, 43572, 43628, 43677, 43726,
        43768, 43817,

        // 2020
        43859, 43908, 43957, 43992, 44034, 44090,
        44139, 44181,

        // 2021
        44223, 44272, 44314, 44363, 44405, 44454,
        44503, 44552,

        // 2022
        44601, 44636, 44671, 44727, 44769, 44818,
        44867, 44916,

        // 2023
        44965, 45007, 45056, 45098, 45140, 45189,
        45231, 45280,

        // 2024
        45322, 45364, 45399, 45455, 45497, 45553,
        45588, 45644,
    ];

    private readonly ImmutableSortedSet<NanoQL.Time.Date> knownDates;
    private readonly int hash;

    private ECB(ImmutableSortedSet<NanoQL.Time.Date> knownDates)
    {
        this.knownDates = knownDates;
        var h = default(HashCode);
        foreach (NanoQL.Time.Date d in knownDates)
        {
            h.Add(d);
        }

        hash = h.ToHashCode();
    }

    /// <summary>Gets QuantLib's known dates, from January 2005 to December 2024.</summary>
    public static ECB Default { get; } =
        new(KnownDateSerials.Select(s => new NanoQL.Time.Date(s)).ToImmutableSortedSet());

    /// <summary>Gets the known maintenance period start dates.</summary>
    public ImmutableSortedSet<NanoQL.Time.Date> KnownDates => knownDates;

    /// <summary>Whether the given string is an ECB code: a month's three letters, in either case, and two
    /// digits.</summary>
    /// <param name="ecbCode">The string.</param>
    /// <returns><see langword="true"/> for an ECB code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ecbCode"/> is <see langword="null"/>.</exception>
    public static bool IsECBcode(string ecbCode)
    {
        ArgumentNullException.ThrowIfNull(ecbCode);
        if (ecbCode.Length != 5)
        {
            return false;
        }

        // first 3 characters need to represent month, case insensitive
        if (MonthOf(ecbCode) == 0)
        {
            return false;
        }

        // 4th, 5th characters need to be digit
        return char.IsAsciiDigit(ecbCode[3]) && char.IsAsciiDigit(ecbCode[4]);
    }

    /// <summary>The ECB code following the given code (e.g. FEB06 after JAN06, JAN00 after DEC99).</summary>
    /// <param name="ecbCode">The ECB code, its month in either case.</param>
    /// <returns>The next code, its month in capitals.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ecbCode"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The string isn't an ECB code.</exception>
    public static string NextCode(string ecbCode)
    {
        RequireCode(ecbCode);
        int month = MonthOf(ecbCode);
        char[] next = new char[5];
        if (month != 12)
        {
            // use next month
            MonthCodes[month].CopyTo(0, next, 0, 3);

            // copy year
            next[3] = ecbCode[3];
            next[4] = ecbCode[4];
        }
        else
        {
            // previous month was DEC
            "JAN".CopyTo(0, next, 0, 3);

            // init with previous year
            next[3] = ecbCode[3];
            next[4] = ecbCode[4];

            // increment year's last digit (e.g. '22' -> '23').
            // if overflow (e.g. '29' -> '20'), then also increment 2nd digit (e.g. '20' -> '30').
            if (IncrementAndCheckForOverflow(ref next[4]))
            {
                IncrementAndCheckForOverflow(ref next[3]);
            }
        }

        return new string(next);
    }

    /// <summary>A copy of the value with the date added to the known dates.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The new value; the receiver is unchanged.</returns>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    public ECB AddDate(NanoQL.Time.Date d)
    {
        NanoQL.Time.Date.ThrowIfDefault(d, nameof(d));
        return new ECB(knownDates.Add(d));
    }

    /// <summary>A copy of the value with the date removed from the known dates.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The new value; the receiver is unchanged.</returns>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    public ECB RemoveDate(NanoQL.Time.Date d)
    {
        NanoQL.Time.Date.ThrowIfDefault(d, nameof(d));
        return new ECB(knownDates.Remove(d));
    }

    /// <summary>The maintenance period start date in the given month and year: the first known date from its first
    /// day.</summary>
    /// <param name="m">The month.</param>
    /// <param name="y">The year.</param>
    /// <returns>The date.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The month or the year is outside Date's range.</exception>
    /// <exception cref="NanoQLException">No known date is on or after the month's first day.</exception>
    public NanoQL.Time.Date Date(Month m, int y) => FirstOnOrAfter(new NanoQL.Time.Date(1, m, y));

    /// <summary>The ECB date of an ECB code (e.g. March xxth, 2013 for MAR13), in the century of the reference
    /// date.</summary>
    /// <param name="ecbCode">The ECB code, its month in either case.</param>
    /// <param name="context">The evaluation context, whose evaluation date is the default reference date.</param>
    /// <param name="referenceDate">The reference date, or <see langword="null"/> for the evaluation date.</param>
    /// <returns>The date.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="ecbCode"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The string isn't an ECB code, or <paramref name="referenceDate"/> is <c>default(Date)</c>.
    /// </exception>
    /// <exception cref="NanoQLException">No known date is on or after the month's first day.</exception>
    public NanoQL.Time.Date Date(string ecbCode, EvaluationContext context, NanoQL.Time.Date? referenceDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireCode(ecbCode);
        NanoQL.Time.Date.ThrowIfDefault(referenceDate, nameof(referenceDate));

        // convert first 3 characters to the month
        var m = (Month)MonthOf(ecbCode);

        // convert 4th, 5th characters to the year
        int y = ((ecbCode[3] - '0') * 10) + (ecbCode[4] - '0');
        NanoQL.Time.Date refDate = referenceDate ?? context.EvaluationDate;
        int referenceYear = refDate.Year % 100;
        y += refDate.Year - referenceYear;
        if (y < NanoQL.Time.Date.MinDate.Year)
        {
            return After(NanoQL.Time.Date.MinDate);
        }

        return FirstOnOrAfter(new NanoQL.Time.Date(1, m, y));
    }

    /// <summary>The ECB code of an ECB date (e.g. MAR10 for March xxth, 2010).</summary>
    /// <param name="ecbDate">The ECB date.</param>
    /// <returns>The code, its month in capitals.</returns>
    /// <exception cref="ArgumentException">The date isn't an ECB date, or is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The date is after the last known date.</exception>
    public string Code(NanoQL.Time.Date ecbDate)
    {
        if (!IsECBdate(ecbDate))
        {
            throw new ArgumentException($"{ecbDate} is not a valid ECB date", nameof(ecbDate));
        }

        // 3 characters for the month, the last two digits of the year
        return MonthCodes[(int)ecbDate.Month - 1]
            + (ecbDate.Year % 100).ToString("D2", CultureInfo.InvariantCulture);
    }

    /// <summary>The next maintenance period start date after the given date.</summary>
    /// <param name="context">The evaluation context, whose evaluation date is the default date.</param>
    /// <param name="d">The date, or <see langword="null"/> for the evaluation date.</param>
    /// <returns>The first known date after the date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">No known date is after the date.</exception>
    public NanoQL.Time.Date NextDate(EvaluationContext context, NanoQL.Time.Date? d = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        NanoQL.Time.Date.ThrowIfDefault(d, nameof(d));
        return After(d ?? context.EvaluationDate);
    }

    /// <summary>The next maintenance period start date after the date of an ECB code.</summary>
    /// <param name="ecbCode">The ECB code.</param>
    /// <param name="context">The evaluation context, whose evaluation date is the default reference date.</param>
    /// <param name="referenceDate">The code's reference date, or <see langword="null"/> for the evaluation
    /// date.</param>
    /// <returns>The date.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="ecbCode"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The string isn't an ECB code, or <paramref name="referenceDate"/> is <c>default(Date)</c>.
    /// </exception>
    /// <exception cref="NanoQLException">No known date follows.</exception>
    public NanoQL.Time.Date NextDate(string ecbCode, EvaluationContext context, NanoQL.Time.Date? referenceDate = null) =>
        After(Date(ecbCode, context, referenceDate));

    /// <summary>The maintenance period start dates after the given date.</summary>
    /// <param name="context">The evaluation context, whose evaluation date is the default date.</param>
    /// <param name="d">The date, or <see langword="null"/> for the evaluation date.</param>
    /// <returns>The known dates after the date, in order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">No known date is after the date.</exception>
    public ImmutableArray<NanoQL.Time.Date> NextDates(EvaluationContext context, NanoQL.Time.Date? d = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        NanoQL.Time.Date.ThrowIfDefault(d, nameof(d));
        return AllAfter(d ?? context.EvaluationDate);
    }

    /// <summary>The maintenance period start dates after the date of an ECB code.</summary>
    /// <param name="ecbCode">The ECB code.</param>
    /// <param name="context">The evaluation context, whose evaluation date is the default reference date.</param>
    /// <param name="referenceDate">The code's reference date, or <see langword="null"/> for the evaluation
    /// date.</param>
    /// <returns>The known dates after the code's date, in order.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="ecbCode"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The string isn't an ECB code, or <paramref name="referenceDate"/> is <c>default(Date)</c>.
    /// </exception>
    /// <exception cref="NanoQLException">No known date follows.</exception>
    public ImmutableArray<NanoQL.Time.Date> NextDates(
        string ecbCode, EvaluationContext context, NanoQL.Time.Date? referenceDate = null) =>
        AllAfter(Date(ecbCode, context, referenceDate));

    /// <summary>Whether the given date is a maintenance period start date.</summary>
    /// <param name="d">The date.</param>
    /// <returns><see langword="true"/> for a known date.</returns>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The date is after the last known date, which QuantLib's answer
    /// reads.</exception>
    public bool IsECBdate(NanoQL.Time.Date d)
    {
        NanoQL.Time.Date.ThrowIfDefault(d, nameof(d));

        // QuantLib's nextDate(d - 1) == d, without the step before the range's first day
        if (d > Last())
        {
            throw new NanoQLException($"ECB dates after {Last()} are unknown");
        }

        return knownDates.Contains(d);
    }

    /// <summary>The ECB code of the next maintenance period start date after the given date.</summary>
    /// <param name="context">The evaluation context, whose evaluation date is the default date.</param>
    /// <param name="d">The date, or <see langword="null"/> for the evaluation date.</param>
    /// <returns>The code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">No known date is after the date.</exception>
    public string NextCode(EvaluationContext context, NanoQL.Time.Date? d = null) => Code(NextDate(context, d));

    /// <summary>Whether two values hold the same known dates.</summary>
    /// <param name="other">The other value.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    public bool Equals(ECB? other) =>
        other is not null
        && (ReferenceEquals(this, other) || (hash == other.hash && knownDates.SetEquals(other.knownDates)));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ECB other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => hash;

    // The month of a code's first three letters, 1 to 12, reading ASCII letters in either case, as std::toupper reads
    // them; 0 for none.
    private static int MonthOf(string code)
    {
        Span<char> upper = stackalloc char[3];
        for (int i = 0; i < 3; ++i)
        {
            char c = code[i];
            upper[i] = c is >= 'a' and <= 'z' ? (char)(c - 'a' + 'A') : c;
        }

        for (int m = 0; m < 12; ++m)
        {
            if (upper.SequenceEqual(MonthCodes[m]))
            {
                return m + 1;
            }
        }

        return 0;
    }

    private static void RequireCode(string ecbCode)
    {
        ArgumentNullException.ThrowIfNull(ecbCode);
        if (!IsECBcode(ecbCode))
        {
            throw new ArgumentException($"{ecbCode} is not a valid ECB code", nameof(ecbCode));
        }
    }

    private static bool IncrementAndCheckForOverflow(ref char dig)
    {
        if (dig == '9')
        {
            dig = '0';
            return true;
        }
        else
        {
            ++dig;
            return false;
        }
    }

    // The last known date. QuantLib's refusal reads it unchecked, which is undefined on an empty set: here an empty set
    // is refused with a text of its own, a documented divergence.
    private NanoQL.Time.Date Last() =>
        !knownDates.IsEmpty ? knownDates.Max : throw new NanoQLException("no ECB dates are known");

    // QuantLib's nextDate(d): the first known date after d.
    private NanoQL.Time.Date After(NanoQL.Time.Date d) => knownDates[IndexAfter(d)];

    private ImmutableArray<NanoQL.Time.Date> AllAfter(NanoQL.Time.Date d)
    {
        int i = IndexAfter(d);
        var result = ImmutableArray.CreateBuilder<NanoQL.Time.Date>(knownDates.Count - i);
        for (; i < knownDates.Count; ++i)
        {
            result.Add(knownDates[i]);
        }

        return result.MoveToImmutable();
    }

    // The index of the first known date after d (std::upper_bound), refused where there is none.
    private int IndexAfter(NanoQL.Time.Date d)
    {
        int i = knownDates.IndexOf(d);
        i = i >= 0 ? i + 1 : ~i;
        if (i == knownDates.Count)
        {
            throw new NanoQLException($"ECB dates after {Last()} are unknown");
        }

        return i;
    }

    // The first known date on or after d: QuantLib's nextDate(d - 1), without the step before the range's first day.
    private NanoQL.Time.Date FirstOnOrAfter(NanoQL.Time.Date d)
    {
        int i = knownDates.IndexOf(d);
        i = i >= 0 ? i : ~i;
        if (i == knownDates.Count)
        {
            throw new NanoQLException($"ECB dates after {Last()} are unknown");
        }

        return knownDates[i];
    }
}
