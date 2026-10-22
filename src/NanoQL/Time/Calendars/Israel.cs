// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: Copyright (C) 2015 Riccardo Barone
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/israel.hpp ql/time/calendars/israel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Time;

/// <summary>The Israeli calendars.</summary>
/// <remarks>
/// <para>
/// Due to the lack of reliable sources, the settlement calendar has the same holidays as the Tel Aviv stock-exchange.
/// </para>
/// <para>
/// Holidays for the Tel-Aviv Stock Exchange (TASE) (data from http://www.tase.co.il):
/// <list type="bullet">
/// <item><description>Weekdays: Friday and Saturday until 2025, Saturday and Sunday since 2026</description></item>
/// </list>
/// </para>
/// <para>
/// Other holidays for which no rule is given (data available for 2000-2050 only:)
/// <list type="bullet">
/// <item><description>Purim, Adar 14th (between Feb 24th &amp; Mar 26th)</description></item>
/// <item><description>Passover I, Nisan 15th (between Mar 26th &amp; Apr 25th)</description></item>
/// <item><description>Passover VII, Nisan 21st (between Apr 1st &amp; May 1st)</description></item>
/// <item><description>Memorial Day, Nisan 27th (between Apr 7th &amp; May 7th)</description></item>
/// <item><description>Indipendence Day, Iyar 5th (between Apr 15th &amp; May 15th)</description></item>
/// <item><description>Pentecost (Shavuot), Sivan 6th (between May 15th &amp; June 14th)</description></item>
/// <item><description>Fast Day</description></item>
/// <item><description>Jewish New Year, Tishrei 1st &amp; 2nd (between Sep 5th &amp; Oct 5th)</description></item>
/// <item><description>Yom Kippur, Tishrei 10th (between Sep 14th &amp; Oct 14th)</description></item>
/// <item><description>Sukkoth, Tishrei 15th (between Sep 19th &amp; Oct 19th)</description></item>
/// <item><description>Simchat Tora, Tishrei 22nd (between Sep 26th &amp; Oct 26th)</description></item>
/// </list>
/// </para>
/// <para>
/// Holidays for the Telbor fixing calendar:
/// <list type="bullet">
/// <item><description>Weekdays: Saturday and Sunday</description></item>
/// <item><description>Purim I and II</description></item>
/// <item><description>Passover Eve, I and VII</description></item>
/// <item><description>Indipendence Day</description></item>
/// <item><description>Pentecost (Shavuot)</description></item>
/// <item><description>Fast Day</description></item>
/// <item><description>Jewish New Year I and II</description></item>
/// <item><description>Yom Kippur</description></item>
/// <item><description>Sukkoth</description></item>
/// <item><description>Simchat Torah</description></item>
/// <item><description>Western New Year, January 1st</description></item>
/// <item><description>Spring Bank Holiday, last Monday of May</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>Boxing Day, December 26th</description></item>
/// <item><description>One-off days for elections</description></item>
/// </list>
/// </para>
/// <para>
/// Holidays for the SHIR fixing calendar:
/// <list type="bullet">
/// <item><description>Weekdays: Saturday and Sunday</description></item>
/// <item><description>Purim I and II</description></item>
/// <item><description>Passover Eve, I and VII</description></item>
/// <item><description>Indipendence Day</description></item>
/// <item><description>Pentecost (Shavuot)</description></item>
/// <item><description>Fast Day</description></item>
/// <item><description>Jewish New Year Eve, I and II</description></item>
/// <item><description>Yom Kippur and its Eve</description></item>
/// <item><description>Sukkoth</description></item>
/// <item><description>Simchat Torah</description></item>
/// <item><description>Western New Year, January 1st</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Spring Bank Holiday, last Monday of May</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>Boxing Day, December 26th</description></item>
/// <item><description>One-off days for elections</description></item>
/// </list>
/// </para>
/// <para>
/// The feasts' tables hold 2000 to 2050. A rule that reads a neighbouring day (an eve, Yom Kippur nine days after the
/// New Year) reads it by serial number, so near the ends of Date's range, where QuantLib's rules step out of the range
/// and throw (January 1st to 21st, 1901, and December 31st, 2199), the neighbour is in no table and the rule reads
/// false: a documented divergence, the answers QuantLib's weekend and fixed-date rules give.
/// </para>
/// <para>
/// The Tel Aviv stock exchange's weekend was Friday and Saturday until January 4th, 2026, and is Saturday and Sunday
/// since. <see cref="Calendar.IsWeekend"/> answers today's weekend, as QuantLib's does; the holiday list without
/// weekends leaves out each date's own weekend days, where QuantLib's holds every Friday before 2026.
/// </para>
/// </remarks>
public sealed class Israel : Calendar
{
    // all calendar instances for the same market share the same implementation instance
    private static readonly Impl TelAvivImplInstance = new TelAvivImpl();
    private static readonly Impl ShirImplInstance = new ShirImpl();
    private static readonly Impl TelborImplInstance = new TelborImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Israel"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows; the Tel Aviv stock exchange by
    /// default.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The market isn't a member of the enum, QuantLib's deprecated <c>Settlement</c>, 0, among them.
    /// </exception>
    public Israel(IsraelMarket market = IsraelMarket.TASE)
    {
        switch (market)
        {
            case IsraelMarket.TASE:
                impl = TelAvivImplInstance;
                break;
            case IsraelMarket.Telbor:
                impl = TelborImplInstance;
                break;
            case IsraelMarket.SHIR:
                impl = ShirImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }
    }

