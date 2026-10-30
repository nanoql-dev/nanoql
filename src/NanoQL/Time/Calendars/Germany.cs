// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/germany.hpp ql/time/calendars/germany.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The German calendars.</summary>
/// <remarks>
/// <para>
/// Public holidays:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Ascension Thursday</description></item>
/// <item><description>Whit Monday</description></item>
/// <item><description>Corpus Christi</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>National Day, October 3rd</description></item>
/// <item><description>Christmas Eve, December 24th</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>Boxing Day, December 26th</description></item>
/// </list>
/// </para>
/// <para>
/// Holidays for the Frankfurt Stock exchange (data from http://deutsche-boerse.com/):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>Christmas' Eve, December 24th</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>Christmas Holiday, December 26th</description></item>
/// </list>
/// </para>
/// <para>
/// Holidays for the Xetra exchange (data from http://deutsche-boerse.com/):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>Christmas' Eve, December 24th</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>Christmas Holiday, December 26th</description></item>
/// </list>
/// </para>
/// <para>
/// Holidays for the Eurex exchange (data from http://www.eurexchange.com/index.html):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>Christmas' Eve, December 24th</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>Christmas Holiday, December 26th</description></item>
/// <item><description>New Year's Eve, December 31st</description></item>
/// </list>
/// </para>
/// <para>
/// Holidays for the Euwax exchange (data from http://www.boerse-stuttgart.de):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>Whit Monday</description></item>
/// <item><description>Christmas' Eve, December 24th</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>Christmas Holiday, December 26th</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class Germany : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl SettlementImplInstance = new SettlementImpl();
    private static readonly Impl FrankfurtStockExchangeImplInstance = new FrankfurtStockExchangeImpl();
    private static readonly Impl XetraImplInstance = new XetraImpl();
    private static readonly Impl EurexImplInstance = new EurexImpl();
    private static readonly Impl EuwaxImplInstance = new EuwaxImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Germany"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Germany(GermanyMarket market = GermanyMarket.FrankfurtStockExchange)
    {
        switch (market)
        {
            case GermanyMarket.Settlement:
                impl = SettlementImplInstance;
                break;
            case GermanyMarket.FrankfurtStockExchange:
                impl = FrankfurtStockExchangeImplInstance;
                break;
            case GermanyMarket.Xetra:
                impl = XetraImplInstance;
                break;
            case GermanyMarket.Eurex:
                impl = EurexImplInstance;
                break;
            case GermanyMarket.Euwax:
                impl = EuwaxImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }

        ApplyEdits(ConventionProvider.ReadCalendarEdits($"Germany.{market}"));
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
        public override string Name => "German settlement";

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
                || (d == 1 && m == Month.January)
                // Good Friday
                || (dd == em - 3)
                // Easter Monday
                || (dd == em)
                // Ascension Thursday
                || (dd == em + 38)
                // Whit Monday
                || (dd == em + 49)
                // Corpus Christi
                || (dd == em + 59)
                // Labour Day
                || (d == 1 && m == Month.May)
                // National Day
                || (d == 3 && m == Month.October)
                // Christmas Eve
                || (d == 24 && m == Month.December)
                // Christmas
                || (d == 25 && m == Month.December)
                // Boxing Day
                || (d == 26 && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }

    private sealed class FrankfurtStockExchangeImpl : Impl
    {
        public override string Name => "Frankfurt stock exchange";

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
                || (d == 1 && m == Month.January)
                // Good Friday
                || (dd == em - 3)
                // Easter Monday
                || (dd == em)
                // Labour Day
                || (d == 1 && m == Month.May)
                // Christmas' Eve
                || (d == 24 && m == Month.December)
                // Christmas
                || (d == 25 && m == Month.December)
                // Christmas Day
                || (d == 26 && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }

    private sealed class XetraImpl : Impl
    {
        public override string Name => "Xetra";

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
                || (d == 1 && m == Month.January)
                // Good Friday
                || (dd == em - 3)
                // Easter Monday
                || (dd == em)
                // Labour Day
                || (d == 1 && m == Month.May)
                // Christmas' Eve
                || (d == 24 && m == Month.December)
                // Christmas
                || (d == 25 && m == Month.December)
                // Christmas Day
                || (d == 26 && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }

    private sealed class EurexImpl : Impl
    {
        public override string Name => "Eurex";

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
                || (d == 1 && m == Month.January)
                // Good Friday
                || (dd == em - 3)
                // Easter Monday
                || (dd == em)
                // Labour Day
                || (d == 1 && m == Month.May)
                // Christmas' Eve
                || (d == 24 && m == Month.December)
                // Christmas
                || (d == 25 && m == Month.December)
                // Christmas Day
                || (d == 26 && m == Month.December)
                // New Year's Eve
                || (d == 31 && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }

    private sealed class EuwaxImpl : Impl
    {
        public override string Name => "Euwax";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);
            if ((w == Weekday.Saturday || w == Weekday.Sunday)
                // New Year's Day
                || (d == 1 && m == Month.January)
                // Good Friday
                || (dd == em - 3)
                // Easter Monday
                || (dd == em)
                // Labour Day
                || (d == 1 && m == Month.May)
                // Whit Monday
                || (dd == em + 49)
                // Christmas' Eve
                || (d == 24 && m == Month.December)
                // Christmas
                || (d == 25 && m == Month.December)
                // Christmas Day
                || (d == 26 && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }
}
