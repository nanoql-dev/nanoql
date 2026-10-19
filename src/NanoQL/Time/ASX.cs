// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2015 Maddalena Zanzi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/asx.hpp ql/time/asx.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The main cycle of the Australian Securities Exchange (ASX) months.</summary>
/// <remarks>
/// <para>
/// QuantLib's functions whose date defaults to <c>Date()</c>, read as the global evaluation date, take the context
/// after QuantLib's parameters without a default, and the date as a nullable after it: the context's evaluation date is
/// read only when the date is <see langword="null"/>.
/// </para>
/// <para>
/// A code's month letter is upper-cased in ASCII only, so a letter outside ASCII is no month letter, which is what
/// QuantLib's <c>std::toupper</c> gives on the platform the vectors come from. A method of this class is named
/// <c>Date</c>, which hides the type inside it, so the class writes the type <c>NanoQL.Time.Date</c>.
/// </para>
/// </remarks>
public static class ASX
{
    private const string AllMonthCodes = "FGHJKMNQUVXZ";

    /// <summary>Whether the given date is an ASX date: the second Friday of its month.</summary>
    /// <param name="d">The date.</param>
    /// <param name="mainCycle">Whether only March, June, September and December count.</param>
    /// <returns><see langword="true"/> for an ASX date.</returns>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    public static bool IsASXdate(NanoQL.Time.Date d, bool mainCycle = true)
    {
        NanoQL.Time.Date.ThrowIfDefault(d, nameof(d));
        if (d.Weekday != Weekday.Friday)
        {
            return false;
        }

        int day = d.DayOfMonth;
        if (day < 8 || day > 14)
        {
            return false;
        }

        if (!mainCycle)
        {
            return true;
        }

        return d.Month is Month.March or Month.June or Month.September or Month.December;
    }

    /// <summary>Whether the given string is an ASX code: a month letter, either case, and a year digit.</summary>
    /// <param name="in">The string.</param>
    /// <param name="mainCycle">Whether only March, June, September and December count.</param>
    /// <returns><see langword="true"/> for an ASX code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="in"/> is <see langword="null"/>.</exception>
    public static bool IsASXcode(string @in, bool mainCycle = true)
    {
        ArgumentNullException.ThrowIfNull(@in);
        if (@in.Length != 2)
        {
            return false;
        }

        // 2nd character of code needs to be digit
        if (@in[1] is < '0' or > '9')
        {
            return false;
        }

        // 1st character needs to represent the correct month
        string validMonthCodes = mainCycle ? "HMUZ" : AllMonthCodes;
        return validMonthCodes.Contains(AsciiUpper(@in[0]), StringComparison.Ordinal);
    }

    /// <summary>The ASX code of an ASX date (e.g. M5 for June 12th, 2015).</summary>
    /// <param name="asxDate">The ASX date.</param>
    /// <returns>The code.</returns>
    /// <exception cref="ArgumentException">The date isn't an ASX date, or is <c>default(Date)</c>.</exception>
    public static string Code(NanoQL.Time.Date asxDate)
    {
        NanoQL.Time.Date.ThrowIfDefault(asxDate, nameof(asxDate));
        if (!IsASXdate(asxDate, false))
        {
            throw new ArgumentException($"{asxDate} is not an ASX date", nameof(asxDate));
        }

        // month() is 1-based!
        char monthCode = AllMonthCodes[(int)asxDate.Month - 1];
        char yearDigit = (char)('0' + (asxDate.Year % 10));
        return $"{monthCode}{yearDigit}";
    }

    /// <summary>The ASX date of an ASX code (e.g. June 12th, 2015 for M5): the first on or after the reference date.</summary>
    /// <param name="asxCode">The ASX code, its letter in either case.</param>
    /// <param name="context">The evaluation context, whose evaluation date is the default reference date.</param>
    /// <param name="referenceDate">The reference date, or <see langword="null"/> for the evaluation date.</param>
    /// <returns>The date.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="asxCode"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The code isn't an ASX code, or <paramref name="referenceDate"/> is <c>default(Date)</c>.
    /// </exception>
    public static NanoQL.Time.Date Date(string asxCode, EvaluationContext context, NanoQL.Time.Date? referenceDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(asxCode);
        NanoQL.Time.Date.ThrowIfDefault(referenceDate, nameof(referenceDate));
        return DateOf(asxCode, referenceDate ?? context.EvaluationDate);
    }