    /// <inheritdoc/>
    public override string Name => impl.Name;

    /// <inheritdoc/>
    public override bool IsWeekend(Weekday w) => impl.IsWeekend(w);

    /// <inheritdoc/>
    internal override bool IsWeekendOn(Date d) => impl.IsWeekendOn(d);

    /// <inheritdoc/>
    protected override bool IsBusinessDayCore(Date date) => impl.IsBusinessDay(date);

    // The market-specific rules; QuantLib's Calendar::Impl, with the date-aware weekend the holiday list reads.
    private abstract class Impl
    {
        public abstract string Name { get; }

        public abstract bool IsWeekend(Weekday w);

        public virtual bool IsWeekendOn(Date d) => IsWeekend(d.Weekday);

        public abstract bool IsBusinessDay(Date date);
    }

    private sealed class TelAvivImpl : Impl
    {
        private static readonly Date SwitchDate = new(5, Month.January, 2026);

        public override string Name => "Tel Aviv stock exchange";

        public override bool IsWeekend(Weekday w) => w == Weekday.Saturday || w == Weekday.Sunday;

        // the weekend on its own date: Friday and Saturday until the switch, Saturday and Sunday since
        public override bool IsWeekendOn(Date d)
        {
            Weekday w = d.Weekday;
            return d >= SwitchDate
                ? w == Weekday.Saturday || w == Weekday.Sunday
                : w == Weekday.Friday || w == Weekday.Saturday;
        }

        public override bool IsBusinessDay(Date date)
        {
            int y = date.Year;

            if (IsWeekendOn(date)
                || IsraelFeasts.IsPurim(date)
                || (y <= 2020 && IsraelFeasts.IsPassover1st(date, 1)) // Eve of Passover, until 2020
                || IsraelFeasts.IsPassover1st(date)
                || IsraelFeasts.IsPassover1st(date, -5) // Eve of Passover VII, until 2020
                || IsraelFeasts.IsPassover1st(date, -6) // Passover VII
                || IsraelFeasts.IsMemorialDay(date)
                || IsraelFeasts.IsIndependenceDay(date)
                || (y <= 2020 && IsraelFeasts.IsShavuot(date, 1)) // Eve of Shavuot, until 2020
                || IsraelFeasts.IsShavuot(date)
                || IsraelFeasts.IsFastDay(date)
                || (y <= 2019 && IsraelFeasts.IsNewYearsDay(date, 1)) // Eve of new year, until 2019
                || IsraelFeasts.IsNewYearsDay(date)
                || IsraelFeasts.IsNewYearsDay(date, -1) // 2nd day of new year
                || IsraelFeasts.IsYomKippur(date, 1) // Eve of Yom Kippur
                || IsraelFeasts.IsYomKippur(date)
                || IsraelFeasts.IsSukkot(date, 1) // Eve of Sukkot
                || IsraelFeasts.IsSukkot(date)
                || IsraelFeasts.IsSimchatTorah(date, 1) // Eve of Simchat Torah
                || IsraelFeasts.IsSimchatTorah(date))
            {
                return false;
            }

            return true;
        }
    }

