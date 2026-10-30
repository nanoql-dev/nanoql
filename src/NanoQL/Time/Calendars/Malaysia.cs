// SPDX-FileCopyrightText: Copyright (C) 2026 Lawrenz Law
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/malaysia.hpp ql/time/calendars/malaysia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Time;

/// <summary>The Malaysian calendar.</summary>
/// <remarks>
/// <para>
/// Holidays for the Kuala Lumpur stock exchange (Bursa Malaysia).
/// </para>
/// <para>
/// Bursa Malaysia observes the federal public holidays gazetted for the Federal Territory of Kuala Lumpur, so
/// state-only holidays (for example Good Friday in Sabah and Sarawak, or the Harvest Festival in Sabah and Labuan) are
/// deliberately not included.
/// </para>
/// <para>
/// Holidays with a fixed date:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Federal Territory Day, February 1st</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>National Day (Hari Merdeka), August 31st</description></item>
/// <item><description>Malaysia Day, September 16th (a federal holiday since 2010)</description></item>
/// <item><description>Christmas Day, December 25th</description></item>
/// </list>
/// </para>
/// <para>
/// Holidays with no fixed rule, tabulated for 2015 to 2027:
/// <list type="bullet">
/// <item><description>Chinese New Year (two days)</description></item>
/// <item><description>Thaipusam</description></item>
/// <item><description>Wesak Day</description></item>
/// <item><description>Deepavali</description></item>
/// <item><description>Nuzul Al-Quran</description></item>
/// <item><description>Hari Raya Aidilfitri (two days)</description></item>
/// <item><description>Hari Raya Haji</description></item>
/// <item><description>Awal Muharram</description></item>
/// <item><description>Maulidur Rasul</description></item>
/// <item><description>Birthday of the Yang di-Pertuan Agong</description></item>
/// <item><description>Substitute days granted when a gazetted holiday falls on a Sunday</description></item>
/// <item><description>One-off federal holidays (royal installations, the 2018 general election, the 2017 SEA
/// Games)</description></item>
/// </list>
/// </para>
/// <para>
/// The Islamic, Chinese and Hindu festivals are set each year by proclamation rather than by calculation, so they are
/// tabulated from the published holiday lists rather than computed. Outside the tabulated range only the fixed-date
/// holidays above are returned.
/// </para>
/// <para>
/// Holiday dates compiled from the gazetted Malaysian public holiday lists published at
/// https://www.officeholidays.com/countries/malaysia, cross-checked for 2021-2026 against Google's public Malaysia
/// holiday calendar. They should be verified against the trading calendar published by Bursa Malaysia at
/// https://www.bursamalaysia.com/ before being relied upon.
/// </para>
/// </remarks>
public sealed class Malaysia : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl KlseImplInstance = new KlseImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Malaysia"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Malaysia(MalaysiaMarket market = MalaysiaMarket.KLSE)
    {
        switch (market)
        {
            case MalaysiaMarket.KLSE:
                impl = KlseImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }

        ApplyEdits(ConventionProvider.ReadCalendarEdits($"Malaysia.{market}"));
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

    private sealed class KlseImpl : Impl
    {
        public override string Name => "Kuala Lumpur stock exchange";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth;
            Month m = date.Month;
            int y = date.Year;

            if (WesternImpl.IsWeekend(w)
                // New Year's Day
                || (d == 1 && m == Month.January)
                // Federal Territory Day
                || (d == 1 && m == Month.February)
                // Labour Day
                || (d == 1 && m == Month.May)
                // National Day
                || (d == 31 && m == Month.August)
                // Malaysia Day (a federal holiday since 2010)
                || (d == 16 && m == Month.September && y >= 2010)
                // Christmas Day
                || (d == 25 && m == Month.December))
            {
                return false;
            }

            return !MalaysiaHelpers.IsTabulatedHoliday(date);
        }
    }
}

