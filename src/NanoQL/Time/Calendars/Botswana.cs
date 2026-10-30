// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2017 Francois Botha
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/botswana.hpp ql/time/calendars/botswana.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Botswana calendar.</summary>
/// <remarks>
/// <para>
/// Holidays, from the Botswana Public Holidays Act
/// (http://www.ilo.org/dyn/travail/docs/1766/Public%20Holidays%20Act.pdf): the days named in the Schedule shall
/// be public holidays within Botswana, provided that
/// <list type="bullet">
/// <item><description>when any of the said days fall on a Sunday the following Monday shall be observed as a public
/// holiday;</description></item>
/// <item><description>if 2nd January, 1st October or Boxing Day falls on a Monday, the following Tuesday shall be
/// observed as a public holiday;</description></item>
/// <item><description>when Botswana Day referred to in the Schedule falls on a Saturday, the next following Monday
/// shall be observed as a public holiday.</description></item>
/// </list>
/// </para>
/// <para>
/// The days:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>Ascension</description></item>
/// <item><description>Sir Seretse Khama Day, July 1st</description></item>
/// <item><description>Presidents' Day</description></item>
/// <item><description>Independence Day, September 30th</description></item>
/// <item><description>Botswana Day, October 1st</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>Boxing Day, December 26th</description></item>
/// </list>
/// </para>
/// <para>
/// The Tuesday after a Monday October 1st or a Monday Boxing Day is a holiday, as the Act's proviso says;
/// QuantLib observes the proviso's Tuesday for January 2nd alone: a documented divergence. January 2nd itself,
/// which the Act's schedule holds, and the Tuesday after Presidents' Day, which the stock exchange observes, are
/// kept as QuantLib has them.
/// </para>
/// </remarks>
public sealed class Botswana : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl BotswanaImplInstance = new BotswanaImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Botswana"/> class.</summary>
    public Botswana()
    {
        impl = BotswanaImplInstance;
        ApplyEdits(ConventionProvider.ReadCalendarEdits("Botswana"));
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

    private sealed class BotswanaImpl : Impl
    {
        public override string Name => "Botswana";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);
            if (WesternImpl.IsWeekend(w)
                // New Year's Day (possibly moved to Monday or Tuesday)
                || ((d == 1 || (d == 2 && w == Weekday.Monday) || (d == 3 && w == Weekday.Tuesday))
                    && m == Month.January)
                // Good Friday
                || (dd == em - 3)
                // Easter Monday
                || (dd == em)
                // Labour Day, May 1st (possibly moved to Monday)
                || ((d == 1 || (d == 2 && w == Weekday.Monday))
                    && m == Month.May)
                // Ascension
                || (dd == em + 38)
                // Sir Seretse Khama Day, July 1st (possibly moved to Monday)
                || ((d == 1 || (d == 2 && w == Weekday.Monday))
                    && m == Month.July)
                // Presidents' Day (third Monday of July)
                || ((d >= 15 && d <= 21) && w == Weekday.Monday && m == Month.July)
                // Independence Day, September 30th (possibly moved to Monday)
                || ((d == 30 && m == Month.September) ||
                    (d == 1 && w == Weekday.Monday && m == Month.October))
                // Botswana Day, October 1st (possibly moved to Monday or Tuesday; the Tuesday after it on a Monday)
                || ((d == 1 || (d == 2 && w == Weekday.Monday) || (d == 3 && w == Weekday.Tuesday)
                    || (d == 2 && w == Weekday.Tuesday))
                    && m == Month.October)
                // Christmas
                || (d == 25 && m == Month.December)
                // Boxing Day (possibly moved to Monday; the Tuesday after it on a Monday)
                || ((d == 26 || (d == 27 && w == Weekday.Monday) || (d == 27 && w == Weekday.Tuesday))
                    && m == Month.December)
                )
            {
                return false;
            }

            return true;
        }
    }
}
