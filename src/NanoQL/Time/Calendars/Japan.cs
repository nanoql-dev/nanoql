// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2000-2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003 Kawanishi Tomoya
// SPDX-FileCopyrightText: Copyright (C) 2016, 2019, 2020 Eisuke Tani
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/japan.hpp ql/time/calendars/japan.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Japanese calendar.</summary>
/// <remarks>
/// <para>
/// Holidays:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Bank Holiday, January 2nd</description></item>
/// <item><description>Bank Holiday, January 3rd</description></item>
/// <item><description>Coming of Age Day, 2nd Monday in January</description></item>
/// <item><description>National Foundation Day, February 11th</description></item>
/// <item><description>Emperor's Birthday, February 23rd since 2020 and December 23rd before</description></item>
/// <item><description>Vernal Equinox</description></item>
/// <item><description>Greenery Day, April 29th</description></item>
/// <item><description>Constitution Memorial Day, May 3rd</description></item>
/// <item><description>Holiday for a Nation, May 4th</description></item>
/// <item><description>Children's Day, May 5th</description></item>
/// <item><description>Marine Day, 3rd Monday in July</description></item>
/// <item><description>Mountain Day, August 11th (from 2016 onwards)</description></item>
/// <item><description>Respect for the Aged Day, 3rd Monday in September</description></item>
/// <item><description>Autumnal Equinox</description></item>
/// <item><description>Health and Sports Day, 2nd Monday in October</description></item>
/// <item><description>National Culture Day, November 3rd</description></item>
/// <item><description>Labor Thanksgiving Day, November 23rd</description></item>
/// <item><description>Bank Holiday, December 31st</description></item>
/// <item><description>a few one-shot holidays</description></item>
/// </list>
/// </para>
/// <para>
/// Holidays falling on a Sunday are observed on the Monday following except for the bank holidays associated with the
/// new year.
/// </para>
/// </remarks>
public sealed class Japan : Calendar
{
    /// <summary>Initializes a new instance of the <see cref="Japan"/> class.</summary>
    public Japan()
    {
    }

    /// <inheritdoc/>
    public override string Name => "Japan";

    /// <inheritdoc/>
    public override bool IsWeekend(Weekday w) => w == Weekday.Saturday || w == Weekday.Sunday;