    private sealed class TelborImpl : Impl
    {
        public override string Name => "Telbor fixing calendar";

        public override bool IsWeekend(Weekday w) => w == Weekday.Saturday || w == Weekday.Sunday;

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth;
            Month m = date.Month;
            int y = date.Year;

            if (IsWeekend(w)
                // New Year's Day
                || (d == 1 && m == Month.January)
                // General Elections
                || (((d == 9 && m == Month.April) || (d == 17 && m == Month.September)) && y == 2019)
                || (d == 2 && m == Month.March && y == 2020)
                // Holiday abroad
                || (((d == 22 && m == Month.April) || (d == 27 && m == Month.May)) && y == 2019)
                || ((((d == 10 || d == 13) && m == Month.April) || ((d == 8 || d == 25) && m == Month.May)) && y == 2020)
                // Purim
                || IsraelFeasts.IsPurim(date)
                || IsraelFeasts.IsPurim(date, -1) // Shushan Purim
                // Passover I and Passover VII
                || IsraelFeasts.IsPassover1st(date, 1) // Eve of Passover
                || IsraelFeasts.IsPassover1st(date)
                || IsraelFeasts.IsPassover1st(date, -6) // Passover VII
                // Israel Independence Day
                || IsraelFeasts.IsIndependenceDay(date)
                // Feast of Shavuot (Pentecost)
                || IsraelFeasts.IsShavuot(date)
                // Fast of Ninth of Av
                || IsraelFeasts.IsFastDay(date)
                // Jewish New Year (Rosh Hashanah)
                || IsraelFeasts.IsNewYearsDay(date)
                || IsraelFeasts.IsNewYearsDay(date, -1) // 2nd day of new year
                // Day of Atonement (Yom Kippur)
                || IsraelFeasts.IsYomKippur(date)
                // First Day of Sukkot (Tabernacles)
                || IsraelFeasts.IsSukkot(date)
                // Rejoicing of the Law Festival (Simchat Torah)
                || IsraelFeasts.IsSimchatTorah(date)
                // last Monday of May (Spring Bank Holiday)
                || (d >= 25 && w == Weekday.Monday && m == Month.May && y != 2002 && y != 2012)
                // Christmas
                || (d == 25 && m == Month.December)
                // Day of Goodwill (Boxing Day)
                || (d == 26 && m == Month.December && y >= 2000 && y != 2020))
            {
                return false;
            }

