// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/canada.hpp ql/time/calendars/canada.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Canadian calendars.</summary>
/// <remarks>
/// <para>
/// Banking holidays (data from http://www.bankofcanada.ca/en/about/holiday.html):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st (possibly moved to Monday)</description></item>
/// <item><description>Family Day, third Monday of February (since 2008)</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Victoria Day, the Monday on or preceding May 24th</description></item>
/// <item><description>Canada Day, July 1st (possibly moved to Monday)</description></item>
/// <item><description>Provincial Holiday, first Monday of August</description></item>
/// <item><description>Labour Day, first Monday of September</description></item>
/// <item><description>
/// National Day for Truth and Reconciliation, September 30th (possibly moved to Monday)
/// </description></item>
/// <item><description>Thanksgiving Day, second Monday of October</description></item>
/// <item><description>Remembrance Day, November 11th (possibly moved to Monday)</description></item>
/// <item><description>Christmas, December 25th (possibly moved to Monday or Tuesday)</description></item>
/// <item><description>Boxing Day, December 26th (possibly moved to Monday or Tuesday)</description></item>
/// </list>
/// </para>
/// <para>
/// Holidays for the Toronto stock exchange (data from http://www.tsx.com/en/about_tsx/market_hours.html):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st (possibly moved to Monday)</description></item>
/// <item><description>Family Day, third Monday of February (since 2008)</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Victoria Day, the Monday on or preceding May 24th</description></item>
/// <item><description>Canada Day, July 1st (possibly moved to Monday)</description></item>
/// <item><description>Provincial Holiday, first Monday of August</description></item>
/// <item><description>Labour Day, first Monday of September</description></item>
/// <item><description>Thanksgiving Day, second Monday of October</description></item>
/// <item><description>Christmas, December 25th (possibly moved to Monday or Tuesday)</description></item>
/// <item><description>Boxing Day, December 26th (possibly moved to Monday or Tuesday)</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class Canada : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl SettlementImplInstance = new SettlementImpl();
    private static readonly Impl TsxImplInstance = new TsxImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Canada"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows; the generic settlement calendar by default.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Canada(CanadaMarket market = CanadaMarket.Settlement)
    {
        switch (market)
        {
            case CanadaMarket.Settlement:
                impl = SettlementImplInstance;
                break;
            case CanadaMarket.TSX:
                impl = TsxImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }

        ApplyEdits(ConventionProvider.ReadCalendarEdits($"Canada.{market}"));
    }

    /// <inheritdoc/>
    public override string Name => impl.Name;

    /// <inheritdoc/>
    public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

    /// <inheritdoc/>
    protected override bool IsBusinessDayCore(Date date) => impl.IsBusinessDay(date);

    // The market-specific rules; QuantLib's Calendar::Impl, with Calendar::WesternImpl's weekend.
    private abstract class Impl
    {
        public abstract string Name { get; }

        public abstract bool IsBusinessDay(Date date);
    }

    private sealed class SettlementImpl : Impl
    {
        public override string Name => "Canada";

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);
            if (WesternImpl.IsWeekend(w)
                // New Year's Day (possibly moved to Monday)
                || ((d == 1 || ((d == 2 || d == 3) && w == Weekday.Monday)) && m == Month.January)
                // Family Day (third Monday in February, since 2008)
                || ((d >= 15 && d <= 21) && w == Weekday.Monday && m == Month.February
                    && y >= 2008)
                // Good Friday
                || (dd == em - 3)
                // The Monday on or preceding 24 May (Victoria Day)
                || (d > 17 && d <= 24 && w == Weekday.Monday && m == Month.May)
                // July 1st, possibly moved to Monday (Canada Day)
                || ((d == 1 || ((d == 2 || d == 3) && w == Weekday.Monday)) && m == Month.July)
                // first Monday of August (Provincial Holiday)
                || (d <= 7 && w == Weekday.Monday && m == Month.August)
                // first Monday of September (Labor Day)
                || (d <= 7 && w == Weekday.Monday && m == Month.September)
                // September 30th, possibly moved to Monday
                // (National Day for Truth and Reconciliation, since 2021)
                || (((d == 30 && m == Month.September) || (d <= 2 && m == Month.October && w == Weekday.Monday))
                    && y >= 2021)
                // second Monday of October (Thanksgiving Day)
                || (d > 7 && d <= 14 && w == Weekday.Monday && m == Month.October)
                // November 11th (possibly moved to Monday)
                || ((d == 11 || ((d == 12 || d == 13) && w == Weekday.Monday))
                    && m == Month.November)
                // Christmas (possibly moved to Monday or Tuesday)
                || ((d == 25 || (d == 27 && (w == Weekday.Monday || w == Weekday.Tuesday)))
                    && m == Month.December)
                // Boxing Day (possibly moved to Monday or Tuesday)
                || ((d == 26 || (d == 28 && (w == Weekday.Monday || w == Weekday.Tuesday)))
                    && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }

    private sealed class TsxImpl : Impl
    {
        public override string Name => "TSX";

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);
            if (WesternImpl.IsWeekend(w)
                // New Year's Day (possibly moved to Monday)
                || ((d == 1 || ((d == 2 || d == 3) && w == Weekday.Monday)) && m == Month.January)
                // Family Day (third Monday in February, since 2008)
                || ((d >= 15 && d <= 21) && w == Weekday.Monday && m == Month.February
                    && y >= 2008)
                // Good Friday
                || (dd == em - 3)
                // The Monday on or preceding 24 May (Victoria Day)
                || (d > 17 && d <= 24 && w == Weekday.Monday && m == Month.May)
                // July 1st, possibly moved to Monday (Canada Day)
                || ((d == 1 || ((d == 2 || d == 3) && w == Weekday.Monday)) && m == Month.July)
                // first Monday of August (Provincial Holiday)
                || (d <= 7 && w == Weekday.Monday && m == Month.August)
                // first Monday of September (Labor Day)
                || (d <= 7 && w == Weekday.Monday && m == Month.September)
                // second Monday of October (Thanksgiving Day)
                || (d > 7 && d <= 14 && w == Weekday.Monday && m == Month.October)
                // Christmas (possibly moved to Monday or Tuesday)
                || ((d == 25 || (d == 27 && (w == Weekday.Monday || w == Weekday.Tuesday)))
                    && m == Month.December)
                // Boxing Day (possibly moved to Monday or Tuesday)
                || ((d == 26 || (d == 28 && (w == Weekday.Monday || w == Weekday.Tuesday)))
                    && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }
}
