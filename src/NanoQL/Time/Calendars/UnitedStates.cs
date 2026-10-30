// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2017 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2017 Oleg Kulkov
// SPDX-FileCopyrightText: Copyright (C) 2023 Skandinaviska Enskilda Banken AB (publ)
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2024 Dirk Eddelbuettel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/unitedstates.hpp ql/time/calendars/unitedstates.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The United States calendars.</summary>
/// <remarks>
/// <para>
/// Public holidays (see https://www.opm.gov/policy-data-oversight/pay-leave/federal-holidays):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st (possibly moved to Monday if actually on Sunday, or to Friday if on Saturday)</description></item>
/// <item><description>Martin Luther King's birthday, third Monday in January (since 1983)</description></item>
/// <item><description>Presidents' Day (a.k.a. Washington's birthday), third Monday in February</description></item>
/// <item><description>Memorial Day, last Monday in May</description></item>
/// <item><description>Juneteenth, June 19th (moved to Monday if Sunday or Friday if Saturday)</description></item>
/// <item><description>Independence Day, July 4th (moved to Monday if Sunday or Friday if Saturday)</description></item>
/// <item><description>Labor Day, first Monday in September</description></item>
/// <item><description>Columbus Day, second Monday in October</description></item>
/// <item><description>Veterans' Day, November 11th (moved to Monday if Sunday or Friday if Saturday)</description></item>
/// <item><description>Thanksgiving Day, fourth Thursday in November</description></item>
/// <item><description>Christmas, December 25th (moved to Monday if Sunday or Friday if Saturday)</description></item>
/// </list>
/// </para>
/// <para>
/// Note that since 2015 Independence Day only impacts Libor if it falls on a weekday (see
/// https://www.theice.com/iba/libor, https://www.theice.com/marketdata/reports/170 and
/// https://www.theice.com/publicdocs/LIBOR_Holiday_Calendar_2015.pdf for the fixing and value date calendars).
/// </para>
/// <para>
/// Holidays for the stock exchange (data from http://www.nyse.com):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st (possibly moved to Monday if actually on Sunday)</description></item>
/// <item><description>Martin Luther King's birthday, third Monday in January (since 1998)</description></item>
/// <item><description>Presidents' Day (a.k.a. Washington's birthday), third Monday in February</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Memorial Day, last Monday in May</description></item>
/// <item><description>Independence Day, July 4th (moved to Monday if Sunday or Friday if Saturday)</description></item>
/// <item><description>Labor Day, first Monday in September</description></item>
/// <item><description>Thanksgiving Day, fourth Thursday in November</description></item>
/// <item><description>Presidential election day, first Tuesday in November of election years (until 1980)</description></item>
/// <item><description>Christmas, December 25th (moved to Monday if Sunday or Friday if Saturday)</description></item>
/// <item><description>Special historic closings (see http://www.nyse.com/pdfs/closings.pdf)</description></item>
/// </list>
/// </para>
/// <para>
/// Holidays for the government bond market (data from http://www.bondmarkets.com):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st (possibly moved to Monday if actually on Sunday)</description></item>
/// <item><description>Martin Luther King's birthday, third Monday in January (since 1983)</description></item>
/// <item><description>Presidents' Day (a.k.a. Washington's birthday), third Monday in February</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Memorial Day, last Monday in May</description></item>
/// <item><description>Independence Day, July 4th (moved to Monday if Sunday or Friday if Saturday)</description></item>
/// <item><description>Labor Day, first Monday in September</description></item>
/// <item><description>Columbus Day, second Monday in October</description></item>
/// <item><description>Veterans' Day, November 11th (moved to Monday if Sunday or Friday if Saturday)</description></item>
/// <item><description>Thanksgiving Day, fourth Thursday in November</description></item>
/// <item><description>Christmas, December 25th (moved to Monday if Sunday or Friday if Saturday)</description></item>
/// </list>
/// </para>
/// <para>
/// Holidays for the North American Energy Reliability Council (data from http://www.nerc.com/~oc/offpeaks.html):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st (possibly moved to Monday if actually on Sunday)</description></item>
/// <item><description>Memorial Day, last Monday in May</description></item>
/// <item><description>Independence Day, July 4th (moved to Monday if Sunday)</description></item>
/// <item><description>Labor Day, first Monday in September</description></item>
/// <item><description>Thanksgiving Day, fourth Thursday in November</description></item>
/// <item><description>Christmas, December 25th (moved to Monday if Sunday)</description></item>
/// </list>
/// </para>
/// <para>
/// Holidays for the Federal Reserve Bankwire System (data from https://www.federalreserve.gov/aboutthefed/k8.htm and
/// https://www.frbservices.org/about/holiday-schedules):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st (possibly moved to Monday if actually on Sunday)</description></item>
/// <item><description>Martin Luther King's birthday, third Monday in January (since 1983)</description></item>
/// <item><description>Presidents' Day (a.k.a. Washington's birthday), third Monday in February</description></item>
/// <item><description>Memorial Day, last Monday in May</description></item>
/// <item><description>Juneteenth, June 19th (moved to Monday if Sunday)</description></item>
/// <item><description>Independence Day, July 4th (moved to Monday if Sunday)</description></item>
/// <item><description>Labor Day, first Monday in September</description></item>
/// <item><description>Columbus Day, second Monday in October</description></item>
/// <item><description>Veterans' Day, November 11th (moved to Monday if Sunday)</description></item>
/// <item><description>Thanksgiving Day, fourth Thursday in November</description></item>
/// <item><description>Christmas, December 25th (moved to Monday if Sunday)</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class UnitedStates : Calendar
{
    // all calendar instances on the same market share the same implementation instance
    private static readonly Impl SettlementImplInstance = new SettlementImpl();
    private static readonly Impl LiborImpactImplInstance = new LiborImpactImpl();
    private static readonly Impl NyseImplInstance = new NyseImpl();
    private static readonly Impl GovernmentImplInstance = new GovernmentBondImpl();
    private static readonly Impl NercImplInstance = new NercImpl();
    private static readonly Impl FederalReserveImplInstance = new FederalReserveImpl();
    private static readonly Impl SofrImplInstance = new SofrImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="UnitedStates"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public UnitedStates(UnitedStatesMarket market)
    {
        switch (market)
        {
            case UnitedStatesMarket.Settlement:
                impl = SettlementImplInstance;
                break;
            case UnitedStatesMarket.LiborImpact:
                impl = LiborImpactImplInstance;
                break;
            case UnitedStatesMarket.NYSE:
                impl = NyseImplInstance;
                break;
            case UnitedStatesMarket.GovernmentBond:
                impl = GovernmentImplInstance;
                break;
            case UnitedStatesMarket.SOFR:
                impl = SofrImplInstance;
                break;
            case UnitedStatesMarket.NERC:
                impl = NercImplInstance;
                break;
            case UnitedStatesMarket.FederalReserve:
                impl = FederalReserveImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }

        ApplyEdits(ConventionProvider.ReadCalendarEdits($"UnitedStates.{market}"));
    }

    /// <inheritdoc/>
    public override string Name => impl.Name;

    /// <inheritdoc/>
    public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

    /// <inheritdoc/>
    protected override bool IsBusinessDayCore(Date date) => impl.IsBusinessDay(date);

    // The market-specific rules; QuantLib's Calendar::Impl, with Calendar::WesternImpl's weekend.
    private abstract class Impl
    {
        public abstract string Name { get; }

        public abstract bool IsBusinessDay(Date date);
    }

    private class SettlementImpl : Impl
    {
        public override string Name => "US settlement";

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth;
            Month m = date.Month;
            int y = date.Year;
            if (WesternImpl.IsWeekend(w)
                // New Year's Day (possibly moved to Monday if on Sunday)
                || ((d == 1 || (d == 2 && w == Weekday.Monday)) && m == Month.January)
                // (or to Friday if on Saturday)
                || (d == 31 && w == Weekday.Friday && m == Month.December)
                // Martin Luther King's birthday (third Monday in January)
                || ((d >= 15 && d <= 21) && w == Weekday.Monday && m == Month.January
                    && y >= 1983)
                // Washington's birthday (third Monday in February)
                || UnitedStatesRules.IsWashingtonBirthday(d, m, y, w)
                // Memorial Day (last Monday in May)
                || UnitedStatesRules.IsMemorialDay(d, m, y, w)
                // Juneteenth (Monday if Sunday or Friday if Saturday)
                || UnitedStatesRules.IsJuneteenth(d, m, y, w)
                // Independence Day (Monday if Sunday or Friday if Saturday)
                || ((d == 4 || (d == 5 && w == Weekday.Monday)
                     || (d == 3 && w == Weekday.Friday)) && m == Month.July)
                // Labor Day (first Monday in September)
                || UnitedStatesRules.IsLaborDay(d, m, y, w)
                // Columbus Day (second Monday in October)
                || UnitedStatesRules.IsColumbusDay(d, m, y, w)
                // Veteran's Day (Monday if Sunday or Friday if Saturday)
                || UnitedStatesRules.IsVeteransDay(d, m, y, w)
                // Thanksgiving Day (fourth Thursday in November)
                || ((d >= 22 && d <= 28) && w == Weekday.Thursday && m == Month.November)
                // Christmas (Monday if Sunday or Friday if Saturday)
                || ((d == 25 || (d == 26 && w == Weekday.Monday)
                     || (d == 24 && w == Weekday.Friday)) && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }

    private sealed class LiborImpactImpl : SettlementImpl
    {
        public override string Name => "US with Libor impact";

        public override bool IsBusinessDay(Date date)
        {
            // Since 2015 Independence Day only impacts Libor if it falls
            // on a weekday
            Weekday w = date.Weekday;
            int d = date.DayOfMonth;
            Month m = date.Month;
            int y = date.Year;
            if (((d == 5 && w == Weekday.Monday)
                 || (d == 3 && w == Weekday.Friday)) && m == Month.July && y >= 2015)
            {
                return true;
            }

            return base.IsBusinessDay(date);
        }
    }

    private sealed class NyseImpl : Impl
    {
        public override string Name => "New York stock exchange";

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);
            if (WesternImpl.IsWeekend(w)
                // New Year's Day (possibly moved to Monday if on Sunday)
                || ((d == 1 || (d == 2 && w == Weekday.Monday)) && m == Month.January)
                // Washington's birthday (third Monday in February)
                || UnitedStatesRules.IsWashingtonBirthday(d, m, y, w)
                // Good Friday
                || (dd == em - 3)
                // Memorial Day (last Monday in May)
                || UnitedStatesRules.IsMemorialDay(d, m, y, w)
                // Juneteenth (Monday if Sunday or Friday if Saturday)
                || UnitedStatesRules.IsJuneteenth(d, m, y, w)
                // Independence Day (Monday if Sunday or Friday if Saturday)
                || ((d == 4 || (d == 5 && w == Weekday.Monday)
                     || (d == 3 && w == Weekday.Friday)) && m == Month.July)
                // Labor Day (first Monday in September)
                || UnitedStatesRules.IsLaborDay(d, m, y, w)
                // Thanksgiving Day (fourth Thursday in November)
                || ((d >= 22 && d <= 28) && w == Weekday.Thursday && m == Month.November)
                // Christmas (Monday if Sunday or Friday if Saturday)
                || ((d == 25 || (d == 26 && w == Weekday.Monday)
                     || (d == 24 && w == Weekday.Friday)) && m == Month.December))
            {
                return false;
            }

            if (y >= 1998 && (d >= 15 && d <= 21) && w == Weekday.Monday && m == Month.January)
            {
                // Martin Luther King's birthday (third Monday in January)
                return false;
            }

            if ((y <= 1968 || (y <= 1980 && y % 4 == 0)) && m == Month.November
                && d <= 7 && w == Weekday.Tuesday)
            {
                // Presidential election days
                return false;
            }

            // Special closings
            if (// President Carter's Funeral
                (y == 2025 && m == Month.January && d == 9)
                // President Bush's Funeral
                || (y == 2018 && m == Month.December && d == 5)
                // Hurricane Sandy
                || (y == 2012 && m == Month.October && (d == 29 || d == 30))
                // President Ford's funeral
                || (y == 2007 && m == Month.January && d == 2)
                // President Reagan's funeral
                || (y == 2004 && m == Month.June && d == 11)
                // September 11-14, 2001
                || (y == 2001 && m == Month.September && (11 <= d && d <= 14))
                // President Nixon's funeral
                || (y == 1994 && m == Month.April && d == 27)
                // Hurricane Gloria
                || (y == 1985 && m == Month.September && d == 27)
                // 1977 Blackout
                || (y == 1977 && m == Month.July && d == 14)
                // Funeral of former President Lyndon B. Johnson.
                || (y == 1973 && m == Month.January && d == 25)
                // Funeral of former President Harry S. Truman
                || (y == 1972 && m == Month.December && d == 28)
                // National Day of Participation for the lunar exploration.
                || (y == 1969 && m == Month.July && d == 21)
                // Funeral of former President Eisenhower.
                || (y == 1969 && m == Month.March && d == 31)
                // Closed all day - heavy snow.
                || (y == 1969 && m == Month.February && d == 10)
                // Day after Independence Day.
                || (y == 1968 && m == Month.July && d == 5)
                // June 12-Dec. 31, 1968
                // Four day week (closed on Wednesdays) - Paperwork Crisis
                || (y == 1968 && dd >= 163 && w == Weekday.Wednesday)
                // Day of mourning for Martin Luther King Jr.
                || (y == 1968 && m == Month.April && d == 9)
                // Funeral of President Kennedy
                || (y == 1963 && m == Month.November && d == 25)
                // Day before Decoration Day
                || (y == 1961 && m == Month.May && d == 29)
                // Day after Christmas
                || (y == 1958 && m == Month.December && d == 26)
                // Christmas Eve
                || ((y == 1954 || y == 1956 || y == 1965)
                    && m == Month.December && d == 24))
            {
                return false;
            }

            return true;
        }
    }

    private class GovernmentBondImpl : Impl
    {
        public override string Name => "US government bond market";

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);
            if (WesternImpl.IsWeekend(w)
                // New Year's Day (possibly moved to Monday if on Sunday)
                || ((d == 1 || (d == 2 && w == Weekday.Monday)) && m == Month.January)
                // Martin Luther King's birthday (third Monday in January)
                || ((d >= 15 && d <= 21) && w == Weekday.Monday && m == Month.January
                    && y >= 1983)
                // Washington's birthday (third Monday in February)
                || UnitedStatesRules.IsWashingtonBirthday(d, m, y, w)
                // Good Friday. Since 1996 it's an early close and not a full market
                // close when it coincides with the NFP release date, which is the
                // first Friday of the month(*).
                // See <https://www.sifma.org/resources/general/holiday-schedule/>
                //
                // (*) The full rule is "the third Friday after the conclusion of the
                // week which includes the 12th of the month". This is usually the
                // first Friday of the next month, but can be the second Friday if the
                // month has fewer than 31 days. Since Good Friday is always between
                // March 20th and April 23rd, it can only coincide with the April NFP,
                // which is always on the first Friday, because March has 31 days.
                || (dd == em - 3 && (y < 1996 || d > 7))
                // Memorial Day (last Monday in May)
                || UnitedStatesRules.IsMemorialDay(d, m, y, w)
                // Juneteenth (Monday if Sunday or Friday if Saturday)
                || UnitedStatesRules.IsJuneteenth(d, m, y, w)
                // Independence Day (Monday if Sunday or Friday if Saturday)
                || ((d == 4 || (d == 5 && w == Weekday.Monday)
                     || (d == 3 && w == Weekday.Friday)) && m == Month.July)
                // Labor Day (first Monday in September)
                || UnitedStatesRules.IsLaborDay(d, m, y, w)
                // Columbus Day (second Monday in October)
                || UnitedStatesRules.IsColumbusDay(d, m, y, w)
                // Veteran's Day (Monday if Sunday)
                || UnitedStatesRules.IsVeteransDayNoSaturday(d, m, y, w)
                // Thanksgiving Day (fourth Thursday in November)
                || ((d >= 22 && d <= 28) && w == Weekday.Thursday && m == Month.November)
                // Christmas (Monday if Sunday or Friday if Saturday)
                || ((d == 25 || (d == 26 && w == Weekday.Monday)
                     || (d == 24 && w == Weekday.Friday)) && m == Month.December))
            {
                return false;
            }

            // Special closings
            if (// President Bush's Funeral
                (y == 2018 && m == Month.December && d == 5)
                // Hurricane Sandy
                || (y == 2012 && m == Month.October && d == 30)
                // President Reagan's funeral
                || (y == 2004 && m == Month.June && d == 11))
            {
                return false;
            }

            return true;
        }
    }

    private sealed class SofrImpl : GovernmentBondImpl
    {
        public override string Name => "SOFR fixing calendar";

        public override bool IsBusinessDay(Date date)
        {
            // so far (that is, up to 2023 at the time of this change) SOFR never fixed
            // on Good Friday.  We're extrapolating that pattern.  This might change if
            // a fixing on Good Friday occurs in future years.
            int dY = date.DayOfYear;
            int y = date.Year;

            // Good Friday
            if (dY == (WesternImpl.EasterMonday(y) - 3))
            {
                return false;
            }

            return base.IsBusinessDay(date);
        }
    }

    private sealed class NercImpl : Impl
    {
        public override string Name => "North American Energy Reliability Council";

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth;
            Month m = date.Month;
            int y = date.Year;
            if (WesternImpl.IsWeekend(w)
                // New Year's Day (possibly moved to Monday if on Sunday)
                || ((d == 1 || (d == 2 && w == Weekday.Monday)) && m == Month.January)
                // Memorial Day (last Monday in May)
                || UnitedStatesRules.IsMemorialDay(d, m, y, w)
                // Independence Day (Monday if Sunday)
                || ((d == 4 || (d == 5 && w == Weekday.Monday)) && m == Month.July)
                // Labor Day (first Monday in September)
                || UnitedStatesRules.IsLaborDay(d, m, y, w)
                // Thanksgiving Day (fourth Thursday in November)
                || ((d >= 22 && d <= 28) && w == Weekday.Thursday && m == Month.November)
                // Christmas (Monday if Sunday)
                || ((d == 25 || (d == 26 && w == Weekday.Monday)) && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }

    private sealed class FederalReserveImpl : Impl
    {
        public override string Name => "Federal Reserve Bankwire System";

        public override bool IsBusinessDay(Date date)
        {
            // see https://www.frbservices.org/about/holiday-schedules for details
            Weekday w = date.Weekday;
            int d = date.DayOfMonth;
            Month m = date.Month;
            int y = date.Year;
            if (WesternImpl.IsWeekend(w)
                // New Year's Day (possibly moved to Monday if on Sunday)
                || ((d == 1 || (d == 2 && w == Weekday.Monday)) && m == Month.January)
                // Martin Luther King's birthday (third Monday in January)
                || ((d >= 15 && d <= 21) && w == Weekday.Monday && m == Month.January
                    && y >= 1983)
                // Washington's birthday (third Monday in February)
                || UnitedStatesRules.IsWashingtonBirthday(d, m, y, w)
                // Memorial Day (last Monday in May)
                || UnitedStatesRules.IsMemorialDay(d, m, y, w)
                // Juneteenth (Monday if Sunday)
                || UnitedStatesRules.IsJuneteenth(d, m, y, w, false)
                // Independence Day (Monday if Sunday)
                || ((d == 4 || (d == 5 && w == Weekday.Monday)) && m == Month.July)
                // Labor Day (first Monday in September)
                || UnitedStatesRules.IsLaborDay(d, m, y, w)
                // Columbus Day (second Monday in October)
                || UnitedStatesRules.IsColumbusDay(d, m, y, w)
                // Veteran's Day (Monday if Sunday)
                || UnitedStatesRules.IsVeteransDayNoSaturday(d, m, y, w)
                // Thanksgiving Day (fourth Thursday in November)
                || ((d >= 22 && d <= 28) && w == Weekday.Thursday && m == Month.November)
                // Christmas (Monday if Sunday)
                || ((d == 25 || (d == 26 && w == Weekday.Monday)) && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }
}

// a few rules used by multiple calendars
file static class UnitedStatesRules
{
    public static bool IsWashingtonBirthday(int d, Month m, int y, Weekday w)
    {
        if (y >= 1971)
        {
            // third Monday in February
            return (d >= 15 && d <= 21) && w == Weekday.Monday && m == Month.February;
        }
        else
        {
            // February 22nd, possibly adjusted
            return (d == 22 || (d == 23 && w == Weekday.Monday)
                    || (d == 21 && w == Weekday.Friday)) && m == Month.February;
        }
    }

    public static bool IsMemorialDay(int d, Month m, int y, Weekday w)
    {
        if (y >= 1971)
        {
            // last Monday in May
            return d >= 25 && w == Weekday.Monday && m == Month.May;
        }
        else
        {
            // May 30th, possibly adjusted
            return (d == 30 || (d == 31 && w == Weekday.Monday)
                    || (d == 29 && w == Weekday.Friday)) && m == Month.May;
        }
    }

    public static bool IsLaborDay(int d, Month m, int y, Weekday w)
    {
        // first Monday in September
        return d <= 7 && w == Weekday.Monday && m == Month.September;
    }

    public static bool IsColumbusDay(int d, Month m, int y, Weekday w)
    {
        // second Monday in October
        return (d >= 8 && d <= 14) && w == Weekday.Monday && m == Month.October
            && y >= 1971;
    }

    public static bool IsVeteransDay(int d, Month m, int y, Weekday w)
    {
        if (y <= 1970 || y >= 1978)
        {
            // November 11th, adjusted
            return (d == 11 || (d == 12 && w == Weekday.Monday)
                    || (d == 10 && w == Weekday.Friday)) && m == Month.November;
        }
        else
        {
            // fourth Monday in October
            return (d >= 22 && d <= 28) && w == Weekday.Monday && m == Month.October;
        }
    }

    public static bool IsVeteransDayNoSaturday(int d, Month m, int y, Weekday w)
    {
        if (y <= 1970 || y >= 1978)
        {
            // November 11th, adjusted, but no Saturday to Friday
            return (d == 11 || (d == 12 && w == Weekday.Monday)) && m == Month.November;
        }
        else
        {
            // fourth Monday in October
            return (d >= 22 && d <= 28) && w == Weekday.Monday && m == Month.October;
        }
    }

    public static bool IsJuneteenth(int d, Month m, int y, Weekday w, bool moveToFriday = true)
    {
        // declared in 2021, but only observed by exchanges since 2022
        return (d == 19 || (d == 20 && w == Weekday.Monday) || ((d == 18 && w == Weekday.Friday) && moveToFriday))
            && m == Month.June && y >= 2022;
    }
}
