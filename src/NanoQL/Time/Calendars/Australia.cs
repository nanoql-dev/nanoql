// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/australia.hpp ql/time/calendars/australia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Australian calendars.</summary>
/// <remarks>
/// <para>
/// Holidays:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st (possibly moved to Monday)</description></item>
/// <item><description>Australia Day, January 26th (possibly moved to Monday)</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>ANZAC Day, April 25th</description></item>
/// <item><description>Queen's Birthday, second Monday in June</description></item>
/// <item><description>Bank Holiday, first Monday in August</description></item>
/// <item><description>Labour Day, first Monday in October</description></item>
/// <item><description>Christmas, December 25th (possibly moved to Monday or Tuesday)</description></item>
/// <item><description>Boxing Day, December 26th (possibly moved to Monday or Tuesday)</description></item>
/// <item><description>National Day of Mourning for Her Majesty, September 22, 2022</description></item>
/// </list>
/// </para>
/// <para>
/// QuantLib's header still moves ANZAC Day to Monday; its code stopped doing so in 2022, and this list follows
/// the code.
/// </para>
/// </remarks>
public sealed class Australia : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl SettlementImplInstance = new SettlementImpl();
    private static readonly Impl AsxImplInstance = new AsxImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Australia"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Australia(AustraliaMarket market = AustraliaMarket.Settlement)
    {
        switch (market)
        {
            case AustraliaMarket.Settlement:
                impl = SettlementImplInstance;
                break;
            case AustraliaMarket.ASX:
                impl = AsxImplInstance;
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
        public override string Name => "Australia settlement";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

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
                // Australia Day, January 26th (possibly moved to Monday)
                || ((d == 26 || ((d == 27 || d == 28) && w == Weekday.Monday)) &&
                    m == Month.January)
                // Good Friday
                || (dd == em - 3)
                // Easter Monday
                || (dd == em)
                // ANZAC Day, April 25th
                || (d == 25 && m == Month.April)
                // Queen's Birthday, second Monday in June
                || ((d > 7 && d <= 14) && w == Weekday.Monday && m == Month.June)
                // Bank Holiday, first Monday in August
                || (d <= 7 && w == Weekday.Monday && m == Month.August)
                // Labour Day, first Monday in October
                || (d <= 7 && w == Weekday.Monday && m == Month.October)
                // Christmas, December 25th (possibly Monday or Tuesday)
                || ((d == 25 || (d == 27 && (w == Weekday.Monday || w == Weekday.Tuesday)))
                    && m == Month.December)
                // Boxing Day, December 26th (possibly Monday or Tuesday)
                || ((d == 26 || (d == 28 && (w == Weekday.Monday || w == Weekday.Tuesday)))
                    && m == Month.December)
                // National Day of Mourning for Her Majesty, September 22 (only 2022)
                || (d == 22 && m == Month.September && y == 2022))
            {
                return false;
            }

            return true;
        }
    }

    private sealed class AsxImpl : Impl
    {
        public override string Name => "Australia exchange";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

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
                // Australia Day, January 26th (possibly moved to Monday)
                || ((d == 26 || ((d == 27 || d == 28) && w == Weekday.Monday)) &&
                    m == Month.January)
                // Good Friday
                || (dd == em - 3)
                // Easter Monday
                || (dd == em)
                // ANZAC Day, April 25th
                || (d == 25 && m == Month.April)
                // Queen's Birthday, second Monday in June
                || ((d > 7 && d <= 14) && w == Weekday.Monday && m == Month.June)
                // Christmas, December 25th (possibly Monday or Tuesday)
                || ((d == 25 || (d == 27 && (w == Weekday.Monday || w == Weekday.Tuesday)))
                    && m == Month.December)
                // Boxing Day, December 26th (possibly Monday or Tuesday)
                || ((d == 26 || (d == 28 && (w == Weekday.Monday || w == Weekday.Tuesday)))
                    && m == Month.December)
                // National Day of Mourning for Her Majesty, September 22 (only 2022)
                || (d == 22 && m == Month.September && y == 2022))
            {
                return false;
            }

            return true;
        }
    }
}
