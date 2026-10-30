// SPDX-FileCopyrightText: Copyright (C) 2004 FIMAT Group
// SPDX-FileCopyrightText: Copyright (C) 2007, 2011 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/saudiarabia.hpp ql/time/calendars/saudiarabia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Saudi Arabian calendar.</summary>
/// <remarks>
/// <para>
/// Holidays for the Tadawul financial market (data from https://www.saudiexchange.sa):
/// <list type="bullet">
/// <item><description>Thursdays and Fridays (until 28 June 2013)</description></item>
/// <item><description>Fridays and Saturdays (from 29 June 2013)</description></item>
/// <item><description>National Day of Saudi Arabia, September 23rd</description></item>
/// <item><description>Founding Day of Saudi Arabia, February 22nd (from 2022)</description></item>
/// </list>
/// </para>
/// <para>
/// Other holidays for which no rule is given (Eid Al-Adha and Eid Al-Fitr windows through 2022 use published Gregorian
/// dates, with exchange-documented ranges for 2012-2015; windows from 2023 through 2029 follow the Saudi Exchange
/// holiday calendar):
/// <list type="bullet">
/// <item><description>Eid Al-Adha</description></item>
/// <item><description>Eid Al-Fitr</description></item>
/// </list>
/// </para>
/// <para>
/// The Eid windows run from 1998 to 2029: after 2029 only the fixed dates and the weekend apply.
/// <see cref="Calendar.IsWeekend"/> answers today's weekend, Friday and Saturday, as QuantLib's does; the holiday list
/// without weekends leaves out each date's own weekend days, where QuantLib's holds every Thursday before July 2013.
/// </para>
/// </remarks>
public sealed class SaudiArabia : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl TadawulImplInstance = new TadawulImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="SaudiArabia"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public SaudiArabia(SaudiArabiaMarket market = SaudiArabiaMarket.Tadawul)
    {
        switch (market)
        {
            case SaudiArabiaMarket.Tadawul:
                impl = TadawulImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }

        ApplyEdits(ConventionProvider.ReadCalendarEdits($"SaudiArabia.{market}"));
    }

    /// <inheritdoc/>
    public override string Name => impl.Name;

    /// <inheritdoc/>
    public override bool IsWeekend(Weekday w) => impl.IsWeekend(w);

    /// <inheritdoc/>
    internal override bool IsWeekendOn(Date d) => impl.IsWeekendOn(d);

    /// <inheritdoc/>
    protected override bool IsBusinessDayCore(Date date) => impl.IsBusinessDay(date);

    // The market-specific rules; QuantLib's Calendar::Impl, with the date-aware weekend the holiday list reads.
    private abstract class Impl
    {
        public abstract string Name { get; }

        public abstract bool IsWeekend(Weekday w);

        public virtual bool IsWeekendOn(Date d) => IsWeekend(d.Weekday);

        public abstract bool IsBusinessDay(Date date);
    }

    private sealed class TadawulImpl : Impl
    {
        public override string Name => "Tadawul";

        public override bool IsWeekend(Weekday w)
        {
            return w == Weekday.Friday || w == Weekday.Saturday;
        }

        // the weekend on its own date: Thursday and Friday until June 28th, 2013, Friday and Saturday since
        public override bool IsWeekendOn(Date d) => SaudiArabiaHelpers.IsTrueWeekend(d);

        public override bool IsBusinessDay(Date date)
        {
            int d = date.DayOfMonth;
            Month m = date.Month;
            int y = date.Year;
            Weekday w = date.Weekday;

            if (SaudiArabiaHelpers.IsTrueWeekend(date)
                || SaudiArabiaHelpers.IsEidAlAdha(date)
                || SaudiArabiaHelpers.IsEidAlFitr(date)
                // National Day
                || (d == 23 && m == Month.September)
                // Founding Day (from 2022; rolls to the following Sunday when on Fri/Sat)
                || ((d == 22 || ((d == 23 || d == 24) && w == Weekday.Sunday))
                    && m == Month.February && y >= 2022)
                // other one-shot holidays
                || (d == 26 && m == Month.February && y == 2011)
                || (d == 19 && m == Month.March && y == 2011)
                )
            {
                return false;
            }

            return true;
        }
    }
}

