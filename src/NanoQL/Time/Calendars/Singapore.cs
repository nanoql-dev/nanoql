// SPDX-FileCopyrightText: Copyright (C) 2004 FIMAT Group
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009, 2010 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2024 Skandinaviska Enskilda Banken AB (publ)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/singapore.hpp ql/time/calendars/singapore.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Singapore calendars.</summary>
/// <remarks>
/// <para>
/// Holidays for the Singapore exchange (data from
/// http://www.sgx.com/wps/portal/sgxweb/home/trading/securities/trading_hours_calendar):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's day, January 1st</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>National Day, August 9th</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// </list>
/// </para>
/// <para>
/// Other holidays for which no rule is given (data available for 2004-2010, 2012-2014, 2019-2027 only:)
/// <list type="bullet">
/// <item><description>Chinese New Year</description></item>
/// <item><description>Hari Raya Haji</description></item>
/// <item><description>Vesak Poya Day</description></item>
/// <item><description>Deepavali</description></item>
/// <item><description>Diwali</description></item>
/// <item><description>Hari Raya Puasa</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class Singapore : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl SgxImplInstance = new SgxImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Singapore"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Singapore(SingaporeMarket market = SingaporeMarket.SGX)
    {
        switch (market)
        {
            case SingaporeMarket.SGX:
                impl = SgxImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }

        ApplyEdits(ConventionProvider.ReadCalendarEdits($"Singapore.{market}"));
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

    private sealed class SgxImpl : Impl
    {
        public override string Name => "Singapore exchange";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);

            if (WesternImpl.IsWeekend(w)
                // New Year's Day
                || ((d == 1 || (d == 2 && w == Weekday.Monday)) && m == Month.January)
                // Good Friday
                || (dd == em - 3)
                // Labor Day
                || (d == 1 && m == Month.May)
                // National Day
                || ((d == 9 || (d == 10 && w == Weekday.Monday)) && m == Month.August)
                // Christmas Day
                || (d == 25 && m == Month.December)

                // Chinese New Year
                || ((d == 22 || d == 23) && m == Month.January && y == 2004)
                || ((d == 9 || d == 10) && m == Month.February && y == 2005)
                || ((d == 30 || d == 31) && m == Month.January && y == 2006)
                || ((d == 19 || d == 20) && m == Month.February && y == 2007)
                || ((d == 7 || d == 8) && m == Month.February && y == 2008)
                || ((d == 26 || d == 27) && m == Month.January && y == 2009)
                || ((d == 15 || d == 16) && m == Month.January && y == 2010)
                || ((d == 23 || d == 24) && m == Month.January && y == 2012)
                || ((d == 11 || d == 12) && m == Month.February && y == 2013)
                || (d == 31 && m == Month.January && y == 2014)
                || (d == 1 && m == Month.February && y == 2014)

                // Hari Raya Haji
                || ((d == 1 || d == 2) && m == Month.February && y == 2004)
                || (d == 21 && m == Month.January && y == 2005)
                || (d == 10 && m == Month.January && y == 2006)
                || (d == 2 && m == Month.January && y == 2007)
                || (d == 20 && m == Month.December && y == 2007)
                || (d == 8 && m == Month.December && y == 2008)
                || (d == 27 && m == Month.November && y == 2009)
                || (d == 17 && m == Month.November && y == 2010)
                || (d == 26 && m == Month.October && y == 2012)
                || (d == 15 && m == Month.October && y == 2013)
                || (d == 6 && m == Month.October && y == 2014)

                // Vesak Poya Day
                || (d == 2 && m == Month.June && y == 2004)
                || (d == 22 && m == Month.May && y == 2005)
                || (d == 12 && m == Month.May && y == 2006)
                || (d == 31 && m == Month.May && y == 2007)
                || (d == 18 && m == Month.May && y == 2008)
                || (d == 9 && m == Month.May && y == 2009)
                || (d == 28 && m == Month.May && y == 2010)
                || (d == 5 && m == Month.May && y == 2012)
                || (d == 24 && m == Month.May && y == 2013)
                || (d == 13 && m == Month.May && y == 2014)

                // Deepavali
                || (d == 11 && m == Month.November && y == 2004)
                || (d == 8 && m == Month.November && y == 2007)
                || (d == 28 && m == Month.October && y == 2008)
                || (d == 16 && m == Month.November && y == 2009)
                || (d == 5 && m == Month.November && y == 2010)
                || (d == 13 && m == Month.November && y == 2012)
                || (d == 2 && m == Month.November && y == 2013)
                || (d == 23 && m == Month.October && y == 2014)

                // Diwali
                || (d == 1 && m == Month.November && y == 2005)

                // Hari Raya Puasa
                || ((d == 14 || d == 15) && m == Month.November && y == 2004)
                || (d == 3 && m == Month.November && y == 2005)
                || (d == 24 && m == Month.October && y == 2006)
                || (d == 13 && m == Month.October && y == 2007)
                || (d == 1 && m == Month.October && y == 2008)
                || (d == 21 && m == Month.September && y == 2009)
                || (d == 10 && m == Month.September && y == 2010)
                || (d == 20 && m == Month.August && y == 2012)
                || (d == 8 && m == Month.August && y == 2013)
                || (d == 28 && m == Month.July && y == 2014)
                )
            {
                return false;
            }

            // https://api2.sgx.com/sites/default/files/2019-01/2019%20DT%20Calendar.pdf
            if (y == 2019)
            {
                if ( // Chinese New Year
                    ((d == 5 || d == 6) && m == Month.February)
                    // Vesak Poya Day
                    || (d == 20 && m == Month.May)
                    // Hari Raya Puasa
                    || (d == 5 && m == Month.June)
                    // Hari Raya Haji
                    || (d == 12 && m == Month.August)
                    // Deepavali
                    || (d == 28 && m == Month.October)
                    )
                {
                    return false;
                }
            }

            // https://api2.sgx.com/sites/default/files/2020-11/SGX%20Derivatives%20Trading%20Calendar%202020_Dec%20Update_D3.pdf
            if (y == 2020)
            {
                if ( // Chinese New Year
                    (d == 27 && m == Month.January)
                    // Vesak Poya Day
                    || (d == 7 && m == Month.May)
                    // Hari Raya Puasa
                    || (d == 25 && m == Month.May)
                    // Hari Raya Haji
                    || (d == 31 && m == Month.July)
                    // Deepavali
                    || (d == 14 && m == Month.November)
                    )
                {
                    return false;
                }
            }

            // https://api2.sgx.com/sites/default/files/2021-07/SGX_Derivatives%20Trading%20Calendar%202021%20%28Final%20-%20Jul%29.pdf
            if (y == 2021)
            {
                if ( // Chinese New Year
                    (d == 12 && m == Month.February)
                    // Hari Raya Puasa
                    || (d == 13 && m == Month.May)
                    // Vesak Poya Day
                    || (d == 26 && m == Month.May)
                    // Hari Raya Haji
                    || (d == 20 && m == Month.July)
                    // Deepavali
                    || (d == 4 && m == Month.November)
                    )
                {
                    return false;
                }
            }

            // https://api2.sgx.com/sites/default/files/2022-06/DT%20Trading%20Calendar%202022%20%28Final%29.pdf
            if (y == 2022)
            {
                if (// Chinese New Year
                    ((d == 1 || d == 2) && m == Month.February)
                    // Labour Day
                    || (d == 2 && m == Month.May)
                    // Hari Raya Puasa
                    || (d == 3 && m == Month.May)
                    // Vesak Poya Day
                    || (d == 16 && m == Month.May)
                    // Hari Raya Haji
                    || (d == 11 && m == Month.July)
                    // Deepavali
                    || (d == 24 && m == Month.October)
                    // Christmas Day
                    || (d == 26 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            // https://api2.sgx.com/sites/default/files/2023-01/SGX%20Calendar%202023_0.pdf
            if (y == 2023)
            {
                if (// Chinese New Year
                    ((d == 23 || d == 24) && m == Month.January)
                    // Hari Raya Puasa
                    || (d == 22 && m == Month.April)
                    // Vesak Poya Day
                    || (d == 2 && m == Month.June)
                    // Hari Raya Haji
                    || (d == 29 && m == Month.June)
                    // Public holiday on polling day
                    || (d == 1 && m == Month.September)
                    // Deepavali
                    || (d == 13 && m == Month.November))
                {
                    return false;
                }
            }
            // https://api2.sgx.com/sites/default/files/2024-01/SGX%20Calendar%202024_2.pdf
            if (y == 2024)
            {
                if (// Chinese New Year
                    (d == 12 && m == Month.February)
                    // Hari Raya Puasa
                    || (d == 10 && m == Month.April)
                    // Vesak Poya Day
                    || (d == 22 && m == Month.May)
                    // Hari Raya Haji
                    || (d == 17 && m == Month.June)
                    // Deepavali
                    || (d == 31 && m == Month.October))
                {
                    return false;
                }
            }

            // https://api2.sgx.com/sites/default/files/2025-07/DT%20Trading%20Calendar%202025%20%28updated%2031%20Jul%202025%29.pdf
            if (y == 2025)
            {
                if (// Chinese New Year
                    ((d == 29 || d == 30) && m == Month.January)
                    // Hari Raya Puasa
                    || (d == 31 && m == Month.March)
                    // Vesak Poya Day
                    || (d == 12 && m == Month.May)
                    // Deepavali
                    || (d == 20 && m == Month.October))
                {
                    return false;
                }
            }

            // https://api2.sgx.com/sites/default/files/2026-01/SGX%20Calendar%202026_2.pdf
            if (y == 2026)
            {
                if (// Chinese New Year
                    ((d == 17 || d == 18) && m == Month.February)
                    // Hari Raya Puasa
                    || (d == 20 && m == Month.March)
                    // Hari Raya Haji
                    || (d == 27 && m == Month.May)
                    // Vesak Day (Sunday May 31st, observed Monday June 1st)
                    || (d == 1 && m == Month.June)
                    // Deepavali (Sunday Nov 8th, observed Monday Nov 9th)
                    || (d == 9 && m == Month.November))
                {
                    return false;
                }
            }

            // https://data.gov.sg/datasets/d_0ba69fc6d56717ff0bf8083c6af3bb84/view
            if (y == 2027)
            {
                if (// Chinese New Year (Sat/Sun 6-7 Feb, observed Monday 8 Feb)
                    (d == 8 && m == Month.February)
                    // Hari Raya Puasa
                    || (d == 10 && m == Month.March)
                    // Hari Raya Haji
                    || (d == 17 && m == Month.May)
                    // Vesak Day
                    || (d == 20 && m == Month.May)
                    // Deepavali
                    || (d == 28 && m == Month.October))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
