// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/imm.hpp ql/time/imm.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The main cycle of the International Money Market (IMM) months.</summary>
/// <remarks>
/// <para>
/// QuantLib's functions whose date defaults to <c>Date()</c>, read as the global evaluation date, take the context
/// after QuantLib's parameters without a default, and the date as a nullable after it: the context's evaluation date is
/// read only when the date is <see langword="null"/>.
/// </para>
/// <para>
/// A method of this class is named <c>Date</c>, which hides the type inside it, so the class writes the type
/// <c>NanoQL.Time.Date</c>.
/// </para>
/// </remarks>
public static class IMM
{
    /// <summary>Whether the given date is an IMM date: the third Wednesday of its month.</summary>
    /// <param name="d">The date.</param>
    /// <param name="mainCycle">Whether only March, June, September and December count.</param>
    /// <returns><see langword="true"/> for an IMM date.</returns>
    public static bool IsIMMdate(NanoQL.Time.Date d, bool mainCycle = true)
    {
        if (d.Weekday != Weekday.Wednesday)
        {
            return false;
        }

        int day = d.DayOfMonth;
        if (day < 15 || day > 21)
        {
            return false;
        }

        if (!mainCycle)
        {
            return true;
        }

        return d.Month is Month.March or Month.June or Month.September or Month.December;
    }

    /// <summary>Whether the given string is an IMM code: a month letter, either case, and a year digit.</summary>
    /// <param name="in">The string.</param>
    /// <param name="mainCycle">Whether only March, June, September and December count.</param>
    /// <returns><see langword="true"/> for an IMM code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="in"/> is <see langword="null"/>.</exception>
    public static bool IsIMMcode(string @in, bool mainCycle = true)
    {
        ArgumentNullException.ThrowIfNull(@in);
        if (@in.Length != 2)
        {
            return false;
        }

        string str1 = "0123456789";
        if (!str1.Contains(@in[1], StringComparison.Ordinal))
        {
            return false;
        }

        str1 = mainCycle ? "hmzuHMZU" : "fghjkmnquvxzFGHJKMNQUVXZ";
        return str1.Contains(@in[0], StringComparison.Ordinal);
    }

    /// <summary>The IMM code of an IMM date (e.g. H3 for March 20th, 2013).</summary>
    /// <param name="immDate">The IMM date.</param>
    /// <returns>The code.</returns>
    /// <exception cref="ArgumentException">The date isn't an IMM date, or is <c>default(Date)</c>.</exception>
    public static string Code(NanoQL.Time.Date immDate)
    {
        NanoQL.Time.Date.ThrowIfDefault(immDate, nameof(immDate));
        if (!IsIMMdate(immDate, false))
        {
            throw new ArgumentException($"{immDate} is not an IMM date", nameof(immDate));
        }

        int y = immDate.Year % 10;
        char letter = immDate.Month switch
        {
            Month.January => 'F',
            Month.February => 'G',
            Month.March => 'H',
            Month.April => 'J',
            Month.May => 'K',
            Month.June => 'M',
            Month.July => 'N',
            Month.August => 'Q',
            Month.September => 'U',
            Month.October => 'V',
            Month.November => 'X',
            _ => 'Z',
        };
        return $"{letter}{y}";
    }

    /// <summary>The IMM date of an IMM code (e.g. March 20th, 2013 for H3): the first on or after the reference date.</summary>
    /// <param name="immCode">The IMM code, its letter in either case.</param>
    /// <param name="context">The evaluation context, whose evaluation date is the default reference date.</param>
    /// <param name="referenceDate">The reference date, or <see langword="null"/> for the evaluation date.</param>
    /// <returns>The date.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="immCode"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The code isn't an IMM code, or <paramref name="referenceDate"/> is <c>default(Date)</c>.
    /// </exception>
    public static NanoQL.Time.Date Date(string immCode, EvaluationContext context, NanoQL.Time.Date? referenceDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(immCode);
        NanoQL.Time.Date.ThrowIfDefault(referenceDate, nameof(referenceDate));
        return DateOf(immCode, referenceDate ?? context.EvaluationDate);
    }

