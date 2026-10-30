// SPDX-FileCopyrightText: Copyright (C) 2026 Lawrenz Law
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/philippines.hpp ql/time/calendars/philippines.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Time;

/// <summary>The Philippine calendar.</summary>
/// <remarks>
/// <para>
/// Holidays for the Philippine Stock Exchange.
/// </para>
/// <para>
/// The exchange is closed on both classes of holiday defined by the Labor Code, the regular holidays and the special
/// (non-working) days, which are also the days on which banks supervised by the Bangko Sentral ng Pilipinas are closed.
/// The list additionally contains a few dates on which the exchange alone suspended trading, so it is not a banking
/// calendar.
/// </para>
/// <para>
/// Holidays with a fixed date or a computable rule:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Maundy Thursday</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Araw ng Kagitingan (Day of Valor), April 9th</description></item>
/// <item><description>Labor Day, May 1st</description></item>
/// <item><description>Independence Day, June 12th</description></item>
/// <item><description>Ninoy Aquino Day, August 21st (a special non-working day since RA 9256 of
/// 2004)</description></item>
/// <item><description>National Heroes Day, the last Monday of August</description></item>
/// <item><description>All Saints' Day, November 1st</description></item>
/// <item><description>Bonifacio Day, November 30th</description></item>
/// <item><description>Feast of the Immaculate Conception, December 8th (a special non-working day since RA 10966 of
/// 2017)</description></item>
/// <item><description>Christmas Day, December 25th</description></item>
/// <item><description>Rizal Day, December 30th</description></item>
/// <item><description>Last Day of the Year, December 31st</description></item>
/// </list>
/// </para>
/// <para>
/// Black Saturday is proclaimed a special (non-working) day every year, but by construction it always falls on a
/// Saturday and so is already covered by the weekend rule.
/// </para>
/// <para>
/// Holidays with no fixed rule, tabulated for 2020 to 2027:
/// <list type="bullet">
/// <item><description>Eid'l Fitr</description></item>
/// <item><description>Eid'l Adha</description></item>
/// <item><description>Chinese New Year (a special non-working day since 2012)</description></item>
/// <item><description>All Souls' Day and Christmas Eve, which are proclaimed in most but not all
/// years</description></item>
/// <item><description>EDSA People Power Revolution Anniversary, a special non-working day up to 2023 and a special
/// working day from 2025</description></item>
/// <item><description>Election days</description></item>
/// <item><description>One-off additional special non-working days, and the two days on which the exchange was closed by
/// the COVID-19 trading suspension of March 2020</description></item>
/// </list>
/// </para>
/// <para>
/// A handful of the rule-based holidays above were transferred to another date by proclamation (Bonifacio Day in 2023,
/// Ninoy Aquino Day in 2024) or downgraded to a special working day (the Last Day of the Year in 2021); those years are
/// handled as explicit exceptions.
/// </para>
/// <para>
/// The Islamic festivals, Chinese New Year and the additional special days are set each year by presidential
/// proclamation rather than by rule, so they are tabulated from the published proclamations rather than computed.
/// Outside the tabulated range only the rule-based holidays above are returned. The 2027 proclamation had not been
/// issued when this calendar was written, so the 2027 entries cover only the festivals whose dates can be predicted
/// (Chinese New Year and the two Islamic festivals, the latter still subject to moon sighting); any additional special
/// (non-working) day declared for that year is missing. The rules also predate the "holiday economics" transfers of RA
/// 9492, so years before 2011 are not reliable.
/// </para>
/// <para>
/// Holiday dates compiled from the presidential proclamations published by the Official Gazette and the Presidential
/// Communications Office (Proclamations 845 s.2019, 986 and 1107 s.2021, 1236 s.2021, 1357 s.2022, 42 and 90 s.2022,
/// 167 and 425 s.2023, 368 and 665 s.2024, 727 s.2024 and 1006 s.2025), cross-checked against
/// https://www.officeholidays.com/countries/philippines. They should be verified against the trading calendar published
/// by the Philippine Stock Exchange at https://www.pse.com.ph/ before being relied upon.
/// </para>
/// </remarks>
public sealed class Philippines : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl PseImplInstance = new PseImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Philippines"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Philippines(PhilippinesMarket market = PhilippinesMarket.PSE)
    {
        switch (market)
        {
            case PhilippinesMarket.PSE:
                impl = PseImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }

        ApplyEdits(ConventionProvider.ReadCalendarEdits($"Philippines.{market}"));
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

    private sealed class PseImpl : Impl
    {
        public override string Name => "Philippine stock exchange";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);

            if (WesternImpl.IsWeekend(w))
            {
                return false;
            }

            if (PhilippinesHelpers.IsWithdrawnPhilippineHoliday(date))
            {
                return true;
            }

            if ( // New Year's Day
                (d == 1 && m == Month.January)
                // Maundy Thursday
                || (dd == em - 4)
                // Good Friday
                || (dd == em - 3)
                // Araw ng Kagitingan (Day of Valor)
                || (d == 9 && m == Month.April)
                // Labor Day
                || (d == 1 && m == Month.May)
                // Independence Day
                || (d == 12 && m == Month.June)
                // Ninoy Aquino Day (a special non-working day since RA 9256)
                || (d == 21 && m == Month.August && y >= 2004)
                // National Heroes Day (the last Monday of August)
                || (d >= 25 && m == Month.August && w == Weekday.Monday)
                // All Saints' Day
                || (d == 1 && m == Month.November)
                // Bonifacio Day
                || (d == 30 && m == Month.November)
                // Feast of the Immaculate Conception (since RA 10966)
                || (d == 8 && m == Month.December && y >= 2018)
                // Christmas Day
                || (d == 25 && m == Month.December)
                // Rizal Day
                || (d == 30 && m == Month.December)
                // Last Day of the Year
                || (d == 31 && m == Month.December))
            {
                return false;
            }

            return !PhilippinesHelpers.IsTabulatedPhilippineHoliday(date);
        }
    }
}