// QuantLib's anonymous-namespace helpers in saudiarabia.cpp.
file static class SaudiArabiaHelpers
{
    // Tadawul Eid holiday windows documented below for 2012-2015; other
    // years through 2022 use Eid-1 to Eid+4 around published Gregorian
    // dates.  Windows from 2023 through 2029 follow the Saudi Exchange
    // holiday calendar at https://www.saudiexchange.sa
    //
    // Eid Date    Holiday     Offset    Remarks
    // 2015-09-23    22-27     -1,+4     Eid al-Adha
    // 2015-07-17    18-21     -1,+4     Eid al-Fitr
    // 2014-10-05    03-11     -1,+4     Eid al-Adha
    // 2014-07-28    25-03     -1,+4     Eid al-Fitr
    // 2013-10-15    11-19     -2,+4     Eid al-Adha
    // 2013-08-08    06-12     -2,+4     Eid al-Fitr
    // 2012-10-26    25-03     -1,+5     Eid al-Adha
    // 2012-08-19    16-24     -1,+4     Eid al-Fitr

    private static readonly (Date First, Date Last)[] EidAlAdhaWindows =
    [
        (new(6, Month.April, 1998), new(11, Month.April, 1998)),
        (new(26, Month.March, 1999), new(31, Month.March, 1999)),
        (new(15, Month.March, 2000), new(20, Month.March, 2000)),
        (new(4, Month.March, 2001), new(9, Month.March, 2001)),
        (new(22, Month.February, 2002), new(27, Month.February, 2002)),
        (new(11, Month.February, 2003), new(16, Month.February, 2003)),
        (new(31, Month.January, 2004), new(5, Month.February, 2004)),
        (new(20, Month.January, 2005), new(25, Month.January, 2005)),
        (new(9, Month.January, 2006), new(14, Month.January, 2006)),
        (new(30, Month.December, 2006), new(4, Month.January, 2007)),
        (new(19, Month.December, 2007), new(24, Month.December, 2007)),
        (new(7, Month.December, 2008), new(12, Month.December, 2008)),
        (new(26, Month.November, 2009), new(1, Month.December, 2009)),
        (new(15, Month.November, 2010), new(20, Month.November, 2010)),
        (new(5, Month.November, 2011), new(10, Month.November, 2011)),
        (new(25, Month.October, 2012), new(3, Month.November, 2012)),
        (new(11, Month.October, 2013), new(19, Month.October, 2013)),
        (new(3, Month.October, 2014), new(11, Month.October, 2014)),
        (new(22, Month.September, 2015), new(27, Month.September, 2015)),
        (new(10, Month.September, 2016), new(15, Month.September, 2016)),
        (new(31, Month.August, 2017), new(5, Month.September, 2017)),
        (new(22, Month.August, 2018), new(27, Month.August, 2018)),
        (new(11, Month.August, 2019), new(16, Month.August, 2019)),
        (new(30, Month.July, 2020), new(4, Month.August, 2020)),
        (new(19, Month.July, 2021), new(24, Month.July, 2021)),
        (new(9, Month.July, 2022), new(14, Month.July, 2022)),
        (new(23, Month.June, 2023), new(1, Month.July, 2023)),
        (new(14, Month.June, 2024), new(22, Month.June, 2024)),
        (new(5, Month.June, 2025), new(10, Month.June, 2025)),
        (new(22, Month.May, 2026), new(30, Month.May, 2026)),
        (new(16, Month.May, 2027), new(20, Month.May, 2027)),
        (new(3, Month.May, 2028), new(9, Month.May, 2028)),
        (new(22, Month.April, 2029), new(26, Month.April, 2029)),
    ];

    private static readonly (Date First, Date Last)[] EidAlFitrWindows =
    [
        (new(15, Month.December, 2001), new(20, Month.December, 2001)),
        (new(4, Month.December, 2002), new(9, Month.December, 2002)),
        (new(24, Month.November, 2003), new(29, Month.November, 2003)),
        (new(12, Month.November, 2004), new(17, Month.November, 2004)),
        (new(2, Month.November, 2005), new(7, Month.November, 2005)),
        (new(22, Month.October, 2006), new(27, Month.October, 2006)),
        (new(11, Month.October, 2007), new(16, Month.October, 2007)),
        (new(29, Month.September, 2008), new(4, Month.October, 2008)),
        (new(19, Month.September, 2009), new(24, Month.September, 2009)),
        (new(9, Month.September, 2010), new(14, Month.September, 2010)),
        (new(29, Month.August, 2011), new(3, Month.September, 2011)),
        (new(16, Month.August, 2012), new(24, Month.August, 2012)),
        (new(6, Month.August, 2013), new(12, Month.August, 2013)),
        (new(25, Month.July, 2014), new(3, Month.August, 2014)),
        (new(18, Month.July, 2015), new(21, Month.July, 2015)),
        (new(5, Month.July, 2016), new(10, Month.July, 2016)),
        (new(24, Month.June, 2017), new(29, Month.June, 2017)),
        (new(14, Month.June, 2018), new(19, Month.June, 2018)),
        (new(3, Month.June, 2019), new(8, Month.June, 2019)),
        (new(23, Month.May, 2020), new(28, Month.May, 2020)),
        (new(12, Month.May, 2021), new(17, Month.May, 2021)),
        (new(1, Month.May, 2022), new(6, Month.May, 2022)),
        (new(17, Month.April, 2023), new(25, Month.April, 2023)),
        (new(4, Month.April, 2024), new(14, Month.April, 2024)),
        (new(27, Month.March, 2025), new(2, Month.April, 2025)),
        (new(17, Month.March, 2026), new(23, Month.March, 2026)),
        (new(7, Month.March, 2027), new(11, Month.March, 2027)),
        (new(27, Month.February, 2028), new(2, Month.March, 2028)),
        (new(12, Month.February, 2029), new(18, Month.February, 2029)),
    ];

    public static bool IsTrueWeekend(Date d)
    {
        // The Saudi weekend was changed from 29th June 2013
        Weekday w = d.Weekday;
        return (d < new Date(29, Month.June, 2013))
            ? (w == Weekday.Thursday || w == Weekday.Friday)
            : (w == Weekday.Friday || w == Weekday.Saturday);
    }

    public static bool IsEidAlAdha(Date d) => Array.Exists(EidAlAdhaWindows, p => d >= p.First && d <= p.Last);

    public static bool IsEidAlFitr(Date d) => Array.Exists(EidAlFitrWindows, p => d >= p.First && d <= p.Last);
}
