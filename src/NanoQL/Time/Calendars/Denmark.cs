// SPDX-FileCopyrightText: Copyright (C) 2003 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2022 Skandinaviska Enskilda Banken AB (publ)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/denmark.hpp ql/time/calendars/denmark.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Danish calendar.</summary>
/// <remarks>
/// <para>
/// Holidays:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>Maunday Thursday</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>General Prayer Day, 25 days after Easter Monday (up until 2023)</description></item>
/// <item><description>Ascension</description></item>
/// <item><description>Day after Ascension (from 2009)</description></item>
/// <item><description>Whit (Pentecost) Monday</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Constitution Day, June 5th</description></item>
/// <item><description>Christmas Eve, December 24th</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>Boxing Day, December 26th</description></item>
/// <item><description>New Year's Eve, December 31st</description></item>
/// </list>
/// </para>
/// <para>
/// See: https://www.nasdaqomxnordic.com/tradinghours, and:
/// https://www.nationalbanken.dk/da/Kontakt/aabningstider/Sider/default.aspx
/// </para>
/// </remarks>
public sealed class Denmark : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl DenmarkImplInstance = new DenmarkImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Denmark"/> class.</summary>
    public Denmark()
    {
        impl = DenmarkImplInstance;
        ApplyEdits(ConventionProvider.ReadCalendarEdits("Denmark"));
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

    private sealed class DenmarkImpl : Impl
    {
        public override string Name => "Denmark";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);
            if (WesternImpl.IsWeekend(w)
                // Maunday Thursday
                || (dd == em - 4)
                // Good Friday
                || (dd == em - 3)
                // Easter Monday
                || (dd == em)
                // General Prayer Day
                || (dd == em + 25 && y <= 2023)
                // Ascension
                || (dd == em + 38)
                // Day after Ascension
                || (dd == em + 39 && y >= 2009)
                // Whit Monday
                || (dd == em + 49)
                // New Year's Day
                || (d == 1 && m == Month.January)
                // Constitution Day, June 5th
                || (d == 5 && m == Month.June)
                // Christmas Eve
                || (d == 24 && m == Month.December)
                // Christmas
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
