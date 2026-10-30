// SPDX-FileCopyrightText: Copyright (C) 2005, 2007, 2008, 2009, 2010, 2011 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/indonesia.hpp ql/time/calendars/indonesia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Indonesian calendars.</summary>
/// <remarks>
/// <para>
/// Holidays for the Indonesia stock exchange (data from http://www.idx.co.id/):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Ascension of Jesus Christ</description></item>
/// <item><description>Independence Day, August 17th</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// </list>
/// </para>
/// <para>
/// Other holidays for which no rule is given (data available for 2005-2026 only:)
/// <list type="bullet">
/// <item><description>Idul Adha</description></item>
/// <item><description>Ied Adha</description></item>
/// <item><description>Imlek</description></item>
/// <item><description>Moslem's New Year Day</description></item>
/// <item><description>Chinese New Year</description></item>
/// <item><description>Nyepi (Saka's New Year)</description></item>
/// <item><description>Birthday of Prophet Muhammad SAW</description></item>
/// <item><description>Waisak</description></item>
/// <item><description>Ascension of Prophet Muhammad SAW</description></item>
/// <item><description>Idul Fitri</description></item>
/// <item><description>Ied Fitri</description></item>
/// <item><description>Other national leaves</description></item>
/// </list>
/// </para>
/// <para>
/// The year-by-year data end in 2026: after it, only the rules for fixed dates and weekends apply,
/// as in QuantLib.
/// </para>
/// </remarks>
public sealed class Indonesia : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl BejImplInstance = new BejImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Indonesia"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Indonesia(IndonesiaMarket market = IndonesiaMarket.IDX)
    {
        switch (market)
        {
            case IndonesiaMarket.BEJ:
                impl = BejImplInstance;
                break;
            case IndonesiaMarket.JSX:
                impl = BejImplInstance;
                break;
            case IndonesiaMarket.IDX:
                impl = BejImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }

        // BEJ, JSX and IDX share QuantLib's one implementation, so they share its edits, BEJ's.
        ApplyEdits(ConventionProvider.ReadCalendarEdits("Indonesia.BEJ"));
    }

    /// <inheritdoc/>
    public override string Name => impl.Name;

    /// <inheritdoc/>
    public override bool IsWeekend(Weekday w) => impl.IsWeekend(w);

    /// <inheritdoc/>
    protected override bool IsBusinessDayCore(Date date) => impl.IsBusinessDay(date);

    // The market-specific rules; QuantLib's Calendar::Impl.
    private abstract class Impl
    {
        public abstract string Name { get; }

        public abstract bool IsWeekend(Weekday w);

        public abstract bool IsBusinessDay(Date date);
    }

    private sealed class BejImpl : Impl
    {
        public override string Name => "Jakarta stock exchange";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth;
            Month m = date.Month;
            int y = date.Year;
            int dd = date.DayOfYear;
            int em = WesternImpl.EasterMonday(y);

            if (WesternImpl.IsWeekend(w)
                // New Year's Day
                || (d == 1 && m == Month.January)
                // Good Friday
                || (dd == em - 3)
                // Ascension Thursday
                || (dd == em + 38)
                // Labour Day (national holiday since 2014)
                || (d == 1 && m == Month.May && y >= 2014)
                // Pancasila Day (national holiday since 2017)
                || (d == 1 && m == Month.June && y >= 2017)
                // Independence Day
                || (d == 17 && m == Month.August)
                // Christmas
                || (d == 25 && m == Month.December)
                )
            {
                return false;
            }

            if (y == 2005)
            {
                if (// Idul Adha
                    (d == 21 && m == Month.January)
                    // Imlek
                    || (d == 9 && m == Month.February)
                    // Moslem's New Year Day
                    || (d == 10 && m == Month.February)
                    // Nyepi
                    || (d == 11 && m == Month.March)
                    // Birthday of Prophet Muhammad SAW
                    || (d == 22 && m == Month.April)
                    // Waisak
                    || (d == 24 && m == Month.May)
                    // Ascension of Prophet Muhammad SAW
                    || (d == 2 && m == Month.September)
                    // Idul Fitri
                    || ((d == 3 || d == 4) && m == Month.November)
                    // National leaves
                    || ((d == 2 || d == 7 || d == 8) && m == Month.November)
                    || (d == 26 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            if (y == 2006)
            {
                if (// Idul Adha
                    (d == 10 && m == Month.January)
                    // Moslem's New Year Day
                    || (d == 31 && m == Month.January)
                    // Nyepi
                    || (d == 30 && m == Month.March)
                    // Birthday of Prophet Muhammad SAW
                    || (d == 10 && m == Month.April)
                    // Ascension of Prophet Muhammad SAW
                    || (d == 21 && m == Month.August)
                    // Idul Fitri
                    || ((d == 24 || d == 25) && m == Month.October)
                    // National leaves
                    || ((d == 23 || d == 26 || d == 27) && m == Month.October)
                    )
                {
                    return false;
                }
            }

            if (y == 2007)
            {
                if (// Nyepi
                    (d == 19 && m == Month.March)
                    // Waisak
                    || (d == 1 && m == Month.June)
                    // Ied Adha
                    || (d == 20 && m == Month.December)
                    // National leaves
                    || (d == 18 && m == Month.May)
                    || ((d == 12 || d == 15 || d == 16) && m == Month.October)
                    || ((d == 21 || d == 24) && m == Month.October)
                    )
                {
                    return false;
                }
            }

            if (y == 2008)
            {
                if (// Islamic New Year
                    ((d == 10 || d == 11) && m == Month.January)
                    // Chinese New Year
                    || ((d == 7 || d == 8) && m == Month.February)
                    // Saka's New Year
                    || (d == 7 && m == Month.March)
                    // Birthday of the prophet Muhammad SAW
                    || (d == 20 && m == Month.March)
                    // Vesak Day
                    || (d == 20 && m == Month.May)
                    // Isra' Mi'raj of the prophet Muhammad SAW
                    || (d == 30 && m == Month.July)
                    // National leave
                    || (d == 18 && m == Month.August)
                    // Ied Fitr
                    || (d == 30 && m == Month.September)
                    || ((d == 1 || d == 2 || d == 3) && m == Month.October)
                    // Ied Adha
                    || (d == 8 && m == Month.December)
                    // Islamic New Year
                    || (d == 29 && m == Month.December)
                    // New Year's Eve
                    || (d == 31 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            if (y == 2009)
            {
                if (// Public holiday
                    (d == 2 && m == Month.January)
                    // Chinese New Year
                    || (d == 26 && m == Month.January)
                    // Birthday of the prophet Muhammad SAW
                    || (d == 9 && m == Month.March)
                    // Saka's New Year
                    || (d == 26 && m == Month.March)
                    // National leave
                    || (d == 9 && m == Month.April)
                    // Isra' Mi'raj of the prophet Muhammad SAW
                    || (d == 20 && m == Month.July)
                    // Ied Fitr
                    || (d >= 18 && d <= 23 && m == Month.September)
                    // Ied Adha
                    || (d == 27 && m == Month.November)
                    // Islamic New Year
                    || (d == 18 && m == Month.December)
                    // Public Holiday
                    || (d == 24 && m == Month.December)
                    // Trading holiday
                    || (d == 31 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            if (y == 2010)
            {
                if (// Birthday of the prophet Muhammad SAW
                       (d == 26 && m == Month.February)
                    // Saka's New Year
                    || (d == 16 && m == Month.March)
                    // Birth of Buddha
                    || (d == 28 && m == Month.May)
                    // Ied Fitr
                    || (d >= 8 && d <= 14 && m == Month.September)
                    // Ied Adha
                    || (d == 17 && m == Month.November)
                    // Islamic New Year
                    || (d == 7 && m == Month.December)
                    // Public Holiday
                    || (d == 24 && m == Month.December)
                    // Trading holiday
                    || (d == 31 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            if (y == 2011)
            {
                if (// Chinese New Year
                       (d == 3 && m == Month.February)
                    // Birthday of the prophet Muhammad SAW
                    || (d == 15 && m == Month.February)
                    // Birth of Buddha
                    || (d == 17 && m == Month.May)
                    // Isra' Mi'raj of the prophet Muhammad SAW
                    || (d == 29 && m == Month.June)
                    // Ied Fitr
                    || (d >= 29 && m == Month.August)
                    || (d <= 2 && m == Month.September)
                    // Public Holiday
                    || (d == 26 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            if (y == 2012)
            {
                if (// Chinese New Year
                    (d == 23 && m == Month.January)
                    // Saka New Year
                    || (d == 23 && m == Month.March)
                    // Ied ul-Fitr
                    || (d >= 20 && d <= 22 && m == Month.August)
                    // Eid ul-Adha
                    || (d == 26 && m == Month.October)
                    // Islamic New Year
                    || (d >= 15 && d <= 16 && m == Month.November)
                    // Public Holiday
                    || (d == 24 && m == Month.December)
                    // Trading Holiday
                    || (d == 31 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            if (y == 2013)
            {
                if (// Birthday of the prophet Muhammad SAW
                    (d == 24 && m == Month.January)
                    // Saka New Year
                    || (d == 12 && m == Month.March)
                    // Isra' Mi'raj of the prophet Muhammad SAW
                    || (d == 6 && m == Month.June)
                    // Ied ul-Fitr
                    || (d >= 5 && d <= 9 && m == Month.August)
                    // Eid ul-Adha
                    || (d >= 14 && d <= 15 && m == Month.October)
                    // Islamic New Year
                    || (d == 5 && m == Month.November)
                    // Public Holiday
                    || (d == 26 && m == Month.December)
                    // Trading Holiday
                    || (d == 31 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            if (y == 2014)
            {
                if (// Birthday of the prophet Muhammad SAW
                    (d == 14 && m == Month.January)
                    // Chinese New Year
                    || (d == 31 && m == Month.January)
                    // Saka New Year
                    || (d == 31 && m == Month.March)
                    // Birth of Buddha
                    || (d == 15 && m == Month.May)
                    // Isra' Mi'raj of the prophet Muhammad SAW
                    || (d == 27 && m == Month.May)
                    // Ascension Day of Jesus Christ
                    || (d == 29 && m == Month.May)
                    // Ied ul-Fitr
                    || ((d >= 28 && m == Month.July) || (d == 1 && m == Month.August))
                    // Public Holiday
                    || (d == 26 && m == Month.December)
                    // Trading Holiday
                    || (d == 31 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            if (y == 2015)
            {
                if (// Chinese New Year
                    (d == 19 && m == Month.February)
                    // Birth of Buddha
                    || (d == 2 && m == Month.June)
                    // Ied ul-Fitr
                    || ((d == 16 || d == 17) && m == Month.July)
                    || ((d == 20 || d == 21) && m == Month.July)
                    // Eid ul-Adha
                    || (d == 24 && m == Month.September)
                    // Islamic New Year
                    || (d == 14 && m == Month.October)
                    // Regional elections
                    || (d == 9 && m == Month.December)
                    // Birthday of the prophet Muhammad SAW
                    || (d == 24 && m == Month.December)
                    // Trading Holiday
                    || (d == 31 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            if (y == 2016)
            {
                if (// Chinese New Year
                    (d == 8 && m == Month.February)
                    // Saka New Year
                    || (d == 9 && m == Month.March)
                    // Isra' Mi'raj of the prophet Muhammad SAW
                    || (d == 6 && m == Month.May)
                    // Ied ul-Fitr
                    || (d >= 4 && d <= 8 && m == Month.July)
                    // Eid ul-Adha
                    || (d == 12 && m == Month.September)
                    // Birthday of the prophet Muhammad SAW
                    || (d == 12 && m == Month.December)
                    // National leave
                    || (d == 26 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            if (y == 2017)
            {
                if (// National leave
                    (d == 2 && m == Month.January)
                    // Regional elections
                    || (d == 15 && m == Month.February)
                    // Saka New Year
                    || (d == 28 && m == Month.March)
                    // Regional elections (Jakarta runoff)
                    || (d == 19 && m == Month.April)
                    // Isra' Mi'raj of the prophet Muhammad SAW
                    || (d == 24 && m == Month.April)
                    // Birth of Buddha
                    || (d == 11 && m == Month.May)
                    // Ied ul-Fitr
                    || (d == 23 && m == Month.June)
                    || (d >= 26 && d <= 30 && m == Month.June)
                    // Eid ul-Adha
                    || (d == 1 && m == Month.September)
                    // Islamic New Year
                    || (d == 21 && m == Month.September)
                    // Birthday of the prophet Muhammad SAW
                    || (d == 1 && m == Month.December)
                    // National leave
                    || (d == 26 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            if (y == 2018)
            {
                if (// Chinese New Year
                    (d == 16 && m == Month.February)
                    // Birth of Buddha
                    || (d == 29 && m == Month.May)
                    // Ied ul-Fitr
                    || (d >= 11 && d <= 15 && m == Month.June)
                    || ((d == 18 || d == 19) && m == Month.June)
                    // Eid ul-Adha
                    || (d == 22 && m == Month.August)
                    // Islamic New Year
                    || (d == 11 && m == Month.September)
                    // Birthday of the prophet Muhammad SAW
                    || (d == 20 && m == Month.November)
                    // National leave
                    || (d == 24 && m == Month.December)
                    // Trading Holiday
                    || (d == 31 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            if (y == 2019)
            {
                if (// Chinese New Year
                    (d == 5 && m == Month.February)
                    // Saka New Year
                    || (d == 7 && m == Month.March)
                    // Isra' Mi'raj of the prophet Muhammad SAW
                    || (d == 3 && m == Month.April)
                    // General elections
                    || (d == 17 && m == Month.April)
                    // Ied ul-Fitr
                    || (d >= 3 && d <= 7 && m == Month.June)
                    // National leave
                    || (d == 24 && m == Month.December)
                    // Trading Holiday
                    || (d == 31 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            if (y == 2020)
            {
                if (// Saka New Year
                    (d == 25 && m == Month.March)
                    // Birth of Buddha
                    || (d == 7 && m == Month.May)
                    // Ied ul-Fitr
                    || (d == 22 && m == Month.May)
                    || (d == 25 && m == Month.May)
                    // Eid ul-Adha
                    || (d == 31 && m == Month.July)
                    // Islamic New Year
                    || (d == 20 && m == Month.August)
                    // National leave
                    || (d == 21 && m == Month.August)
                    || (d == 28 && m == Month.October)
                    // Birthday of the prophet Muhammad SAW
                    || (d == 29 && m == Month.October)
                    // National leave
                    || (d == 30 && m == Month.October)
                    // Regional elections
                    || (d == 9 && m == Month.December)
                    // National leave
                    || (d == 24 && m == Month.December)
                    // Ied ul-Fitr
                    || (d == 31 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            if (y == 2021)
            {
                if (// Chinese New Year
                    (d == 12 && m == Month.February)
                    // Isra' Mi'raj of the prophet Muhammad SAW
                    || (d == 11 && m == Month.March)
                    // Ied ul-Fitr
                    || (d == 12 && m == Month.May)
                    || (d == 14 && m == Month.May)
                    // Birth of Buddha
                    || (d == 26 && m == Month.May)
                    // Eid ul-Adha
                    || (d == 20 && m == Month.July)
                    // Islamic New Year
                    || (d == 11 && m == Month.August)
                    // Birthday of the prophet Muhammad SAW
                    || (d == 20 && m == Month.October)
                    // Trading Holiday
                    || (d == 31 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            if (y == 2022)
            {
                if (// Chinese New Year
                    (d == 1 && m == Month.February)
                    // Isra' Mi'raj of the prophet Muhammad SAW
                    || (d == 28 && m == Month.February)
                    // Saka New Year
                    || (d == 3 && m == Month.March)
                    // Ied ul-Fitr
                    || ((d == 2 || d == 3) && m == Month.May)
                    // Birth of Buddha
                    || (d == 16 && m == Month.May)
                    )
                {
                    return false;
                }
            }

            if (y == 2023)
            {
                if (// National leave
                    (d == 23 && m == Month.January)
                    // Saka New Year
                    || (d == 22 && m == Month.March)
                    // National leave
                    || (d == 23 && m == Month.March)
                    // Ied ul-Fitr
                    || (d >= 19 && d <= 21 && m == Month.April)
                    || ((d == 24 || d == 25) && m == Month.April)
                    // National leave
                    || (d == 2 && m == Month.June)
                    || (d == 28 && m == Month.June)
                    // Eid ul-Adha
                    || (d == 29 && m == Month.June)
                    // National leave
                    || (d == 30 && m == Month.June)
                    // Islamic New Year
                    || (d == 19 && m == Month.July)
                    // Birthday of the prophet Muhammad SAW
                    || (d == 28 && m == Month.September)
                    // National leave
                    || (d == 26 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            if (y == 2024)
            {
                if (// Isra' Mi'raj of the prophet Muhammad SAW
                    (d == 8 && m == Month.February)
                    // National leave
                    || (d == 9 && m == Month.February)
                    // General elections
                    || (d == 14 && m == Month.February)
                    // Saka New Year
                    || (d == 11 && m == Month.March)
                    // National leave
                    || (d == 12 && m == Month.March)
                    // Ied ul-Fitr
                    || (d >= 8 && d <= 12 && m == Month.April)
                    || (d == 15 && m == Month.April)
                    // National leave
                    || (d == 10 && m == Month.May)
                    // Birth of Buddha
                    || (d == 23 && m == Month.May)
                    // National leave
                    || (d == 24 && m == Month.May)
                    // Eid ul-Adha
                    || (d == 17 && m == Month.June)
                    // National leave
                    || (d == 18 && m == Month.June)
                    // Birthday of the prophet Muhammad SAW
                    || (d == 16 && m == Month.September)
                    // Regional elections
                    || (d == 27 && m == Month.November)
                    // National leave
                    || (d == 26 && m == Month.December)
                    // Trading Holiday
                    || (d == 31 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            if (y == 2025)
            {
                if (// Isra' Mi'raj of the prophet Muhammad SAW
                    (d == 27 && m == Month.January)
                    // National leave
                    || (d == 28 && m == Month.January)
                    // Chinese New Year
                    || (d == 29 && m == Month.January)
                    // National leave
                    || (d == 28 && m == Month.March)
                    // Ied ul-Fitr
                    || (d == 31 && m == Month.March)
                    || (d >= 1 && d <= 4 && m == Month.April)
                    || (d == 7 && m == Month.April)
                    // Birth of Buddha
                    || (d == 12 && m == Month.May)
                    // National leave
                    || (d == 13 && m == Month.May)
                    || (d == 30 && m == Month.May)
                    // Eid ul-Adha
                    || (d == 6 && m == Month.June)
                    // National leave
                    || (d == 9 && m == Month.June)
                    // Islamic New Year
                    || (d == 27 && m == Month.June)
                    // Birthday of the prophet Muhammad SAW
                    || (d == 5 && m == Month.September)
                    // National leave
                    || (d == 26 && m == Month.December)
                    // Trading Holiday
                    || (d == 31 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            if (y == 2026)
            {
                if (// Isra' Mi'raj of the prophet Muhammad SAW
                    (d == 16 && m == Month.January)
                    // National leave
                    || (d == 16 && m == Month.February)
                    // Chinese New Year
                    || (d == 17 && m == Month.February)
                    // National leave
                    || (d == 18 && m == Month.March)
                    // Saka New Year
                    || (d == 19 && m == Month.March)
                    // Ied ul-Fitr
                    || (d == 20 && m == Month.March)
                    || ((d == 23 || d == 24) && m == Month.March)
                    // National leave
                    || (d == 15 && m == Month.May)
                    // Eid ul-Adha
                    || (d == 27 && m == Month.May)
                    // National leave
                    || (d == 28 && m == Month.May)
                    // Islamic New Year
                    || (d == 16 && m == Month.June)
                    // Birthday of the prophet Muhammad SAW
                    || (d == 25 && m == Month.August)
                    // National leave
                    || (d == 24 && m == Month.December)
                    // Trading Holiday
                    || (d == 31 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            return true;
        }
    }
}