    /// <inheritdoc/>
    protected override bool IsBusinessDayCore(Date date)
    {
        Weekday w = date.Weekday;
        int d = date.DayOfMonth;
        Month m = date.Month;
        int y = date.Year;

        // equinox calculation
        const double exactVernalEquinoxTime = 20.69115;
        const double exactAutumnalEquinoxTime = 23.09;
        const double diffPerYear = 0.242194;
        double movingAmount = (y - 2000) * diffPerYear;
        int numberOfLeapYears = ((y - 2000) / 4) + ((y - 2000) / 100) - ((y - 2000) / 400);

        // vernal equinox day; QuantLib's Day(...) truncates towards zero, as the cast does
        int ve = (int)(exactVernalEquinoxTime + movingAmount - numberOfLeapYears);

        // autumnal equinox day
        int ae = (int)(exactAutumnalEquinoxTime + movingAmount - numberOfLeapYears);

        // checks
        if (IsWeekend(w)
            // New Year's Day
            || (d == 1 && m == Month.January)
            // Bank Holiday
            || (d == 2 && m == Month.January)
            // Bank Holiday
            || (d == 3 && m == Month.January)
            // Coming of Age Day (2nd Monday in January),
            // was January 15th until 2000
            || (w == Weekday.Monday && (d >= 8 && d <= 14) && m == Month.January
                && y >= 2000)
            || ((d == 15 || (d == 16 && w == Weekday.Monday)) && m == Month.January
                && y < 2000)
            // National Foundation Day
            || ((d == 11 || (d == 12 && w == Weekday.Monday)) && m == Month.February)
            // Emperor's Birthday (Emperor Naruhito)
            || ((d == 23 || (d == 24 && w == Weekday.Monday)) && m == Month.February
                && y >= 2020)
            // Emperor's Birthday (Emperor Akihito)
            || ((d == 23 || (d == 24 && w == Weekday.Monday)) && m == Month.December
                && (y >= 1989 && y < 2019))
            // Vernal Equinox
            || ((d == ve || (d == ve + 1 && w == Weekday.Monday)) && m == Month.March)
            // Greenery Day
            || ((d == 29 || (d == 30 && w == Weekday.Monday)) && m == Month.April)
            // Constitution Memorial Day
            || (d == 3 && m == Month.May)
            // Holiday for a Nation
            || (d == 4 && m == Month.May)
            // Children's Day
            || (d == 5 && m == Month.May)
            // any of the three above observed later if on Saturday or Sunday
            || (d == 6 && m == Month.May
                && (w == Weekday.Monday || w == Weekday.Tuesday || w == Weekday.Wednesday))
            // Marine Day (3rd Monday in July),
            // was July 20th until 2003, not a holiday before 1996,
            // July 23rd in 2020 due to Olympics games
            // July 22nd in 2021 due to Olympics games
            || (w == Weekday.Monday && (d >= 15 && d <= 21) && m == Month.July
                && ((y >= 2003 && y < 2020) || y >= 2022))
            || ((d == 20 || (d == 21 && w == Weekday.Monday)) && m == Month.July
                && y >= 1996 && y < 2003)
            || (d == 23 && m == Month.July && y == 2020)
            || (d == 22 && m == Month.July && y == 2021)
            // Mountain Day
            // (moved in 2020 due to Olympics games)
            // (moved in 2021 due to Olympics games)
            || ((d == 11 || (d == 12 && w == Weekday.Monday)) && m == Month.August
                && ((y >= 2016 && y < 2020) || y >= 2022))
            || (d == 10 && m == Month.August && y == 2020)
            || (d == 9 && m == Month.August && y == 2021)
            // Respect for the Aged Day (3rd Monday in September),
            // was September 15th until 2003
            || (w == Weekday.Monday && (d >= 15 && d <= 21) && m == Month.September
                && y >= 2003)
            || ((d == 15 || (d == 16 && w == Weekday.Monday)) && m == Month.September
                && y < 2003)
            // If a single day falls between Respect for the Aged Day
            // and the Autumnal Equinox, it is holiday
            || (w == Weekday.Tuesday && d + 1 == ae && d >= 16 && d <= 22
                && m == Month.September && y >= 2003)
            // Autumnal Equinox
            || ((d == ae || (d == ae + 1 && w == Weekday.Monday)) && m == Month.September)
            // Health and Sports Day (2nd Monday in October),
            // was October 10th until 2000,
            // July 24th in 2020 due to Olympics games
            // July 23rd in 2021 due to Olympics games
            || (w == Weekday.Monday && (d >= 8 && d <= 14) && m == Month.October
                && ((y >= 2000 && y < 2020) || y >= 2022))
            || ((d == 10 || (d == 11 && w == Weekday.Monday)) && m == Month.October
                && y < 2000)
            || (d == 24 && m == Month.July && y == 2020)
            || (d == 23 && m == Month.July && y == 2021)
            // National Culture Day
            || ((d == 3 || (d == 4 && w == Weekday.Monday)) && m == Month.November)
            // Labor Thanksgiving Day
            || ((d == 23 || (d == 24 && w == Weekday.Monday)) && m == Month.November)
            // Bank Holiday
            || (d == 31 && m == Month.December)
            // one-shot holidays
            // Marriage of Prince Akihito
            || (d == 10 && m == Month.April && y == 1959)
            // Rites of Imperial Funeral
            || (d == 24 && m == Month.February && y == 1989)
            // Enthronement Ceremony (Emperor Akihito)
            || (d == 12 && m == Month.November && y == 1990)
            // Marriage of Prince Naruhito
            || (d == 9 && m == Month.June && y == 1993)
            // Special holiday based on Japanese public holidays law
            || (d == 30 && m == Month.April && y == 2019)
            // Enthronement Day (Emperor Naruhito)
            || (d == 1 && m == Month.May && y == 2019)
            // Special holiday based on Japanese public holidays law
            || (d == 2 && m == Month.May && y == 2019)
            // Enthronement Ceremony (Emperor Naruhito)
            || (d == 22 && m == Month.October && y == 2019))
        {
            return false;
        }

        return true;
    }
}