    /// <summary>
    /// The next ASX date after a date: the first delivery date of the next contract listed in the Australian
    /// Securities Exchange.
    /// </summary>
    /// <param name="context">The evaluation context, whose evaluation date is the default date.</param>
    /// <param name="d">The date, or <see langword="null"/> for the evaluation date.</param>
    /// <param name="mainCycle">Whether only March, June, September and December count.</param>
    /// <returns>The ASX date, strictly after the date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    public static NanoQL.Time.Date NextDate(EvaluationContext context, NanoQL.Time.Date? d = null, bool mainCycle = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        NanoQL.Time.Date.ThrowIfDefault(d, nameof(d));
        return NextDate(d ?? context.EvaluationDate, mainCycle);
    }

    /// <summary>The next ASX date after the date of an ASX code.</summary>
    /// <param name="asxCode">The ASX code.</param>
    /// <param name="context">The evaluation context, whose evaluation date is the default reference date.</param>
    /// <param name="mainCycle">Whether only March, June, September and December count.</param>
    /// <param name="referenceDate">The code's reference date, or <see langword="null"/> for the evaluation date.</param>
    /// <returns>The ASX date.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="asxCode"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The code isn't an ASX code, or <paramref name="referenceDate"/> is <c>default(Date)</c>.
    /// </exception>
    public static NanoQL.Time.Date NextDate(
        string asxCode, EvaluationContext context, bool mainCycle = true, NanoQL.Time.Date? referenceDate = null)
    {
        NanoQL.Time.Date asxDate = Date(asxCode, context, referenceDate);
        return NextDate(asxDate + 1, mainCycle);
    }

    /// <summary>The ASX code of the next ASX date after a date.</summary>
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

    /// <summary>The ASX code of the next ASX date after the date of an ASX code.</summary>
    /// <param name="asxCode">The ASX code.</param>
    /// <param name="context">The evaluation context, whose evaluation date is the default reference date.</param>
    /// <param name="mainCycle">Whether only March, June, September and December count.</param>
    /// <param name="referenceDate">The code's reference date, or <see langword="null"/> for the evaluation date.</param>
    /// <returns>The code.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="asxCode"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The code isn't an ASX code, or <paramref name="referenceDate"/> is <c>default(Date)</c>.
    /// </exception>
    public static string NextCode(
        string asxCode, EvaluationContext context, bool mainCycle = true, NanoQL.Time.Date? referenceDate = null)
    {
        NanoQL.Time.Date date = NextDate(asxCode, context, mainCycle, referenceDate);
        return Code(date);
    }

    // QuantLib's nextDate(date, mainCycle) for a date given: the form the futures rate helper calls, which reads no
    // context.
    internal static NanoQL.Time.Date NextDate(NanoQL.Time.Date date, bool mainCycle)
    {
        NanoQL.Time.Date refDate = date;
        int y = refDate.Year;
        Month m = refDate.Month;

        int offset = mainCycle ? 3 : 1;
        int skipMonths = offset - ((int)m % offset);
        if (skipMonths != offset || refDate.DayOfMonth > 14)
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

        NanoQL.Time.Date result = NanoQL.Time.Date.NthWeekday(2, Weekday.Friday, m, y);
        if (result <= refDate)
        {
            result = NextDate(new NanoQL.Time.Date(15, m, y), mainCycle);
        }

        return result;
    }

    // QuantLib's date(asxCode, refDate) for a reference date given.
    private static NanoQL.Time.Date DateOf(string asxCode, NanoQL.Time.Date referenceDate)
    {
        if (!IsASXcode(asxCode, false))
        {
            throw new ArgumentException($"{asxCode} is not a valid ASX code", nameof(asxCode));
        }

        // QuantLib asserts the month letter and the year digit here, which IsASXcode has checked
        int idxZeroBased = AllMonthCodes.IndexOf(AsciiUpper(asxCode[0]), StringComparison.Ordinal);

        // QuantLib::Month is 1-based!
        var m = (Month)(idxZeroBased + 1);

        // convert 2nd char to year digit
        int y = asxCode[1] - '0';

        // year<1900 are not valid QuantLib years: to avoid a run-time
        // exception few lines below we need to add 10 years right away
        if (y == 0 && referenceDate.Year <= 1909)
        {
            y += 10;
        }

        int referenceYear = referenceDate.Year % 10;
        y += referenceDate.Year - referenceYear;
        NanoQL.Time.Date result = NextDate(new NanoQL.Time.Date(1, m, y), false);
        return result >= referenceDate ? result : NextDate(new NanoQL.Time.Date(1, m, y + 10), false);
    }

    private static char AsciiUpper(char c) => c is >= 'a' and <= 'z' ? (char)(c - ('a' - 'A')) : c;
}
