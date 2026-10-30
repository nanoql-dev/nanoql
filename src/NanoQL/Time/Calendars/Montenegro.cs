// SPDX-FileCopyrightText: Copyright (C) 2026 SoftSolution srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/montenegro.hpp ql/time/calendars/montenegro.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Montenegrin calendars.</summary>
/// <remarks>
/// <para>
/// Holidays for the Montenegrin Stock Exchange (https://mnse.me):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>New Year Holiday, January 2nd</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>Labour Day Holiday, May 2nd</description></item>
/// <item><description>Independence Day, May 21st</description></item>
/// <item><description>Independence Day Holiday, May 22nd</description></item>
/// <item><description>Statehood Day, July 13th</description></item>
/// <item><description>Statehood Day Holiday, July 14th</description></item>
/// <item><description>Njegos Day, November 13th</description></item>
/// <item><description>Njegos Day Holiday, November 14th</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class Montenegro : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl MnseImplInstance = new MnseImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Montenegro"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Montenegro(MontenegroMarket market = MontenegroMarket.MNSE)
    {
        switch (market)
        {
            case MontenegroMarket.MNSE:
                impl = MnseImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }

        ApplyEdits(ConventionProvider.ReadCalendarEdits($"Montenegro.{market}"));
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

    private sealed class MnseImpl : Impl
    {
        public override string Name => "Montenegro Stock Exchange";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth;
            Month m = date.Month;

            if (WesternImpl.IsWeekend(w)
                // New Year's Day
                || (d == 1 && m == Month.January)
                // New Year Holiday
                || (d == 2 && m == Month.January)
                // Labour Day
                || (d == 1 && m == Month.May)
                // Labour Day Holiday
                || (d == 2 && m == Month.May)
                // Independence Day
                || (d == 21 && m == Month.May)
                // Independence Day Holiday
                || (d == 22 && m == Month.May)
                // Statehood Day
                || (d == 13 && m == Month.July)
                // Statehood Day Holiday
                || (d == 14 && m == Month.July)
                // Njegos Day
                || (d == 13 && m == Month.November)
                // Njegos Day Holiday
                || (d == 14 && m == Month.November))
            {
                return false;
            }

            return true;
        }
    }
}
