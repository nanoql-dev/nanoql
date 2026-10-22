// SPDX-FileCopyrightText: Copyright (C) 2026 SoftSolution srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/malta.hpp ql/time/calendars/malta.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Maltese calendar.</summary>
/// <remarks>
/// <para>
/// Holidays for the Malta Stock Exchange (https://borzamalta.com.mt): Calendars here
/// https://borzamalta.com.mt/publications-and-statistics?category=54
/// https://cdn.borzamalta.com.mt/download/2025%20Trading%20Calendar%20EUR_GBP_USD%20Final.pdf
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>St. Paul's Shipwreck, February 10th</description></item>
/// <item><description>St. Joseph's Day, March 19th</description></item>
/// <item><description>Freedom Day, March 31st</description></item>
/// <item><description>Good Friday, variable date</description></item>
/// <item><description>Easter Monday (variable date)</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>Feast of Saints Peter and Paul (Imnarja), June 29th</description></item>
/// <item><description>Assumption of Mary, August 15th</description></item>
/// <item><description>Feast of Our Lady of Victories (Nativity of Mary), September 8th</description></item>
/// <item><description>Independence Day, September 21st</description></item>
/// <item><description>Immaculate Conception, December 8th</description></item>
/// <item><description>Republic Day, December 13th</description></item>
/// <item><description>Christmas Vigil, December 24th</description></item>
/// <item><description>Christmas Day, December 25th</description></item>
/// <item><description>Boxing Day, December 26th (occasionally observed)</description></item>
/// <item><description>New Year's Eve, December 31st (non-trading day)</description></item>
/// </list>
/// </para>
/// <para>
/// Notes:
/// <list type="bullet">
/// <item><description>Holidays falling on a weekend are generally not shifted.</description></item>
/// <item><description>Trading hours and holiday observance are set by the Malta Stock Exchange each
/// year.</description></item>
/// </list>
/// </para>
/// <para>
/// The weekend is Saturday and Sunday, as the list above and the exchange's calendar have it; QuantLib 1.44's code
/// takes Friday and Saturday, so every Friday is a holiday and every Sunday a business day there: a documented
/// divergence.
/// </para>
/// </remarks>
public sealed class Malta : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl MseImplInstance = new MseImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Malta"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Malta(MaltaMarket market = MaltaMarket.MSE)
    {
        switch (market)
        {
            case MaltaMarket.MSE:
                impl = MseImplInstance;
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

    private sealed class MseImpl : Impl
    {
        public override string Name => "Malta Stock Exchange";

        // Saturdays and Sundays, as the header's list and the exchange's calendar have them; QuantLib's code overrides
        // the weekend with Friday and Saturday
        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);
            int gf = em - 3; // Good Friday

            if (IsWeekend(w)
                // New Year's Day
                || (d == 1 && m == Month.January)
                // St. Paul's Shipwreck
                || (d == 10 && m == Month.February)
                // St. Joseph's Day
                || (d == 19 && m == Month.March)
                // Freedom Day
                || (d == 31 && m == Month.March)
                // Good Friday
                || (date.DayOfYear == gf)
                // Easter Monday (exchange holiday)
                || (date.DayOfYear == em)
                // Labour Day
                || (d == 1 && m == Month.May)
                // Imnarja (Feast of Saints Peter & Paul)
                || (d == 29 && m == Month.June)
                // Assumption of Mary
                || (d == 15 && m == Month.August)
                // Our Lady of Victories (Nativity of Mary)
                || (d == 8 && m == Month.September)
                // Independence Day
                || (d == 21 && m == Month.September)
                // Immaculate Conception
                || (d == 8 && m == Month.December)
                // Republic Day
                || (d == 13 && m == Month.December)
                // Christmas Vigil
                || (d == 24 && m == Month.December)
                // Christmas Day
                || (d == 25 && m == Month.December)
                // Boxing Day (occasionally observed by MSE)
                || (d == 26 && m == Month.December)
                // New Year's Eve (non-trading)
                || (d == 31 && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }
}
