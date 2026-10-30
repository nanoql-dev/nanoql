// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2023, 2024 Skandinaviska Enskilda Banken AB (publ)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/southafrica.hpp ql/time/calendars/southafrica.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The South African calendar.</summary>
/// <remarks>
/// <para>
/// Holidays:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st (possibly moved to Monday)</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Family Day, Easter Monday</description></item>
/// <item><description>Human Rights Day, March 21st (possibly moved to Monday)</description></item>
/// <item><description>Freedom Day, April 27th (possibly moved to Monday)</description></item>
/// <item><description>Workers Day, May 1st (possibly moved to Monday)</description></item>
/// <item><description>Youth Day, June 16th (possibly moved to Monday)</description></item>
/// <item><description>National Women's Day, August 9th (possibly moved to Monday)</description></item>
/// <item><description>Heritage Day, September 24th (possibly moved to Monday)</description></item>
/// <item><description>Day of Reconciliation, December 16th (possibly moved to Monday)</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>Day of Goodwill, December 26th (possibly moved to Monday)</description></item>
/// <item><description>Election Days</description></item>
/// </list>
/// </para>
/// <para>
/// Note that there are some one-off holidays not listed above. See the implementation for the complete list.
/// </para>
/// </remarks>
public sealed class SouthAfrica : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl SouthAfricaImplInstance = new SouthAfricaImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="SouthAfrica"/> class.</summary>
    public SouthAfrica()
    {
        impl = SouthAfricaImplInstance;
        ApplyEdits(ConventionProvider.ReadCalendarEdits("SouthAfrica"));
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

    private sealed class SouthAfricaImpl : Impl
    {
        public override string Name => "South Africa";

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
                || ((d == 1 || (d == 2 && w == Weekday.Monday)) && m == Month.January)
                // Good Friday
                || (dd == em - 3)
                // Family Day
                || (dd == em)
                // Human Rights Day, March 21st (possibly moved to Monday)
                || ((d == 21 || (d == 22 && w == Weekday.Monday))
                    && m == Month.March)
                // Freedom Day, April 27th (possibly moved to Monday)
                || ((d == 27 || (d == 28 && w == Weekday.Monday))
                    && m == Month.April)
                // Workers Day, May 1st (possibly moved to Monday)
                || ((d == 1 || (d == 2 && w == Weekday.Monday))
                    && m == Month.May)
                // Youth Day, June 16th (possibly moved to Monday)
                || ((d == 16 || (d == 17 && w == Weekday.Monday))
                    && m == Month.June)
                // National Women's Day, August 9th (possibly moved to Monday)
                || ((d == 9 || (d == 10 && w == Weekday.Monday))
                    && m == Month.August)
                // Heritage Day, September 24th (possibly moved to Monday)
                || ((d == 24 || (d == 25 && w == Weekday.Monday))
                    && m == Month.September)
                // Day of Reconciliation, December 16th
                // (possibly moved to Monday)
                || ((d == 16 || (d == 17 && w == Weekday.Monday))
                    && m == Month.December)
                // Christmas
                || (d == 25 && m == Month.December)
                // Day of Goodwill (possibly moved to Monday)
                || ((d == 26 || (d == 27 && w == Weekday.Monday))
                    && m == Month.December)
                // Election Days
                || (d == 14 && m == Month.April && y == 2004)
                || (d == 22 && m == Month.April && y == 2009)
                || (d == 3 && m == Month.August && y == 2016)
                || (d == 1 && m == Month.November && y == 2021)
                || (d == 29 && m == Month.May && y == 2024)
                || (d == 4 && m == Month.November && y == 2026)
                // one-shot: In lieu of Christmas falling on Sunday in 2022
                || (d == 27 && m == Month.December && y == 2022)
                // one-shot: Special holiday to celebrate winning of Rugby World Cup 2023
                || (d == 15 && m == Month.December && y == 2023)
                )
            {
                return false;
            }

            return true;
        }
    }
}