// QuantLib's anonymous-namespace helpers in philippines.cpp.
file static class PhilippinesHelpers
{
    private static readonly ImmutableHashSet<Date> Holidays =
    [
        // 2020
        new(25, Month.January, 2020), // Chinese New Year
        new(25, Month.February, 2020), // EDSA People Power Revolution Anniversary
        new(17, Month.March, 2020), // COVID-19 trading suspension
        new(18, Month.March, 2020), // Market Holiday
        new(25, Month.May, 2020), // Eid'l Fitr
        new(31, Month.July, 2020), // Eid'l Adha
        new(2, Month.November, 2020), // All Souls' Day
        new(12, Month.November, 2020), // No Trading
        new(24, Month.December, 2020), // Christmas Eve
        // 2021
        new(12, Month.February, 2021), // Chinese New Year
        new(25, Month.February, 2021), // EDSA People Power Revolution Anniversary
        new(13, Month.May, 2021), // Eid'l Fitr
        new(20, Month.July, 2021), // Eid'l Adha
        // 2022
        new(1, Month.February, 2022), // Chinese New Year
        new(25, Month.February, 2022), // EDSA People Power Revolution Anniversary
        new(3, Month.May, 2022), // Eid'l Fitr
        new(9, Month.May, 2022), // National and local elections
        new(10, Month.July, 2022), // Eid'l Adha
        new(26, Month.September, 2022), // No Trading
        new(31, Month.October, 2022), // Additional special (non-working) day
        new(26, Month.December, 2022), // Additional special (non-working) day
        // 2023
        new(2, Month.January, 2023), // Additional special (non-working) day
        new(24, Month.February, 2023), // EDSA anniversary, in lieu of February 25th
        new(10, Month.April, 2023), // Araw ng Kagitingan, in lieu of April 9th
        new(21, Month.April, 2023), // Eid'l Fitr
        new(28, Month.June, 2023), // Eid'l Adha
        new(30, Month.October, 2023), // Barangay and Sangguniang Kabataan elections
        new(2, Month.November, 2023), // All Souls' Day
        new(27, Month.November, 2023), // Bonifacio Day, in lieu of November 30th
        new(26, Month.December, 2023), // Additional special (non-working) day
        // 2024
        new(9, Month.February, 2024), // Additional special (non-working) day
        new(10, Month.February, 2024), // Chinese New Year
        new(10, Month.April, 2024), // Eid'l Fitr
        new(17, Month.June, 2024), // Eid'l Adha
        new(24, Month.July, 2024), // Closed - Typhoon
        new(23, Month.August, 2024), // Ninoy Aquino Day, in lieu of August 21st
        new(2, Month.November, 2024), // All Souls' Day
        new(24, Month.December, 2024), // Christmas Eve
        // 2025
        new(29, Month.January, 2025), // Chinese New Year
        new(1, Month.April, 2025), // Eid'l Fitr
        new(12, Month.May, 2025), // National and local elections
        new(6, Month.June, 2025), // Eid'l Adha
        new(27, Month.July, 2025), // Founding anniversary of the Iglesia ni Cristo
        new(31, Month.October, 2025), // Additional special (non-working) day
        new(24, Month.December, 2025), // Christmas Eve
        // 2026
        new(17, Month.February, 2026), // Chinese New Year
        new(20, Month.March, 2026), // Eid'l Fitr
        new(27, Month.May, 2026), // Eid'l Adha
        new(2, Month.November, 2026), // All Souls' Day
        new(24, Month.December, 2026), // Christmas Eve
        // 2027
        new(6, Month.February, 2027), // Chinese New Year
        new(10, Month.March, 2027), // Eid'l Fitr
        new(17, Month.May, 2027), // Eid'l Adha
    ];

    private static readonly ImmutableHashSet<Date> Withdrawn =
    [
        new(31, Month.December, 2021), // declared a special working day
        new(30, Month.November, 2023), // Bonifacio Day moved to November 27th
        new(21, Month.August, 2024), // Ninoy Aquino Day moved to August 23rd
    ];

    // Holidays that cannot be derived from a rule: the Islamic and
    // Chinese festivals, the additional special (non-working) days
    // proclaimed in a given year only, election days, and the days on
    // which the exchange itself was closed.
    public static bool IsTabulatedPhilippineHoliday(Date date) => Holidays.Contains(date);

    // Rule-based holidays that a proclamation moved to another date or
    // turned into a special working day for one year only.
    public static bool IsWithdrawnPhilippineHoliday(Date date) => Withdrawn.Contains(date);
}