            return true;
        }
    }

    private sealed class ShirImpl : Impl
    {
        public override string Name => "SHIR fixing calendar";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth;
            int dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;

            if (WesternImpl.IsWeekend(w)
                || IsraelFeasts.IsPurim(date)
                || IsraelFeasts.IsPurim(date, -1) // Purim (Jerusalem)
                || IsraelFeasts.IsPassover1st(date, 1) // Eve of Passover
                || IsraelFeasts.IsPassover1st(date)
                || IsraelFeasts.IsPassover1st(date, -6) // Last day of Passover
                || IsraelFeasts.IsIndependenceDay(date)
                || IsraelFeasts.IsShavuot(date)
                || IsraelFeasts.IsFastDay(date)
                || IsraelFeasts.IsNewYearsDay(date, 1) // Eve of new year, until 2019
                || IsraelFeasts.IsNewYearsDay(date)
                || IsraelFeasts.IsNewYearsDay(date, -1) // 2nd day of new year
                || IsraelFeasts.IsYomKippur(date, 1) // Eve of Yom Kippur
                || IsraelFeasts.IsYomKippur(date)
                || IsraelFeasts.IsSukkot(date)
                || IsraelFeasts.IsSimchatTorah(date)
                // one-off closings
                || (d == 27 && m == Month.February && y == 2024) // Municipal elections
                // holidays abroad
                || (d == 1 && m == Month.January) // Western New Year's day
                || dd == WesternImpl.EasterMonday(y) - 3 // Good Friday
                || (d >= 25 && w == Weekday.Monday && m == Month.May && y != 2022) // Spring Bank Holiday
                || (d == 3 && m == Month.June && y == 2022)
                || (d == 25 && m == Month.December) // Christmas
                || (d == 26 && m == Month.December) // Boxing day
                // other days when fixings were not published
                || (d == 1 && m == Month.November && y == 2022) // no idea why
                || (d == 2 && m == Month.January && y == 2023) // Maybe New Year's Day is adjusted to Monday?
                || (d == 10 && m == Month.April && y == 2023) // Easter Monday, not a holiday in 2024 and 2025
                )
            {
                return false;
            }

            return true;
        }
    }
}

