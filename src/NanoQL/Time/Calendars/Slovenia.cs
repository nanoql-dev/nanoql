// SPDX-FileCopyrightText: Copyright (C) 2026 SoftSolution srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/slovenia.hpp ql/time/calendars/slovenia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Slovenian calendars.</summary>
/// <remarks>
/// <para>
/// Holidays for the Slovenia stock exchange (data from https://ljse.si/en/non-trading-days/110):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>New Year's Holiday, January 2nd</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>May Day, May 1st</description></item>
/// <item><description>May Day Holiday, May 2nd</description></item>
/// <item><description>Statehood Day, June 25th</description></item>
/// <item><description>Assumption of Mary, August 15th</description></item>
/// <item><description>Reformation Day, October 31st</description></item>
/// <item><description>Christmas Eve, December 24th</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>St. Stephen, December 26th</description></item>
/// <item><description>New Year's Eve, December 31st</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class Slovenia : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl LseImplInstance = new LseImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Slovenia"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Slovenia(SloveniaMarket market = SloveniaMarket.LSE)
    {
        switch (market)
        {
            case SloveniaMarket.LSE:
                impl = LseImplInstance;
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

    private sealed class LseImpl : Impl
    {
        public override string Name => "Ljubljana stock exchange";

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
                // New Year's Holiday
                || (d == 2 && m == Month.January)
                // Good Friday
                || (dd == em - 3)
                // Easter Monday
                || (dd == em)
                // May Day
                || (d == 1 && m == Month.May)
                // May Day Holiday
                || (d == 2 && m == Month.May)
                // Statehood Day
                || (d == 25 && m == Month.June)
                // Assumption of Mary
                || (d == 15 && m == Month.August)
                // Reformation Day
                || (d == 31 && m == Month.October)
                // Christmas Eve
                || (d == 24 && m == Month.December)
                // Christmas
                || (d == 25 && m == Month.December)
                // St. Stephen
                || (d == 26 && m == Month.December)
                // New Year's Eve
                || (d == 31 && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }
}
