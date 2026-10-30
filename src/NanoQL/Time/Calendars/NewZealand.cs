// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/newzealand.hpp ql/time/calendars/newzealand.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The New Zealand calendars.</summary>
/// <remarks>
/// <para>
/// Common holidays:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st (possibly moved to Monday or Tuesday)</description></item>
/// <item><description>Day after New Year's Day, January 2st (possibly moved to Monday or Tuesday)</description></item>
/// <item><description>Waitangi Day. February 6th (possibly moved to Monday since 2013)</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>ANZAC Day. April 25th (possibly moved to Monday since 2013)</description></item>
/// <item><description>Queen's Birthday, first Monday in June</description></item>
/// <item><description>Labour Day, fourth Monday in October</description></item>
/// <item><description>Christmas, December 25th (possibly moved to Monday or Tuesday)</description></item>
/// <item><description>Boxing Day, December 26th (possibly moved to Monday or Tuesday)</description></item>
/// <item><description>Matariki, in June or July, official calendar released for years 2022-2052</description></item>
/// </list>
/// </para>
/// <para>
/// Additional holidays for Wellington: Anniversary Day, Monday nearest January 22nd. Additional holidays for Auckland:
/// Anniversary Day, Monday nearest January 29nd.
/// </para>
/// <para>
/// The holiday rules for New Zealand were documented by David Gilbert for IDB (http://www.jrefinery.com/ibd/). The
/// Matariki holiday calendar has been released by the NZ Government
/// (https://www.legislation.govt.nz/act/public/2022/0014/latest/LMS557893.html).
/// </para>
/// </remarks>
public sealed class NewZealand : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly CommonImpl WellingtonImplInstance = new WellingtonImpl();
    private static readonly CommonImpl AucklandImplInstance = new AucklandImpl();

    private readonly CommonImpl impl;

    /// <summary>Initializes a new instance of the <see cref="NewZealand"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows; Wellington by default.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public NewZealand(NewZealandMarket market = NewZealandMarket.Wellington)
    {
        impl = market switch
        {
            NewZealandMarket.Wellington => WellingtonImplInstance,
            NewZealandMarket.Auckland => AucklandImplInstance,
            _ => throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market"),
        };
        ApplyEdits(ConventionProvider.ReadCalendarEdits($"NewZealand.{market}"));
    }

    /// <inheritdoc/>
    public override string Name => impl.Name;

    /// <inheritdoc/>
    public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

    /// <inheritdoc/>
    protected override bool IsBusinessDayCore(Date date) => impl.IsBusinessDay(date);

    // QuantLib's CommonImpl, the holidays every market keeps, with Calendar::WesternImpl's weekend.
    private abstract class CommonImpl
    {
        public abstract string Name { get; }

        public virtual bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);
            if (WesternImpl.IsWeekend(w)
                // New Year's Day (possibly moved to Monday or Tuesday)
                || ((d == 1 || (d == 3 && (w == Weekday.Monday || w == Weekday.Tuesday))) && m == Month.January)
                // Day after New Year's Day (possibly moved to Mon or Tuesday)
                || ((d == 2 || (d == 4 && (w == Weekday.Monday || w == Weekday.Tuesday))) && m == Month.January)
                // Waitangi Day. February 6th (possibly moved to Monday since 2013)
                || (d == 6 && m == Month.February)
                || ((d == 7 || d == 8) && w == Weekday.Monday && m == Month.February && y > 2013)
                // Good Friday
                || (dd == em - 3)
                // Easter Monday
                || (dd == em)
                // ANZAC Day. April 25th (possibly moved to Monday since 2013)
                || (d == 25 && m == Month.April)
                || ((d == 26 || d == 27) && w == Weekday.Monday && m == Month.April && y > 2013)
                // Queen's Birthday, first Monday in June
                || (d <= 7 && w == Weekday.Monday && m == Month.June)
                // Labour Day, fourth Monday in October
                || ((d >= 22 && d <= 28) && w == Weekday.Monday && m == Month.October)
                // Christmas, December 25th (possibly Monday or Tuesday)
                || ((d == 25 || (d == 27 && (w == Weekday.Monday || w == Weekday.Tuesday))) && m == Month.December)
                // Boxing Day, December 26th (possibly Monday or Tuesday)
                || ((d == 26 || (d == 28 && (w == Weekday.Monday || w == Weekday.Tuesday))) && m == Month.December)
                // Matariki, it happens on Friday in June or July
                // official calendar released by the NZ government for the
                // next 30 years
                || (d == 20 && m == Month.June && y == 2025)
                || (d == 21 && m == Month.June && (y == 2030 || y == 2052))
                || (d == 24 && m == Month.June && (y == 2022 || y == 2033 || y == 2044))
                || (d == 25 && m == Month.June && (y == 2027 || y == 2038 || y == 2049))
                || (d == 28 && m == Month.June && y == 2024)
                || (d == 29 && m == Month.June && (y == 2035 || y == 2046))
                || (d == 30 && m == Month.June && y == 2051)
                || (d == 2 && m == Month.July && y == 2032)
                || (d == 3 && m == Month.July && (y == 2043 || y == 2048))
                || (d == 6 && m == Month.July && (y == 2029 || y == 2040))
                || (d == 7 && m == Month.July && (y == 2034 || y == 2045))
                || (d == 10 && m == Month.July && (y == 2026 || y == 2037))
                || (d == 11 && m == Month.July && (y == 2031 || y == 2042))
                || (d == 14 && m == Month.July && (y == 2023 || y == 2028))
                || (d == 15 && m == Month.July && (y == 2039 || y == 2050))
                || (d == 18 && m == Month.July && y == 2036)
                || (d == 19 && m == Month.July && (y == 2041 || y == 2047))
                // Queen Elizabeth's funeral
                || (d == 26 && m == Month.September && y == 2022))
            {
                return false;
            }

            return true;
        }
    }

    private sealed class WellingtonImpl : CommonImpl
    {
        public override string Name => "New Zealand (Wellington)";

        public override bool IsBusinessDay(Date date)
        {
            if (!base.IsBusinessDay(date))
            {
                return false;
            }

            Weekday w = date.Weekday;
            int d = date.DayOfMonth;
            Month m = date.Month;

            // Anniversary Day, Monday nearest January 22nd
            if ((d >= 19 && d <= 25) && w == Weekday.Monday && m == Month.January)
            {
                return false;
            }

            return true;
        }
    }

    private sealed class AucklandImpl : CommonImpl
    {
        public override string Name => "New Zealand (Auckland)";

        public override bool IsBusinessDay(Date date)
        {
            if (!base.IsBusinessDay(date))
            {
                return false;
            }

            Weekday w = date.Weekday;
            int d = date.DayOfMonth;
            Month m = date.Month;

            // Anniversary Day, Monday nearest January 29nd
            if ((d >= 26 && w == Weekday.Monday && m == Month.January)
                || (d == 1 && w == Weekday.Monday && m == Month.February))
            {
                return false;
            }

            return true;
        }
    }
}
