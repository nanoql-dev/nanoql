// SPDX-FileCopyrightText: Copyright (C) 2010 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2015 Dmitri Nesteruk
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/russia.hpp ql/time/calendars/russia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Russian calendars.</summary>
/// <remarks>
/// <para>
/// Public holidays (see http://www.cbr.ru/eng/:):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year holidays, January 1st to 5th (only 1st and 2nd until 2005)</description></item>
/// <item><description>Christmas, January 7th (possibly moved to Monday)</description></item>
/// <item><description>Defender of the Fatherland Day, February 23rd (possibly moved to Monday)</description></item>
/// <item><description>International Women's Day, March 8th (possibly moved to Monday)</description></item>
/// <item><description>Labour Day, May 1st (possibly moved to Monday)</description></item>
/// <item><description>Victory Day, May 9th (possibly moved to Monday)</description></item>
/// <item><description>Russia Day, June 12th (possibly moved to Monday)</description></item>
/// <item><description>Unity Day, November 4th (possibly moved to Monday)</description></item>
/// </list>
/// </para>
/// <para>
/// Holidays for the Moscow Exchange (MOEX) taken from http://moex.com/s726 and related pages. These holidays are not
/// consistent year-to-year, may or may not correlate to public holidays, and are only available for dates since the
/// introduction of the MOEX 'brand' (a merger of the stock and futures markets).
/// </para>
/// <para>
/// The settlement calendar's one-off holidays run from 2017 to 2020, and the Moscow Exchange's year-by-year data from
/// 2012 to 2020: after 2020 only their rules apply. The exchange's calendar refuses a year before 2012, as QuantLib's
/// does.
/// </para>
/// </remarks>
public sealed class Russia : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl SettlementImplInstance = new SettlementImpl();
    private static readonly Impl ExchangeImplInstance = new ExchangeImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Russia"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Russia(RussiaMarket market = RussiaMarket.Settlement)
    {
        switch (market)
        {
            case RussiaMarket.Settlement:
                impl = SettlementImplInstance;
                break;
            case RussiaMarket.MOEX:
                impl = ExchangeImplInstance;
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

    private sealed class SettlementImpl : Impl
    {
        public override string Name => "Russian settlement";

        public override bool IsWeekend(Weekday w) => OrthodoxImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth;
            Month m = date.Month;
            int y = date.Year;
            if (OrthodoxImpl.IsWeekend(w)
                // New Year's holidays
                || (y <= 2005 && d <= 2 && m == Month.January)
                || (y >= 2005 && d <= 5 && m == Month.January)
                // in 2012, the 6th was also a holiday
                || (y == 2012 && d == 6 && m == Month.January)
                // Christmas (possibly moved to Monday)
                || ((d == 7 || ((d == 8 || d == 9) && w == Weekday.Monday)) &&
                    m == Month.January)
                // Defender of the Fatherland Day (possibly moved to Monday)
                || ((d == 23 || ((d == 24 || d == 25) && w == Weekday.Monday)) &&
                    m == Month.February)
                // International Women's Day (possibly moved to Monday)
                || ((d == 8 || ((d == 9 || d == 10) && w == Weekday.Monday)) &&
                    m == Month.March)
                // Labour Day (possibly moved to Monday)
                || ((d == 1 || ((d == 2 || d == 3) && w == Weekday.Monday)) &&
                    m == Month.May)
                // Victory Day (possibly moved to Monday)
                || ((d == 9 || ((d == 10 || d == 11) && w == Weekday.Monday)) &&
                    m == Month.May)
                // Russia Day (possibly moved to Monday)
                || ((d == 12 || ((d == 13 || d == 14) && w == Weekday.Monday)) &&
                    m == Month.June)
                // Unity Day (possibly moved to Monday)
                || ((d == 4 || ((d == 5 || d == 6) && w == Weekday.Monday)) &&
                    m == Month.November))
            {
                return false;
            }

            if (RussiaHelpers.IsExtraHolidaySettlementImpl(d, m, y))
            {
                return false;
            }

            return true;
        }
    }

    private sealed class ExchangeImpl : Impl
    {
        public override string Name => "Moscow exchange";

        public override bool IsWeekend(Weekday w) => OrthodoxImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth;
            Month m = date.Month;
            int y = date.Year;

            // the exchange was formally established in 2011, so data are only
            // available from 2012 to present
            if (y < 2012)
            {
                throw new NanoQLException($"MOEX calendar for the year {y} does not exist.");
            }

            if (RussiaHelpers.IsWorkingWeekend(d, m, y))
            {
                return true;
            }

            // Known holidays
            if (OrthodoxImpl.IsWeekend(w)
                // Defender of the Fatherland Day
                || (d == 23 && m == Month.February)
                // International Women's Day (possibly moved to Monday)
                || ((d == 8 || ((d == 9 || d == 10) && w == Weekday.Monday)) && m == Month.March)
                // Labour Day
                || (d == 1 && m == Month.May)
                // Victory Day (possibly moved to Monday)
                || ((d == 9 || ((d == 10 || d == 11) && w == Weekday.Monday)) && m == Month.May)
                // Russia Day
                || (d == 12 && m == Month.June)
                // Unity Day (possibly moved to Monday)
                || ((d == 4 || ((d == 5 || d == 6) && w == Weekday.Monday))
                    && m == Month.November)
                // New Years Eve
                || (d == 31 && m == Month.December))
            {
                return false;
            }

            if (RussiaHelpers.IsExtraHolidayExchangeImpl(d, m, y))
            {
                return false;
            }

            return true;
        }
    }
}

