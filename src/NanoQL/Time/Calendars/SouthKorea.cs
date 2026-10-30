// SPDX-FileCopyrightText: Copyright (C) 2004 FIMAT Group
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2008 Charles Chongseok Hyun
// SPDX-FileCopyrightText: Copyright (C) 2015 Riccardo Barone
// SPDX-FileCopyrightText: Copyright (C) 2007, 2011 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2008, 2011 Charles Chongseok Hyun
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: Copyright (C) 2023 Skandinaviska Enskilda Banken AB (publ)
// SPDX-FileCopyrightText: Copyright (C) 2023 Jonghee Lee
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/southkorea.hpp ql/time/calendars/southkorea.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The South Korean calendars.</summary>
/// <remarks>
/// <para>
/// Public holidays:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Independence Day, March 1st</description></item>
/// <item><description>Arbour Day, April 5th (until 2005)</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>Children's Day, May 5th</description></item>
/// <item><description>Memorial Day, June 6th</description></item>
/// <item><description>Constitution Day, July 17th (until 2007)</description></item>
/// <item><description>Liberation Day, August 15th</description></item>
/// <item><description>National Fondation Day, October 3th</description></item>
/// <item><description>Hangeul Day, October 9th (from 2013)</description></item>
/// <item><description>Christmas Day, December 25th</description></item>
/// </list>
/// </para>
/// <para>
/// Other holidays for which no rule is given (data available for 2004-2050 only:)
/// <list type="bullet">
/// <item><description>Lunar New Year, the last day of the previous lunar year</description></item>
/// <item><description>Election Days</description></item>
/// <item><description>National Assemblies</description></item>
/// <item><description>Presidency</description></item>
/// <item><description>Regional Election Days</description></item>
/// <item><description>Buddha's birthday</description></item>
/// <item><description>Harvest Moon Day</description></item>
/// </list>
/// </para>
/// <para>
/// Holidays for the Korea exchange (data from http://eng.krx.co.kr/ or
/// http://www.dooriworld.com/daishin/holiday/holiday.html
/// https://www.law.go.kr/%EB%B2%95%EB%A0%B9/%EA%B4%80%EA%B3%B5%EC%84%9C%EC%9D%98%20%EA%B3%B5%ED%9C%B4%EC%9D%BC%EC%97%90%20%EA%B4%80%ED%95%9C%20%EA%B7%9C%EC%A0%95):
/// <list type="bullet">
/// <item><description>Public holidays as listed above</description></item>
/// <item><description>Year-end closing</description></item>
/// <item><description>Occasional closing days</description></item>
/// </list>
/// </para>
/// <para>
/// The 2025 presidential election day, June 3rd, 2025, is a holiday, as QuantLib's master has it since 1.44 (#2894);
/// QuantLib 1.44 misses it: a documented divergence.
/// </para>
/// </remarks>
public sealed class SouthKorea : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl SettlementImplInstance = new SettlementImpl();
    private static readonly Impl KrxImplInstance = new KrxImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="SouthKorea"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public SouthKorea(SouthKoreaMarket market = SouthKoreaMarket.KRX)
    {
        switch (market)
        {
            case SouthKoreaMarket.Settlement:
                impl = SettlementImplInstance;
                break;
            case SouthKoreaMarket.KRX:
                impl = KrxImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }

        ApplyEdits(ConventionProvider.ReadCalendarEdits($"SouthKorea.{market}"));
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

    private class SettlementImpl : Impl
    {
        public override string Name => "South-Korean settlement";

        public override bool IsWeekend(Weekday w)
        {
            return w == Weekday.Saturday || w == Weekday.Sunday;
        }

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth;
            Month m = date.Month;
            int y = date.Year;

            if (IsWeekend(w)
                // New Year's Day
                || (d == 1 && m == Month.January)
                // Independence Day
                || (d == 1 && m == Month.March)
                || (w == Weekday.Monday && (d == 2 || d == 3) && m == Month.March && y > 2021)
                // Arbour Day
                || (d == 5 && m == Month.April && y <= 2005)
                // Labour Day
                || (d == 1 && m == Month.May)
                // Children's Day
                || (d == 5 && m == Month.May)
                || (w == Weekday.Monday && (d == 6 || d == 7) && m == Month.May && y > 2013)
                // Memorial Day
                || (d == 6 && m == Month.June)
                // Constitution Day
                || (d == 17 && m == Month.July && y <= 2007)
                // Liberation Day
                || (d == 15 && m == Month.August)
                || (w == Weekday.Monday && (d == 16 || d == 17) && m == Month.August && y > 2020)
                // National Foundation Day
                || (d == 3 && m == Month.October)
                || (w == Weekday.Monday && (d == 4 || d == 5) && m == Month.October && y > 2020)
                // Christmas Day
                || (d == 25 && m == Month.December)
                || (w == Weekday.Monday && (d == 26 || d == 27) && m == Month.December && y > 2022)

                // Lunar New Year
                || ((d == 21 || d == 22 || d == 23) && m == Month.January && y == 2004)
                || ((d == 8 || d == 9 || d == 10) && m == Month.February && y == 2005)
                || ((d == 28 || d == 29 || d == 30) && m == Month.January && y == 2006)
                || (d == 19 && m == Month.February && y == 2007)
                || ((d == 6 || d == 7 || d == 8) && m == Month.February && y == 2008)
                || ((d == 25 || d == 26 || d == 27) && m == Month.January && y == 2009)
                || ((d == 13 || d == 14 || d == 15) && m == Month.February && y == 2010)
                || ((d == 2 || d == 3 || d == 4) && m == Month.February && y == 2011)
                || ((d == 23 || d == 24) && m == Month.January && y == 2012)
                || (d == 11 && m == Month.February && y == 2013)
                || ((d == 30 || d == 31) && m == Month.January && y == 2014)
                || ((d == 18 || d == 19 || d == 20) && m == Month.February && y == 2015)
                || ((d >= 7 && d <= 10) && m == Month.February && y == 2016)
                || ((d >= 27 && d <= 30) && m == Month.January && y == 2017)
                || ((d == 15 || d == 16 || d == 17) && m == Month.February && y == 2018)
                || ((d == 4 || d == 5 || d == 6) && m == Month.February && y == 2019)
                || ((d >= 24 && d <= 27) && m == Month.January && y == 2020)
                || ((d == 11 || d == 12 || d == 13) && m == Month.February && y == 2021)
                || (((d == 31 && m == Month.January) || ((d == 1 || d == 2)
                                                  && m == Month.February)) && y == 2022)
                || ((d == 23 || d == 24) && m == Month.January && y == 2023)
                || ((d >= 9 && d <= 12) && m == Month.February && y == 2024)
                || ((d == 28 || d == 29 || d == 30) && m == Month.January && y == 2025)
                || ((d == 16 || d == 17 || d == 18) && m == Month.February && y == 2026)
                || ((d == 8 || d == 9) && m == Month.February && y == 2027)
                || ((d == 26 || d == 27 || d == 28) && m == Month.January && y == 2028)
                || ((d == 12 || d == 13 || d == 14) && m == Month.February && y == 2029)
                || ((d == 4 || d == 5) && m == Month.February && y == 2030)
                || ((d == 22 || d == 23 || d == 24) && m == Month.January && y == 2031)
                || ((d == 10 || d == 11 || d == 12) && m == Month.February && y == 2032)
                || (((d == 31 && m == Month.January) || ((d == 1 || d == 2)
                                                   && m == Month.February)) && y == 2033)
                || ((d == 20 || d == 21) && m == Month.February && y == 2034)
                || ((d == 7 || d == 8 || d == 9) && m == Month.February && y == 2035)
                || ((d == 28 || d == 29 || d == 30) && m == Month.January && y == 2036)
                || ((d == 16 || d == 17) && m == Month.February && y == 2037)
                || ((d == 3 || d == 4 || d == 5) && m == Month.February && y == 2038)
                || ((d == 24 || d == 25 || d == 26) && m == Month.January && y == 2039)
                || ((d == 13 || d == 14) && m == Month.February && y == 2040)
                || (((d == 31 && m == Month.January) || ((d == 1 || d == 2)
                                                   && m == Month.February)) && y == 2041)
                || ((d == 21 || d == 22 || d == 23) && m == Month.January && y == 2042)
                || ((d == 9 || d == 10 || d == 11) && m == Month.February && y == 2043)
                || ((((d == 29 || d == 30 || d == 31) && m == Month.January)
                                       || (d == 1 && m == Month.February)) && y == 2044)
                || ((d == 16 || d == 17 || d == 18) && m == Month.February && y == 2045)
                || ((d == 5 || d == 6 || d == 7) && m == Month.February && y == 2046)
                || ((d >= 25 && d <= 28) && m == Month.January && y == 2047)
                || ((d == 13 || d == 14 || d == 15) && m == Month.February && y == 2048)
                || ((d == 1 || d == 2 || d == 3) && m == Month.February && y == 2049)
                || ((d == 24 || d == 25) && m == Month.January && y == 2050)

                // Election Days
                || (d == 15 && m == Month.April && y == 2004) // National Assembly
                || (d == 31 && m == Month.May && y == 2006) // Regional election
                || (d == 19 && m == Month.December && y == 2007) // Presidency
                || (d == 9 && m == Month.April && y == 2008) // National Assembly
                || (d == 2 && m == Month.June && y == 2010) // Local election
                || (d == 11 && m == Month.April && y == 2012) // National Assembly
                || (d == 19 && m == Month.December && y == 2012) // Presidency
                || (d == 4 && m == Month.June && y == 2014) // Local election
                || (d == 13 && m == Month.April && y == 2016) // National Assembly
                || (d == 9 && m == Month.May && y == 2017) // Presidency
                || (d == 13 && m == Month.June && y == 2018) // Local election
                || (d == 15 && m == Month.April && y == 2020) // National Assembly
                || (d == 9 && m == Month.March && y == 2022) // Presidency
                || (d == 1 && m == Month.June && y == 2022) // Local election
                || (d == 10 && m == Month.April && y == 2024) // National Assembly
                || (d == 3 && m == Month.June && y == 2025) // Presidency
                // Buddha's birthday
                || (d == 26 && m == Month.May && y == 2004)
                || (d == 15 && m == Month.May && y == 2005)
                || (d == 5 && m == Month.May && y == 2006)
                || (d == 24 && m == Month.May && y == 2007)
                || (d == 12 && m == Month.May && y == 2008)
                || (d == 2 && m == Month.May && y == 2009)
                || (d == 21 && m == Month.May && y == 2010)
                || (d == 10 && m == Month.May && y == 2011)
                || (d == 28 && m == Month.May && y == 2012)
                || (d == 17 && m == Month.May && y == 2013)
                || (d == 6 && m == Month.May && y == 2014)
                || (d == 25 && m == Month.May && y == 2015)
                || (d == 14 && m == Month.May && y == 2016)
                || (d == 3 && m == Month.May && y == 2017)
                || (d == 22 && m == Month.May && y == 2018)
                || (d == 12 && m == Month.May && y == 2019)
                || (d == 30 && m == Month.April && y == 2020)
                || (d == 19 && m == Month.May && y == 2021)
                || (d == 8 && m == Month.May && y == 2022)
                || (d == 29 && m == Month.May && y == 2023) // Substitute holiday
                || (d == 15 && m == Month.May && y == 2024)
                || (d == 6 && m == Month.May && y == 2025)
                || (d == 25 && m == Month.May && y == 2026) // Substitute holiday
                || (d == 13 && m == Month.May && y == 2027)
                || (d == 2 && m == Month.May && y == 2028)
                || (d == 21 && m == Month.May && y == 2029) // Substitute holiday
                || (d == 9 && m == Month.May && y == 2030)
                || (d == 28 && m == Month.May && y == 2031)
                || (d == 17 && m == Month.May && y == 2032) // Substitute holiday
                || (d == 6 && m == Month.May && y == 2033)
                || (d == 25 && m == Month.May && y == 2034)
                || (d == 15 && m == Month.May && y == 2035)
                || (d == 6 && m == Month.May && y == 2036) // Substitute holiday
                || (d == 22 && m == Month.May && y == 2037)
                || (d == 11 && m == Month.May && y == 2038)
                || (d == 2 && m == Month.May && y == 2039) // Substitute holiday
                || (d == 18 && m == Month.May && y == 2040)
                || (d == 7 && m == Month.May && y == 2041)
                || (d == 26 && m == Month.May && y == 2042)
                || (d == 18 && m == Month.May && y == 2043) // Substitute holiday
                || (d == 6 && m == Month.May && y == 2044)
                || (d == 24 && m == Month.May && y == 2045)
                || (d == 14 && m == Month.May && y == 2046) // Substitute holiday
                || (d == 2 && m == Month.May && y == 2047)
                || (d == 20 && m == Month.May && y == 2048)
                || (d == 10 && m == Month.May && y == 2049) // Substitute holiday
                || (d == 30 && m == Month.May && y == 2050) // Substitute holiday

                // Special holiday: 70 years from Independence Day
                || (d == 14 && m == Month.August && y == 2015)
                // Special temporary holiday
                || (d == 17 && m == Month.August && y == 2020)
                || (d == 2 && m == Month.October && y == 2023)
                || (d == 1 && m == Month.October && y == 2024)
                || (d == 27 && m == Month.January && y == 2025)

                // Harvest Moon Day
                || ((d == 27 || d == 28 || d == 29) && m == Month.September && y == 2004)
                || ((d == 17 || d == 18 || d == 19) && m == Month.September && y == 2005)
                || ((d == 5 || d == 6 || d == 7) && m == Month.October && y == 2006)
                || ((d == 24 || d == 25 || d == 26) && m == Month.September && y == 2007)
                || ((d == 13 || d == 14 || d == 15) && m == Month.September && y == 2008)
                || ((d == 2 || d == 3 || d == 4) && m == Month.October && y == 2009)
                || ((d == 21 || d == 22 || d == 23) && m == Month.September && y == 2010)
                || ((d == 12 || d == 13) && m == Month.September && y == 2011)
                || ((d == 1) && m == Month.October && y == 2012)
                || ((d == 18 || d == 19 || d == 20) && m == Month.September && y == 2013)
                || ((d == 8 || d == 9 || d == 10) && m == Month.September && y == 2014)
                || ((d == 28 || d == 29) && m == Month.September && y == 2015)
                || ((d == 14 || d == 15 || d == 16) && m == Month.September && y == 2016)
                || ((d >= 3 && d <= 6) && m == Month.October && y == 2017)
                || ((d >= 23 && d <= 26) && m == Month.September && y == 2018)
                || ((d == 12 || d == 13 || d == 14) && m == Month.September && y == 2019)
                || (((d == 30 && m == Month.September) || ((d == 1 || d == 2)
                                                    && m == Month.October)) && y == 2020)
                || ((d == 20 || d == 21 || d == 22) && m == Month.September && y == 2021)
                || ((d == 9 || d == 10 || d == 11) && m == Month.September && y == 2022)
                || ((d >= 9 && d <= 12) && m == Month.September && y == 2022)
                || ((d == 28 || d == 29 || d == 30) && m == Month.September && y == 2023)
                || ((d == 16 || d == 17 || d == 18) && m == Month.September && y == 2024)
                || ((d == 6 || d == 7 || d == 8) && m == Month.October && y == 2025)
                || ((d == 24 || d == 25 || d == 26) && m == Month.September && y == 2026)
                || ((d == 14 || d == 15 || d == 16) && m == Month.September && y == 2027)
                || ((d >= 2 && d <= 5) && m == Month.October && y == 2028)
                || ((d >= 21 && d <= 24) && m == Month.September && y == 2029)
                || ((d == 11 || d == 12 || d == 13) && m == Month.September && y == 2030)
                || (((d == 30 && m == Month.September) || ((d == 1 || d == 2)
                                                    && m == Month.October)) && y == 2031)
                || ((d == 20 || d == 21) && m == Month.September && y == 2032)
                || ((d == 7 || d == 8 || d == 9) && m == Month.September && y == 2033)
                || ((d == 26 || d == 27 || d == 28) && m == Month.September && y == 2034)
                || ((d == 17 || d == 18) && m == Month.September && y == 2035)
                || ((d >= 3 && d <= 7) && m == Month.October && y == 2036)
                || ((d == 23 || d == 24 || d == 25) && m == Month.September && y == 2037)
                || ((d == 13 || d == 14 || d == 15) && m == Month.September && y == 2038)
                || ((d == 3 || d == 4 || d == 5) && m == Month.October && y == 2039)
                || ((d == 20 || d == 21 || d == 22) && m == Month.September && y == 2040)
                || ((d == 9 || d == 10 || d == 11) && m == Month.September && y == 2041)
                || ((d == 29 || d == 30) && m == Month.September && y == 2042)
                || ((d == 16 || d == 17 || d == 18) && m == Month.September && y == 2043)
                || ((d == 4 || d == 5 || d == 6) && m == Month.October && y == 2044)
                || ((d == 25 || d == 26 || d == 27) && m == Month.September && y == 2045)
                || ((d >= 14 && d <= 17) && m == Month.September && y == 2046)
                || ((d == 4 || d == 5 || d == 7) && m == Month.October && y == 2047)
                || ((d == 21 || d == 22 || d == 23) && m == Month.September && y == 2048)
                || ((d >= 10 && d <= 13) && m == Month.September && y == 2049)
                || ((((d == 29 || d == 30) && m == Month.September)
                                        || (d == 1 && m == Month.October)) && y == 2050)

                // Hangul Proclamation of Korea
                || (d == 9 && m == Month.October && y >= 2013)
                || (w == Weekday.Monday && (d == 10 || d == 11) && m == Month.October && y > 2020)
                )
            {
                return false;
            }

            return true;
        }
    }

    private sealed class KrxImpl : SettlementImpl
    {
        public override string Name => "South-Korea exchange";

        public override bool IsBusinessDay(Date date)
        {
            // public holidays
            if (!base.IsBusinessDay(date))
            {
                return false;
            }

            int d = date.DayOfMonth;
            Weekday w = date.Weekday;
            Month m = date.Month;
            int y = date.Year;

            if (// Year-end closing
                ((((d == 29 || d == 30) && w == Weekday.Friday) || d == 31)
                 && m == Month.December))
            {
                return false;
            }

            if (// occasional closing days (KRX day)
                (d == 6 && m == Month.May && y == 2016)
                || (d == 2 && m == Month.October && y == 2017))
            {
                return false;
            }

            return true;
        }
    }
}
