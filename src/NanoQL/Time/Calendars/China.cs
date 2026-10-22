// SPDX-FileCopyrightText: Copyright (C) 2004 FIMAT Group
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009, 2010, 2011 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009, 2010, 2011 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/china.hpp ql/time/calendars/china.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Time;

/// <summary>The Chinese calendars.</summary>
/// <remarks>
/// <para>
/// Holidays:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's day, January 1st (possibly followed by one or two more holidays)</description></item>
/// <item><description>Labour Day, first week in May</description></item>
/// <item><description>National Day, one week from October 1st</description></item>
/// </list>
/// </para>
/// <para>
/// Other holidays for which no rule is given (data available for 2004-2026 only):
/// <list type="bullet">
/// <item><description>Chinese New Year</description></item>
/// <item><description>Ching Ming Festival</description></item>
/// <item><description>Tuen Ng Festival</description></item>
/// <item><description>Mid-Autumn Festival</description></item>
/// <item><description>70th anniversary of the victory of anti-Japaneses war</description></item>
/// </list>
/// </para>
/// <para>
/// SSE data from http://www.sse.com.cn/ IB data from http://www.chinamoney.com.cn/
/// </para>
/// <para>
/// The year-by-year data end in 2026: after it, only the rules for fixed dates and weekends apply,
/// as in QuantLib.
/// </para>
/// </remarks>
public sealed class China : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl SseImplInstance = new SseImpl();
    private static readonly Impl IbImplInstance = new IbImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="China"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public China(ChinaMarket market = ChinaMarket.SSE)
    {
        switch (market)
        {
            case ChinaMarket.SSE:
                impl = SseImplInstance;
                break;
            case ChinaMarket.IB:
                impl = IbImplInstance;
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
    protected override bool IsBusinessDayCore(Date date) => impl.IsBusinessDay(date);

    // The market-specific rules; QuantLib's Calendar::Impl.
    private abstract class Impl
    {
        public abstract string Name { get; }

        public abstract bool IsWeekend(Weekday w);

        public abstract bool IsBusinessDay(Date date);
    }

    private sealed class SseImpl : Impl
    {
        public override string Name => "Shanghai stock exchange";

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
                || (y == 2005 && d == 3 && m == Month.January)
                || (y == 2006 && (d == 2 || d == 3) && m == Month.January)
                || (y == 2007 && d <= 3 && m == Month.January)
                || (y == 2007 && d == 31 && m == Month.December)
                || (y == 2009 && d == 2 && m == Month.January)
                || (y == 2011 && d == 3 && m == Month.January)
                || (y == 2012 && (d == 2 || d == 3) && m == Month.January)
                || (y == 2013 && d <= 3 && m == Month.January)
                || (y == 2014 && d == 1 && m == Month.January)
                || (y == 2015 && d <= 3 && m == Month.January)
                || (y == 2017 && d == 2 && m == Month.January)
                || (y == 2018 && d == 1 && m == Month.January)
                || (y == 2018 && d == 31 && m == Month.December)
                || (y == 2019 && d == 1 && m == Month.January)
                || (y == 2020 && d == 1 && m == Month.January)
                || (y == 2021 && d == 1 && m == Month.January)
                || (y == 2022 && d == 3 && m == Month.January)
                || (y == 2023 && d == 2 && m == Month.January)
                || (y == 2026 && (d == 1 || d == 2) && m == Month.January)
                // Chinese New Year
                || (y == 2004 && d >= 19 && d <= 28 && m == Month.January)
                || (y == 2005 && d >= 7 && d <= 15 && m == Month.February)
                || (y == 2006 && ((d >= 26 && m == Month.January) ||
                                  (d <= 3 && m == Month.February)))
                || (y == 2007 && d >= 17 && d <= 25 && m == Month.February)
                || (y == 2008 && d >= 6 && d <= 12 && m == Month.February)
                || (y == 2009 && d >= 26 && d <= 30 && m == Month.January)
                || (y == 2010 && d >= 15 && d <= 19 && m == Month.February)
                || (y == 2011 && d >= 2 && d <= 8 && m == Month.February)
                || (y == 2012 && d >= 23 && d <= 28 && m == Month.January)
                || (y == 2013 && d >= 11 && d <= 15 && m == Month.February)
                || (y == 2014 && d >= 31 && m == Month.January)
                || (y == 2014 && d <= 6 && m == Month.February)
                || (y == 2015 && d >= 18 && d <= 24 && m == Month.February)
                || (y == 2016 && d >= 8 && d <= 12 && m == Month.February)
                || (y == 2017 && ((d >= 27 && m == Month.January) ||
                                  (d <= 2 && m == Month.February)))
                || (y == 2018 && (d >= 15 && d <= 21 && m == Month.February))
                || (y == 2019 && d >= 4 && d <= 8 && m == Month.February)
                || (y == 2020 && (d == 24 || (d >= 27 && d <= 31)) && m == Month.January)
                || (y == 2021 && (d == 11 || d == 12 || d == 15 || d == 16 || d == 17) && m == Month.February)
                || (y == 2022 && ((d == 31 && m == Month.January) || (d <= 4 && m == Month.February)))
                || (y == 2023 && d >= 23 && d <= 27 && m == Month.January)
                || (y == 2024 && (d == 9 || (d >= 12 && d <= 16)) && m == Month.February)
                || (y == 2025 && ((d >= 28 && d <= 31 && m == Month.January) || (d >= 3 && d <= 4 && m == Month.February)))
                || (y == 2026 && ((d >= 16 && d <= 20) || d == 23) && m == Month.February)
                // Ching Ming Festival
                || (y <= 2008 && d == 4 && m == Month.April)
                || (y == 2009 && d == 6 && m == Month.April)
                || (y == 2010 && d == 5 && m == Month.April)
                || (y == 2011 && d >= 3 && d <= 5 && m == Month.April)
                || (y == 2012 && d >= 2 && d <= 4 && m == Month.April)
                || (y == 2013 && d >= 4 && d <= 5 && m == Month.April)
                || (y == 2014 && d == 7 && m == Month.April)
                || (y == 2015 && d >= 5 && d <= 6 && m == Month.April)
                || (y == 2016 && d == 4 && m == Month.April)
                || (y == 2017 && d >= 3 && d <= 4 && m == Month.April)
                || (y == 2018 && d >= 5 && d <= 6 && m == Month.April)
                || (y == 2019 && d == 5 && m == Month.April)
                || (y == 2020 && d == 6 && m == Month.April)
                || (y == 2021 && d == 5 && m == Month.April)
                || (y == 2022 && d >= 4 && d <= 5 && m == Month.April)
                || (y == 2023 && d == 5 && m == Month.April)
                || (y == 2024 && d >= 4 && d <= 5 && m == Month.April)
                || (y == 2025 && d == 4 && m == Month.April)
                || (y == 2026 && d == 6 && m == Month.April)
                // Labor Day
                || (y <= 2007 && d >= 1 && d <= 7 && m == Month.May)
                || (y == 2008 && d >= 1 && d <= 2 && m == Month.May)
                || (y == 2009 && d == 1 && m == Month.May)
                || (y == 2010 && d == 3 && m == Month.May)
                || (y == 2011 && d == 2 && m == Month.May)
                || (y == 2012 && ((d == 30 && m == Month.April) ||
                                  (d == 1 && m == Month.May)))
                || (y == 2013 && ((d >= 29 && m == Month.April) ||
                                  (d == 1 && m == Month.May)))
                || (y == 2014 && d >= 1 && d <= 3 && m == Month.May)
                || (y == 2015 && d == 1 && m == Month.May)
                || (y == 2016 && d >= 1 && d <= 2 && m == Month.May)
                || (y == 2017 && d == 1 && m == Month.May)
                || (y == 2018 && ((d == 30 && m == Month.April) || (d == 1 && m == Month.May)))
                || (y == 2019 && d >= 1 && d <= 3 && m == Month.May)
                || (y == 2020 && (d == 1 || d == 4 || d == 5) && m == Month.May)
                || (y == 2021 && (d == 3 || d == 4 || d == 5) && m == Month.May)
                || (y == 2022 && d >= 2 && d <= 4 && m == Month.May)
                || (y == 2023 && d >= 1 && d <= 3 && m == Month.May)
                || (y == 2024 && d >= 1 && d <= 3 && m == Month.May)
                || (y == 2025 && (d == 1 || d == 2 || d == 5) && m == Month.May)
                || (y == 2026 && (d == 1 || d == 4 || d == 5) && m == Month.May)
                // Tuen Ng Festival
                || (y <= 2008 && d == 9 && m == Month.June)
                || (y == 2009 && (d == 28 || d == 29) && m == Month.May)
                || (y == 2010 && d >= 14 && d <= 16 && m == Month.June)
                || (y == 2011 && d >= 4 && d <= 6 && m == Month.June)
                || (y == 2012 && d >= 22 && d <= 24 && m == Month.June)
                || (y == 2013 && d >= 10 && d <= 12 && m == Month.June)
                || (y == 2014 && d == 2 && m == Month.June)
                || (y == 2015 && d == 22 && m == Month.June)
                || (y == 2016 && d >= 9 && d <= 10 && m == Month.June)
                || (y == 2017 && d >= 29 && d <= 30 && m == Month.May)
                || (y == 2018 && d == 18 && m == Month.June)
                || (y == 2019 && d == 7 && m == Month.June)
                || (y == 2020 && d >= 25 && d <= 26 && m == Month.June)
                || (y == 2021 && d == 14 && m == Month.June)
                || (y == 2022 && d == 3 && m == Month.June)
                || (y == 2023 && d >= 22 && d <= 23 && m == Month.June)
                || (y == 2024 && d == 10 && m == Month.June)
                || (y == 2025 && d == 2 && m == Month.June)
                || (y == 2026 && d == 19 && m == Month.June)
                // Mid-Autumn Festival
                || (y <= 2008 && d == 15 && m == Month.September)
                || (y == 2010 && d >= 22 && d <= 24 && m == Month.September)
                || (y == 2011 && d >= 10 && d <= 12 && m == Month.September)
                || (y == 2012 && d == 30 && m == Month.September)
                || (y == 2013 && d >= 19 && d <= 20 && m == Month.September)
                || (y == 2014 && d == 8 && m == Month.September)
                || (y == 2015 && d == 27 && m == Month.September)
                || (y == 2016 && d >= 15 && d <= 16 && m == Month.September)
                || (y == 2018 && d == 24 && m == Month.September)
                || (y == 2019 && d == 13 && m == Month.September)
                || (y == 2021 && (d == 20 || d == 21) && m == Month.September)
                || (y == 2022 && d == 12 && m == Month.September)
                || (y == 2023 && d == 29 && m == Month.September)
                || (y == 2024 && d >= 16 && d <= 17 && m == Month.September)
                || (y == 2026 && d == 25 && m == Month.September)
                // National Day
                || (y <= 2007 && d >= 1 && d <= 7 && m == Month.October)
                || (y == 2008 && ((d >= 29 && m == Month.September) ||
                                  (d <= 3 && m == Month.October)))
                || (y == 2009 && d >= 1 && d <= 8 && m == Month.October)
                || (y == 2010 && d >= 1 && d <= 7 && m == Month.October)
                || (y == 2011 && d >= 1 && d <= 7 && m == Month.October)
                || (y == 2012 && d >= 1 && d <= 7 && m == Month.October)
                || (y == 2013 && d >= 1 && d <= 7 && m == Month.October)
                || (y == 2014 && d >= 1 && d <= 7 && m == Month.October)
                || (y == 2015 && d >= 1 && d <= 7 && m == Month.October)
                || (y == 2016 && d >= 3 && d <= 7 && m == Month.October)
                || (y == 2017 && d >= 2 && d <= 6 && m == Month.October)
                || (y == 2018 && d >= 1 && d <= 5 && m == Month.October)
                || (y == 2019 && d >= 1 && d <= 7 && m == Month.October)
                || (y == 2020 && d >= 1 && d <= 2 && m == Month.October)
                || (y == 2020 && d >= 5 && d <= 8 && m == Month.October)
                || (y == 2021 && (d == 1 || d == 4 || d == 5 || d == 6 || d == 7) && m == Month.October)
                || (y == 2022 && d >= 3 && d <= 7 && m == Month.October)
                || (y == 2023 && d >= 2 && d <= 6 && m == Month.October)
                || (y == 2024 && ((d >= 1 && d <= 4) || d == 7) && m == Month.October)
                || (y == 2025 && ((d >= 1 && d <= 3) || (d >= 6 && d <= 8)) && m == Month.October)
                || (y == 2026 && ((d >= 1 && d <= 2) || (d >= 5 && d <= 7)) && m == Month.October)
                // 70th anniversary of the victory of anti-Japaneses war
                || (y == 2015 && d >= 3 && d <= 4 && m == Month.September)
                )
            {
                return false;
            }

            return true;
        }
    }

    private sealed class IbImpl : Impl
    {
        // the working weekends of the inter-bank market, from 2005
        private static readonly ImmutableHashSet<Date> WorkingWeekends =
        [
        // 2005
        new(5, Month.February, 2005),
        new(6, Month.February, 2005),
        new(30, Month.April, 2005),
        new(8, Month.May, 2005),
        new(8, Month.October, 2005),
        new(9, Month.October, 2005),
        new(31, Month.December, 2005),

        //2006
        new(28, Month.January, 2006),
        new(29, Month.April, 2006),
        new(30, Month.April, 2006),
        new(30, Month.September, 2006),
        new(30, Month.December, 2006),
        new(31, Month.December, 2006),

        // 2007
        new(17, Month.February, 2007),
        new(25, Month.February, 2007),
        new(28, Month.April, 2007),
        new(29, Month.April, 2007),
        new(29, Month.September, 2007),
        new(30, Month.September, 2007),
        new(29, Month.December, 2007),

        // 2008
        new(2, Month.February, 2008),
        new(3, Month.February, 2008),
        new(4, Month.May, 2008),
        new(27, Month.September, 2008),
        new(28, Month.September, 2008),

        // 2009
        new(4, Month.January, 2009),
        new(24, Month.January, 2009),
        new(1, Month.February, 2009),
        new(31, Month.May, 2009),
        new(27, Month.September, 2009),
        new(10, Month.October, 2009),

        // 2010
        new(20, Month.February, 2010),
        new(21, Month.February, 2010),
        new(12, Month.June, 2010),
        new(13, Month.June, 2010),
        new(19, Month.September, 2010),
        new(25, Month.September, 2010),
        new(26, Month.September, 2010),
        new(9, Month.October, 2010),

        // 2011
        new(30, Month.January, 2011),
        new(12, Month.February, 2011),
        new(2, Month.April, 2011),
        new(8, Month.October, 2011),
        new(9, Month.October, 2011),
        new(31, Month.December, 2011),

        // 2012
        new(21, Month.January, 2012),
        new(29, Month.January, 2012),
        new(31, Month.March, 2012),
        new(1, Month.April, 2012),
        new(28, Month.April, 2012),
        new(29, Month.September, 2012),

        // 2013
        new(5, Month.January, 2013),
        new(6, Month.January, 2013),
        new(16, Month.February, 2013),
        new(17, Month.February, 2013),
        new(7, Month.April, 2013),
        new(27, Month.April, 2013),
        new(28, Month.April, 2013),
        new(8, Month.June, 2013),
        new(9, Month.June, 2013),
        new(22, Month.September, 2013),
        new(29, Month.September, 2013),
        new(12, Month.October, 2013),

        // 2014
        new(26, Month.January, 2014),
        new(8, Month.February, 2014),
        new(4, Month.May, 2014),
        new(28, Month.September, 2014),
        new(11, Month.October, 2014),

        // 2015
        new(4, Month.January, 2015),
        new(15, Month.February, 2015),
        new(28, Month.February, 2015),
        new(6, Month.September, 2015),
        new(10, Month.October, 2015),

        // 2016
        new(6, Month.February, 2016),
        new(14, Month.February, 2016),
        new(12, Month.June, 2016),
        new(18, Month.September, 2016),
        new(8, Month.October, 2016),
        new(9, Month.October, 2016),

        // 2017
        new(22, Month.January, 2017),
        new(4, Month.February, 2017),
        new(1, Month.April, 2017),
        new(27, Month.May, 2017),
        new(30, Month.September, 2017),

        // 2018
        new(11, Month.February, 2018),
        new(24, Month.February, 2018),
        new(8, Month.April, 2018),
        new(28, Month.April, 2018),
        new(29, Month.September, 2018),
        new(30, Month.September, 2018),
        new(29, Month.December, 2018),

        // 2019
        new(2, Month.February, 2019),
        new(3, Month.February, 2019),
        new(28, Month.April, 2019),
        new(5, Month.May, 2019),
        new(29, Month.September, 2019),
        new(12, Month.October, 2019),

        // 2020
        new(19, Month.January, 2020),
        new(26, Month.April, 2020),
        new(9, Month.May, 2020),
        new(28, Month.June, 2020),
        new(27, Month.September, 2020),
        new(10, Month.October, 2020),

        // 2021
        new(7, Month.February, 2021),
        new(20, Month.February, 2021),
        new(25, Month.April, 2021),
        new(8, Month.May, 2021),
        new(18, Month.September, 2021),
        new(26, Month.September, 2021),
        new(9, Month.October, 2021),

        // 2022
        new(29, Month.January, 2022),
        new(30, Month.January, 2022),
        new(2, Month.April, 2022),
        new(24, Month.April, 2022),
        new(7, Month.May, 2022),
        new(8, Month.October, 2022),
        new(9, Month.October, 2022),

        // 2023
        new(28, Month.January, 2023),
        new(29, Month.January, 2023),
        new(23, Month.April, 2023),
        new(6, Month.May, 2023),
        new(25, Month.June, 2023),
        new(7, Month.October, 2023),
        new(8, Month.October, 2023),

        // 2024
        new(4, Month.Feb, 2024),
        new(9, Month.Feb, 2024),
        new(18, Month.Feb, 2024),
        new(7, Month.Apr, 2024),
        new(28, Month.Apr, 2024),
        new(11, Month.May, 2024),
        new(14, Month.Sep, 2024),
        new(29, Month.Sep, 2024),
        new(12, Month.October, 2024),

        // 2025
        new(26, Month.Jan, 2025),
        new(8, Month.Feb, 2025),
        new(27, Month.Apr, 2025),
        new(28, Month.Sep, 2025),
        new(11, Month.Oct, 2025),

        // 2026
        new(4, Month.Jan, 2026),
        new(14, Month.Feb, 2026),
        new(28, Month.Feb, 2026),
        new(9, Month.May, 2026),
        new(20, Month.Sep, 2026),
        new(10, Month.Oct, 2026),
        ];

        private static readonly SseImpl Sse = new();

        public override string Name => "China inter bank market";

        public override bool IsWeekend(Weekday w) => w == Weekday.Saturday || w == Weekday.Sunday;

        public override bool IsBusinessDay(Date date) =>
            // If it is already a SSE business day, it must be a IB business day
            Sse.IsBusinessDay(date) || WorkingWeekends.Contains(date);
    }
}
