// SPDX-FileCopyrightText: Copyright (C) 2018 Matthias Groncki
// SPDX-FileCopyrightText: Copyright (C) 2023 Skandinaviska Enskilda Banken AB (publ)
// SPDX-FileCopyrightText: Copyright (C) 2023, 2024 Skandinaviska Enskilda Banken AB (publ)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/thailand.hpp ql/time/calendars/thailand.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Thai calendars.</summary>
/// <remarks>
/// <para>
/// Holidays for the Thailand exchange Holidays observed by financial institutions (not to be confused with bank
/// holidays in the United Kingdom) are regulated by the Bank of Thailand. If a holiday fall on a weekend the government
/// will announce a replacement day (usually the following Monday).
/// </para>
/// <para>
/// Sometimes the government add one or two extra holidays in a year.
/// </para>
/// <para>
/// (data from https://www.bot.or.th/en/financial-institutions-holiday.html: Fixed holidays
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>Chakri Memorial Day, April 6th</description></item>
/// <item><description>Songkran holiday, April 13th - 15th</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>H.M. the King's Birthday, July 28th (from 2017)</description></item>
/// <item><description>H.M. the Queen's Birthday, August 12th</description></item>
/// <item><description>The Passing of H.M. the Late King Bhumibol Adulyadej (Rama IX), October 13th (from
/// 2017)</description></item>
/// <item><description>H.M. the Late King Bhumibol Adulyadej's Birthday, December 5th</description></item>
/// <item><description>Constitution Day, December 10th</description></item>
/// <item><description>New Year's Eve, December 31st</description></item>
/// </list>
/// </para>
/// <para>
/// Other holidays for which no rule is given (data available for 2000-2025 with some years missing)
/// <list type="bullet">
/// <item><description>Makha Bucha Day</description></item>
/// <item><description>Wisakha Bucha Day</description></item>
/// <item><description>Buddhist Lent Day (until 2006)</description></item>
/// <item><description>Asarnha Bucha Day (from 2007)</description></item>
/// <item><description>Chulalongkorn Day</description></item>
/// <item><description>Other special holidays</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class Thailand : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl SetImplInstance = new SetImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Thailand"/> class.</summary>
    public Thailand()
    {
        impl = SetImplInstance;
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

    private sealed class SetImpl : Impl
    {
        public override string Name => "Thailand stock exchange";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth;
            Month m = date.Month;
            int y = date.Year;

            if (WesternImpl.IsWeekend(w)
                // New Year's Day
                || ((d == 1 || (d == 3 && w == Weekday.Monday)) && m == Month.January)
                // Chakri Memorial Day
                || ((d == 6 || ((d == 7 || d == 8) && w == Weekday.Monday)) && m == Month.April)
                // Songkran Festival (was cancelled in 2020 due to the Covid-19 Pandemic)
                || ((d == 13 || d == 14 || d == 15) && m == Month.April && y != 2020)
                // Substitution Songkran Festival, usually not more than 5 days in total (was cancelled
                // in 2020 due to the Covid-19 Pandemic)
                || (d == 16 && (w == Weekday.Monday || w == Weekday.Tuesday) && m == Month.April && y != 2020)
                // Labor Day
                || ((d == 1 || ((d == 2 || d == 3) && w == Weekday.Monday)) && m == Month.May)
                // Coronation Day
                || ((d == 4 || ((d == 5 || d == 6) && w == Weekday.Monday)) && m == Month.May && y >= 2019)
                // H.M.Queen Suthida Bajrasudhabimalalakshana’s Birthday
                || ((d == 03 || ((d == 04 || d == 05) && w == Weekday.Monday)) && m == Month.June && y >= 2019)
                // H.M. King Maha Vajiralongkorn Phra Vajiraklaochaoyuhua’s Birthday
                || ((d == 28 || ((d == 29 || d == 30) && w == Weekday.Monday)) && m == Month.July && y >= 2017)
                // H.M. Queen Sirikit The Queen Mother’s Birthday / Mother’s Day
                || ((d == 12 || ((d == 13 || d == 14) && w == Weekday.Monday)) && m == Month.August)
                // H.M. King Bhumibol Adulyadej The Great Memorial Day
                || ((d == 13 || ((d == 14 || d == 15) && w == Weekday.Monday)) && m == Month.October && y >= 2017)
                // Chulalongkorn Day
                || ((d == 23 || ((d == 24 || d == 25) && w == Weekday.Monday)) && m == Month.October && y != 2021) // Moved 2021, see below
                // H.M. King Bhumibol Adulyadej The Great’s Birthday/ National Day / Father’s Day
                || ((d == 5 || ((d == 6 || d == 7) && w == Weekday.Monday)) && m == Month.December)
                // Constitution Day
                || ((d == 10 || ((d == 11 || d == 12) && w == Weekday.Monday)) && m == Month.December)
                // New Year’s Eve
                || ((d == 31 && m == Month.December) || (d == 2 && w == Weekday.Monday && m == Month.January && y != 2024)) // Moved 2024
                )
            {
                return false;
            }

            if ((y == 2000) &&
                ((d == 21 && m == Month.February) // Makha Bucha Day (Substitution Day)
                 || (d == 5 && m == Month.May) // Coronation Day
                 || (d == 17 && m == Month.May) // Wisakha Bucha Day
                 || (d == 17 && m == Month.July) // Buddhist Lent Day
                 || (d == 23 && m == Month.October) // Chulalongkorn Day
                    ))
            {
                return false;
            }

            if ((y == 2001) &&
                ((d == 8 && m == Month.February) // Makha Bucha Day
                 || (d == 7 && m == Month.May) // Wisakha Bucha Day
                 || (d == 8 && m == Month.May) // Coronation Day (Substitution Day)
                 || (d == 6 && m == Month.July) // Buddhist Lent Day
                 || (d == 23 && m == Month.October) // Chulalongkorn Day
                    ))
            {
                return false;
            }

            // 2002, 2003 and 2004 are missing

            if ((y == 2005) &&
                ((d == 23 && m == Month.February) // Makha Bucha Day
                 || (d == 5 && m == Month.May) // Coronation Day
                 || (d == 23 && m == Month.May) // Wisakha Bucha Day (Substitution Day for Sunday 22 May)
                 || (d == 1 && m == Month.July) // Mid Year Closing Day
                 || (d == 22 && m == Month.July) // Buddhist Lent Day
                 || (d == 24 && m == Month.October) // Chulalongkorn Day (Substitution Day for Sunday 23 October)
                    ))
            {
                return false;
            }

            if ((y == 2006) &&
                ((d == 13 && m == Month.February) // Makha Bucha Day
                || (d == 19 && m == Month.April) // Special Holiday
                || (d == 5 && m == Month.May) // Coronation Day
                || (d == 12 && m == Month.May) // Wisakha Bucha Day
                || (d == 12 && m == Month.June) // Special Holidays (Due to the auspicious occasion of the
                                          // celebration of 60th Anniversary of His Majesty's Accession
                                          // to the throne. For Bangkok, Samut Prakan, Nonthaburi,
                                          // Pathumthani and Nakhon Pathom province)
                || (d == 13 && m == Month.June) // Special Holidays (as above)
                || (d == 11 && m == Month.July) // Buddhist Lent Day
                || (d == 23 && m == Month.October) // Chulalongkorn Day
                   ))
            {
                return false;
            }

            if ((y == 2007) &&
                ((d == 5 && m == Month.March) // Makha Bucha Day (Substitution Day for Saturday 3 March)
                 || (d == 7 && m == Month.May) // Coronation Day (Substitution Day for Saturday 5 May)
                 || (d == 31 && m == Month.May) // Wisakha Bucha Day
                 || (d == 30 && m == Month.July) // Asarnha Bucha Day (Substitution Day for Sunday 29 July)
                 || (d == 23 && m == Month.October) // Chulalongkorn Day
                 || (d == 24 && m == Month.December) // Special Holiday
                    ))
            {
                return false;
            }

            if ((y == 2008) &&
                ((d == 21 && m == Month.February) // Makha Bucha Day
                 || (d == 5 && m == Month.May) // Coronation Day
                 || (d == 19 && m == Month.May) // Wisakha Bucha Day
                 || (d == 1 && m == Month.July) // Mid Year Closing Day
                 || (d == 17 && m == Month.July) // Asarnha Bucha Day
                 || (d == 23 && m == Month.October) // Chulalongkorn Day
                   ))
            {
                return false;
            }

            if ((y == 2009) &&
                ((d == 2 && m == Month.January) // Special Holiday
                 || (d == 9 && m == Month.February) // Makha Bucha Day
                 || (d == 5 && m == Month.May) // Coronation Day
                 || (d == 8 && m == Month.May) // Wisakha Bucha Day
                 || (d == 1 && m == Month.July) // Mid Year Closing Day
                 || (d == 6 && m == Month.July) // Special Holiday
                 || (d == 7 && m == Month.July) // Asarnha Bucha Day
                 || (d == 23 && m == Month.October) // Chulalongkorn Day
                   ))
            {
                return false;
            }

            if ((y == 2010) &&
                ((d == 1 && m == Month.March) // Substitution for Makha Bucha Day(Sunday 28 February)
                 || (d == 5 && m == Month.May) // Coronation Day
                 || (d == 20 && m == Month.May) // Special Holiday
                 || (d == 21 && m == Month.May) // Special Holiday
                 || (d == 28 && m == Month.May) // Wisakha Bucha Day
                 || (d == 1 && m == Month.July) // Mid Year Closing Day
                 || (d == 26 && m == Month.July) // Asarnha Bucha Day
                 || (d == 13 && m == Month.August) // Special Holiday
                 || (d == 25 && m == Month.October) // Substitution for Chulalongkorn Day(Saturday 23 October)
                   ))
            {
                return false;
            }

            if ((y == 2011) &&
                ((d == 18 && m == Month.February) // Makha Bucha Day
                 || (d == 5 && m == Month.May) // Coronation Day
                 || (d == 16 && m == Month.May) // Special Holiday
                 || (d == 17 && m == Month.May) // Wisakha Bucha Day
                 || (d == 1 && m == Month.July) // Mid Year Closing Day
                 || (d == 15 && m == Month.July) // Asarnha Bucha Day
                 || (d == 24 && m == Month.October) // Substitution for Chulalongkorn Day(Sunday 23 October)
                   ))
            {
                return false;
            }

            if ((y == 2012) &&
                ((d == 3 && m == Month.January) // Special Holiday
                 || (d == 7 && m == Month.March) // Makha Bucha Day 2/
                 || (d == 9 && m == Month.April) // Special Holiday
                 || (d == 7 && m == Month.May) // Substitution for Coronation Day(Saturday 5 May)
                 || (d == 4 && m == Month.June) // Wisakha Bucha Day
                 || (d == 2 && m == Month.August) // Asarnha Bucha Day
                 || (d == 23 && m == Month.October) // Chulalongkorn Day
                    ))
            {
                return false;
            }

            if ((y == 2013) &&
                ((d == 25 && m == Month.February) // Makha Bucha Day
                 || (d == 6 && m == Month.May) // Substitution for Coronation Day(Sunday 5 May)
                 || (d == 24 && m == Month.May) // Wisakha Bucha Day
                 || (d == 1 && m == Month.July) // Mid Year Closing Day
                 || (d == 22 && m == Month.July) // Asarnha Bucha Day 2/
                 || (d == 23 && m == Month.October) // Chulalongkorn Day
                 || (d == 30 && m == Month.December) // Special Holiday
                    ))
            {
                return false;
            }

            if ((y == 2014) &&
                ((d == 14 && m == Month.February) // Makha Bucha Day
                 || (d == 5 && m == Month.May) // Coronation Day
                 || (d == 13 && m == Month.May) // Wisakha Bucha Day
                 || (d == 1 && m == Month.July) // Mid Year Closing Day
                 || (d == 11 && m == Month.July) // Asarnha Bucha Day 1/
                 || (d == 11 && m == Month.August) // Special Holiday
                 || (d == 23 && m == Month.October) // Chulalongkorn Day
                    ))
            {
                return false;
            }

            if ((y == 2015) &&
                ((d == 2 && m == Month.January) // Special Holiday
                 || (d == 4 && m == Month.March) // Makha Bucha Day
                 || (d == 4 && m == Month.May) // Special Holiday
                 || (d == 5 && m == Month.May) // Coronation Day
                 || (d == 1 && m == Month.June) // Wisakha Bucha Day
                 || (d == 1 && m == Month.July) // Mid Year Closing Day
                 || (d == 30 && m == Month.July) // Asarnha Bucha Day 1/
                 || (d == 23 && m == Month.October) // Chulalongkorn Day
                    ))
            {
                return false;
            }

            if ((y == 2016) &&
                ((d == 22 && m == Month.February) // Makha Bucha Day
                 || (d == 5 && m == Month.May) // Coronation Day
                 || (d == 6 && m == Month.May) // Special Holiday
                 || (d == 20 && m == Month.May) // Wisakha Bucha Day
                 || (d == 1 && m == Month.July) //  Mid Year Closing Day
                 || (d == 18 && m == Month.July) // Special Holiday
                 || (d == 19 && m == Month.July) // Asarnha Bucha Day 1/
                 || (d == 24 && m == Month.October) // Substitution for Chulalongkorn Day (Sunday 23rd October)
                    ))
            {
                return false;
            }

            if ((y == 2017) &&
                ((d == 13 && m == Month.February) // Makha Bucha Day
                    || (d == 10 && m == Month.May) // Wisakha Bucha Day
                    || (d == 10 && m == Month.July) // Asarnha Bucha Day
                    || (d == 23 && m == Month.October) // Chulalongkorn Day
                    || (d == 26 && m == Month.October) // Special Holiday
                    ))
            {
                return false;
            }

            if ((y == 2018) &&
                ((d == 1 && m == Month.March) // Makha Bucha Day
                 || (d == 29 && m == Month.May) // Wisakha Bucha Day
                 || (d == 27 && m == Month.July) // Asarnha Bucha Day1
                 || (d == 23 && m == Month.October) // Chulalongkorn Day
                    ))
            {
                return false;
            }

            if ((y == 2019) && ((d == 19 && m == Month.February) // Makha Bucha Day
                                || (d == 6 && m == Month.May) // Special Holiday
                                || (d == 20 && m == Month.May) // Wisakha Bucha Day
                                || (d == 16 && m == Month.July) // Asarnha Bucha Day
                                ))
            {
                return false;
            }

            if ((y == 2020) && ((d == 10 && m == Month.February) // Makha Bucha Day
                                || (d == 6 && m == Month.May) // Wisakha Bucha Day
                                || (d == 6 && m == Month.July) // Asarnha Bucha Day
                                || (d == 27 && m == Month.July) // Substitution for Songkran Festival
                                || (d == 4 && m == Month.September) // Substitution for Songkran Festival
                                || (d == 7 && m == Month.September) // Substitution for Songkran Festival
                                || (d == 11 && m == Month.December) // Special Holiday
                                ))
            {
                return false;
            }

            if ((y == 2021) && ((d == 12 && m == Month.February) // Special Holiday
                                || (d == 26 && m == Month.February) // Makha Bucha Day
                                || (d == 26 && m == Month.May) // Wisakha Bucha Day
                                || (d == 26 && m == Month.July) // Substitution for Asarnha Bucha Day (Saturday 24th July 2021)
                                || (d == 24 && m == Month.September) // Special Holiday
                                || (d == 22 && m == Month.October) // Substitution for Chulalongkorn Day
                                ))
            {
                return false;
            }

            if ((y == 2022) && ((d == 16 && m == Month.February) // Makha Bucha Day
                                || (d == 16 && m == Month.May) // Substitution for Wisakha Bucha Day (Sunday 15th May 2022)
                                || (d == 13 && m == Month.July) // Asarnha Bucha Day
                                || (d == 29 && m == Month.July) // Additional special holiday (added)
                                || (d == 14 && m == Month.October) // Additional special holiday (added)
                                || (d == 24 && m == Month.October) // Substitution for Chulalongkorn Day (Sunday 23rd October 2022)
                ))
            {
                return false;
            }

            if ((y == 2023) && ((d == 6 && m == Month.March) // Makha Bucha Day
                                || (d == 5 && m == Month.May) // Additional special holiday (added)
                                || (d == 5 && m == Month.June) // Substitution for H.M. Queen's birthday and Wisakha Bucha Day (Saturday 3rd June 2022)
                                || (d == 1 && m == Month.August) // Asarnha Bucha Day
                                || (d == 23 && m == Month.October) // Chulalongkorn Day
                                || (d == 29 && m == Month.December) // Substitution for New Year’s Eve (Sunday 31st December 2023) (added)
                ))
            {
                return false;
            }

            if ((y == 2024) && ((d == 26 && m == Month.February) // Substitution for Makha Bucha Day (Saturday 24th February 2024)
                                || (d == 8 && m == Month.April) // Substitution for Chakri Memorial Day (Saturday 6th April 2024)
                                || (d == 12 && m == Month.April) // Additional holiday in relation to the Songkran festival
                                || (d == 6 && m == Month.May) // Substitution for Coronation Day (Saturday 4th May 2024)
                                || (d == 22 && m == Month.May) // Wisakha Bucha Day
                                || (d == 22 && m == Month.July) // Substitution for Asarnha Bucha Day (Saturday 20th July 2024)
                                || (d == 23 && m == Month.October) // Chulalongkorn Day
                ))
            {
                return false;
            }

            if ((y == 2025) && ((d == 12 && m == Month.February) // Substitution for Makha Bucha Day (Wednesday 12th February 2025)
                || (d == 7 && m == Month.April) // Substitution for Chakri Memorial Day (Sunday 6th April 2025)
                || (d == 5 && m == Month.May) // Substitution for Coronation Day (Sunday 4th May 2025)
                || (d == 12 && m == Month.May) // Wisakha Bucha Day
                || (d == 10 && m == Month.July) // Substitution for Asarnha Bucha Day (Tuesday 20th July 2025)
                || (d == 23 && m == Month.October) // Chulalongkorn Day
                ))
            {
                return false;
            }

            return true;
        }
    }
}
