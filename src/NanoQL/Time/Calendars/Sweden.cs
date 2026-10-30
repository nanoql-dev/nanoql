// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/sweden.hpp ql/time/calendars/sweden.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Swedish calendar.</summary>
/// <remarks>
/// <para>
/// Holidays:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Epiphany, January 6th</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Ascension</description></item>
/// <item><description>Whit(Pentecost) Monday (until 2004)</description></item>
/// <item><description>May Day, May 1st</description></item>
/// <item><description>National Day, June 6th</description></item>
/// <item><description>Midsummer Eve (Friday between June 19-25)</description></item>
/// <item><description>Christmas Eve, December 24th</description></item>
/// <item><description>Christmas Day, December 25th</description></item>
/// <item><description>Boxing Day, December 26th</description></item>
/// <item><description>New Year's Eve, December 31st</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class Sweden : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl SwedenImplInstance = new SwedenImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Sweden"/> class.</summary>
    public Sweden()
    {
        impl = SwedenImplInstance;
        ApplyEdits(ConventionProvider.ReadCalendarEdits("Sweden"));
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

    private sealed class SwedenImpl : Impl
    {
        public override string Name => "Sweden";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);
            if (WesternImpl.IsWeekend(w)
                // Good Friday
                || (dd == em - 3)
                // Easter Monday
                || (dd == em)
                // Ascension Thursday
                || (dd == em + 38)
                // Whit Monday (till 2004)
                || (dd == em + 49 && y < 2005)
                // New Year's Day
                || (d == 1 && m == Month.January)
                // Epiphany
                || (d == 6 && m == Month.January)
                // May Day
                || (d == 1 && m == Month.May)
                // National Day
                // Only a holiday since 2005
                || (d == 6 && m == Month.June && y >= 2005)
                // Midsummer Eve (Friday between June 19-25)
                || (w == Weekday.Friday && (d >= 19 && d <= 25) && m == Month.June)
                // Christmas Eve
                || (d == 24 && m == Month.December)
                // Christmas Day
                || (d == 25 && m == Month.December)
                // Boxing Day
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