    /// <summary>
    /// The next IMM date after a date: the first delivery date of the next contract listed in the International Money
    /// Market section of the Chicago Mercantile Exchange.
    /// </summary>
    /// <param name="context">The evaluation context, whose evaluation date is the default date.</param>
    /// <param name="d">The date, or <see langword="null"/> for the evaluation date.</param>
    /// <param name="mainCycle">Whether only March, June, September and December count.</param>
    /// <returns>The IMM date, strictly after the date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    public static NanoQL.Time.Date NextDate(EvaluationContext context, NanoQL.Time.Date? d = null, bool mainCycle = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        NanoQL.Time.Date.ThrowIfDefault(d, nameof(d));
        return NextDate(d ?? context.EvaluationDate, mainCycle);
    }

    /// <summary>The next IMM date after the date of an IMM code.</summary>
    /// <param name="immCode">The IMM code.</param>
    /// <param name="context">The evaluation context, whose evaluation date is the default reference date.</param>
    /// <param name="mainCycle">Whether only March, June, September and December count.</param>
    /// <param name="referenceDate">The code's reference date, or <see langword="null"/> for the evaluation date.</param>
    /// <returns>The IMM date.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="immCode"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The code isn't an IMM code, or <paramref name="referenceDate"/> is <c>default(Date)</c>.
    /// </exception>
    public static NanoQL.Time.Date NextDate(
        string immCode, EvaluationContext context, bool mainCycle = true, NanoQL.Time.Date? referenceDate = null)
    {
        NanoQL.Time.Date immDate = Date(immCode, context, referenceDate);
        return NextDate(immDate + 1, mainCycle);
    }

    /// <summary>The IMM code of the next IMM date after a date.</summary>
    /// <param name="context">The evaluation context, whose evaluation date is the default date.</param>
    /// <param name="d">The date, or <see langword="null"/> for the evaluation date.</param>
    /// <param name="mainCycle">Whether only March, June, September and December count.</param>
    /// <returns>The code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    public static string NextCode(EvaluationContext context, NanoQL.Time.Date? d = null, bool mainCycle = true)
    {
        NanoQL.Time.Date date = NextDate(context, d, mainCycle);
        return Code(date);
    }

    /// <summary>The IMM code of the next IMM date after the date of an IMM code.</summary>
    /// <param name="immCode">The IMM code.</param>
    /// <param name="context">The evaluation context, whose evaluation date is the default reference date.</param>
    /// <param name="mainCycle">Whether only March, June, September and December count.</param>
    /// <param name="referenceDate">The code's reference date, or <see langword="null"/> for the evaluation date.</param>
    /// <returns>The code.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="immCode"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The code isn't an IMM code, or <paramref name="referenceDate"/> is <c>default(Date)</c>.
    /// </exception>
    public static string NextCode(
        string immCode, EvaluationContext context, bool mainCycle = true, NanoQL.Time.Date? referenceDate = null)
    {
        NanoQL.Time.Date date = NextDate(immCode, context, mainCycle, referenceDate);
        return Code(date);
    }

    // QuantLib's nextDate(date, mainCycle) for a date given: the form the rate helpers call, which reads no context.
    internal static NanoQL.Time.Date NextDate(NanoQL.Time.Date date, bool mainCycle)
    {
        NanoQL.Time.Date refDate = date;
        int y = refDate.Year;
        Month m = refDate.Month;

        int offset = mainCycle ? 3 : 1;
        int skipMonths = offset - ((int)m % offset);
        if (skipMonths != offset || refDate.DayOfMonth > 21)
        {
            skipMonths += (int)m;
            if (skipMonths <= 12)
            {
                m = (Month)skipMonths;
            }
            else
            {
                m = (Month)(skipMonths - 12);
                y += 1;
            }
        }

        NanoQL.Time.Date result = NanoQL.Time.Date.NthWeekday(3, Weekday.Wednesday, m, y);
        if (result <= refDate)
        {
            result = NextDate(new NanoQL.Time.Date(22, m, y), mainCycle);
        }

        return result;
    }

    // QuantLib's date(immCode, refDate) for a reference date given.
    private static NanoQL.Time.Date DateOf(string immCode, NanoQL.Time.Date referenceDate)
    {
        if (!IsIMMcode(immCode, false))
        {
            throw new ArgumentException($"{immCode} is not a valid IMM code", nameof(immCode));
        }

        // the letter is one of IsIMMcode's, upper-cased in ASCII
        char ms = immCode[0] is >= 'a' and <= 'z' ? (char)(immCode[0] - ('a' - 'A')) : immCode[0];
        Month m = ms switch
        {
            'F' => Month.January,
            'G' => Month.February,
            'H' => Month.March,
            'J' => Month.April,
            'K' => Month.May,
            'M' => Month.June,
            'N' => Month.July,
            'Q' => Month.August,
            'U' => Month.September,
            'V' => Month.October,
            'X' => Month.November,
            _ => Month.December,
        };

        int y = immCode[1] - '0';

        // year<1900 are not valid QuantLib years: to avoid a run-time
        // exception few lines below we need to add 10 years right away
        if (y == 0 && referenceDate.Year <= 1909)
        {
            y += 10;
        }

        int referenceYear = referenceDate.Year % 10;
        y += referenceDate.Year - referenceYear;
        NanoQL.Time.Date result = NextDate(new NanoQL.Time.Date(1, m, y), false);
        if (result < referenceDate)
        {
            return NextDate(new NanoQL.Time.Date(1, m, y + 10), false);
        }

        return result;
    }
}