// QuantLib's anonymous-namespace feasts in israel.cpp, each a table of its dates from 2000 to 2050 read by serial
// number, so a neighbour of a date near the ends of Date's range is read without leaving the range.
file static class IsraelFeasts
{
    private static readonly ImmutableHashSet<int> PurimDates = Serials(
    [
        new(21, Month.March, 2000),
        new(9, Month.March, 2001),
        new(26, Month.February, 2002),
        new(18, Month.March, 2003),
        new(7, Month.March, 2004),
        new(25, Month.March, 2005),
        new(14, Month.March, 2006),
        new(4, Month.March, 2007),
        new(21, Month.March, 2008),
        new(10, Month.March, 2009),
        new(28, Month.February, 2010),
        new(20, Month.March, 2011),
        new(8, Month.March, 2012),
        new(24, Month.February, 2013),
        new(16, Month.March, 2014),
        new(5, Month.March, 2015),
        new(24, Month.March, 2016),
        new(12, Month.March, 2017),
        new(1, Month.March, 2018),
        new(21, Month.March, 2019),
        new(10, Month.March, 2020),
        new(26, Month.February, 2021),
        new(17, Month.March, 2022),
        new(7, Month.March, 2023),
        new(24, Month.March, 2024),
        new(14, Month.March, 2025),
        new(3, Month.March, 2026),
        new(23, Month.March, 2027),
        new(12, Month.March, 2028),
        new(1, Month.March, 2029),
        new(19, Month.March, 2030),
        new(9, Month.March, 2031),
        new(26, Month.February, 2032),
        new(15, Month.March, 2033),
        new(5, Month.March, 2034),
        new(25, Month.March, 2035),
        new(13, Month.March, 2036),
        new(1, Month.March, 2037),
        new(21, Month.March, 2038),
        new(10, Month.March, 2039),
        new(28, Month.February, 2040),
        new(17, Month.March, 2041),
        new(6, Month.March, 2042),
        new(26, Month.March, 2043),
        new(13, Month.March, 2044),
        new(3, Month.March, 2045),
        new(22, Month.March, 2046),
        new(12, Month.March, 2047),
        new(28, Month.February, 2048),
        new(18, Month.March, 2049),
        new(8, Month.March, 2050),
    ]);

    private static readonly ImmutableHashSet<int> Passover1stDates = Serials(
    [
        new(20, Month.April, 2000),
        new(8, Month.April, 2001),
        new(28, Month.March, 2002),
        new(17, Month.April, 2003),
        new(6, Month.April, 2004),
        new(24, Month.April, 2005),
        new(13, Month.April, 2006),
        new(3, Month.April, 2007),
        new(20, Month.April, 2008),
        new(9, Month.April, 2009),
        new(30, Month.March, 2010),
        new(19, Month.April, 2011),
        new(7, Month.April, 2012),
        new(26, Month.March, 2013),
        new(15, Month.April, 2014),
        new(4, Month.April, 2015),
        new(23, Month.April, 2016),
        new(11, Month.April, 2017),
        new(31, Month.March, 2018),
        new(20, Month.April, 2019),
        new(9, Month.April, 2020),
        new(28, Month.March, 2021),
        new(16, Month.April, 2022),
        new(6, Month.April, 2023),
        new(23, Month.April, 2024),
        new(13, Month.April, 2025),
        new(2, Month.April, 2026),
        new(22, Month.April, 2027),
        new(11, Month.April, 2028),
        new(31, Month.March, 2029),
        new(18, Month.April, 2030),
        new(8, Month.April, 2031),
        new(27, Month.March, 2032),
        new(14, Month.April, 2033),
        new(4, Month.April, 2034),
        new(24, Month.April, 2035),
        new(12, Month.April, 2036),
        new(31, Month.March, 2037),
        new(20, Month.April, 2038),
        new(9, Month.April, 2039),
        new(29, Month.March, 2040),
        new(16, Month.April, 2041),
        new(5, Month.April, 2042),
        new(25, Month.April, 2043),
        new(12, Month.April, 2044),
        new(2, Month.April, 2045),
        new(21, Month.April, 2046),
        new(11, Month.April, 2047),
        new(29, Month.March, 2048),
        new(17, Month.April, 2049),
        new(7, Month.April, 2050),
    ]);

    private static readonly ImmutableHashSet<int> IndependenceDayDates = Serials(
    [
        new(10, Month.May, 2000),
        new(26, Month.April, 2001),
        new(17, Month.April, 2002),
        new(7, Month.May, 2003),
        new(27, Month.April, 2004),
        new(12, Month.May, 2005),
        new(3, Month.May, 2006),
        new(24, Month.April, 2007),
        new(8, Month.May, 2008),
        new(29, Month.April, 2009),
        new(20, Month.April, 2010),
        new(10, Month.May, 2011),
        new(26, Month.April, 2012),
        new(16, Month.April, 2013),
        new(6, Month.May, 2014),
        new(23, Month.April, 2015),
        new(12, Month.May, 2016),
        new(2, Month.May, 2017),
        new(19, Month.April, 2018),
        new(9, Month.May, 2019),
        new(29, Month.April, 2020),
        new(15, Month.April, 2021),
        new(5, Month.May, 2022),
        new(26, Month.April, 2023),
        new(14, Month.May, 2024),
        new(1, Month.May, 2025),
        new(22, Month.April, 2026),
        new(12, Month.May, 2027),
        new(2, Month.May, 2028),
        new(19, Month.April, 2029),
        new(8, Month.May, 2030),
        new(29, Month.April, 2031),
        new(15, Month.April, 2032),
        new(4, Month.May, 2033),
        new(25, Month.April, 2034),
        new(15, Month.May, 2035),
        new(1, Month.May, 2036),
        new(21, Month.April, 2037),
        new(10, Month.May, 2038),
        new(28, Month.April, 2039),
        new(18, Month.April, 2040),
        new(7, Month.May, 2041),
        new(24, Month.April, 2042),
        new(14, Month.May, 2043),
        new(3, Month.May, 2044),
        new(20, Month.April, 2045),
        new(10, Month.May, 2046),
        new(1, Month.May, 2047),
        new(16, Month.April, 2048),
        new(6, Month.May, 2049),
        new(27, Month.April, 2050),
    ]);

    private static readonly ImmutableHashSet<int> ShavuotDates = Serials(
    [
        new(9, Month.June, 2000),
        new(28, Month.May, 2001),
        new(17, Month.May, 2002),
        new(6, Month.June, 2003),
        new(26, Month.May, 2004),
        new(13, Month.June, 2005),
        new(2, Month.June, 2006),
        new(23, Month.May, 2007),
        new(9, Month.June, 2008),
        new(29, Month.May, 2009),
        new(19, Month.May, 2010),
        new(8, Month.June, 2011),
        new(27, Month.May, 2012),
        new(15, Month.May, 2013),
        new(4, Month.June, 2014),
        new(24, Month.May, 2015),
        new(12, Month.June, 2016),
        new(31, Month.May, 2017),
        new(20, Month.May, 2018),
        new(9, Month.June, 2019),
        new(29, Month.May, 2020),
        new(17, Month.May, 2021),
        new(5, Month.June, 2022),
        new(26, Month.May, 2023),
        new(12, Month.June, 2024),
        new(2, Month.June, 2025),
        new(22, Month.May, 2026),
        new(11, Month.June, 2027),
        new(31, Month.May, 2028),
        new(20, Month.May, 2029),
        new(7, Month.June, 2030),
        new(28, Month.May, 2031),
        new(16, Month.May, 2032),
        new(3, Month.June, 2033),
        new(24, Month.May, 2034),
        new(13, Month.June, 2035),
        new(1, Month.June, 2036),
        new(20, Month.May, 2037),
        new(9, Month.June, 2038),
        new(29, Month.May, 2039),
        new(18, Month.May, 2040),
        new(5, Month.June, 2041),
        new(25, Month.May, 2042),
        new(14, Month.June, 2043),
        new(1, Month.June, 2044),
        new(22, Month.May, 2045),
        new(10, Month.June, 2046),
        new(31, Month.May, 2047),
        new(18, Month.May, 2048),
        new(6, Month.June, 2049),
        new(27, Month.May, 2050),
    ]);

    private static readonly ImmutableHashSet<int> FastDayDates = Serials(
    [
        new(10, Month.August, 2000),
        new(29, Month.July, 2001),
        new(18, Month.July, 2002),
        new(7, Month.August, 2003),
        new(27, Month.July, 2004),
        new(14, Month.August, 2005),
        new(3, Month.August, 2006),
        new(24, Month.July, 2007),
        new(10, Month.August, 2008),
        new(30, Month.July, 2009),
        new(20, Month.July, 2010),
        new(9, Month.August, 2011),
        new(29, Month.July, 2012),
        new(16, Month.July, 2013),
        new(5, Month.August, 2014),
        new(26, Month.July, 2015),
        new(14, Month.August, 2016),
        new(1, Month.August, 2017),
        new(22, Month.July, 2018),
        new(11, Month.August, 2019),
        new(30, Month.July, 2020),
        new(18, Month.July, 2021),
        new(7, Month.August, 2022),
        new(27, Month.July, 2023),
        new(13, Month.August, 2024),
        new(3, Month.August, 2025),
        new(23, Month.July, 2026),
        new(12, Month.August, 2027),
        new(1, Month.August, 2028),
        new(22, Month.July, 2029),
        new(8, Month.August, 2030),
        new(29, Month.July, 2031),
        new(18, Month.July, 2032),
        new(4, Month.August, 2033),
        new(25, Month.July, 2034),
        new(14, Month.August, 2035),
        new(3, Month.August, 2036),
        new(21, Month.July, 2037),
        new(10, Month.August, 2038),
        new(31, Month.July, 2039),
        new(19, Month.July, 2040),
        new(6, Month.August, 2041),
        new(27, Month.July, 2042),
        new(16, Month.August, 2043),
        new(2, Month.August, 2044),
        new(23, Month.July, 2045),
        new(12, Month.August, 2046),
        new(1, Month.August, 2047),
        new(19, Month.July, 2048),
        new(8, Month.August, 2049),
        new(28, Month.July, 2050),
    ]);

    private static readonly ImmutableHashSet<int> NewYearsDayDates = Serials(
    [
        new(30, Month.September, 2000),
        new(17, Month.September, 2001),
        new(7, Month.September, 2002),
        new(27, Month.September, 2003),
        new(16, Month.September, 2004),
        new(4, Month.October, 2005),
        new(23, Month.September, 2006),
        new(13, Month.September, 2007),
        new(30, Month.September, 2008),
        new(19, Month.September, 2009),
        new(9, Month.September, 2010),
        new(29, Month.September, 2011),
        new(17, Month.September, 2012),
        new(5, Month.September, 2013),
        new(25, Month.September, 2014),
        new(14, Month.September, 2015),
        new(3, Month.October, 2016),
        new(21, Month.September, 2017),
        new(10, Month.September, 2018),
        new(30, Month.September, 2019),
        new(19, Month.September, 2020),
        new(7, Month.September, 2021),
        new(26, Month.September, 2022),
        new(16, Month.September, 2023),
        new(3, Month.October, 2024),
        new(23, Month.September, 2025),
        new(12, Month.September, 2026),
        new(2, Month.October, 2027),
        new(21, Month.September, 2028),
        new(10, Month.September, 2029),
        new(28, Month.September, 2030),
        new(18, Month.September, 2031),
        new(6, Month.September, 2032),
        new(24, Month.September, 2033),
        new(14, Month.September, 2034),
        new(4, Month.October, 2035),
        new(22, Month.September, 2036),
        new(10, Month.September, 2037),
        new(30, Month.September, 2038),
        new(19, Month.September, 2039),
        new(8, Month.September, 2040),
        new(26, Month.September, 2041),
        new(15, Month.September, 2042),
        new(5, Month.October, 2043),
        new(22, Month.September, 2044),
        new(12, Month.September, 2045),
        new(1, Month.October, 2046),
        new(21, Month.September, 2047),
        new(8, Month.September, 2048),
        new(27, Month.September, 2049),
        new(17, Month.September, 2050),
    ]);

    // QuantLib's isPurim(d + offset)
    public static bool IsPurim(Date d, int offset = 0) => PurimDates.Contains(d.SerialNumber + offset);

    // QuantLib's isPassover1st(d + offset)
    public static bool IsPassover1st(Date d, int offset = 0) => Passover1stDates.Contains(d.SerialNumber + offset);

    // QuantLib's isIndependenceDay(d + offset)
    public static bool IsIndependenceDay(Date d, int offset = 0) => IndependenceDayDates.Contains(d.SerialNumber + offset);

    // QuantLib's isShavuot(d + offset)
    public static bool IsShavuot(Date d, int offset = 0) => ShavuotDates.Contains(d.SerialNumber + offset);

    // QuantLib's isFastDay(d + offset)
    public static bool IsFastDay(Date d, int offset = 0) => FastDayDates.Contains(d.SerialNumber + offset);

    // QuantLib's isNewYearsDay(d + offset)
    public static bool IsNewYearsDay(Date d, int offset = 0) => NewYearsDayDates.Contains(d.SerialNumber + offset);

    // QuantLib's isMemorialDay(d): isIndependenceDay(d + 1)
    public static bool IsMemorialDay(Date d, int offset = 0) => IsIndependenceDay(d, offset + 1);

    // QuantLib's isYomKippur(d): isNewYearsDay(d - 9)
    public static bool IsYomKippur(Date d, int offset = 0) => IsNewYearsDay(d, offset - 9);

    // QuantLib's isSukkot(d): isYomKippur(d - 5)
    public static bool IsSukkot(Date d, int offset = 0) => IsYomKippur(d, offset - 5);

    // QuantLib's isSimchatTorah(d): isSukkot(d - 7)
    public static bool IsSimchatTorah(Date d, int offset = 0) => IsSukkot(d, offset - 7);

    private static ImmutableHashSet<int> Serials(Date[] dates) => [.. dates.Select(d => d.SerialNumber)];
}