// QuantLib's anonymous-namespace helpers in malaysia.cpp.
file static class MalaysiaHelpers
{
    private static readonly ImmutableHashSet<Date> Holidays =
    [
        // 2015
        new(3, Month.January, 2015), // Maulidur Rasul
        new(2, Month.February, 2015), // Federal Territory Day
        new(3, Month.February, 2015), // Thaipusam
        new(19, Month.February, 2015), // Chinese New Year
        new(20, Month.February, 2015), // Chinese New Year
        new(4, Month.May, 2015), // Wesak Day
        new(6, Month.June, 2015), // Agong's Birthday
        new(4, Month.July, 2015), // Nuzul Al-Quran
        new(17, Month.July, 2015), // Hari Raya Aidilfitri
        new(18, Month.July, 2015), // Hari Raya Aidilfitri
        new(24, Month.September, 2015), // Hari Raya Haji
        new(14, Month.October, 2015), // Awal Muharram
        new(10, Month.November, 2015), // Deepavali
        new(24, Month.December, 2015), // Maulidur Rasul
        // 2016
        new(24, Month.January, 2016), // Thaipusam
        new(25, Month.January, 2016), // Thaipusam
        new(8, Month.February, 2016), // Chinese New Year
        new(9, Month.February, 2016), // Chinese New Year
        new(2, Month.May, 2016), // Labour Day
        new(21, Month.May, 2016), // Wesak Day
        new(4, Month.June, 2016), // Agong's Birthday
        new(22, Month.June, 2016), // Nuzul Al-Quran
        new(6, Month.July, 2016), // Hari Raya Aidilfitri
        new(7, Month.July, 2016), // Hari Raya Aidilfitri
        new(12, Month.September, 2016), // Hari Raya Haji
        new(2, Month.October, 2016), // Awal Muharram
        new(3, Month.October, 2016), // Awal Muharram
        new(29, Month.October, 2016), // Deepavali
        new(12, Month.December, 2016), // Maulidur Rasul
        new(26, Month.December, 2016), // Christmas Day (in lieu)
        // 2017
        new(2, Month.January, 2017), // New Year's Day (in lieu)
        new(28, Month.January, 2017), // Chinese New Year
        new(29, Month.January, 2017), // Chinese New Year
        new(30, Month.January, 2017), // Chinese New Year
        new(9, Month.February, 2017), // Thaipusam
        new(11, Month.February, 2017), // Thaipusam
        new(24, Month.April, 2017), // Installation of the 15th Yang di-Pertuan Agong
        new(10, Month.May, 2017), // Wesak Day
        new(3, Month.June, 2017), // Agong's Birthday
        new(11, Month.June, 2017), // Nuzul Al-Quran
        new(12, Month.June, 2017), // Nuzul Al-Quran
        new(25, Month.June, 2017), // Hari Raya Aidilfitri
        new(26, Month.June, 2017), // Hari Raya Aidilfitri
        new(27, Month.June, 2017), // Hari Raya Aidilfitri
        new(1, Month.September, 2017), // Hari Raya Haji
        new(4, Month.September, 2017), // Special holiday, 29th SEA Games
        new(22, Month.September, 2017), // Awal Muharram
        new(18, Month.October, 2017), // Deepavali
        new(1, Month.December, 2017), // Maulidur Rasul
        // 2018
        new(31, Month.January, 2018), // Thaipusam
        new(16, Month.February, 2018), // Chinese New Year
        new(17, Month.February, 2018), // Chinese New Year
        new(9, Month.May, 2018), // GE14 polling day
        new(10, Month.May, 2018), // Special Holiday
        new(11, Month.May, 2018), // Special Holiday
        new(29, Month.May, 2018), // Wesak Day
        new(2, Month.June, 2018), // Nuzul Al-Quran
        new(15, Month.June, 2018), // Hari Raya Aidilfitri
        new(16, Month.June, 2018), // Hari Raya Aidilfitri
        new(22, Month.August, 2018), // Hari Raya Haji
        new(10, Month.September, 2018), // Agong's Birthday
        new(11, Month.September, 2018), // Awal Muharram
        new(17, Month.September, 2018), // Malaysia Day (in lieu)
        new(6, Month.November, 2018), // Deepavali
        new(20, Month.November, 2018), // Maulidur Rasul
        // 2019
        new(21, Month.January, 2019), // Thaipusam
        new(5, Month.February, 2019), // Chinese New Year
        new(6, Month.February, 2019), // Chinese New Year
        new(20, Month.May, 2019), // Wesak Day
        new(22, Month.May, 2019), // Nuzul Al-Quran
        new(5, Month.June, 2019), // Hari Raya Aidilfitri
        new(6, Month.June, 2019), // Hari Raya Aidilfitri (in lieu)
        new(30, Month.July, 2019), // Installation of the 16th Yang di-Pertuan Agong
        new(11, Month.August, 2019), // Hari Raya Haji
        new(12, Month.August, 2019), // Hari Raya Haji (in lieu)
        new(1, Month.September, 2019), // Awal Muharram
        new(2, Month.September, 2019), // Awal Muharram (in lieu)
        new(9, Month.September, 2019), // Agong's Birthday
        new(27, Month.October, 2019), // Deepavali
        new(28, Month.October, 2019), // Deepavali (in lieu)
        new(9, Month.November, 2019), // Maulidur Rasul
        // 2020
        new(25, Month.January, 2020), // Chinese New Year
        new(26, Month.January, 2020), // Chinese New Year
        new(27, Month.January, 2020), // Chinese New Year
        new(9, Month.February, 2020), // Thaipusam
        new(7, Month.May, 2020), // Wesak Day
        new(10, Month.May, 2020), // Nuzul Al-Quran
        new(11, Month.May, 2020), // Nuzul Al-Quran (in lieu)
        new(24, Month.May, 2020), // Hari Raya Aidilfitri
        new(25, Month.May, 2020), // Hari Raya Aidilfitri (in lieu)
        new(26, Month.May, 2020), // Hari Raya Aidilfitri (in lieu)
        new(8, Month.June, 2020), // Agong's Birthday
        new(31, Month.July, 2020), // Hari Raya Haji
        new(20, Month.August, 2020), // Awal Muharram
        new(29, Month.October, 2020), // Maulidur Rasul
        new(14, Month.November, 2020), // Deepavali
        // 2021
        new(28, Month.January, 2021), // Thaipusam
        new(12, Month.February, 2021), // Chinese New Year
        new(13, Month.February, 2021), // Chinese New Year
        new(29, Month.April, 2021), // Nuzul Al-Quran
        new(13, Month.May, 2021), // Hari Raya Aidilfitri
        new(14, Month.May, 2021), // Hari Raya Aidilfitri (in lieu)
        new(26, Month.May, 2021), // Wesak Day
        new(7, Month.June, 2021), // Agong's Birthday
        new(20, Month.July, 2021), // Hari Raya Haji
        new(10, Month.August, 2021), // Awal Muharram
        new(19, Month.October, 2021), // Maulidur Rasul
        new(4, Month.November, 2021), // Deepavali
        new(3, Month.December, 2021), // Special Holiday (Federal Territories)
        // 2022
        new(18, Month.January, 2022), // Thaipusam
        new(1, Month.February, 2022), // Chinese New Year
        new(2, Month.February, 2022), // Chinese New Year
        new(3, Month.February, 2022), // Federal Territory Day (in lieu)
        new(19, Month.April, 2022), // Nuzul Al-Quran
        new(2, Month.May, 2022), // Hari Raya Aidilfitri
        new(3, Month.May, 2022), // Hari Raya Aidilfitri (in lieu)
        new(4, Month.May, 2022), // Labour Day (in lieu)
        new(15, Month.May, 2022), // Wesak Day
        new(16, Month.May, 2022), // Wesak Day (in lieu)
        new(6, Month.June, 2022), // Agong's Birthday
        new(10, Month.July, 2022), // Hari Raya Haji
        new(11, Month.July, 2022), // Hari Raya Haji (in lieu)
        new(30, Month.July, 2022), // Awal Muharram
        new(9, Month.October, 2022), // Maulidur Rasul
        new(10, Month.October, 2022), // Maulidur Rasul (in lieu)
        new(24, Month.October, 2022), // Deepavali
        new(18, Month.November, 2022), // Special days
        new(28, Month.November, 2022), // Special Bank Holiday
        new(26, Month.December, 2022), // Christmas Day (in lieu)
        // 2023
        new(2, Month.January, 2023), // New Year's Day (in lieu)
        new(22, Month.January, 2023), // Chinese New Year
        new(23, Month.January, 2023), // Chinese New Year
        new(24, Month.January, 2023), // Chinese New Year
        new(5, Month.February, 2023), // Thaipusam
        new(6, Month.February, 2023), // Thaipusam (in lieu)
        new(8, Month.April, 2023), // Nuzul Al-Quran
        new(21, Month.April, 2023), // Hari Raya Aidilfitri (in lieu)
        new(22, Month.April, 2023), // Hari Raya Aidilfitri
        new(23, Month.April, 2023), // Hari Raya Aidilfitri (in lieu)
        new(24, Month.April, 2023), // Hari Raya Aidilfitri (in lieu)
        new(4, Month.May, 2023), // Wesak Day
        new(5, Month.June, 2023), // Agong's Birthday
        new(29, Month.June, 2023), // Hari Raya Haji
        new(19, Month.July, 2023), // Awal Muharram
        new(28, Month.September, 2023), // Maulidur Rasul
        new(12, Month.November, 2023), // Deepavali
        new(13, Month.November, 2023), // Deepavali (in lieu)
        // 2024
        new(25, Month.January, 2024), // Thaipusam
        new(10, Month.February, 2024), // Chinese New Year
        new(11, Month.February, 2024), // Chinese New Year
        new(12, Month.February, 2024), // Chinese New Year
        new(28, Month.March, 2024), // Nuzul Al-Quran
        new(10, Month.April, 2024), // Hari Raya Aidilfitri
        new(11, Month.April, 2024), // Hari Raya Aidilfitri
        new(22, Month.May, 2024), // Wesak Day
        new(3, Month.June, 2024), // Agong's Birthday
        new(17, Month.June, 2024), // Hari Raya Haji
        new(7, Month.July, 2024), // Awal Muharram
        new(8, Month.July, 2024), // Awal Muharram
        new(16, Month.September, 2024), // Maulidur Rasul
        new(31, Month.October, 2024), // Deepavali
        // 2025
        new(29, Month.January, 2025), // Chinese New Year
        new(30, Month.January, 2025), // Chinese New Year
        new(11, Month.February, 2025), // Thaipusam
        new(18, Month.March, 2025), // Nuzul Al-Quran
        new(30, Month.March, 2025), // Hari Raya Aidilfitri (in lieu)
        new(31, Month.March, 2025), // Hari Raya Aidilfitri
        new(1, Month.April, 2025), // Hari Raya Aidilfitri (in lieu)
        new(12, Month.May, 2025), // Wesak Day
        new(2, Month.June, 2025), // Agong's Birthday
        new(7, Month.June, 2025), // Hari Raya Haji
        new(27, Month.June, 2025), // Awal Muharram
        new(1, Month.September, 2025), // National Day (in lieu)
        new(5, Month.September, 2025), // Maulidur Rasul
        new(15, Month.September, 2025), // Malaysia Day (in lieu)
        new(20, Month.October, 2025), // Deepavali
        // 2026
        new(1, Month.February, 2026), // Thaipusam
        new(2, Month.February, 2026), // Thaipusam (in lieu)
        new(17, Month.February, 2026), // Chinese New Year
        new(18, Month.February, 2026), // Chinese New Year
        new(7, Month.March, 2026), // Nuzul Al-Quran
        new(20, Month.March, 2026), // Hari Raya Aidilfitri (in lieu)
        new(21, Month.March, 2026), // Hari Raya Aidilfitri
        new(22, Month.March, 2026), // Hari Raya Aidilfitri (in lieu)
        new(23, Month.March, 2026), // Hari Raya Aidilfitri (in lieu)
        new(27, Month.May, 2026), // Hari Raya Haji
        new(31, Month.May, 2026), // Wesak Day
        new(1, Month.June, 2026), // Agong's Birthday
        new(17, Month.June, 2026), // Awal Muharram
        new(25, Month.August, 2026), // Maulidur Rasul
        new(8, Month.November, 2026), // Deepavali
        new(9, Month.November, 2026), // Deepavali (in lieu)
        // 2027
        new(22, Month.January, 2027), // Thaipusam
        new(6, Month.February, 2027), // Chinese New Year
        new(7, Month.February, 2027), // Chinese New Year
        new(24, Month.February, 2027), // Nuzul Al-Quran
        new(10, Month.March, 2027), // Hari Raya Aidilfitri
        new(11, Month.March, 2027), // Hari Raya Aidilfitri (in lieu)
        new(17, Month.May, 2027), // Hari Raya Haji (in lieu)
        new(20, Month.May, 2027), // Wesak Day
        new(5, Month.June, 2027), // Agong's Birthday
        new(6, Month.June, 2027), // Awal Muharram
        new(15, Month.August, 2027), // Maulidur Rasul
        new(30, Month.October, 2027), // Deepavali
    ];

    // Holidays that cannot be derived from a rule: the lunar (Chinese and
    // Hindu) and Islamic festivals, the substitute days granted when a
    // gazetted holiday falls on a Sunday, and one-off federal closures.
    public static bool IsTabulatedHoliday(Date date) => Holidays.Contains(date);
}