// QuantLib's anonymous-namespace helpers in russia.cpp.
file static class RussiaHelpers
{
    public static bool IsExtraHolidaySettlementImpl(int d, Month month, int year)
    {
        switch (year)
        {
            case 2017:
                switch (month)
                {
                    case Month.February: return d == 24;
                    case Month.May: return d == 8;
                    case Month.November: return d == 6;
                    default: return false;
                }

            case 2018:
                switch (month)
                {
                    case Month.March: return d == 9;
                    case Month.April: return d == 30;
                    case Month.May: return d == 2;
                    case Month.June: return d == 11;
                    case Month.December: return d == 31;
                    default: return false;
                }

            case 2019:
                switch (month)
                {
                    case Month.May: return d == 2 || d == 3 || d == 10;
                    default: return false;
                }

            case 2020:
                switch (month)
                {
                    case Month.March: return d == 30 || d == 31;
                    case Month.April: return d == 1 || d == 2 || d == 3;
                    case Month.May: return d == 4 || d == 5;
                    default: return false;
                }

            default:
                return false;
        }
    }

    public static bool IsWorkingWeekend(int d, Month month, int year)
    {
        switch (year)
        {
            case 2012:
                switch (month)
                {
                    case Month.March: return d == 11;
                    case Month.April: return d == 28;
                    case Month.May: return d == 5 || d == 12;
                    case Month.June: return d == 9;
                    default: return false;
                }

            case 2016:
                switch (month)
                {
                    case Month.February: return d == 20;
                    default: return false;
                }

            case 2018:
                switch (month)
                {
                    case Month.April: return d == 28;
                    case Month.June: return d == 9;
                    case Month.December: return d == 29;
                    default: return false;
                }

            default:
                return false;
        }
    }

    public static bool IsExtraHolidayExchangeImpl(int d, Month month, int year)
    {
        switch (year)
        {
            case 2012:
                switch (month)
                {
                    case Month.January: return d == 2;
                    case Month.March: return d == 9;
                    case Month.April: return d == 30;
                    case Month.June: return d == 11;
                    default: return false;
                }

            case 2013:
                switch (month)
                {
                    case Month.January: return d == 1 || d == 2 || d == 3
                                            || d == 4 || d == 7;
                    default: return false;
                }

            case 2014:
                switch (month)
                {
                    case Month.January: return d == 1 || d == 2 || d == 3 || d == 7;
                    default: return false;
                }

            case 2015:
                switch (month)
                {
                    case Month.January: return d == 1 || d == 2 || d == 7;
                    default: return false;
                }

            case 2016:
                switch (month)
                {
                    case Month.January: return d == 1 || d == 7 || d == 8;
                    case Month.May: return d == 2 || d == 3;
                    case Month.June: return d == 13;
                    case Month.December: return d == 30;
                    default: return false;
                }

            case 2017:
                switch (month)
                {
                    case Month.January: return d == 2;
                    case Month.May: return d == 8;
                    default: return false;
                }

            case 2018:
                switch (month)
                {
                    case Month.January: return d == 1 || d == 2 || d == 8;
                    case Month.December: return d == 31;
                    default: return false;
                }

            case 2019:
                switch (month)
                {
                    case Month.January: return d == 1 || d == 2 || d == 7;
                    case Month.December: return d == 31;
                    default: return false;
                }

            case 2020:
                switch (month)
                {
                    case Month.January: return d == 1 || d == 2 || d == 7;
                    case Month.February: return d == 24;
                    case Month.June: return d == 24;
                    case Month.July: return d == 1;
                    default: return false;
                }

            default:
                return false;
        }
    }
}
