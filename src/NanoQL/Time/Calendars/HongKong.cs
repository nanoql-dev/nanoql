// SPDX-FileCopyrightText: Copyright (C) 2004 FIMAT Group
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009, 2010, 2011 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/hongkong.hpp ql/time/calendars/hongkong.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Hong Kong calendars.</summary>
/// <remarks>
/// <para>
/// Holidays:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st (possibly moved to Monday)</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Labor Day, May 1st (possibly moved to Monday)</description></item>
/// <item><description>SAR Establishment Day, July 1st (possibly moved to Monday)</description></item>
/// <item><description>National Day, October 1st (possibly moved to Monday)</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>Boxing Day, December 26th</description></item>
/// </list>
/// </para>
/// <para>
/// Other holidays for which no rule is given (data available for 2004-2026 only:)
/// <list type="bullet">
/// <item><description>Lunar New Year</description></item>
/// <item><description>Chinese New Year</description></item>
/// <item><description>Ching Ming Festival</description></item>
/// <item><description>Buddha's birthday</description></item>
/// <item><description>Tuen NG Festival</description></item>
/// <item><description>Mid-autumn Festival</description></item>
/// <item><description>Chung Yeung Festival</description></item>
/// </list>
/// </para>
/// <para>
/// Data from http://www.hkex.com.hk
/// </para>
/// <para>
/// The year-by-year data end in 2026: after it, only the rules for fixed dates and weekends apply,
/// as in QuantLib.
/// </para>
/// </remarks>
public sealed class HongKong : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl HkexImplInstance = new HkexImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="HongKong"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public HongKong(HongKongMarket market = HongKongMarket.HKEx)
    {
        switch (market)
        {
            case HongKongMarket.HKEx:
                impl = HkexImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }

        ApplyEdits(ConventionProvider.ReadCalendarEdits($"HongKong.{market}"));
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

    private sealed class HkexImpl : Impl
    {
        public override string Name => "Hong Kong stock exchange";

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
                || ((d == 1 || ((d == 2) && w == Weekday.Monday))
                    && m == Month.January)
                // Good Friday
                || (dd == em - 3)
                // Easter Monday
                || (dd == em)
                // Labor Day
                || ((d == 1 || ((d == 2) && w == Weekday.Monday)) && m == Month.May)
                // SAR Establishment Day
                || ((d == 1 || ((d == 2) && w == Weekday.Monday)) && m == Month.July)
                // National Day
                || ((d == 1 || ((d == 2) && w == Weekday.Monday))
                    && m == Month.October)
                // Christmas Day
                || (d == 25 && m == Month.December)
                // Boxing Day
                || (d == 26 && m == Month.December)
                )
            {
                return false;
            }

            if (y == 2004)
            {
                if (// Lunar New Year
                    ((d == 22 || d == 23 || d == 24) && m == Month.January)
                    // Ching Ming Festival
                    || (d == 5 && m == Month.April)
                    // Buddha's birthday
                    || (d == 26 && m == Month.May)
                    // Tuen Ng festival
                    || (d == 22 && m == Month.June)
                    // Mid-autumn festival
                    || (d == 29 && m == Month.September)
                    // Chung Yeung
                    || (d == 22 && m == Month.October))
                {
                    return false;
                }
            }

            if (y == 2005)
            {
                if (// Lunar New Year
                    ((d == 9 || d == 10 || d == 11) && m == Month.February)
                    // Ching Ming Festival
                    || (d == 5 && m == Month.April)
                    // Buddha's birthday
                    || (d == 16 && m == Month.May)
                    // Tuen Ng festival
                    || (d == 11 && m == Month.June)
                    // Mid-autumn festival
                    || (d == 19 && m == Month.September)
                    // Chung Yeung festival
                    || (d == 11 && m == Month.October))
                {
                    return false;
                }
            }

            if (y == 2006)
            {
                if (// Lunar New Year
                    ((d >= 28 && d <= 31) && m == Month.January)
                    // Ching Ming Festival
                    || (d == 5 && m == Month.April)
                    // Buddha's birthday
                    || (d == 5 && m == Month.May)
                    // Tuen Ng festival
                    || (d == 31 && m == Month.May)
                    // Mid-autumn festival
                    || (d == 7 && m == Month.October)
                    // Chung Yeung festival
                    || (d == 30 && m == Month.October))
                {
                    return false;
                }
            }

            if (y == 2007)
            {
                if (// Lunar New Year
                    ((d >= 17 && d <= 20) && m == Month.February)
                    // Ching Ming Festival
                    || (d == 5 && m == Month.April)
                    // Buddha's birthday
                    || (d == 24 && m == Month.May)
                    // Tuen Ng festival
                    || (d == 19 && m == Month.June)
                    // Mid-autumn festival
                    || (d == 26 && m == Month.September)
                    // Chung Yeung festival
                    || (d == 19 && m == Month.October))
                {
                    return false;
                }
            }

            if (y == 2008)
            {
                if (// Lunar New Year
                    ((d >= 7 && d <= 9) && m == Month.February)
                    // Ching Ming Festival
                    || (d == 4 && m == Month.April)
                    // Buddha's birthday
                    || (d == 12 && m == Month.May)
                    // Tuen Ng festival
                    || (d == 9 && m == Month.June)
                    // Mid-autumn festival
                    || (d == 15 && m == Month.September)
                    // Chung Yeung festival
                    || (d == 7 && m == Month.October))
                {
                    return false;
                }
            }

            if (y == 2009)
            {
                if (// Lunar New Year
                    ((d >= 26 && d <= 28) && m == Month.January)
                    // Ching Ming Festival
                    || (d == 4 && m == Month.April)
                    // Buddha's birthday
                    || (d == 2 && m == Month.May)
                    // Tuen Ng festival
                    || (d == 28 && m == Month.May)
                    // Mid-autumn festival
                    || (d == 3 && m == Month.October)
                    // Chung Yeung festival
                    || (d == 26 && m == Month.October))
                {
                    return false;
                }
            }

            if (y == 2010)
            {
                if (// Lunar New Year
                    ((d == 15 || d == 16) && m == Month.February)
                    // Ching Ming Festival
                    || (d == 6 && m == Month.April)
                    // Buddha's birthday
                    || (d == 21 && m == Month.May)
                    // Tuen Ng festival
                    || (d == 16 && m == Month.June)
                    // Mid-autumn festival
                    || (d == 23 && m == Month.September))
                {
                    return false;
                }
            }

            if (y == 2011)
            {
                if (// Lunar New Year
                    ((d == 3 || d == 4) && m == Month.February)
                    // Ching Ming Festival
                    || (d == 5 && m == Month.April)
                    // Buddha's birthday
                    || (d == 10 && m == Month.May)
                    // Tuen Ng festival
                    || (d == 6 && m == Month.June)
                    // Mid-autumn festival
                    || (d == 13 && m == Month.September)
                    // Chung Yeung festival
                    || (d == 5 && m == Month.October)
                    // Second day after Christmas
                    || (d == 27 && m == Month.December))
                {
                    return false;
                }
            }

            if (y == 2012)
            {
                if (// Lunar New Year
                    (d >= 23 && d <= 25 && m == Month.January)
                    // Ching Ming Festival
                    || (d == 4 && m == Month.April)
                    // Buddha's birthday
                    || (d == 10 && m == Month.May)
                    // Mid-autumn festival
                    || (d == 1 && m == Month.October)
                    // Chung Yeung festival
                    || (d == 23 && m == Month.October))
                {
                    return false;
                }
            }

            if (y == 2013)
            {
                if (// Lunar New Year
                    (d >= 11 && d <= 13 && m == Month.February)
                    // Ching Ming Festival
                    || (d == 4 && m == Month.April)
                    // Buddha's birthday
                    || (d == 17 && m == Month.May)
                    // Tuen Ng festival
                    || (d == 12 && m == Month.June)
                    // Mid-autumn festival
                    || (d == 20 && m == Month.September)
                    // Chung Yeung festival
                    || (d == 14 && m == Month.October))
                {
                    return false;
                }
            }

            if (y == 2014)
            {
                if (// Lunar New Year
                    ((d == 31 && m == Month.January) || (d <= 3 && m == Month.February))
                    // Buddha's birthday
                    || (d == 6 && m == Month.May)
                    // Tuen Ng festival
                    || (d == 2 && m == Month.June)
                    // Mid-autumn festival
                    || (d == 9 && m == Month.September)
                    // Chung Yeung festival
                    || (d == 2 && m == Month.October))
                {
                    return false;
                }
            }

            if (y == 2015)
            {
                if (// Lunar New Year
                    ((d == 19 && m == Month.February) || (d == 20 && m == Month.February))
                    // The day following Easter Monday
                    || (d == 7 && m == Month.April)
                    // Buddha's birthday
                    || (d == 25 && m == Month.May)
                    // Tuen Ng festival
                    || (d == 20 && m == Month.June)
                    // The 70th anniversary day of the victory of the Chinese
                    // people's war of resistance against Japanese aggression
                    || (d == 3 && m == Month.September)
                    // Mid-autumn festival
                    || (d == 28 && m == Month.September)
                    // Chung Yeung festival
                    || (d == 21 && m == Month.October))
                {
                    return false;
                }
            }

            if (y == 2016)
            {
                if (// Lunar New Year
                    ((d >= 8 && d <= 10) && m == Month.February)
                    // Ching Ming Festival
                    || (d == 4 && m == Month.April)
                    // Tuen Ng festival
                    || (d == 9 && m == Month.June)
                    // Mid-autumn festival
                    || (d == 16 && m == Month.September)
                    // Chung Yeung festival
                    || (d == 10 && m == Month.October)
                    // Second day after Christmas
                    || (d == 27 && m == Month.December))
                {
                    return false;
                }
            }

            if (y == 2017)
            {
                if (// Lunar New Year
                    ((d == 30 || d == 31) && m == Month.January)
                    // Ching Ming Festival
                    || (d == 4 && m == Month.April)
                    // Buddha's birthday
                    || (d == 3 && m == Month.May)
                    // Tuen Ng festival
                    || (d == 30 && m == Month.May)
                    // Mid-autumn festival
                    || (d == 5 && m == Month.October))
                {
                    return false;
                }
            }

            if (y == 2018)
            {
                if (// Lunar New Year
                    ((d == 16 && m == Month.February) || (d == 19 && m == Month.February))
                    // Ching Ming Festival
                    || (d == 5 && m == Month.April)
                    // Buddha's birthday
                    || (d == 22 && m == Month.May)
                    // Tuen Ng festival
                    || (d == 18 && m == Month.June)
                    // Mid-autumn festival
                    || (d == 25 && m == Month.September)
                    // Chung Yeung festival
                    || (d == 17 && m == Month.October))
                {
                    return false;
                }
            }

            if (y == 2019)
            {
                if (// Lunar New Year
                    ((d >= 5 && d <= 7) && m == Month.February)
                    // Ching Ming Festival
                    || (d == 5 && m == Month.April)
                    // Tuen Ng festival
                    || (d == 7 && m == Month.June)
                    // Chung Yeung festival
                    || (d == 7 && m == Month.October))
                {
                    return false;
                }
            }

            if (y == 2020)
            {
                if (// Lunar New Year
                    ((d == 27 || d == 28) && m == Month.January)
                    // Ching Ming Festival
                    || (d == 4 && m == Month.April)
                    // Buddha's birthday
                    || (d == 30 && m == Month.April)
                    // Tuen Ng festival
                    || (d == 25 && m == Month.June)
                    // Mid-autumn festival
                    || (d == 2 && m == Month.October)
                    // Chung Yeung festival
                    || (d == 26 && m == Month.October))
                {
                    return false;
                }
            }

            // data from
            // https://www.hkex.com.hk/-/media/hkex-market/services/circulars-and-notices/participant-and-members-circulars/sehk/2020/ce_sehk_ct_038_2020.pdf
            if (y == 2021)
            {
                if (// Lunar New Year
                    ((d == 12 || d == 15) && m == Month.February)
                    // Ching Ming Festival
                    || (d == 5 && m == Month.April)
                    // Buddha's birthday
                    || (d == 19 && m == Month.May)
                    // Tuen Ng festival
                    || (d == 14 && m == Month.June)
                    // Mid-autumn festival
                    || (d == 22 && m == Month.September)
                    // Chung Yeung festival
                    || (d == 14 && m == Month.October))
                {
                    return false;
                }
            }

            // data from
            // https://www.hkex.com.hk/-/media/HKEX-Market/Services/Circulars-and-Notices/Participant-and-Members-Circulars/SEHK/2021/ce_SEHK_CT_082_2021.pdf
            if (y == 2022)
            {
                if (// Lunar New Year
                    ((d >= 1 && d <= 3) && m == Month.February)
                    // Ching Ming Festival
                    || (d == 5 && m == Month.April)
                    // Buddha's birthday
                    || (d == 9 && m == Month.May)
                    // Tuen Ng festival
                    || (d == 3 && m == Month.June)
                    // Mid-autumn festival
                    || (d == 12 && m == Month.September)
                    // Chung Yeung festival
                    || (d == 4 && m == Month.October))
                {
                    return false;
                }
            }

            // data from
            // https://www.hkex.com.hk/-/media/HKEX-Market/Services/Circulars-and-Notices/Participant-and-Members-Circulars/SEHK/2022/ce_SEHK_CT_058_2022.pdf
            if (y == 2023)
            {
                if (// Lunar New Year
                    ((d >= 23 && d <= 25) && m == Month.January)
                    // Ching Ming Festival
                    || (d == 5 && m == Month.April)
                    // Buddha's birthday
                    || (d == 26 && m == Month.May)
                    // Tuen Ng festival
                    || (d == 22 && m == Month.June)
                    // Chung Yeung festival
                    || (d == 23 && m == Month.October))
                {
                    return false;
                }
            }

            // data from
            // https://www.hkex.com.hk/-/media/HKEX-Market/Services/Circulars-and-Notices/Participant-and-Members-Circulars/SEHK/2023/ce_SEHK_CT_079_2023.pdf
            if (y == 2024)
            {
                if (// Lunar New Year
                    ((d == 12 || d == 13) && m == Month.February)
                    // Ching Ming Festival
                    || (d == 4 && m == Month.April)
                    // Buddha's birthday
                    || (d == 15 && m == Month.May)
                    // Tuen Ng festival
                    || (d == 10 && m == Month.June)
                    // Mid-autumn festival
                    || (d == 18 && m == Month.September)
                    // Chung Yeung festival
                    || (d == 11 && m == Month.October))
                {
                    return false;
                }
            }

            // data from
            // https://www.hkex.com.hk/-/media/HKEX-Market/Services/Circulars-and-Notices/Participant-and-Members-Circulars/SEHK/2024/ce_SEHK_CT_063_2024.pdf
            if (y == 2025)
            {
                if (// Lunar New Year
                    ((d >= 29 && d <= 31) && m == Month.January)
                    // Ching Ming Festival
                    || (d == 4 && m == Month.April)
                    // Buddha's birthday
                    || (d == 5 && m == Month.May)
                    // Mid-autumn festival
                    || (d == 7 && m == Month.October)
                    // Chung Yeung festival
                    || (d == 29 && m == Month.October))
                {
                    return false;
                }
            }

            // data from https://www.gov.hk/en/about/abouthk/holiday/2026.htm
            if (y == 2026)
            {
                if (// Lunar New Year
                    ((d >= 17 && d <= 19) && m == Month.February)
                    // The day following Easter Monday
                    || (d == 7 && m == Month.April)
                    // Buddha's birthday
                    || (d == 25 && m == Month.May)
                    // Tuen Ng festival
                    || (d == 19 && m == Month.June)
                    // Chung Yeung festival
                    || (d == 19 && m == Month.October))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
