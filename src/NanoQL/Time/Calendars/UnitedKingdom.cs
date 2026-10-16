// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/unitedkingdom.hpp ql/time/calendars/unitedkingdom.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The United Kingdom calendars.</summary>
/// <remarks>
/// <para>
/// Repeating public holidays (data from https://www.gov.uk/bank-holidays):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st (possibly moved to Monday)</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Early May Bank Holiday, first Monday of May</description></item>
/// <item><description>Spring Bank Holiday, last Monday of May</description></item>
/// <item><description>Summer Bank Holiday, last Monday of August</description></item>
/// <item><description>Christmas Day, December 25th (possibly moved to Monday or Tuesday)</description></item>
/// <item><description>Boxing Day, December 26th (possibly moved to Monday or Tuesday)</description></item>
/// </list>
/// </para>
/// <para>
/// Holidays for the stock exchange:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st (possibly moved to Monday)</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Early May Bank Holiday, first Monday of May</description></item>
/// <item><description>Spring Bank Holiday, last Monday of May</description></item>
/// <item><description>Summer Bank Holiday, last Monday of August</description></item>
/// <item><description>Christmas Day, December 25th (possibly moved to Monday or Tuesday)</description></item>
/// <item><description>Boxing Day, December 26th (possibly moved to Monday or Tuesday)</description></item>
/// </list>
/// </para>
/// <para>
/// Holidays for the metals exchange:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st (possibly moved to Monday)</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Early May Bank Holiday, first Monday of May</description></item>
/// <item><description>Spring Bank Holiday, last Monday of May</description></item>
/// <item><description>Summer Bank Holiday, last Monday of August</description></item>
/// <item><description>Christmas Day, December 25th (possibly moved to Monday or Tuesday)</description></item>
/// <item><description>Boxing Day, December 26th (possibly moved to Monday or Tuesday)</description></item>
/// </list>
/// </para>
/// <para>
/// Note that there are some one-off holidays not listed above. See the implementation for the complete list.
/// </para>
/// </remarks>
public sealed class UnitedKingdom : Calendar
{
    // all calendar instances on the same market share the same
    // implementation instance
    private static readonly Impl SettlementImplInstance = new SettlementImpl();
    private static readonly Impl ExchangeImplInstance = new ExchangeImpl();
    private static readonly Impl MetalsImplInstance = new MetalsImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="UnitedKingdom"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows; the generic settlement calendar by default.</param>
    /// <exception cref="NanoQLException">The market is unknown.</exception>
    public UnitedKingdom(UnitedKingdomMarket market = UnitedKingdomMarket.Settlement)
    {
        switch (market)
        {
            case UnitedKingdomMarket.Settlement:
                impl = SettlementImplInstance;
                break;
            case UnitedKingdomMarket.Exchange:
                impl = ExchangeImplInstance;
                break;
            case UnitedKingdomMarket.Metals:
                impl = MetalsImplInstance;
                break;
            default:
                throw new NanoQLException("unknown market");
        }
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
        public override string Name => "UK settlement";

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);
            if (WesternImpl.IsWeekend(w)
                // New Year's Day (possibly moved to Monday)
                || ((d == 1 || ((d == 2 || d == 3) && w == Weekday.Monday))
                    && m == Month.January)
                // Good Friday
                || (dd == em - 3)
                // Easter Monday
                || (dd == em)
                || UnitedKingdomRules.IsBankHoliday(d, w, m, y)
                // Christmas (possibly moved to Monday or Tuesday)
                || ((d == 25 || (d == 27 && (w == Weekday.Monday || w == Weekday.Tuesday)))
                    && m == Month.December)
                // Boxing Day (possibly moved to Monday or Tuesday)
                || ((d == 26 || (d == 28 && (w == Weekday.Monday || w == Weekday.Tuesday)))
                    && m == Month.December)
                // December 31st, 1999 only
                || (d == 31 && m == Month.December && y == 1999))
            {
                return false;
            }

            return true;
        }
    }

    private sealed class ExchangeImpl : Impl
    {
        public override string Name => "London stock exchange";

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);
            if (WesternImpl.IsWeekend(w)
                // New Year's Day (possibly moved to Monday)
                || ((d == 1 || ((d == 2 || d == 3) && w == Weekday.Monday))
                    && m == Month.January)
                // Good Friday
                || (dd == em - 3)
                // Easter Monday
                || (dd == em)
                || UnitedKingdomRules.IsBankHoliday(d, w, m, y)
                // Christmas (possibly moved to Monday or Tuesday)
                || ((d == 25 || (d == 27 && (w == Weekday.Monday || w == Weekday.Tuesday)))
                    && m == Month.December)
                // Boxing Day (possibly moved to Monday or Tuesday)
                || ((d == 26 || (d == 28 && (w == Weekday.Monday || w == Weekday.Tuesday)))
                    && m == Month.December)
                // December 31st, 1999 only
                || (d == 31 && m == Month.December && y == 1999))
            {
                return false;
            }

            return true;
        }
    }

    private sealed class MetalsImpl : Impl
    {
        public override string Name => "London metals exchange";

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);
            if (WesternImpl.IsWeekend(w)
                // New Year's Day (possibly moved to Monday)
                || ((d == 1 || ((d == 2 || d == 3) && w == Weekday.Monday))
                    && m == Month.January)
                // Good Friday
                || (dd == em - 3)
                // Easter Monday
                || (dd == em)
                || UnitedKingdomRules.IsBankHoliday(d, w, m, y)
                // Christmas (possibly moved to Monday or Tuesday)
                || ((d == 25 || (d == 27 && (w == Weekday.Monday || w == Weekday.Tuesday)))
                    && m == Month.December)
                // Boxing Day (possibly moved to Monday or Tuesday)
                || ((d == 26 || (d == 28 && (w == Weekday.Monday || w == Weekday.Tuesday)))
                    && m == Month.December)
                // December 31st, 1999 only
                || (d == 31 && m == Month.December && y == 1999))
            {
                return false;
            }

            return true;
        }
    }
}

// common rules
file static class UnitedKingdomRules
{
    public static bool IsBankHoliday(int d, Weekday w, Month m, int y)
    {
        return
            // first Monday of May (Early May Bank Holiday)
            // moved to May 8th in 1995 and 2020 for V.E. day
            (d <= 7 && w == Weekday.Monday && m == Month.May && y != 1995 && y != 2020)
            || (d == 8 && m == Month.May && (y == 1995 || y == 2020))
            // last Monday of May (Spring Bank Holiday)
            // moved to in 2002, 2012 and 2022 for the Golden, Diamond and Platinum
            // Jubilee with an additional holiday
            || (d >= 25 && w == Weekday.Monday && m == Month.May && y != 2002 && y != 2012 && y != 2022)
            || ((d == 3 || d == 4) && m == Month.June && y == 2002)
            || ((d == 4 || d == 5) && m == Month.June && y == 2012)
            || ((d == 2 || d == 3) && m == Month.June && y == 2022)
            // last Monday of August (Summer Bank Holiday)
            || (d >= 25 && w == Weekday.Monday && m == Month.August)
            // April 29th, 2011 only (Royal Wedding Bank Holiday)
            || (d == 29 && m == Month.April && y == 2011)
            // September 19th, 2022 only (The Queen's Funeral Bank Holiday)
            || (d == 19 && m == Month.September && y == 2022)
            // May 8th, 2023 (King Charles III Coronation Bank Holiday)
            || (d == 8 && m == Month.May && y == 2023);
    }
}
