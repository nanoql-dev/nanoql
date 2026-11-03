// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2005 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Piter Dias
// SPDX-FileCopyrightText: Copyright (C) 2008 Charles Chongseok Hyun
// SPDX-FileCopyrightText: Copyright (C) 2015 Dmitri Nesteruk
// SPDX-FileCopyrightText: Copyright (C) 2020 Piotr Siejda
// SPDX-FileCopyrightText: Copyright (C) 2020 Leonardo Arcari
// SPDX-FileCopyrightText: Copyright (C) 2020 Kline s.r.l.
// SPDX-FileCopyrightText: Copyright (C) 2022, 2024 Skandinaviska Enskilda Banken AB (publ)
// SPDX-FileCopyrightText: Copyright (C) 2023 Jonghee Lee
// SPDX-FileCopyrightText: Copyright (C) 2026 Lawrenz Law
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/calendars.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Moved: testSouthKoreanSettlement line 2146 (Divergences/SouthKoreaElection2025Tests.cs); testKoreaStockExchange line 2843 (Divergences/SouthKoreaElection2025Tests.cs); testMalta line 4173 (Divergences/MaltaWeekendTests.cs); testSerbia line 4301 (Divergences/SerbiaEasterTests.cs)
// Not ported: testIntradayAddHolidays (QL_HIGH_RESOLUTION_DATE, which the default build leaves out)

using System.Collections.Immutable;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

/// <summary>QuantLib's calendar tests, <c>test-suite/calendars.cpp</c>, for the calendars nanoql ports.</summary>
[Trait("Oracle", "QuantLib")]
public class CalendarsTests
{
    // QuantLib's test runner's evaluation date (quantlibglobalfixture.cpp:62-63)
    private static readonly Date RunnerDate = new(16, September, 2015);

    // Adapted: nanoql's calendars are immutable, and an edit returns a modified copy that no other instance sees. The
    // edited calendar's added and removed holidays and results, and New York's unchanged results, are kept. Dropped:
    // lines 147-152, where a new TARGET instance sees the edit, and lines 160-167, which restore the holidays through
    // that instance. Divergences/CalendarEqualityTests.cs tests nanoql's behaviour in their place.
    [Fact]
    public void TestModifiedCalendars()
    {
        Calendar c1 = new TARGET();
        Calendar c2 = new UnitedStates(UnitedStatesMarket.NYSE);
        Date d1 = new(1, May, 2004);    // holiday for both calendars
        Date d2 = new(26, April, 2004); // business day

        Assert.True(c1.IsHoliday(d1), "wrong assumption---correct the test");
        Assert.True(c1.IsBusinessDay(d2), "wrong assumption---correct the test");

        Assert.True(c2.IsHoliday(d1), "wrong assumption---correct the test");
        Assert.True(c2.IsBusinessDay(d2), "wrong assumption---correct the test");

        // modify the TARGET calendar
        c1 = c1.RemoveHoliday(d1);
        c1 = c1.AddHoliday(d2);

        // test
        ImmutableSortedSet<Date> addedHolidays = c1.AddedHolidays;
        ImmutableSortedSet<Date> removedHolidays = c1.RemovedHolidays;

        if (addedHolidays.Contains(d1))
        {
            Assert.Fail("did not expect to find date in addedHolidays");
        }

        if (!addedHolidays.Contains(d2))
        {
            Assert.Fail("expected to find date in addedHolidays");
        }

        if (!removedHolidays.Contains(d1))
        {
            Assert.Fail("expected to find date in removedHolidays");
        }

        if (removedHolidays.Contains(d2))
        {
            Assert.Fail("did not expect to find date in removedHolidays");
        }

        if (c1.IsHoliday(d1))
        {
            Assert.Fail($"{d1} still a holiday for original TARGET instance");
        }

        if (c1.IsBusinessDay(d2))
        {
            Assert.Fail($"{d2} still a business day for original TARGET instance");
        }

        // ...but not other calendars
        if (c2.IsBusinessDay(d1))
        {
            Assert.Fail($"{d1} business day for New York");
        }

        if (c2.IsHoliday(d2))
        {
            Assert.Fail($"{d2} holiday for New York");
        }
    }

    [Fact]
    public void TestJointCalendars()
    {
        // Testing joint calendars...
        Calendar c1 = new TARGET(), c2 = new UnitedKingdom(), c3 = new UnitedStates(UnitedStatesMarket.NYSE),
                 c4 = new Japan(), c5 = new Germany();

        Calendar[] calendarVect = [c1, c2, c3, c4, c5];

        Calendar c12h = new JointCalendar(c1, c2, JointCalendarRule.JoinHolidays),
                 c12b = new JointCalendar(c1, c2, JointCalendarRule.JoinBusinessDays),
                 c123h = new JointCalendar(c1, c2, c3, JointCalendarRule.JoinHolidays),
                 c123b = new JointCalendar(c1, c2, c3, JointCalendarRule.JoinBusinessDays),
                 c1234h = new JointCalendar(c1, c2, c3, c4, JointCalendarRule.JoinHolidays),
                 c1234b = new JointCalendar(c1, c2, c3, c4, JointCalendarRule.JoinBusinessDays),
                 cvh = new JointCalendar(calendarVect, JointCalendarRule.JoinHolidays);

        // test one year, starting today
        Date firstDate = Date.TodaysDate(), endDate = firstDate + (1 * TimeUnit.Years);

        for (Date d = firstDate; d < endDate; d++)
        {
            bool b1 = c1.IsBusinessDay(d), b2 = c2.IsBusinessDay(d), b3 = c3.IsBusinessDay(d),
                 b4 = c4.IsBusinessDay(d), b5 = c5.IsBusinessDay(d);

            if ((b1 && b2) != c12h.IsBusinessDay(d))
            {
                Assert.Fail($"At date {d}:\n    inconsistency between joint calendar {c12h.Name} (joining holidays)\n"
                            + "    and its components");
            }

            if ((b1 || b2) != c12b.IsBusinessDay(d))
            {
                Assert.Fail($"At date {d}:\n    inconsistency between joint calendar {c12b.Name} (joining business days)\n"
                            + "    and its components");
            }

            if ((b1 && b2 && b3) != c123h.IsBusinessDay(d))
            {
                Assert.Fail($"At date {d}:\n    inconsistency between joint calendar {c123h.Name} (joining holidays)\n"
                            + "    and its components");
            }

            if ((b1 || b2 || b3) != c123b.IsBusinessDay(d))
            {
                Assert.Fail($"At date {d}:\n    inconsistency between joint calendar {c123b.Name} (joining business days)\n"
                            + "    and its components");
            }

            if ((b1 && b2 && b3 && b4) != c1234h.IsBusinessDay(d))
            {
                Assert.Fail($"At date {d}:\n    inconsistency between joint calendar {c1234h.Name} (joining holidays)\n"
                            + "    and its components");
            }

            if ((b1 || b2 || b3 || b4) != c1234b.IsBusinessDay(d))
            {
                Assert.Fail($"At date {d}:\n    inconsistency between joint calendar {c1234b.Name} (joining business days)\n"
                            + "    and its components");
            }

            if ((b1 && b2 && b3 && b4 && b5) != cvh.IsBusinessDay(d))
            {
                Assert.Fail($"At date {d}:\n    inconsistency between joint calendar {cvh.Name} (joining holidays)\n"
                            + "    and its components");
            }
        }
    }

    [Fact]
    public void TestUSSettlement()
    {
        Date[] expectedHol =
        [
            new(1, January, 2004),
            new(19, January, 2004),
            new(16, February, 2004),
            new(31, May, 2004),
            new(5, July, 2004),
            new(6, September, 2004),
            new(11, October, 2004),
            new(11, November, 2004),
            new(25, November, 2004),
            new(24, December, 2004),

            new(31, December, 2004),
            new(17, January, 2005),
            new(21, February, 2005),
            new(30, May, 2005),
            new(4, July, 2005),
            new(5, September, 2005),
            new(10, October, 2005),
            new(11, November, 2005),
            new(24, November, 2005),
            new(26, December, 2005),
        ];

        Calendar c = new UnitedStates(UnitedStatesMarket.Settlement);
        Assert.Equal("US settlement", c.Name);
        CheckHolidays(c.HolidayList(new Date(1, January, 2004), new Date(31, December, 2005)), expectedHol);

        // before Uniform Monday Holiday Act
        expectedHol =
        [
            new(2, January, 1961),
            new(22, February, 1961),
            new(30, May, 1961),
            new(4, July, 1961),
            new(4, September, 1961),
            new(10, November, 1961),
            new(23, November, 1961),
            new(25, December, 1961),
        ];

        CheckHolidays(c.HolidayList(new Date(1, January, 1961), new Date(31, December, 1961)), expectedHol);
    }

    [Fact]
    public void TestUSGovernmentBondMarket()
    {
        Date[] expectedHol =
        [
            new(1, January, 2004),
            new(19, January, 2004),
            new(16, February, 2004),
            new(9, April, 2004),
            new(31, May, 2004),
            new(11, June, 2004), // Reagan's funeral
            new(5, July, 2004),
            new(6, September, 2004),
            new(11, October, 2004),
            new(11, November, 2004),
            new(25, November, 2004),
            new(24, December, 2004),
        ];

        Calendar c = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        Assert.Equal("US government bond market", c.Name);
        CheckHolidays(c.HolidayList(new Date(1, January, 2004), new Date(31, December, 2004)), expectedHol);
    }

    [Fact]
    public void TestUSNewYorkStockExchange()
    {
        Date[] expectedHol =
        [
            new(1, January, 2004),
            new(19, January, 2004),
            new(16, February, 2004),
            new(9, April, 2004),
            new(31, May, 2004),
            new(11, June, 2004),
            new(5, July, 2004),
            new(6, September, 2004),
            new(25, November, 2004),
            new(24, December, 2004),

            new(17, January, 2005),
            new(21, February, 2005),
            new(25, March, 2005),
            new(30, May, 2005),
            new(4, July, 2005),
            new(5, September, 2005),
            new(24, November, 2005),
            new(26, December, 2005),

            new(2, January, 2006),
            new(16, January, 2006),
            new(20, February, 2006),
            new(14, April, 2006),
            new(29, May, 2006),
            new(4, July, 2006),
            new(4, September, 2006),
            new(23, November, 2006),
            new(25, December, 2006),
        ];

        Calendar c = new UnitedStates(UnitedStatesMarket.NYSE);
        Assert.Equal("New York stock exchange", c.Name);
        CheckHolidays(c.HolidayList(new Date(1, January, 2004), new Date(31, December, 2006)), expectedHol);

        Date[] histClose =
        [
            new(30, October, 2012),   // Hurricane Sandy
            new(29, October, 2012),   // Hurricane Sandy
            new(11, June, 2004),      // Reagan's funeral
            new(14, September, 2001), // September 11, 2001
            new(13, September, 2001), // September 11, 2001
            new(12, September, 2001), // September 11, 2001
            new(11, September, 2001), // September 11, 2001
            new(27, April, 1994),     // Nixon's funeral.
            new(27, September, 1985), // Hurricane Gloria
            new(14, July, 1977),      // 1977 Blackout
            new(25, January, 1973),   // Johnson's funeral.
            new(28, December, 1972),  // Truman's funeral
            new(21, July, 1969),      // Lunar exploration nat. day
            new(31, March, 1969),     // Eisenhower's funeral
            new(10, February, 1969),  // heavy snow
            new(5, July, 1968),       // Day after Independence Day
            new(9, April, 1968),      // Mourning for MLK
            new(24, December, 1965),  // Christmas Eve
            new(25, November, 1963),  // Kennedy's funeral
            new(29, May, 1961),       // Day before Decoration Day
            new(26, December, 1958),  // Day after Christmas
            new(24, December, 1956),  // Christmas Eve
            new(24, December, 1954),  // Christmas Eve

            // June 12-Dec. 31, 1968
            // Four day week (closed on Wednesdays) - Paperwork Crisis
            new(12, Jun, 1968),
            new(19, Jun, 1968),
            new(26, Jun, 1968),
            new(3, Jul, 1968),
            new(10, Jul, 1968),
            new(17, Jul, 1968),
            new(20, Nov, 1968),
            new(27, Nov, 1968),
            new(4, Dec, 1968),
            new(11, Dec, 1968),
            new(18, Dec, 1968),

            // Presidential election days
            new(4, Nov, 1980),
            new(2, Nov, 1976),
            new(7, Nov, 1972),
            new(5, Nov, 1968),
            new(3, Nov, 1964),
        ];

        foreach (Date i in histClose)
        {
            if (!c.IsHoliday(i))
            {
                Assert.Fail($"{i} should be holiday (historical close)");
            }
        }
    }

    [Fact]
    public void TestSOFR()
    {
        Assert.Equal("SOFR fixing calendar", new UnitedStates(UnitedStatesMarket.SOFR).Name);

        // Good Friday
        foreach (Date goodFriday in new Date[]
                 {
                     new(14, April, 2017), new(30, March, 2018), new(19, April, 2019),
                     new(10, April, 2020), new(2, April, 2021), new(15, April, 2022), new(7, April, 2023),
                     new(29, March, 2024), new(18, April, 2025), new(3, April, 2026), new(26, March, 2027),
                     new(14, April, 2028), new(30, March, 2029), new(19, April, 2030),
                     new(11, April, 2031),
                 })
        {
            Assert.True(new UnitedStates(UnitedStatesMarket.SOFR).IsHoliday(goodFriday));
        }
    }

    [Fact]
    public void TestUSFederalReserveJuneteenth()
    {
        var fedCalendar = new UnitedStates(UnitedStatesMarket.FederalReserve);
        Assert.Equal("Federal Reserve Bankwire System", fedCalendar.Name);

        Date[] expectedHol =
        [
            new(20, June, 2022),
            new(19, June, 2023),
            new(19, June, 2024),
            new(19, June, 2025),
            new(19, June, 2027),
            new(19, June, 2028),
            new(19, June, 2029),
            new(19, June, 2030),
            new(19, June, 2031),
            new(20, June, 2033),
        ];

        foreach (Date holiday in expectedHol)
        {
            if (!fedCalendar.IsHoliday(holiday))
            {
                Assert.Fail($"{holiday} should be a holiday for {fedCalendar.Name}");
            }
        }

        Date notMovedToFriday = new(18, June, 2027);
        if (fedCalendar.IsHoliday(notMovedToFriday))
        {
            Assert.Fail($"{notMovedToFriday} should not be a holiday for {fedCalendar.Name}");
        }
    }

    [Fact]
    public void TestUSLiborImpactCalendar()
    {
        Calendar settlement = new UnitedStates(UnitedStatesMarket.Settlement);
        Calendar liborImpact = new UnitedStates(UnitedStatesMarket.LiborImpact);
        Assert.Equal("US with Libor impact", liborImpact.Name);
        Date observedIndependenceDay = new(3, July, 2020);
        Assert.True(settlement.IsHoliday(observedIndependenceDay));
        Assert.True(liborImpact.IsBusinessDay(observedIndependenceDay));
    }

    [Fact]
    public void TestUSNercCalendar()
    {
        Calendar settlement = new UnitedStates(UnitedStatesMarket.Settlement);
        Calendar nerc = new UnitedStates(UnitedStatesMarket.NERC);
        Assert.Equal("North American Energy Reliability Council", nerc.Name);
        Date juneteenth = new(19, June, 2024);
        Assert.True(settlement.IsHoliday(juneteenth));
        Assert.True(nerc.IsBusinessDay(juneteenth));
    }

    [Fact]
    public void TestTARGET()
    {
        Date[] expectedHol =
        [
            new(1, January, 1999),
            new(31, December, 1999),

            new(21, April, 2000),
            new(24, April, 2000),
            new(1, May, 2000),
            new(25, December, 2000),
            new(26, December, 2000),

            new(1, January, 2001),
            new(13, April, 2001),
            new(16, April, 2001),
            new(1, May, 2001),
            new(25, December, 2001),
            new(26, December, 2001),
            new(31, December, 2001),

            new(1, January, 2002),
            new(29, March, 2002),
            new(1, April, 2002),
            new(1, May, 2002),
            new(25, December, 2002),
            new(26, December, 2002),

            new(1, January, 2003),
            new(18, April, 2003),
            new(21, April, 2003),
            new(1, May, 2003),
            new(25, December, 2003),
            new(26, December, 2003),

            new(1, January, 2004),
            new(9, April, 2004),
            new(12, April, 2004),

            new(25, March, 2005),
            new(28, March, 2005),
            new(26, December, 2005),

            new(14, April, 2006),
            new(17, April, 2006),
            new(1, May, 2006),
            new(25, December, 2006),
            new(26, December, 2006),
        ];

        Calendar c = new TARGET();
        CheckHolidays(c.HolidayList(new Date(1, January, 1999), new Date(31, December, 2006)), expectedHol);
    }

    [Fact]
    public void TestGermanyFrankfurt()
    {
        // Testing Frankfurt Stock Exchange holiday list...

        Date[] expectedHol =
        [
            new(1, January, 2003),
            new(18, April, 2003),
            new(21, April, 2003),
            new(1, May, 2003),
            new(24, December, 2003),
            new(25, December, 2003),
            new(26, December, 2003),

            new(1, January, 2004),
            new(9, April, 2004),
            new(12, April, 2004),
            new(24, December, 2004),
        ];

        Calendar c = new Germany(GermanyMarket.FrankfurtStockExchange);
        Assert.Equal("Frankfurt stock exchange", c.Name);
        CheckHolidays(c.HolidayList(new Date(1, January, 2003), new Date(31, December, 2004)), expectedHol);
    }

    [Fact]
    public void TestGermanyEurex()
    {
        // Testing Eurex holiday list...

        Date[] expectedHol =
        [
            new(1, January, 2003),
            new(18, April, 2003),
            new(21, April, 2003),
            new(1, May, 2003),
            new(24, December, 2003),
            new(25, December, 2003),
            new(26, December, 2003),
            new(31, December, 2003),

            new(1, January, 2004),
            new(9, April, 2004),
            new(12, April, 2004),
            new(24, December, 2004),
            new(31, December, 2004),
        ];

        Calendar c = new Germany(GermanyMarket.Eurex);
        Assert.Equal("Eurex", c.Name);
        CheckHolidays(c.HolidayList(new Date(1, January, 2003), new Date(31, December, 2004)), expectedHol);
    }

    [Fact]
    public void TestGermanyXetra()
    {
        // Testing Xetra holiday list...

        Date[] expectedHol =
        [
            new(1, January, 2003),
            new(18, April, 2003),
            new(21, April, 2003),
            new(1, May, 2003),
            new(24, December, 2003),
            new(25, December, 2003),
            new(26, December, 2003),

            new(1, January, 2004),
            new(9, April, 2004),
            new(12, April, 2004),
            new(24, December, 2004),
        ];

        Calendar c = new Germany(GermanyMarket.Xetra);
        Assert.Equal("Xetra", c.Name);
        CheckHolidays(c.HolidayList(new Date(1, January, 2003), new Date(31, December, 2004)), expectedHol);
    }

    [Fact]
    public void TestGermanySettlementCalendar()
    {
        Calendar settlement = new Germany(GermanyMarket.Settlement);
        Calendar frankfurt = new Germany(GermanyMarket.FrankfurtStockExchange);
        Assert.Equal("German settlement", settlement.Name);
        var corpusChristi = new Date(30, May, 2024);
        Assert.True(settlement.IsHoliday(corpusChristi));
        Assert.True(frankfurt.IsBusinessDay(corpusChristi));
    }

    [Fact]
    public void TestGermanyEuwaxCalendar()
    {
        Calendar euwax = new Germany(GermanyMarket.Euwax);
        Calendar xetra = new Germany(GermanyMarket.Xetra);
        Assert.Equal("Euwax", euwax.Name);
        var whitMonday = new Date(20, May, 2024);
        Assert.True(euwax.IsHoliday(whitMonday));
        Assert.True(xetra.IsBusinessDay(whitMonday));
    }

    [Fact]
    public void TestUKSettlement()
    {
        Date[] expectedHol =
        [
            new(1, January, 2004),
            new(9, April, 2004),
            new(12, April, 2004),
            new(3, May, 2004),
            new(31, May, 2004),
            new(30, August, 2004),
            new(27, December, 2004),
            new(28, December, 2004),

            new(3, January, 2005),
            new(25, March, 2005),
            new(28, March, 2005),
            new(2, May, 2005),
            new(30, May, 2005),
            new(29, August, 2005),
            new(26, December, 2005),
            new(27, December, 2005),

            new(2, January, 2006),
            new(14, April, 2006),
            new(17, April, 2006),
            new(1, May, 2006),
            new(29, May, 2006),
            new(28, August, 2006),
            new(25, December, 2006),
            new(26, December, 2006),

            new(1, January, 2007),
            new(6, April, 2007),
            new(9, April, 2007),
            new(7, May, 2007),
            new(28, May, 2007),
            new(27, August, 2007),
            new(25, December, 2007),
            new(26, December, 2007),
        ];

        Calendar c = new UnitedKingdom(UnitedKingdomMarket.Settlement);
        CheckHolidays(c.HolidayList(new Date(1, January, 2004), new Date(31, December, 2007)), expectedHol);
    }

    [Fact]
    public void TestUKExchange()
    {
        Date[] expectedHol =
        [
            new(1, January, 2004),
            new(9, April, 2004),
            new(12, April, 2004),
            new(3, May, 2004),
            new(31, May, 2004),
            new(30, August, 2004),
            new(27, December, 2004),
            new(28, December, 2004),

            new(3, January, 2005),
            new(25, March, 2005),
            new(28, March, 2005),
            new(2, May, 2005),
            new(30, May, 2005),
            new(29, August, 2005),
            new(26, December, 2005),
            new(27, December, 2005),

            new(2, January, 2006),
            new(14, April, 2006),
            new(17, April, 2006),
            new(1, May, 2006),
            new(29, May, 2006),
            new(28, August, 2006),
            new(25, December, 2006),
            new(26, December, 2006),

            new(1, January, 2007),
            new(6, April, 2007),
            new(9, April, 2007),
            new(7, May, 2007),
            new(28, May, 2007),
            new(27, August, 2007),
            new(25, December, 2007),
            new(26, December, 2007),
        ];

        Calendar c = new UnitedKingdom(UnitedKingdomMarket.Exchange);
        CheckHolidays(c.HolidayList(new Date(1, January, 2004), new Date(31, December, 2007)), expectedHol);
    }

    [Fact]
    public void TestUKMetals()
    {
        Date[] expectedHol =
        [
            new(1, January, 2004),
            new(9, April, 2004),
            new(12, April, 2004),
            new(3, May, 2004),
            new(31, May, 2004),
            new(30, August, 2004),
            new(27, December, 2004),
            new(28, December, 2004),

            new(3, January, 2005),
            new(25, March, 2005),
            new(28, March, 2005),
            new(2, May, 2005),
            new(30, May, 2005),
            new(29, August, 2005),
            new(26, December, 2005),
            new(27, December, 2005),

            new(2, January, 2006),
            new(14, April, 2006),
            new(17, April, 2006),
            new(1, May, 2006),
            new(29, May, 2006),
            new(28, August, 2006),
            new(25, December, 2006),
            new(26, December, 2006),

            new(1, January, 2007),
            new(6, April, 2007),
            new(9, April, 2007),
            new(7, May, 2007),
            new(28, May, 2007),
            new(27, August, 2007),
            new(25, December, 2007),
            new(26, December, 2007),
        ];

        Calendar c = new UnitedKingdom(UnitedKingdomMarket.Metals);
        CheckHolidays(c.HolidayList(new Date(1, January, 2004), new Date(31, December, 2007)), expectedHol);
    }

    // Adapted: without the deprecated Exchange market's check (lines 806-808 and 811), which isn't ported.
    [Fact]
    public void TestItaly()
    {
        // Testing Italian holiday list...

        Calendar c = new Italy(ItalyMarket.Settlement);

        CheckHolidays(c.HolidayList(new Date(1, January, 2002), new Date(31, December, 2004)),
                     [
                         new(1, January, 2002),
                         new(1, April, 2002),
                         new(25, April, 2002),
                         new(1, May, 2002),
                         new(15, August, 2002),
                         new(1, November, 2002),
                         new(25, December, 2002),
                         new(26, December, 2002),

                         new(1, January, 2003),
                         new(6, January, 2003),
                         new(21, April, 2003),
                         new(25, April, 2003),
                         new(1, May, 2003),
                         new(2, June, 2003),
                         new(15, August, 2003),
                         new(8, December, 2003),
                         new(25, December, 2003),
                         new(26, December, 2003),

                         new(1, January, 2004),
                         new(6, January, 2004),
                         new(12, April, 2004),
                         new(2, June, 2004),
                         new(1, November, 2004),
                         new(8, December, 2004),
                     ]);


        CheckHolidays(c.HolidayList(new Date(1, January, 1999), new Date(31, December, 1999)),
                     [
                         new(1, January, 1999),
                         new(6, January, 1999),
                         new(5, April, 1999),
                         new(1, November, 1999),
                         new(8, December, 1999),
                         new(31, December, 1999),
                     ]);

        CheckHolidays(c.HolidayList(new Date(1, January, 2028), new Date(31, December, 2028)),
                     [
                         new(6, January, 2028),
                         new(17, April, 2028),
                         new(25, April, 2028),
                         new(1, May, 2028),
                         new(2, June, 2028),
                         new(15, August, 2028),
                         new(4, October, 2028),
                         new(1, November, 2028),
                         new(8, December, 2028),
                         new(25, December, 2028),
                         new(26, December, 2028),
                     ]);

        var republicDay = new Date(2, June, 2025);
        Assert.True(c.IsHoliday(republicDay));
    }

    [Fact]
    public void TestRussia()
    {
        // Testing Russia holiday list...

        Date[] expectedHol =
        [

        // exhaustive holiday list for the year 2012
            new(1, January, 2012),
            new(2, January, 2012),
            new(7, January, 2012),
            new(8, January, 2012),
            new(14, January, 2012),
            new(15, January, 2012),
            new(21, January, 2012),
            new(22, January, 2012),
            new(28, January, 2012),
            new(29, January, 2012),
            new(4, February, 2012),
            new(5, February, 2012),
            new(11, February, 2012),
            new(12, February, 2012),
            new(18, February, 2012),
            new(19, February, 2012),
            new(23, February, 2012),
            new(25, February, 2012),
            new(26, February, 2012),
            new(3, March, 2012),
            new(4, March, 2012),
            new(8, March, 2012),
            new(9, March, 2012),
            new(10, March, 2012),
            new(17, March, 2012),
            new(18, March, 2012),
            new(24, March, 2012),
            new(25, March, 2012),
            new(31, March, 2012),
            new(1, April, 2012),
            new(7, April, 2012),
            new(8, April, 2012),
            new(14, April, 2012),
            new(15, April, 2012),
            new(21, April, 2012),
            new(22, April, 2012),
            new(29, April, 2012),
            new(30, April, 2012),
            new(1, May, 2012),
            new(6, May, 2012),
            new(9, May, 2012),
            new(13, May, 2012),
            new(19, May, 2012),
            new(20, May, 2012),
            new(26, May, 2012),
            new(27, May, 2012),
            new(2, June, 2012),
            new(3, June, 2012),
            new(10, June, 2012),
            new(11, June, 2012),
            new(12, June, 2012),
            new(16, June, 2012),
            new(17, June, 2012),
            new(23, June, 2012),
            new(24, June, 2012),
            new(30, June, 2012),
            new(1, July, 2012),
            new(7, July, 2012),
            new(8, July, 2012),
            new(14, July, 2012),
            new(15, July, 2012),
            new(21, July, 2012),
            new(22, July, 2012),
            new(28, July, 2012),
            new(29, July, 2012),
            new(4, August, 2012),
            new(5, August, 2012),
            new(11, August, 2012),
            new(12, August, 2012),
            new(18, August, 2012),
            new(19, August, 2012),
            new(25, August, 2012),
            new(26, August, 2012),
            new(1, September, 2012),
            new(2, September, 2012),
            new(8, September, 2012),
            new(9, September, 2012),
            new(15, September, 2012),
            new(16, September, 2012),
            new(22, September, 2012),
            new(23, September, 2012),
            new(29, September, 2012),
            new(30, September, 2012),
            new(6, October, 2012),
            new(7, October, 2012),
            new(13, October, 2012),
            new(14, October, 2012),
            new(20, October, 2012),
            new(21, October, 2012),
            new(27, October, 2012),
            new(28, October, 2012),
            new(3, November, 2012),
            new(4, November, 2012),
            new(5, November, 2012),
            new(10, November, 2012),
            new(11, November, 2012),
            new(17, November, 2012),
            new(18, November, 2012),
            new(24, November, 2012),
            new(25, November, 2012),
            new(1, December, 2012),
            new(2, December, 2012),
            new(8, December, 2012),
            new(9, December, 2012),
            new(15, December, 2012),
            new(16, December, 2012),
            new(22, December, 2012),
            new(23, December, 2012),
            new(29, December, 2012),
            new(30, December, 2012),
            new(31, December, 2012),

        // exhaustive holiday list for the year 2013
            new(1, January, 2013),
            new(2, January, 2013),
            new(3, January, 2013),
            new(4, January, 2013),
            new(5, January, 2013),
            new(6, January, 2013),
            new(7, January, 2013),
            new(12, January, 2013),
            new(13, January, 2013),
            new(19, January, 2013),
            new(20, January, 2013),
            new(26, January, 2013),
            new(27, January, 2013),
            new(2, February, 2013),
            new(3, February, 2013),
            new(9, February, 2013),
            new(10, February, 2013),
            new(16, February, 2013),
            new(17, February, 2013),
            new(23, February, 2013),
            new(24, February, 2013),
            new(2, March, 2013),
            new(3, March, 2013),
            new(8, March, 2013),
            new(9, March, 2013),
            new(10, March, 2013),
            new(16, March, 2013),
            new(17, March, 2013),
            new(23, March, 2013),
            new(24, March, 2013),
            new(30, March, 2013),
            new(31, March, 2013),
            new(6, April, 2013),
            new(7, April, 2013),
            new(13, April, 2013),
            new(14, April, 2013),
            new(20, April, 2013),
            new(21, April, 2013),
            new(27, April, 2013),
            new(28, April, 2013),
            new(1, May, 2013),
            new(4, May, 2013),
            new(5, May, 2013),
            new(9, May, 2013),
            new(11, May, 2013),
            new(12, May, 2013),
            new(18, May, 2013),
            new(19, May, 2013),
            new(25, May, 2013),
            new(26, May, 2013),
            new(1, June, 2013),
            new(2, June, 2013),
            new(8, June, 2013),
            new(9, June, 2013),
            new(12, June, 2013),
            new(15, June, 2013),
            new(16, June, 2013),
            new(22, June, 2013),
            new(23, June, 2013),
            new(29, June, 2013),
            new(30, June, 2013),
            new(6, July, 2013),
            new(7, July, 2013),
            new(13, July, 2013),
            new(14, July, 2013),
            new(20, July, 2013),
            new(21, July, 2013),
            new(27, July, 2013),
            new(28, July, 2013),
            new(3, August, 2013),
            new(4, August, 2013),
            new(10, August, 2013),
            new(11, August, 2013),
            new(17, August, 2013),
            new(18, August, 2013),
            new(24, August, 2013),
            new(25, August, 2013),
            new(31, August, 2013),
            new(1, September, 2013),
            new(7, September, 2013),
            new(8, September, 2013),
            new(14, September, 2013),
            new(15, September, 2013),
            new(21, September, 2013),
            new(22, September, 2013),
            new(28, September, 2013),
            new(29, September, 2013),
            new(5, October, 2013),
            new(6, October, 2013),
            new(12, October, 2013),
            new(13, October, 2013),
            new(19, October, 2013),
            new(20, October, 2013),
            new(26, October, 2013),
            new(27, October, 2013),
            new(2, November, 2013),
            new(3, November, 2013),
            new(4, November, 2013),
            new(9, November, 2013),
            new(10, November, 2013),
            new(16, November, 2013),
            new(17, November, 2013),
            new(23, November, 2013),
            new(24, November, 2013),
            new(30, November, 2013),
            new(1, December, 2013),
            new(7, December, 2013),
            new(8, December, 2013),
            new(14, December, 2013),
            new(15, December, 2013),
            new(21, December, 2013),
            new(22, December, 2013),
            new(28, December, 2013),
            new(29, December, 2013),
            new(31, December, 2013),

        // exhaustive holiday list for the year 2014
            new(1, January, 2014),
            new(2, January, 2014),
            new(3, January, 2014),
            new(4, January, 2014),
            new(5, January, 2014),
            new(7, January, 2014),
            new(11, January, 2014),
            new(12, January, 2014),
            new(18, January, 2014),
            new(19, January, 2014),
            new(25, January, 2014),
            new(26, January, 2014),
            new(1, February, 2014),
            new(2, February, 2014),
            new(8, February, 2014),
            new(9, February, 2014),
            new(15, February, 2014),
            new(16, February, 2014),
            new(22, February, 2014),
            new(23, February, 2014),
            new(1, March, 2014),
            new(2, March, 2014),
            new(8, March, 2014),
            new(9, March, 2014),
            new(10, March, 2014),
            new(15, March, 2014),
            new(16, March, 2014),
            new(22, March, 2014),
            new(23, March, 2014),
            new(29, March, 2014),
            new(30, March, 2014),
            new(5, April, 2014),
            new(6, April, 2014),
            new(12, April, 2014),
            new(13, April, 2014),
            new(19, April, 2014),
            new(20, April, 2014),
            new(26, April, 2014),
            new(27, April, 2014),
            new(1, May, 2014),
            new(3, May, 2014),
            new(4, May, 2014),
            new(9, May, 2014),
            new(10, May, 2014),
            new(11, May, 2014),
            new(17, May, 2014),
            new(18, May, 2014),
            new(24, May, 2014),
            new(25, May, 2014),
            new(31, May, 2014),
            new(1, June, 2014),
            new(7, June, 2014),
            new(8, June, 2014),
            new(12, June, 2014),
            new(14, June, 2014),
            new(15, June, 2014),
            new(21, June, 2014),
            new(22, June, 2014),
            new(28, June, 2014),
            new(29, June, 2014),
            new(5, July, 2014),
            new(6, July, 2014),
            new(12, July, 2014),
            new(13, July, 2014),
            new(19, July, 2014),
            new(20, July, 2014),
            new(26, July, 2014),
            new(27, July, 2014),
            new(2, August, 2014),
            new(3, August, 2014),
            new(9, August, 2014),
            new(10, August, 2014),
            new(16, August, 2014),
            new(17, August, 2014),
            new(23, August, 2014),
            new(24, August, 2014),
            new(30, August, 2014),
            new(31, August, 2014),
            new(6, September, 2014),
            new(7, September, 2014),
            new(13, September, 2014),
            new(14, September, 2014),
            new(20, September, 2014),
            new(21, September, 2014),
            new(27, September, 2014),
            new(28, September, 2014),
            new(4, October, 2014),
            new(5, October, 2014),
            new(11, October, 2014),
            new(12, October, 2014),
            new(18, October, 2014),
            new(19, October, 2014),
            new(25, October, 2014),
            new(26, October, 2014),
            new(1, November, 2014),
            new(2, November, 2014),
            new(4, November, 2014),
            new(8, November, 2014),
            new(9, November, 2014),
            new(15, November, 2014),
            new(16, November, 2014),
            new(22, November, 2014),
            new(23, November, 2014),
            new(29, November, 2014),
            new(30, November, 2014),
            new(6, December, 2014),
            new(7, December, 2014),
            new(13, December, 2014),
            new(14, December, 2014),
            new(20, December, 2014),
            new(21, December, 2014),
            new(27, December, 2014),
            new(28, December, 2014),
            new(31, December, 2014),

        // exhaustive holiday list for the year 2015
            new(1, January, 2015),
            new(2, January, 2015),
            new(3, January, 2015),
            new(4, January, 2015),
            new(7, January, 2015),
            new(10, January, 2015),
            new(11, January, 2015),
            new(17, January, 2015),
            new(18, January, 2015),
            new(24, January, 2015),
            new(25, January, 2015),
            new(31, January, 2015),
            new(1, February, 2015),
            new(7, February, 2015),
            new(8, February, 2015),
            new(14, February, 2015),
            new(15, February, 2015),
            new(21, February, 2015),
            new(22, February, 2015),
            new(23, February, 2015),
            new(28, February, 2015),
            new(1, March, 2015),
            new(7, March, 2015),
            new(8, March, 2015),
            new(9, March, 2015),
            new(14, March, 2015),
            new(15, March, 2015),
            new(21, March, 2015),
            new(22, March, 2015),
            new(28, March, 2015),
            new(29, March, 2015),
            new(4, April, 2015),
            new(5, April, 2015),
            new(11, April, 2015),
            new(12, April, 2015),
            new(18, April, 2015),
            new(19, April, 2015),
            new(25, April, 2015),
            new(26, April, 2015),
            new(1, May, 2015),
            new(2, May, 2015),
            new(3, May, 2015),
            new(9, May, 2015),
            new(10, May, 2015),
            new(11, May, 2015),
            new(16, May, 2015),
            new(17, May, 2015),
            new(23, May, 2015),
            new(24, May, 2015),
            new(30, May, 2015),
            new(31, May, 2015),
            new(6, June, 2015),
            new(7, June, 2015),
            new(12, June, 2015),
            new(13, June, 2015),
            new(14, June, 2015),
            new(20, June, 2015),
            new(21, June, 2015),
            new(27, June, 2015),
            new(28, June, 2015),
            new(4, July, 2015),
            new(5, July, 2015),
            new(11, July, 2015),
            new(12, July, 2015),
            new(18, July, 2015),
            new(19, July, 2015),
            new(25, July, 2015),
            new(26, July, 2015),
            new(1, August, 2015),
            new(2, August, 2015),
            new(8, August, 2015),
            new(9, August, 2015),
            new(15, August, 2015),
            new(16, August, 2015),
            new(22, August, 2015),
            new(23, August, 2015),
            new(29, August, 2015),
            new(30, August, 2015),
            new(5, September, 2015),
            new(6, September, 2015),
            new(12, September, 2015),
            new(13, September, 2015),
            new(19, September, 2015),
            new(20, September, 2015),
            new(26, September, 2015),
            new(27, September, 2015),
            new(3, October, 2015),
            new(4, October, 2015),
            new(10, October, 2015),
            new(11, October, 2015),
            new(17, October, 2015),
            new(18, October, 2015),
            new(24, October, 2015),
            new(25, October, 2015),
            new(31, October, 2015),
            new(1, November, 2015),
            new(4, November, 2015),
            new(7, November, 2015),
            new(8, November, 2015),
            new(14, November, 2015),
            new(15, November, 2015),
            new(21, November, 2015),
            new(22, November, 2015),
            new(28, November, 2015),
            new(29, November, 2015),
            new(5, December, 2015),
            new(6, December, 2015),
            new(12, December, 2015),
            new(13, December, 2015),
            new(19, December, 2015),
            new(20, December, 2015),
            new(26, December, 2015),
            new(27, December, 2015),
            new(31, December, 2015),

        // exhaustive holiday list for the year 2016
            new(1, January, 2016),
            new(2, January, 2016),
            new(3, January, 2016),
            new(7, January, 2016),
            new(8, January, 2016),
            new(9, January, 2016),
            new(10, January, 2016),
            new(16, January, 2016),
            new(17, January, 2016),
            new(23, January, 2016),
            new(24, January, 2016),
            new(30, January, 2016),
            new(31, January, 2016),
            new(6, February, 2016),
            new(7, February, 2016),
            new(13, February, 2016),
            new(14, February, 2016),
            new(21, February, 2016),
            new(23, February, 2016),
            new(27, February, 2016),
            new(28, February, 2016),
            new(5, March, 2016),
            new(6, March, 2016),
            new(8, March, 2016),
            new(12, March, 2016),
            new(13, March, 2016),
            new(19, March, 2016),
            new(20, March, 2016),
            new(26, March, 2016),
            new(27, March, 2016),
            new(2, April, 2016),
            new(3, April, 2016),
            new(9, April, 2016),
            new(10, April, 2016),
            new(16, April, 2016),
            new(17, April, 2016),
            new(23, April, 2016),
            new(24, April, 2016),
            new(30, April, 2016),
            new(1, May, 2016),
            new(2, May, 2016),
            new(3, May, 2016),
            new(7, May, 2016),
            new(8, May, 2016),
            new(9, May, 2016),
            new(14, May, 2016),
            new(15, May, 2016),
            new(21, May, 2016),
            new(22, May, 2016),
            new(28, May, 2016),
            new(29, May, 2016),
            new(4, June, 2016),
            new(5, June, 2016),
            new(11, June, 2016),
            new(12, June, 2016),
            new(13, June, 2016),
            new(18, June, 2016),
            new(19, June, 2016),
            new(25, June, 2016),
            new(26, June, 2016),
            new(2, July, 2016),
            new(3, July, 2016),
            new(9, July, 2016),
            new(10, July, 2016),
            new(16, July, 2016),
            new(17, July, 2016),
            new(23, July, 2016),
            new(24, July, 2016),
            new(30, July, 2016),
            new(31, July, 2016),
            new(6, August, 2016),
            new(7, August, 2016),
            new(13, August, 2016),
            new(14, August, 2016),
            new(20, August, 2016),
            new(21, August, 2016),
            new(27, August, 2016),
            new(28, August, 2016),
            new(3, September, 2016),
            new(4, September, 2016),
            new(10, September, 2016),
            new(11, September, 2016),
            new(17, September, 2016),
            new(18, September, 2016),
            new(24, September, 2016),
            new(25, September, 2016),
            new(1, October, 2016),
            new(2, October, 2016),
            new(8, October, 2016),
            new(9, October, 2016),
            new(15, October, 2016),
            new(16, October, 2016),
            new(22, October, 2016),
            new(23, October, 2016),
            new(29, October, 2016),
            new(30, October, 2016),
            new(4, November, 2016),
            new(5, November, 2016),
            new(6, November, 2016),
            new(12, November, 2016),
            new(13, November, 2016),
            new(19, November, 2016),
            new(20, November, 2016),
            new(26, November, 2016),
            new(27, November, 2016),
            new(3, December, 2016),
            new(4, December, 2016),
            new(10, December, 2016),
            new(11, December, 2016),
            new(17, December, 2016),
            new(18, December, 2016),
            new(24, December, 2016),
            new(25, December, 2016),
            new(30, December, 2016),
            new(31, December, 2016),
        ];

        Calendar moex = new Russia(RussiaMarket.MOEX);
        CheckHolidays(moex.HolidayList(new Date(1, January, 2012), new Date(31, December, 2016), true), expectedHol);

        Calendar settlement = new Russia(RussiaMarket.Settlement);
        var holiday = new Date(6, January, 2012);
        Assert.True(settlement.IsHoliday(holiday));
        Assert.True(moex.IsBusinessDay(holiday));
    }

    [Fact]
    public void TestBrazil()
    {
        // Testing Brazil holiday list...
        Date[] expectedHol =
        [
            // {1,January,2005}, // Saturday
            new(7, February, 2005),
            new(8, February, 2005),
            new(25, March, 2005),
            new(21, April, 2005),

            // {1,May,2005}, // Sunday
            new(26, May, 2005),
            new(7, September, 2005),
            new(12, October, 2005),
            new(2, November, 2005),
            new(15, November, 2005),

            // {25,December,2005}, // Sunday

            // {1,January,2006}, // Sunday
            new(27, February, 2006),
            new(28, February, 2006),
            new(14, April, 2006),
            new(21, April, 2006),
            new(1, May, 2006),
            new(15, June, 2006),
            new(7, September, 2006),
            new(12, October, 2006),
            new(2, November, 2006),
            new(15, November, 2006),
            new(25, December, 2006),
        ];

        Calendar settlement = new Brazil(BrazilMarket.Settlement);
        CheckHolidays(settlement.HolidayList(new Date(1, January, 2005), new Date(31, December, 2006)), expectedHol);

        Calendar exchange = new Brazil(BrazilMarket.Exchange);
        Date christmasEve = new(24, December, 2024);
        Assert.True(settlement.IsBusinessDay(christmasEve));
        Assert.True(exchange.IsHoliday(christmasEve));
    }

    [Fact]
    public void TestDenmark()
    {
        // Testing Denmark holiday list...

        Date[] expectedHol =
        [
            new(1, January, 2020),
            new(9, April, 2020),
            new(10, April, 2020),
            new(13, April, 2020),
            new(8, May, 2020),
            new(21, May, 2020),
            new(22, May, 2020),
            new(1, June, 2020),
            new(5, June, 2020),
            new(24, December, 2020),
            new(25, December, 2020),
            // Saturday: {26, December, 2020},
            new(31, December, 2020),

            new(1, January, 2021),
            new(1, April, 2021),
            new(2, April, 2021),
            new(5, April, 2021),
            new(30, April, 2021),
            new(13, May, 2021),
            new(14, May, 2021),
            new(24, May, 2021),
            // Saturday: {5, June, 2021},
            new(24, December, 2021),
            // Saturday: {25, December, 2021},
            // Sunday: {26, December, 2021},
            new(31, December, 2021),

            // Saturday: {1, January, 2022},
            new(14, April, 2022),
            new(15, April, 2022),
            new(18, April, 2022),
            new(13, May, 2022),
            new(26, May, 2022),
            new(27, May, 2022),
            // Sunday: {5, June, 2022},
            new(6, June, 2022),
            // Saturday: {24, December, 2022},
            // Sunday: {25, December, 2022},
            new(26, December, 2022),
            // Saturday: {31, December, 2022},
        ];

        Calendar c = new Denmark();
        CheckHolidays(c.HolidayList(new Date(1, January, 2020), new Date(31, December, 2022)), expectedHol);
    }

    [Fact]
    public void TestSouthKoreanSettlement()
    {
        // Testing South-Korean settlement holiday list...

        Date[] expectedHol =
        [
            new(1, January, 2004),
            new(21, January, 2004),
            new(22, January, 2004),
            new(23, January, 2004),
            new(1, March, 2004),
            new(5, April, 2004),
            new(15, April, 2004), // election day
            new(5, May, 2004),
            new(26, May, 2004),
            new(27, September, 2004),
            new(28, September, 2004),
            new(29, September, 2004),
            new(8, February, 2005),
            new(9, February, 2005),
            new(10, February, 2005),
            new(1, March, 2005),
            new(5, April, 2005),
            new(5, May, 2005),
            new(6, June, 2005),
            new(15, August, 2005),
            new(19, September, 2005),
            new(3, October, 2005),
            new(30, January, 2006),
            new(1, March, 2006),
            new(1, May, 2006),
            new(5, May, 2006),
            new(31, May, 2006), // election
            new(6, June, 2006),
            new(17, July, 2006),
            new(15, August, 2006),
            new(3, October, 2006),
            new(5, October, 2006),
            new(6, October, 2006),
            new(25, December, 2006),
            new(1, January, 2007),
            new(19, February, 2007),
            new(1, March, 2007),
            new(1, May, 2007),
            new(24, May, 2007),
            new(6, June, 2007),
            new(17, July, 2007),
            new(15, August, 2007),
            new(24, September, 2007),
            new(25, September, 2007),
            new(26, September, 2007),
            new(3, October, 2007),
            new(19, December, 2007), // election
            new(25, December, 2007),
            new(1, January, 2008),
            new(6, February, 2008),
            new(7, February, 2008),
            new(8, February, 2008),
            new(9, April, 2008),
            new(1, May, 2008),
            new(5, May, 2008),
            new(12, May, 2008),
            new(6, June, 2008),
            new(15, August, 2008),
            new(15, September, 2008),
            new(3, October, 2008),
            new(25, December, 2008),
            new(1, January, 2009),
            new(26, January, 2009),
            new(27, January, 2009),
            new(1, May, 2009),
            new(5, May, 2009),
            new(2, October, 2009),
            new(25, December, 2009),
            new(1, January, 2010),
            new(15, February, 2010),
            new(1, March, 2010),
            new(5, May, 2010),
            new(21, May, 2010),
            new(2, June, 2010),
            new(21, September, 2010),
            new(22, September, 2010),
            new(23, September, 2010),
            new(2, February, 2011),
            new(3, February, 2011),
            new(4, February, 2011),
            new(1, March, 2011),
            new(5, May, 2011),
            new(10, May, 2011),
            new(6, June, 2011),
            new(15, August, 2011),
            new(12, September, 2011),
            new(13, September, 2011),
            new(3, October, 2011),
            new(23, January, 2012),
            new(24, January, 2012),
            new(1, March, 2012),
            new(11, April, 2012),
            new(1, May, 2012),
            new(28, May, 2012),
            new(6, June, 2012),
            new(15, August, 2012),
            new(1, October, 2012),
            new(3, October, 2012),
            new(19, December, 2012),
            new(25, December, 2012),
            new(1, January, 2013),
            new(11, February, 2013),
            new(1, March, 2013),
            new(1, May, 2013),
            new(17, May, 2013),
            new(6, June, 2013),
            new(15, August, 2013),
            new(18, September, 2013),
            new(19, September, 2013),
            new(20, September, 2013),
            new(3, October, 2013),
            new(9, October, 2013),
            new(25, December, 2013),
            new(1, January, 2014),
            new(30, January, 2014),
            new(31, January, 2014),
            new(1, May, 2014),
            new(5, May, 2014),
            new(6, May, 2014),
            new(4, June, 2014),
            new(6, June, 2014),
            new(15, August, 2014),
            new(8, September, 2014),
            new(9, September, 2014),
            new(10, September, 2014),
            new(3, October, 2014),
            new(9, October, 2014),
            new(25, December, 2014),
            new(1, January, 2015),
            new(18, February, 2015),
            new(19, February, 2015),
            new(20, February, 2015),
            new(1, May, 2015),
            new(5, May, 2015),
            new(25, May, 2015),
            new(14, August, 2015),
            new(28, September, 2015),
            new(29, September, 2015),
            new(9, October, 2015),
            new(25, December, 2015),
            new(1, January, 2016),
            new(8, February, 2016),
            new(9, February, 2016),
            new(10, February, 2016),
            new(1, March, 2016),
            new(13, April, 2016),
            new(5, May, 2016),
            new(6, June, 2016),
            new(15, August, 2016),
            new(14, September, 2016),
            new(15, September, 2016),
            new(16, September, 2016),
            new(3, October, 2016),
            new(27, January, 2017),
            new(30, January, 2017),
            new(1, March, 2017),
            new(1, May, 2017),
            new(3, May, 2017),
            new(5, May, 2017),
            new(9, May, 2017),
            new(6, June, 2017),
            new(15, August, 2017),
            new(3, October, 2017),
            new(4, October, 2017),
            new(5, October, 2017),
            new(6, October, 2017),
            new(9, October, 2017),
            new(25, December, 2017),
            new(1, January, 2018),
            new(15, February, 2018),
            new(16, February, 2018),
            new(1, March, 2018),
            new(1, May, 2018),
            new(7, May, 2018),
            new(22, May, 2018),
            new(6, June, 2018),
            new(13, June, 2018),
            new(15, August, 2018),
            new(24, September, 2018),
            new(25, September, 2018),
            new(26, September, 2018),
            new(3, October, 2018),
            new(9, October, 2018),
            new(25, December, 2018),
            new(1, January, 2019),
            new(4, February, 2019),
            new(5, February, 2019),
            new(6, February, 2019),
            new(1, March, 2019),
            new(1, May, 2019),
            new(6, May, 2019),
            new(6, June, 2019),
            new(15, August, 2019),
            new(12, September, 2019),
            new(13, September, 2019),
            new(3, October, 2019),
            new(9, October, 2019),
            new(25, December, 2019),
            new(1, January, 2020),
            new(24, January, 2020),
            new(27, January, 2020),
            new(15, April, 2020),
            new(30, April, 2020),
            new(1, May, 2020),
            new(5, May, 2020),
            new(17, August, 2020),
            new(30, September, 2020),
            new(1, October, 2020),
            new(2, October, 2020),
            new(9, October, 2020),
            new(25, December, 2020),
            new(1, January, 2021),
            new(11, February, 2021),
            new(12, February, 2021),
            new(1, March, 2021),
            new(5, May, 2021),
            new(19, May, 2021),
            new(16, August, 2021),
            new(20, September, 2021),
            new(21, September, 2021),
            new(22, September, 2021),
            new(4, October, 2021),
            new(11, October, 2021),
            new(31, January, 2022),
            new(1, February, 2022),
            new(2, February, 2022),
            new(1, March, 2022),
            new(9, March, 2022),
            new(5, May, 2022),
            new(1, June, 2022),
            new(6, June, 2022),
            new(15, August, 2022),
            new(9, September, 2022),
            new(12, September, 2022),
            new(3, October, 2022),
            new(10, October, 2022),
            new(23, January, 2023),
            new(24, January, 2023),
            new(1, March, 2023),
            new(1, May, 2023),
            new(5, May, 2023),
            new(29, May, 2023),
            new(6, June, 2023),
            new(15, August, 2023),
            new(28, September, 2023),
            new(29, September, 2023),
            new(2, October, 2023),
            new(3, October, 2023),
            new(9, October, 2023),
            new(25, December, 2023),
            new(1, January, 2024),
            new(9, February, 2024),
            new(12, February, 2024),
            new(1, March, 2024),
            new(10, April, 2024),
            new(1, May, 2024),
            new(6, May, 2024),
            new(15, May, 2024),
            new(6, June, 2024),
            new(15, August, 2024),
            new(16, September, 2024),
            new(17, September, 2024),
            new(18, September, 2024),
            new(1, October, 2024),
            new(3, October, 2024),
            new(9, October, 2024),
            new(25, December, 2024),
            new(1, January, 2025),
            new(27, January, 2025),
            new(28, January, 2025),
            new(29, January, 2025),
            new(30, January, 2025),
            new(3, March, 2025),
            new(1, May, 2025),
            new(5, May, 2025),
            new(6, May, 2025),
            new(6, June, 2025),
            new(15, August, 2025),
            new(3, October, 2025),
            new(6, October, 2025),
            new(7, October, 2025),
            new(8, October, 2025),
            new(9, October, 2025),
            new(25, December, 2025),
            new(1, January, 2026),
            new(16, February, 2026),
            new(17, February, 2026),
            new(18, February, 2026),
            new(2, March, 2026),
            new(1, May, 2026),
            new(5, May, 2026),
            new(25, May, 2026),
            new(17, August, 2026),
            new(24, September, 2026),
            new(25, September, 2026),
            new(5, October, 2026),
            new(9, October, 2026),
            new(25, December, 2026),
            new(1, January, 2027),
            new(8, February, 2027),
            new(9, February, 2027),
            new(1, March, 2027),
            new(5, May, 2027),
            new(13, May, 2027),
            new(16, August, 2027),
            new(14, September, 2027),
            new(15, September, 2027),
            new(16, September, 2027),
            new(4, October, 2027),
            new(11, October, 2027),
            new(27, December, 2027),
            new(26, January, 2028),
            new(27, January, 2028),
            new(28, January, 2028),
            new(1, March, 2028),
            new(1, May, 2028),
            new(2, May, 2028),
            new(5, May, 2028),
            new(6, June, 2028),
            new(15, August, 2028),
            new(2, October, 2028),
            new(3, October, 2028),
            new(4, October, 2028),
            new(5, October, 2028),
            new(9, October, 2028),
            new(25, December, 2028),
            new(1, January, 2029),
            new(12, February, 2029),
            new(13, February, 2029),
            new(14, February, 2029),
            new(1, March, 2029),
            new(1, May, 2029),
            new(7, May, 2029),
            new(21, May, 2029),
            new(6, June, 2029),
            new(15, August, 2029),
            new(21, September, 2029),
            new(24, September, 2029),
            new(3, October, 2029),
            new(9, October, 2029),
            new(25, December, 2029),
            new(1, January, 2030),
            new(4, February, 2030),
            new(5, February, 2030),
            new(1, March, 2030),
            new(1, May, 2030),
            new(6, May, 2030),
            new(9, May, 2030),
            new(6, June, 2030),
            new(15, August, 2030),
            new(11, September, 2030),
            new(12, September, 2030),
            new(13, September, 2030),
            new(3, October, 2030),
            new(9, October, 2030),
            new(25, December, 2030),
            new(1, January, 2031),
            new(22, January, 2031),
            new(23, January, 2031),
            new(24, January, 2031),
            new(3, March, 2031),
            new(1, May, 2031),
            new(5, May, 2031),
            new(28, May, 2031),
            new(6, June, 2031),
            new(15, August, 2031),
            new(30, September, 2031),
            new(1, October, 2031),
            new(2, October, 2031),
            new(3, October, 2031),
            new(9, October, 2031),
            new(25, December, 2031),
            new(1, January, 2032),
            new(10, February, 2032),
            new(11, February, 2032),
            new(12, February, 2032),
            new(1, March, 2032),
            new(5, May, 2032),
            new(17, May, 2032),
            new(16, August, 2032),
            new(20, September, 2032),
            new(21, September, 2032),
            new(4, October, 2032),
            new(11, October, 2032),
            new(27, December, 2032),
            new(31, January, 2033),
            new(1, February, 2033),
            new(2, February, 2033),
            new(1, March, 2033),
            new(5, May, 2033),
            new(6, May, 2033),
            new(6, June, 2033),
            new(15, August, 2033),
            new(7, September, 2033),
            new(8, September, 2033),
            new(9, September, 2033),
            new(3, October, 2033),
            new(10, October, 2033),
            new(26, December, 2033),
            new(20, February, 2034),
            new(21, February, 2034),
            new(1, March, 2034),
            new(1, May, 2034),
            new(5, May, 2034),
            new(25, May, 2034),
            new(6, June, 2034),
            new(15, August, 2034),
            new(26, September, 2034),
            new(27, September, 2034),
            new(28, September, 2034),
            new(3, October, 2034),
            new(9, October, 2034),
            new(25, December, 2034),
            new(1, January, 2035),
            new(7, February, 2035),
            new(8, February, 2035),
            new(9, February, 2035),
            new(1, March, 2035),
            new(1, May, 2035),
            new(7, May, 2035),
            new(15, May, 2035),
            new(6, June, 2035),
            new(15, August, 2035),
            new(17, September, 2035),
            new(18, September, 2035),
            new(3, October, 2035),
            new(9, October, 2035),
            new(25, December, 2035),
            new(1, January, 2036),
            new(28, January, 2036),
            new(29, January, 2036),
            new(30, January, 2036),
            new(3, March, 2036),
            new(1, May, 2036),
            new(5, May, 2036),
            new(6, May, 2036),
            new(6, June, 2036),
            new(15, August, 2036),
            new(3, October, 2036),
            new(6, October, 2036),
            new(7, October, 2036),
            new(9, October, 2036),
            new(25, December, 2036),
            new(1, January, 2037),
            new(16, February, 2037),
            new(17, February, 2037),
            new(2, March, 2037),
            new(1, May, 2037),
            new(5, May, 2037),
            new(22, May, 2037),
            new(17, August, 2037),
            new(23, September, 2037),
            new(24, September, 2037),
            new(25, September, 2037),
            new(5, October, 2037),
            new(9, October, 2037),
            new(25, December, 2037),
            new(1, January, 2038),
            new(3, February, 2038),
            new(4, February, 2038),
            new(5, February, 2038),
            new(1, March, 2038),
            new(5, May, 2038),
            new(11, May, 2038),
            new(16, August, 2038),
            new(13, September, 2038),
            new(14, September, 2038),
            new(15, September, 2038),
            new(4, October, 2038),
            new(11, October, 2038),
            new(27, December, 2038),
            new(24, January, 2039),
            new(25, January, 2039),
            new(26, January, 2039),
            new(1, March, 2039),
            new(2, May, 2039),
            new(5, May, 2039),
            new(6, June, 2039),
            new(15, August, 2039),
            new(3, October, 2039),
            new(4, October, 2039),
            new(5, October, 2039),
            new(10, October, 2039),
            new(26, December, 2039),
            new(13, February, 2040),
            new(14, February, 2040),
            new(1, March, 2040),
            new(1, May, 2040),
            new(7, May, 2040),
            new(18, May, 2040),
            new(6, June, 2040),
            new(15, August, 2040),
            new(20, September, 2040),
            new(21, September, 2040),
            new(3, October, 2040),
            new(9, October, 2040),
            new(25, December, 2040),
            new(1, January, 2041),
            new(31, January, 2041),
            new(1, February, 2041),
            new(1, March, 2041),
            new(1, May, 2041),
            new(6, May, 2041),
            new(7, May, 2041),
            new(6, June, 2041),
            new(15, August, 2041),
            new(9, September, 2041),
            new(10, September, 2041),
            new(11, September, 2041),
            new(3, October, 2041),
            new(9, October, 2041),
            new(25, December, 2041),
            new(1, January, 2042),
            new(21, January, 2042),
            new(22, January, 2042),
            new(23, January, 2042),
            new(3, March, 2042),
            new(1, May, 2042),
            new(5, May, 2042),
            new(26, May, 2042),
            new(6, June, 2042),
            new(15, August, 2042),
            new(29, September, 2042),
            new(30, September, 2042),
            new(3, October, 2042),
            new(9, October, 2042),
            new(25, December, 2042),
            new(1, January, 2043),
            new(9, February, 2043),
            new(10, February, 2043),
            new(11, February, 2043),
            new(2, March, 2043),
            new(1, May, 2043),
            new(5, May, 2043),
            new(18, May, 2043),
            new(17, August, 2043),
            new(16, September, 2043),
            new(17, September, 2043),
            new(18, September, 2043),
            new(5, October, 2043),
            new(9, October, 2043),
            new(25, December, 2043),
            new(1, January, 2044),
            new(29, January, 2044),
            new(1, February, 2044),
            new(1, March, 2044),
            new(5, May, 2044),
            new(6, May, 2044),
            new(6, June, 2044),
            new(15, August, 2044),
            new(3, October, 2044),
            new(4, October, 2044),
            new(5, October, 2044),
            new(6, October, 2044),
            new(10, October, 2044),
            new(26, December, 2044),
            new(16, February, 2045),
            new(17, February, 2045),
            new(1, March, 2045),
            new(1, May, 2045),
            new(5, May, 2045),
            new(24, May, 2045),
            new(6, June, 2045),
            new(15, August, 2045),
            new(25, September, 2045),
            new(26, September, 2045),
            new(27, September, 2045),
            new(3, October, 2045),
            new(9, October, 2045),
            new(25, December, 2045),
            new(1, January, 2046),
            new(5, February, 2046),
            new(6, February, 2046),
            new(7, February, 2046),
            new(1, March, 2046),
            new(1, May, 2046),
            new(7, May, 2046),
            new(14, May, 2046),
            new(6, June, 2046),
            new(15, August, 2046),
            new(14, September, 2046),
            new(17, September, 2046),
            new(3, October, 2046),
            new(9, October, 2046),
            new(25, December, 2046),
            new(1, January, 2047),
            new(25, January, 2047),
            new(28, January, 2047),
            new(1, March, 2047),
            new(1, May, 2047),
            new(2, May, 2047),
            new(6, May, 2047),
            new(6, June, 2047),
            new(15, August, 2047),
            new(3, October, 2047),
            new(4, October, 2047),
            new(7, October, 2047),
            new(9, October, 2047),
            new(25, December, 2047),
            new(1, January, 2048),
            new(13, February, 2048),
            new(14, February, 2048),
            new(2, March, 2048),
            new(1, May, 2048),
            new(5, May, 2048),
            new(20, May, 2048),
            new(17, August, 2048),
            new(21, September, 2048),
            new(22, September, 2048),
            new(23, September, 2048),
            new(5, October, 2048),
            new(9, October, 2048),
            new(25, December, 2048),
            new(1, January, 2049),
            new(1, February, 2049),
            new(2, February, 2049),
            new(3, February, 2049),
            new(1, March, 2049),
            new(5, May, 2049),
            new(10, May, 2049),
            new(16, August, 2049),
            new(10, September, 2049),
            new(13, September, 2049),
            new(4, October, 2049),
            new(11, October, 2049),
            new(27, December, 2049),
            new(24, January, 2050),
            new(25, January, 2050),
            new(1, March, 2050),
            new(5, May, 2050),
            new(30, May, 2050),
            new(6, June, 2050),
            new(15, August, 2050),
            new(29, September, 2050),
            new(30, September, 2050),
            new(3, October, 2050),
            new(10, October, 2050),
            new(26, December, 2050),
        ];

        Calendar c = new SouthKorea(SouthKoreaMarket.Settlement);

        // Moved: the check of line 2146 on 2025, whose list misses the presidential election day, June 3rd, moves to
        // Divergences/SouthKoreaElection2025Tests.cs with upstream's line; the rest of the list stands.
        static bool Kept(Date d) => d.Year != 2025;
        CheckHolidays(
            c.HolidayList(new Date(1, January, 2004), new Date(31, December, 2050)).Where(Kept),
            expectedHol.Where(Kept));
    }

    [Fact]
    public void TestKoreaStockExchange()
    {
        // Testing Korea Stock Exchange holiday list...

        Date[] expectedHol =
        [
            new(1, January, 2004),
            new(21, January, 2004),
            new(22, January, 2004),
            new(23, January, 2004),
            new(1, March, 2004),
            new(5, April, 2004),
            new(15, April, 2004), // election day
            new(5, May, 2004),
            new(26, May, 2004),
            new(27, September, 2004),
            new(28, September, 2004),
            new(29, September, 2004),
            new(31, December, 2004),
            new(8, February, 2005),
            new(9, February, 2005),
            new(10, February, 2005),
            new(1, March, 2005),
            new(5, April, 2005),
            new(5, May, 2005),
            new(6, June, 2005),
            new(15, August, 2005),
            new(19, September, 2005),
            new(3, October, 2005),
            new(30, December, 2005),
            new(30, January, 2006),
            new(1, March, 2006),
            new(1, May, 2006),
            new(5, May, 2006),
            new(31, May, 2006), // election
            new(6, June, 2006),
            new(17, July, 2006),
            new(15, August, 2006),
            new(3, October, 2006),
            new(5, October, 2006),
            new(6, October, 2006),
            new(25, December, 2006),
            new(29, December, 2006),
            new(1, January, 2007),
            new(19, February, 2007),
            new(1, March, 2007),
            new(1, May, 2007),
            new(24, May, 2007),
            new(6, June, 2007),
            new(17, July, 2007),
            new(15, August, 2007),
            new(24, September, 2007),
            new(25, September, 2007),
            new(26, September, 2007),
            new(3, October, 2007),
            new(19, December, 2007), // election
            new(25, December, 2007),
            new(31, December, 2007),
            new(1, January, 2008),
            new(6, February, 2008),
            new(7, February, 2008),
            new(8, February, 2008),
            new(9, April, 2008),
            new(1, May, 2008),
            new(5, May, 2008),
            new(12, May, 2008),
            new(6, June, 2008),
            new(15, August, 2008),
            new(15, September, 2008),
            new(3, October, 2008),
            new(25, December, 2008),
            new(31, December, 2008),
            new(1, January, 2009),
            new(26, January, 2009),
            new(27, January, 2009),
            new(1, May, 2009),
            new(5, May, 2009),
            new(2, October, 2009),
            new(25, December, 2009),
            new(31, December, 2009),
            new(1, January, 2010),
            new(15, February, 2010),
            new(1, March, 2010),
            new(5, May, 2010),
            new(21, May, 2010),
            new(2, June, 2010),
            new(21, September, 2010),
            new(22, September, 2010),
            new(23, September, 2010),
            new(31, December, 2010),
            new(2, February, 2011),
            new(3, February, 2011),
            new(4, February, 2011),
            new(1, March, 2011),
            new(5, May, 2011),
            new(10, May, 2011),
            new(6, June, 2011),
            new(15, August, 2011),
            new(12, September, 2011),
            new(13, September, 2011),
            new(3, October, 2011),
            new(30, December, 2011),
            new(23, January, 2012),
            new(24, January, 2012),
            new(1, March, 2012),
            new(11, April, 2012),
            new(1, May, 2012),
            new(28, May, 2012),
            new(6, June, 2012),
            new(15, August, 2012),
            new(1, October, 2012),
            new(3, October, 2012),
            new(19, December, 2012),
            new(25, December, 2012),
            new(31, December, 2012),
            new(1, January, 2013),
            new(11, February, 2013),
            new(1, March, 2013),
            new(1, May, 2013),
            new(17, May, 2013),
            new(6, June, 2013),
            new(15, August, 2013),
            new(18, September, 2013),
            new(19, September, 2013),
            new(20, September, 2013),
            new(3, October, 2013),
            new(9, October, 2013),
            new(25, December, 2013),
            new(31, December, 2013),
            new(1, January, 2014),
            new(30, January, 2014),
            new(31, January, 2014),
            new(1, May, 2014),
            new(5, May, 2014),
            new(6, May, 2014),
            new(4, June, 2014),
            new(6, June, 2014),
            new(15, August, 2014),
            new(8, September, 2014),
            new(9, September, 2014),
            new(10, September, 2014),
            new(3, October, 2014),
            new(9, October, 2014),
            new(25, December, 2014),
            new(31, December, 2014),
            new(1, January, 2015),
            new(18, February, 2015),
            new(19, February, 2015),
            new(20, February, 2015),
            new(1, May, 2015),
            new(5, May, 2015),
            new(25, May, 2015),
            new(14, August, 2015),
            new(28, September, 2015),
            new(29, September, 2015),
            new(9, October, 2015),
            new(25, December, 2015),
            new(31, December, 2015),
            new(1, January, 2016),
            new(8, February, 2016),
            new(9, February, 2016),
            new(10, February, 2016),
            new(1, March, 2016),
            new(13, April, 2016),
            new(5, May, 2016),
            new(6, May, 2016),
            new(6, June, 2016),
            new(15, August, 2016),
            new(14, September, 2016),
            new(15, September, 2016),
            new(16, September, 2016),
            new(3, October, 2016),
            new(30, December, 2016),
            new(27, January, 2017),
            new(30, January, 2017),
            new(1, March, 2017),
            new(1, May, 2017),
            new(3, May, 2017),
            new(5, May, 2017),
            new(9, May, 2017),
            new(6, June, 2017),
            new(15, August, 2017),
            new(2, October, 2017),
            new(3, October, 2017),
            new(4, October, 2017),
            new(5, October, 2017),
            new(6, October, 2017),
            new(9, October, 2017),
            new(25, December, 2017),
            new(29, December, 2017),
            new(1, January, 2018),
            new(15, February, 2018),
            new(16, February, 2018),
            new(1, March, 2018),
            new(1, May, 2018),
            new(7, May, 2018),
            new(22, May, 2018),
            new(6, June, 2018),
            new(13, June, 2018),
            new(15, August, 2018),
            new(24, September, 2018),
            new(25, September, 2018),
            new(26, September, 2018),
            new(3, October, 2018),
            new(9, October, 2018),
            new(25, December, 2018),
            new(31, December, 2018),
            new(1, January, 2019),
            new(4, February, 2019),
            new(5, February, 2019),
            new(6, February, 2019),
            new(1, March, 2019),
            new(1, May, 2019),
            new(6, May, 2019),
            new(6, June, 2019),
            new(15, August, 2019),
            new(12, September, 2019),
            new(13, September, 2019),
            new(3, October, 2019),
            new(9, October, 2019),
            new(25, December, 2019),
            new(31, December, 2019),
            new(1, January, 2020),
            new(24, January, 2020),
            new(27, January, 2020),
            new(15, April, 2020),
            new(30, April, 2020),
            new(1, May, 2020),
            new(5, May, 2020),
            new(17, August, 2020),
            new(30, September, 2020),
            new(1, October, 2020),
            new(2, October, 2020),
            new(9, October, 2020),
            new(25, December, 2020),
            new(31, December, 2020),
            new(1, January, 2021),
            new(11, February, 2021),
            new(12, February, 2021),
            new(1, March, 2021),
            new(5, May, 2021),
            new(19, May, 2021),
            new(16, August, 2021),
            new(20, September, 2021),
            new(21, September, 2021),
            new(22, September, 2021),
            new(4, October, 2021),
            new(11, October, 2021),
            new(31, December, 2021),
            new(31, January, 2022),
            new(1, February, 2022),
            new(2, February, 2022),
            new(1, March, 2022),
            new(9, March, 2022),
            new(5, May, 2022),
            new(1, June, 2022),
            new(6, June, 2022),
            new(15, August, 2022),
            new(9, September, 2022),
            new(12, September, 2022),
            new(3, October, 2022),
            new(10, October, 2022),
            new(30, December, 2022),
            new(23, January, 2023),
            new(24, January, 2023),
            new(1, March, 2023),
            new(1, May, 2023),
            new(5, May, 2023),
            new(29, May, 2023),
            new(6, June, 2023),
            new(15, August, 2023),
            new(28, September, 2023),
            new(29, September, 2023),
            new(2, October, 2023),
            new(3, October, 2023),
            new(9, October, 2023),
            new(25, December, 2023),
            new(29, December, 2023),
            new(1, January, 2024),
            new(9, February, 2024),
            new(12, February, 2024),
            new(1, March, 2024),
            new(10, April, 2024),
            new(1, May, 2024),
            new(6, May, 2024),
            new(15, May, 2024),
            new(6, June, 2024),
            new(15, August, 2024),
            new(16, September, 2024),
            new(17, September, 2024),
            new(18, September, 2024),
            new(1, October, 2024),
            new(3, October, 2024),
            new(9, October, 2024),
            new(25, December, 2024),
            new(31, December, 2024),
            new(1, January, 2025),
            new(27, January, 2025),
            new(28, January, 2025),
            new(29, January, 2025),
            new(30, January, 2025),
            new(3, March, 2025),
            new(1, May, 2025),
            new(5, May, 2025),
            new(6, May, 2025),
            new(6, June, 2025),
            new(15, August, 2025),
            new(3, October, 2025),
            new(6, October, 2025),
            new(7, October, 2025),
            new(8, October, 2025),
            new(9, October, 2025),
            new(25, December, 2025),
            new(31, December, 2025),
            new(1, January, 2026),
            new(16, February, 2026),
            new(17, February, 2026),
            new(18, February, 2026),
            new(2, March, 2026),
            new(1, May, 2026),
            new(5, May, 2026),
            new(25, May, 2026),
            new(17, August, 2026),
            new(24, September, 2026),
            new(25, September, 2026),
            new(5, October, 2026),
            new(9, October, 2026),
            new(25, December, 2026),
            new(31, December, 2026),
            new(1, January, 2027),
            new(8, February, 2027),
            new(9, February, 2027),
            new(1, March, 2027),
            new(5, May, 2027),
            new(13, May, 2027),
            new(16, August, 2027),
            new(14, September, 2027),
            new(15, September, 2027),
            new(16, September, 2027),
            new(4, October, 2027),
            new(11, October, 2027),
            new(27, December, 2027),
            new(31, December, 2027),
            new(26, January, 2028),
            new(27, January, 2028),
            new(28, January, 2028),
            new(1, March, 2028),
            new(1, May, 2028),
            new(2, May, 2028),
            new(5, May, 2028),
            new(6, June, 2028),
            new(15, August, 2028),
            new(2, October, 2028),
            new(3, October, 2028),
            new(4, October, 2028),
            new(5, October, 2028),
            new(9, October, 2028),
            new(25, December, 2028),
            new(29, December, 2028),
            new(1, January, 2029),
            new(12, February, 2029),
            new(13, February, 2029),
            new(14, February, 2029),
            new(1, March, 2029),
            new(1, May, 2029),
            new(7, May, 2029),
            new(21, May, 2029),
            new(6, June, 2029),
            new(15, August, 2029),
            new(21, September, 2029),
            new(24, September, 2029),
            new(3, October, 2029),
            new(9, October, 2029),
            new(25, December, 2029),
            new(31, December, 2029),
            new(1, January, 2030),
            new(4, February, 2030),
            new(5, February, 2030),
            new(1, March, 2030),
            new(1, May, 2030),
            new(6, May, 2030),
            new(9, May, 2030),
            new(6, June, 2030),
            new(15, August, 2030),
            new(11, September, 2030),
            new(12, September, 2030),
            new(13, September, 2030),
            new(3, October, 2030),
            new(9, October, 2030),
            new(25, December, 2030),
            new(31, December, 2030),
            new(1, January, 2031),
            new(22, January, 2031),
            new(23, January, 2031),
            new(24, January, 2031),
            new(3, March, 2031),
            new(1, May, 2031),
            new(5, May, 2031),
            new(28, May, 2031),
            new(6, June, 2031),
            new(15, August, 2031),
            new(30, September, 2031),
            new(1, October, 2031),
            new(2, October, 2031),
            new(3, October, 2031),
            new(9, October, 2031),
            new(25, December, 2031),
            new(31, December, 2031),
            new(1, January, 2032),
            new(10, February, 2032),
            new(11, February, 2032),
            new(12, February, 2032),
            new(1, March, 2032),
            new(5, May, 2032),
            new(17, May, 2032),
            new(16, August, 2032),
            new(20, September, 2032),
            new(21, September, 2032),
            new(4, October, 2032),
            new(11, October, 2032),
            new(27, December, 2032),
            new(31, December, 2032),
            new(31, January, 2033),
            new(1, February, 2033),
            new(2, February, 2033),
            new(1, March, 2033),
            new(5, May, 2033),
            new(6, May, 2033),
            new(6, June, 2033),
            new(15, August, 2033),
            new(7, September, 2033),
            new(8, September, 2033),
            new(9, September, 2033),
            new(3, October, 2033),
            new(10, October, 2033),
            new(26, December, 2033),
            new(30, December, 2033),
            new(20, February, 2034),
            new(21, February, 2034),
            new(1, March, 2034),
            new(1, May, 2034),
            new(5, May, 2034),
            new(25, May, 2034),
            new(6, June, 2034),
            new(15, August, 2034),
            new(26, September, 2034),
            new(27, September, 2034),
            new(28, September, 2034),
            new(3, October, 2034),
            new(9, October, 2034),
            new(25, December, 2034),
            new(29, December, 2034),
            new(1, January, 2035),
            new(7, February, 2035),
            new(8, February, 2035),
            new(9, February, 2035),
            new(1, March, 2035),
            new(1, May, 2035),
            new(7, May, 2035),
            new(15, May, 2035),
            new(6, June, 2035),
            new(15, August, 2035),
            new(17, September, 2035),
            new(18, September, 2035),
            new(3, October, 2035),
            new(9, October, 2035),
            new(25, December, 2035),
            new(31, December, 2035),
            new(1, January, 2036),
            new(28, January, 2036),
            new(29, January, 2036),
            new(30, January, 2036),
            new(3, March, 2036),
            new(1, May, 2036),
            new(5, May, 2036),
            new(6, May, 2036),
            new(6, June, 2036),
            new(15, August, 2036),
            new(3, October, 2036),
            new(6, October, 2036),
            new(7, October, 2036),
            new(9, October, 2036),
            new(25, December, 2036),
            new(31, December, 2036),
            new(1, January, 2037),
            new(16, February, 2037),
            new(17, February, 2037),
            new(2, March, 2037),
            new(1, May, 2037),
            new(5, May, 2037),
            new(22, May, 2037),
            new(17, August, 2037),
            new(23, September, 2037),
            new(24, September, 2037),
            new(25, September, 2037),
            new(5, October, 2037),
            new(9, October, 2037),
            new(25, December, 2037),
            new(31, December, 2037),
            new(1, January, 2038),
            new(3, February, 2038),
            new(4, February, 2038),
            new(5, February, 2038),
            new(1, March, 2038),
            new(5, May, 2038),
            new(11, May, 2038),
            new(16, August, 2038),
            new(13, September, 2038),
            new(14, September, 2038),
            new(15, September, 2038),
            new(4, October, 2038),
            new(11, October, 2038),
            new(27, December, 2038),
            new(31, December, 2038),
            new(24, January, 2039),
            new(25, January, 2039),
            new(26, January, 2039),
            new(1, March, 2039),
            new(2, May, 2039),
            new(5, May, 2039),
            new(6, June, 2039),
            new(15, August, 2039),
            new(3, October, 2039),
            new(4, October, 2039),
            new(5, October, 2039),
            new(10, October, 2039),
            new(26, December, 2039),
            new(30, December, 2039),
            new(13, February, 2040),
            new(14, February, 2040),
            new(1, March, 2040),
            new(1, May, 2040),
            new(7, May, 2040),
            new(18, May, 2040),
            new(6, June, 2040),
            new(15, August, 2040),
            new(20, September, 2040),
            new(21, September, 2040),
            new(3, October, 2040),
            new(9, October, 2040),
            new(25, December, 2040),
            new(31, December, 2040),
            new(1, January, 2041),
            new(31, January, 2041),
            new(1, February, 2041),
            new(1, March, 2041),
            new(1, May, 2041),
            new(6, May, 2041),
            new(7, May, 2041),
            new(6, June, 2041),
            new(15, August, 2041),
            new(9, September, 2041),
            new(10, September, 2041),
            new(11, September, 2041),
            new(3, October, 2041),
            new(9, October, 2041),
            new(25, December, 2041),
            new(31, December, 2041),
            new(1, January, 2042),
            new(21, January, 2042),
            new(22, January, 2042),
            new(23, January, 2042),
            new(3, March, 2042),
            new(1, May, 2042),
            new(5, May, 2042),
            new(26, May, 2042),
            new(6, June, 2042),
            new(15, August, 2042),
            new(29, September, 2042),
            new(30, September, 2042),
            new(3, October, 2042),
            new(9, October, 2042),
            new(25, December, 2042),
            new(31, December, 2042),
            new(1, January, 2043),
            new(9, February, 2043),
            new(10, February, 2043),
            new(11, February, 2043),
            new(2, March, 2043),
            new(1, May, 2043),
            new(5, May, 2043),
            new(18, May, 2043),
            new(17, August, 2043),
            new(16, September, 2043),
            new(17, September, 2043),
            new(18, September, 2043),
            new(5, October, 2043),
            new(9, October, 2043),
            new(25, December, 2043),
            new(31, December, 2043),
            new(1, January, 2044),
            new(29, January, 2044),
            new(1, February, 2044),
            new(1, March, 2044),
            new(5, May, 2044),
            new(6, May, 2044),
            new(6, June, 2044),
            new(15, August, 2044),
            new(3, October, 2044),
            new(4, October, 2044),
            new(5, October, 2044),
            new(6, October, 2044),
            new(10, October, 2044),
            new(26, December, 2044),
            new(30, December, 2044),
            new(16, February, 2045),
            new(17, February, 2045),
            new(1, March, 2045),
            new(1, May, 2045),
            new(5, May, 2045),
            new(24, May, 2045),
            new(6, June, 2045),
            new(15, August, 2045),
            new(25, September, 2045),
            new(26, September, 2045),
            new(27, September, 2045),
            new(3, October, 2045),
            new(9, October, 2045),
            new(25, December, 2045),
            new(29, December, 2045),
            new(1, January, 2046),
            new(5, February, 2046),
            new(6, February, 2046),
            new(7, February, 2046),
            new(1, March, 2046),
            new(1, May, 2046),
            new(7, May, 2046),
            new(14, May, 2046),
            new(6, June, 2046),
            new(15, August, 2046),
            new(14, September, 2046),
            new(17, September, 2046),
            new(3, October, 2046),
            new(9, October, 2046),
            new(25, December, 2046),
            new(31, December, 2046),
            new(1, January, 2047),
            new(25, January, 2047),
            new(28, January, 2047),
            new(1, March, 2047),
            new(1, May, 2047),
            new(2, May, 2047),
            new(6, May, 2047),
            new(6, June, 2047),
            new(15, August, 2047),
            new(3, October, 2047),
            new(4, October, 2047),
            new(7, October, 2047),
            new(9, October, 2047),
            new(25, December, 2047),
            new(31, December, 2047),
            new(1, January, 2048),
            new(13, February, 2048),
            new(14, February, 2048),
            new(2, March, 2048),
            new(1, May, 2048),
            new(5, May, 2048),
            new(20, May, 2048),
            new(17, August, 2048),
            new(21, September, 2048),
            new(22, September, 2048),
            new(23, September, 2048),
            new(5, October, 2048),
            new(9, October, 2048),
            new(25, December, 2048),
            new(31, December, 2048),
            new(1, January, 2049),
            new(1, February, 2049),
            new(2, February, 2049),
            new(3, February, 2049),
            new(1, March, 2049),
            new(5, May, 2049),
            new(10, May, 2049),
            new(16, August, 2049),
            new(10, September, 2049),
            new(13, September, 2049),
            new(4, October, 2049),
            new(11, October, 2049),
            new(27, December, 2049),
            new(31, December, 2049),
            new(24, January, 2050),
            new(25, January, 2050),
            new(1, March, 2050),
            new(5, May, 2050),
            new(30, May, 2050),
            new(6, June, 2050),
            new(15, August, 2050),
            new(29, September, 2050),
            new(30, September, 2050),
            new(3, October, 2050),
            new(10, October, 2050),
            new(26, December, 2050),
            new(30, December, 2050),
        ];

        Calendar c = new SouthKorea(SouthKoreaMarket.KRX);

        // Moved: the check of line 2843 on 2025, whose list misses the presidential election day, June 3rd, moves to
        // Divergences/SouthKoreaElection2025Tests.cs with upstream's line; the rest of the list stands.
        static bool Kept(Date d) => d.Year != 2025;
        CheckHolidays(
            c.HolidayList(new Date(1, January, 2004), new Date(31, December, 2050)).Where(Kept),
            expectedHol.Where(Kept));
    }

    [Fact]
    public void TestHongKong()
    {
        // Testing Hong Kong Stock Exchange holiday list...

        Calendar calendar = new HongKong(HongKongMarket.HKEx);
        Assert.Equal("Hong Kong stock exchange", calendar.Name);
        Assert.True(calendar.IsHoliday(new Date(6, January, 2024))); // Saturday
        Assert.True(calendar.IsHoliday(new Date(7, January, 2024))); // Sunday
        Assert.True(calendar.IsHoliday(new Date(1, January, 2024)));
        Assert.True(calendar.IsHoliday(new Date(29, March, 2024))); // Good Friday
        Assert.True(calendar.IsHoliday(new Date(1, April, 2024))); // Easter Monday
        Assert.True(calendar.IsHoliday(new Date(1, May, 2024)));
        Assert.True(calendar.IsHoliday(new Date(1, July, 2024)));
        Assert.True(calendar.IsHoliday(new Date(1, October, 2024)));
        Assert.True(calendar.IsHoliday(new Date(25, December, 2024)));
        Assert.True(calendar.IsHoliday(new Date(26, December, 2024)));
        Assert.True(calendar.IsBusinessDay(new Date(2, January, 2024)));

        // Historical exceptions and observed fixed holidays.
        Assert.True(calendar.IsHoliday(new Date(22, January, 2004)));
        Assert.True(calendar.IsHoliday(new Date(5, April, 2004)));
        Assert.True(calendar.IsHoliday(new Date(2, January, 2006))); // observed New Year

        Date[] expected =
        [
            new(1, January, 2026),  new(17, February, 2026), new(18, February, 2026),
            new(19, February, 2026), new(3, April, 2026),   new(6, April, 2026),
            new(7, April, 2026),   new(1, May, 2026),       new(25, May, 2026),
            new(19, June, 2026),   new(1, July, 2026),      new(1, October, 2026),
            new(19, October, 2026), new(25, December, 2026)
        ];
        CheckHolidays(calendar.HolidayList(new Date(1, January, 2026),
                                           new Date(31, December, 2026)), expected);
        Date[] historical =
        [
            new(23, January, 2004), new(26, May, 2004), new(9, February, 2005),
            new(5, May, 2006), new(17, February, 2007), new(12, May, 2008),
            new(4, April, 2009), new(21, May, 2010), new(3, February, 2011),
            new(10, May, 2012), new(17, May, 2013), new(31, January, 2014),
            new(19, February, 2015), new(8, February, 2016), new(3, May, 2017),
            new(16, February, 2018), new(5, February, 2019), new(30, April, 2020),
            new(12, February, 2021), new(1, February, 2022), new(5, April, 2023),
            new(15, May, 2024), new(5, May, 2025)
        ];
        foreach (Date date in historical)
            Assert.True(calendar.IsHoliday(date), $"{date}");
    }

    [Fact]
    public void TestArgentinaCalendar()
    {
        Calendar calendar = new Argentina();
        Assert.True(calendar.IsHoliday(new Date(1, January, 2024)));
        Assert.True(calendar.IsHoliday(new Date(28, March, 2024))); // Holy Thursday
        Assert.True(calendar.IsHoliday(new Date(29, March, 2024))); // Good Friday
        Assert.True(calendar.IsHoliday(new Date(17, June, 2024))); // Belgrano
        Assert.True(calendar.IsHoliday(new Date(19, August, 2024))); // San Martin
        Assert.True(calendar.IsHoliday(new Date(24, December, 2024)));
        Assert.True(calendar.IsBusinessDay(new Date(2, January, 2024)));
    }

    [Fact]
    public void TestIndonesiaCalendar()
    {
        Calendar calendar = new Indonesia(IndonesiaMarket.IDX);
        Assert.True(calendar.IsHoliday(new Date(1, January, 2024)));
        Assert.True(calendar.IsHoliday(new Date(29, March, 2024))); // Good Friday
        Assert.True(calendar.IsHoliday(new Date(1, May, 2024))); // Labour Day
        Assert.True(calendar.IsHoliday(new Date(1, June, 2024))); // Pancasila Day
        Assert.True(calendar.IsHoliday(new Date(17, August, 2024)));
        Assert.True(calendar.IsHoliday(new Date(25, December, 2024)));
        Assert.True(calendar.IsHoliday(new Date(21, January, 2005))); // Idul Adha
        Assert.True(calendar.IsBusinessDay(new Date(2, January, 2024)));
        Date[] historical =
        [
            new(9, February, 2005), new(11, March, 2005), new(24, May, 2005),
            new(10, January, 2006), new(30, March, 2006), new(24, October, 2006),
            new(19, March, 2007), new(20, December, 2007), new(7, February, 2008),
            new(30, September, 2008), new(26, January, 2009), new(18, September, 2009),
            new(3, February, 2011), new(23, January, 2012), new(24, January, 2013),
            new(14, January, 2014), new(19, February, 2015), new(8, February, 2016),
            new(2, January, 2017), new(16, February, 2018), new(5, February, 2019),
            new(25, March, 2020), new(12, February, 2021), new(1, February, 2022),
            new(23, January, 2023), new(8, February, 2024), new(27, January, 2025),
            new(16, January, 2026)
        ];
        foreach (Date date in historical)
            Assert.True(calendar.IsHoliday(date), $"{date}");
    }

    [Fact]
    public void TestTaiwanCalendar()
    {
        Calendar calendar = new Taiwan(TaiwanMarket.TSEC);
        Assert.True(calendar.IsHoliday(new Date(1, January, 2024)));
        Assert.True(calendar.IsHoliday(new Date(28, February, 2024)));
        Assert.True(calendar.IsHoliday(new Date(1, May, 2024)));
        Assert.True(calendar.IsHoliday(new Date(10, October, 2024)));
        Assert.True(calendar.IsHoliday(new Date(9, February, 2024))); // Lunar New Year
        Assert.True(calendar.IsHoliday(new Date(9, February, 2002)));
        Assert.True(calendar.IsBusinessDay(new Date(2, January, 2024)));
        Date[] historical =
        [
            new(4, June, 2003), new(21, January, 2004),
            new(9, February, 2005), new(5, April, 2006), new(19, February, 2007),
            new(7, February, 2008), new(26, January, 2009),
            new(13, January, 2010),
            new(2, February, 2011), new(23, January, 2012), new(11, February, 2013),
            new(31, January, 2014), new(18, February, 2015), new(8, February, 2016),
            new(27, January, 2017),
            new(16, February, 2018), new(5, February, 2019), new(27, January, 2020),
            new(12, February, 2021), new(1, February, 2022), new(23, January, 2023),
            new(27, January, 2025), new(16, February, 2026)
        ];
        foreach (Date date in historical)
            Assert.True(calendar.IsHoliday(date), $"{date}");
    }

    [Fact]
    public void TestThailandCalendar()
    {
        Calendar calendar = new Thailand();
        Assert.True(calendar.IsHoliday(new Date(1, January, 2024)));
        Assert.True(calendar.IsHoliday(new Date(15, April, 2019))); // Songkran
        Assert.True(calendar.IsBusinessDay(new Date(15, April, 2020))); // cancelled
        Assert.True(calendar.IsHoliday(new Date(4, May, 2020)));
        Assert.True(calendar.IsHoliday(new Date(3, June, 2024)));
        Assert.True(calendar.IsHoliday(new Date(29, July, 2024))); // substitution
        Assert.True(calendar.IsHoliday(new Date(14, October, 2024))); // substitution
        Assert.True(calendar.IsBusinessDay(new Date(2, January, 2024)));
        Date[] historical =
        [
            new(5, May, 2001),
            new(23, February, 2005), new(23, May, 2005), new(22, July, 2005),
            new(13, February, 2006), new(12, June, 2006), new(11, July, 2006),
            new(5, March, 2007), new(31, May, 2007), new(24, December, 2007),
            new(21, February, 2008), new(19, May, 2008), new(17, July, 2008),
            new(9, February, 2009), new(1, March, 2010), new(18, February, 2011),
            new(2, January, 2012), new(7, May, 2012), new(4, June, 2012),
            new(6, May, 2013), new(23, October, 2013),
            new(14, February, 2014), new(4, March, 2015), new(22, February, 2016),
            new(13, February, 2017), new(1, March, 2018), new(4, May, 2019),
            new(3, June, 2019), new(12, February, 2021), new(16, February, 2022),
            new(6, March, 2023)
        ];
        foreach (Date date in historical)
            Assert.True(calendar.IsHoliday(date), $"{date}");
    }

    [Fact]
    public void TestTurkeyCalendar()
    {
        Calendar calendar = new Turkey();
        Assert.Equal("Turkey", calendar.Name);
        Assert.True(calendar.IsHoliday(new Date(1, January, 2024)));
        Assert.True(calendar.IsHoliday(new Date(23, April, 2024)));
        Assert.True(calendar.IsHoliday(new Date(1, May, 2024)));
        Assert.True(calendar.IsHoliday(new Date(19, May, 2024)));
        Assert.True(calendar.IsHoliday(new Date(15, July, 2024)));
        Assert.True(calendar.IsHoliday(new Date(30, August, 2024)));
        Assert.True(calendar.IsHoliday(new Date(29, October, 2024)));
        Assert.True(calendar.IsBusinessDay(new Date(2, January, 2024)));

        Date[] historical =
        [
            new(1, February, 2004), new(19, January, 2005), new(10, January, 2006),
            new(2, January, 2007), new(30, September, 2008), new(20, September, 2009),
            new(9, September, 2010), new(9, November, 2011), new(18, August, 2012),
            new(7, August, 2013), new(27, July, 2014), new(17, July, 2015),
            new(5, July, 2016), new(25, June, 2017), new(15, June, 2018),
            new(4, June, 2019), new(24, May, 2020), new(13, May, 2021),
            new(2, May, 2022), new(21, April, 2023), new(10, April, 2024),
            new(31, March, 2025), new(20, March, 2026),
        ];
        foreach (Date date in historical)
        {
            Assert.True(calendar.IsHoliday(date), date.ToString());
        }
    }

    [Fact]
    public void TestBotswanaCalendar()
    {
        Calendar calendar = new Botswana();
        Assert.True(calendar.IsHoliday(new Date(1, May, 2024)));
        Assert.True(calendar.IsBusinessDay(new Date(2, May, 2024)));
    }

    [Fact]
    public void TestCzechRepublicCalendar()
    {
        Calendar calendar = new CzechRepublic(CzechRepublicMarket.PSE);
        Assert.True(calendar.IsHoliday(new Date(28, October, 2024)));
        Assert.True(calendar.IsBusinessDay(new Date(29, October, 2024)));
    }

    [Fact]
    public void TestFinlandCalendar()
    {
        Calendar calendar = new Finland();
        Assert.True(calendar.IsHoliday(new Date(6, December, 2024)));
        Assert.True(calendar.IsBusinessDay(new Date(5, December, 2024)));
    }

    [Fact]
    public void TestHungaryCalendar()
    {
        Calendar calendar = new Hungary();
        Assert.True(calendar.IsHoliday(new Date(15, March, 2024)));
        Assert.True(calendar.IsBusinessDay(new Date(14, March, 2024)));
    }

    [Fact]
    public void TestIcelandCalendar()
    {
        Calendar calendar = new Iceland(IcelandMarket.ICEX);
        Assert.True(calendar.IsHoliday(new Date(17, June, 2024)));
        Assert.True(calendar.IsBusinessDay(new Date(18, June, 2024)));
    }

    [Fact]
    public void TestNorwayCalendar()
    {
        Calendar calendar = new Norway();
        Assert.True(calendar.IsHoliday(new Date(17, May, 2024)));
        Assert.True(calendar.IsBusinessDay(new Date(16, May, 2024)));
    }

    [Fact]
    public void TestPolandCalendars()
    {
        Calendar settlement = new Poland(PolandMarket.Settlement);
        Calendar exchange = new Poland(PolandMarket.WSE);
        var holiday = new Date(6, January, 2025);
        Assert.True(settlement.IsHoliday(holiday));
        Assert.True(exchange.IsHoliday(holiday));
        Assert.True(settlement.IsBusinessDay(new Date(7, January, 2025)));
        Assert.True(exchange.IsBusinessDay(new Date(7, January, 2025)));
    }

    [Fact]
    public void TestSlovakiaCalendar()
    {
        Calendar calendar = new Slovakia(SlovakiaMarket.BSSE);
        Assert.True(calendar.IsHoliday(new Date(1, September, 2025)));
        Assert.True(calendar.IsBusinessDay(new Date(2, September, 2025)));
    }

    [Fact]
    public void TestSouthAfricaCalendar()
    {
        Calendar calendar = new SouthAfrica();
        Assert.True(calendar.IsHoliday(new Date(16, December, 2024)));
        Assert.True(calendar.IsBusinessDay(new Date(17, December, 2024)));
    }

    [Fact]
    public void TestSwedenCalendar()
    {
        Calendar calendar = new Sweden();
        Assert.True(calendar.IsHoliday(new Date(6, June, 2024)));
        Assert.True(calendar.IsBusinessDay(new Date(7, June, 2024)));
    }

    [Fact]
    public void TestUkraineCalendar()
    {
        Calendar calendar = new Ukraine(UkraineMarket.USE);
        Assert.True(calendar.IsHoliday(new Date(14, October, 2024)));
        Assert.True(calendar.IsBusinessDay(new Date(15, October, 2024)));
    }

    [Fact]
    public void TestSingaporeCalendar()
    {
        Calendar calendar = new Singapore();
        Assert.True(calendar.IsHoliday(new Date(1, January, 2024)));
        Assert.True(calendar.IsHoliday(new Date(29, March, 2024))); // Good Friday
        Assert.True(calendar.IsHoliday(new Date(1, May, 2024)));
        Assert.True(calendar.IsHoliday(new Date(9, August, 2024)));
        Assert.True(calendar.IsHoliday(new Date(25, December, 2024)));
        Assert.True(calendar.IsBusinessDay(new Date(2, January, 2024)));
    }

    [Fact]
    public void TestAustriaCalendars()
    {
        Calendar settlement = new Austria(AustriaMarket.Settlement);
        Calendar exchange = new Austria(AustriaMarket.Exchange);
        Assert.True(settlement.IsHoliday(new Date(1, January, 2024)));
        Assert.True(settlement.IsHoliday(new Date(1, May, 2024)));
        Assert.True(settlement.IsHoliday(new Date(26, October, 2024)));
        Assert.True(exchange.IsHoliday(new Date(29, March, 2024))); // Good Friday
        Assert.True(exchange.IsHoliday(new Date(24, December, 2024)));
        Assert.True(exchange.IsBusinessDay(new Date(2, January, 2024)));
    }

    [Fact]
    public void TestFranceCalendars()
    {
        Calendar settlement = new France(FranceMarket.Settlement);
        Calendar exchange = new France(FranceMarket.Exchange);
        Assert.True(settlement.IsHoliday(new Date(1, January, 2024)));
        Assert.True(settlement.IsHoliday(new Date(1, May, 2024)));
        Assert.True(settlement.IsHoliday(new Date(14, July, 2024)));
        Assert.True(exchange.IsHoliday(new Date(29, March, 2024))); // Good Friday
        Assert.True(exchange.IsHoliday(new Date(24, December, 2024)));
        Assert.True(exchange.IsBusinessDay(new Date(2, January, 2024)));
    }

    [Fact]
    public void TestRomaniaCalendars()
    {
        Calendar publicCalendar = new Romania(RomaniaMarket.Public);
        Calendar exchange = new Romania(RomaniaMarket.BVB);
        Assert.True(publicCalendar.IsHoliday(new Date(1, January, 2024)));
        Assert.True(publicCalendar.IsHoliday(new Date(24, January, 2024)));
        Assert.True(publicCalendar.IsHoliday(new Date(1, May, 2024)));
        Assert.True(publicCalendar.IsHoliday(new Date(1, June, 2024)));
        Assert.True(exchange.IsHoliday(new Date(1, January, 2014)));
        Assert.True(exchange.IsBusinessDay(new Date(3, January, 2024)));
    }

    [Fact]
    public void TestAustraliaCalendars()
    {
        Calendar settlement = new Australia(AustraliaMarket.Settlement);
        Calendar exchange = new Australia(AustraliaMarket.ASX);
        Assert.True(settlement.IsHoliday(new Date(1, January, 2024)));
        Assert.True(settlement.IsHoliday(new Date(26, January, 2024)));
        Assert.True(settlement.IsHoliday(new Date(25, April, 2024)));
        Assert.True(exchange.IsHoliday(new Date(29, March, 2024))); // Good Friday
        Assert.True(exchange.IsHoliday(new Date(10, June, 2024))); // King's Birthday
        Assert.True(exchange.IsBusinessDay(new Date(2, January, 2024)));
    }

    [Fact]
    public void TestCanadaCalendars()
    {
        Calendar settlement = new Canada(CanadaMarket.Settlement);
        Calendar exchange = new Canada(CanadaMarket.TSX);
        Assert.True(settlement.IsHoliday(new Date(1, January, 2024)));
        Assert.True(settlement.IsHoliday(new Date(19, February, 2024))); // Family Day
        Assert.True(settlement.IsHoliday(new Date(1, July, 2024)));
        Assert.True(exchange.IsHoliday(new Date(29, March, 2024))); // Good Friday
        Assert.True(exchange.IsHoliday(new Date(11, November, 2024)) == false);
        Assert.True(exchange.IsBusinessDay(new Date(2, January, 2024)));
    }

    [Fact]
    public void TestChileCalendar()
    {
        Calendar calendar = new Chile();
        Assert.True(calendar.IsHoliday(new Date(1, January, 2024)));
        Assert.True(calendar.IsHoliday(new Date(29, March, 2024))); // Good Friday
        Assert.True(calendar.IsHoliday(new Date(21, May, 2024)));
        Assert.True(calendar.IsHoliday(new Date(18, September, 2024)));
        Assert.True(calendar.IsHoliday(new Date(19, September, 2024)));
        Assert.True(calendar.IsBusinessDay(new Date(2, January, 2024)));
    }

    [Fact]
    public void TestSwitzerlandCalendar()
    {
        Calendar calendar = new Switzerland();
        Assert.True(calendar.IsHoliday(new Date(1, January, 2024)));
        Assert.True(calendar.IsHoliday(new Date(2, January, 2024)));
        Assert.True(calendar.IsHoliday(new Date(29, March, 2024))); // Good Friday
        Assert.True(calendar.IsHoliday(new Date(1, May, 2024)));
        Assert.True(calendar.IsHoliday(new Date(1, August, 2024)));
        Assert.True(calendar.IsBusinessDay(new Date(3, January, 2024)));
    }

    [Fact]
    public void TestChinaSSE()
    {
        // Testing China Shanghai Stock Exchange holiday list...

        Date[] expectedHol =
        [

            // China Shanghai Securities Exchange holiday list in the year 2014
            new(1, Jan, 2014),
            new(31, Jan, 2014),
            new(3, Feb, 2014),
            new(4, Feb, 2014),
            new(5, Feb, 2014),
            new(6, Feb, 2014),
            new(7, Apr, 2014),
            new(1, May, 2014),
            new(2, May, 2014),
            new(2, Jun, 2014),
            new(8, Sep, 2014),
            new(1, Oct, 2014),
            new(2, Oct, 2014),
            new(3, Oct, 2014),
            new(6, Oct, 2014),
            new(7, Oct, 2014),

            // China Shanghai Securities Exchange holiday list in the year 2015
            new(1, Jan, 2015),
            new(2, Jan, 2015),
            new(18, Feb, 2015),
            new(19, Feb, 2015),
            new(20, Feb, 2015),
            new(23, Feb, 2015),
            new(24, Feb, 2015),
            new(6, Apr, 2015),
            new(1, May, 2015),
            new(22, Jun, 2015),
            new(3, Sep, 2015),
            new(4, Sep, 2015),
            new(1, Oct, 2015),
            new(2, Oct, 2015),
            new(5, Oct, 2015),
            new(6, Oct, 2015),
            new(7, Oct, 2015),

            // China Shanghai Securities Exchange holiday list in the year 2016
            new(1, Jan, 2016),
            new(8, Feb, 2016),
            new(9, Feb, 2016),
            new(10, Feb, 2016),
            new(11, Feb, 2016),
            new(12, Feb, 2016),
            new(4, Apr, 2016),
            new(2, May, 2016),
            new(9, Jun, 2016),
            new(10, Jun, 2016),
            new(15, Sep, 2016),
            new(16, Sep, 2016),
            new(3, Oct, 2016),
            new(4, Oct, 2016),
            new(5, Oct, 2016),
            new(6, Oct, 2016),
            new(7, Oct, 2016),

            // China Shanghai Securities Exchange holiday list in the year 2017
            new(2, Jan, 2017),
            new(27, Jan, 2017),
            new(30, Jan, 2017),
            new(31, Jan, 2017),
            new(1, Feb, 2017),
            new(2, Feb, 2017),
            new(3, April, 2017),
            new(4, April, 2017),
            new(1, May, 2017),
            new(29, May, 2017),
            new(30, May, 2017),
            new(2, Oct, 2017),
            new(3, Oct, 2017),
            new(4, Oct, 2017),
            new(5, Oct, 2017),
            new(6, Oct, 2017),

            // China Shanghai Securities Exchange holiday list in the year 2018
            new(1, Jan, 2018),
            new(15, Feb, 2018),
            new(16, Feb, 2018),
            new(19, Feb, 2018),
            new(20, Feb, 2018),
            new(21, Feb, 2018),
            new(5, April, 2018),
            new(6, April, 2018),
            new(30, April, 2018),
            new(1, May, 2018),
            new(18, June, 2018),
            new(24, September, 2018),
            new(1, Oct, 2018),
            new(2, Oct, 2018),
            new(3, Oct, 2018),
            new(4, Oct, 2018),
            new(5, Oct, 2018),
            new(31, December, 2018),

            // China Shanghai Securities Exchange holiday list in the year 2019
            new(1, Jan, 2019),
            new(4, Feb, 2019),
            new(5, Feb, 2019),
            new(6, Feb, 2019),
            new(7, Feb, 2019),
            new(8, Feb, 2019),
            new(5, April, 2019),
            new(1, May, 2019),
            new(2, May, 2019),
            new(3, May, 2019),
            new(7, June, 2019),
            new(13, September, 2019),
            new(1, October, 2019),
            new(2, October, 2019),
            new(3, October, 2019),
            new(4, October, 2019),
            new(7, October, 2019),

            // China Shanghai Securities Exchange holiday list in the year 2020
            new(1, Jan, 2020),
            new(24, Jan, 2020),
            new(27, Jan, 2020),
            new(28, Jan, 2020),
            new(29, Jan, 2020),
            new(30, Jan, 2020),
            new(31, Jan, 2020),
            new(6, April, 2020),
            new(1, May, 2020),
            new(4, May, 2020),
            new(5, May, 2020),
            new(25, June, 2020),
            new(26, June, 2020),
            new(1, October, 2020),
            new(2, October, 2020),
            new(5, October, 2020),
            new(6, October, 2020),
            new(7, October, 2020),
            new(8, October, 2020),

            // China Shanghai Securities Exchange holiday list in the year 2021
            new(1, Jan, 2021),
            new(11, Feb, 2021),
            new(12, Feb, 2021),
            new(15, Feb, 2021),
            new(16, Feb, 2021),
            new(17, Feb, 2021),
            new(5, April, 2021),
            new(3, May, 2021),
            new(4, May, 2021),
            new(5, May, 2021),
            new(14, June, 2021),
            new(20, September, 2021),
            new(21, September, 2021),
            new(1, October, 2021),
            new(4, October, 2021),
            new(5, October, 2021),
            new(6, October, 2021),
            new(7, October, 2021),

            // China Shanghai Securities Exchange holiday list in the year 2022
            new(3, Jan, 2022),
            new(31, Jan, 2022),
            new(1, Feb, 2022),
            new(2, Feb, 2022),
            new(3, Feb, 2022),
            new(4, Feb, 2022),
            new(4, April, 2022),
            new(5, April, 2022),
            new(2, May, 2022),
            new(3, May, 2022),
            new(4, May, 2022),
            new(3, June, 2022),
            new(12, September, 2022),
            new(3, October, 2022),
            new(4, October, 2022),
            new(5, October, 2022),
            new(6, October, 2022),
            new(7, October, 2022),

            // China Shanghai Securities Exchange holiday list in the year 2023
            new(2, Jan, 2023),
            new(23, Jan, 2023),
            new(24, Jan, 2023),
            new(25, Jan, 2023),
            new(26, Jan, 2023),
            new(27, Jan, 2023),
            new(5, April, 2023),
            new(1, May, 2023),
            new(2, May, 2023),
            new(3, May, 2023),
            new(22, June, 2023),
            new(23, June, 2023),
            new(29, September, 2023),
            new(2, October, 2023),
            new(3, October, 2023),
            new(4, October, 2023),
            new(5, October, 2023),
            new(6, October, 2023),

            // China Shanghai Securities Exchange holiday list in the year 2024
            new(1, Jan, 2024),
            new(9, Feb, 2024),
            new(12, Feb, 2024),
            new(13, Feb, 2024),
            new(14, Feb, 2024),
            new(15, Feb, 2024),
            new(16, Feb, 2024),
            new(4, April, 2024),
            new(5, April, 2024),
            new(1, May, 2024),
            new(2, May, 2024),
            new(3, May, 2024),
            new(10, Jun, 2024),
            new(16, Sep, 2024),
            new(17, Sep, 2024),
            new(1, Oct, 2024),
            new(2, Oct, 2024),
            new(3, Oct, 2024),
            new(4, Oct, 2024),
            new(7, Oct, 2024),

            // China Shanghai Securities Exchange holiday list in the year 2025
            new(1, Jan, 2025),
            new(28, Jan, 2025),
            new(29, Jan, 2025),
            new(30, Jan, 2025),
            new(31, Jan, 2025),
            new(3, Feb, 2025),
            new(4, Feb, 2025),
            new(4, April, 2025),
            new(1, May, 2025),
            new(2, May, 2025),
            new(5, May, 2025),
            new(2, Jun, 2025),
            new(1, Oct, 2025),
            new(2, Oct, 2025),
            new(3, Oct, 2025),
            new(6, Oct, 2025),
            new(7, Oct, 2025),
            new(8, Oct, 2025),

            // China Shanghai Securities Exchange holiday list in the year 2026
            new(1, Jan, 2026),
            new(2, Jan, 2026),
            new(16, Feb, 2026),
            new(17, Feb, 2026),
            new(18, Feb, 2026),
            new(19, Feb, 2026),
            new(20, Feb, 2026),
            new(23, Feb, 2026),
            new(6, April, 2026),
            new(1, May, 2026),
            new(4, May, 2026),
            new(5, May, 2026),
            new(19, Jun, 2026),
            new(25, Sep, 2026),
            new(1, Oct, 2026),
            new(2, Oct, 2026),
            new(5, Oct, 2026),
            new(6, Oct, 2026),
            new(7, Oct, 2026),
        ];

        Calendar c = new China(ChinaMarket.SSE);
        CheckHolidays(c.HolidayList(new Date(1, January, 2014), new Date(31, December, 2026)), expectedHol);
    }

    [Fact]
    public void TestChinaIB()
    {
        // Testing China Inter Bank working weekends list...

        Date[] expectedWorkingWeekEnds =
        [

            // China Inter Bank working weekends list in the year 2014
            new(26, Jan, 2014),
            new(8, Feb, 2014),
            new(4, May, 2014),
            new(28, Sep, 2014),
            new(11, Oct, 2014),

            // China Inter Bank working weekends list in the year 2015
            new(4, Jan, 2015),
            new(15, Feb, 2015),
            new(28, Feb, 2015),
            new(6, Sep, 2015),
            new(10, Oct, 2015),

            // China Inter Bank working weekends list in the year 2016
            new(6, Feb, 2016),
            new(14, Feb, 2016),
            new(12, Jun, 2016),
            new(18, Sep, 2016),
            new(8, Oct, 2016),
            new(9, Oct, 2016),

            // China Inter Bank working weekends list in the year 2017
            new(22, Jan, 2017),
            new(4, Feb, 2017),
            new(1, April, 2017),
            new(27, May, 2017),
            new(30, Sep, 2017),

            // China Inter Bank working weekends list in the year 2018
            new(11, Feb, 2018),
            new(24, Feb, 2018),
            new(8, April, 2018),
            new(28, April, 2018),
            new(29, Sep, 2018),
            new(30, Sep, 2018),
            new(29, December, 2018),

            // China Inter Bank working weekends list in the year 2019
            new(2, Feb, 2019),
            new(3, Feb, 2019),
            new(28, April, 2019),
            new(5, May, 2019),
            new(29, September, 2019),
            new(12, October, 2019),

            // China Inter Bank working weekends list in the year 2020
            new(19, January, 2020),
            new(26, April, 2020),
            new(9, May, 2020),
            new(28, June, 2020),
            new(27, September, 2020),
            new(10, October, 2020),

            // China Inter Bank working weekends list in the year 2021
            new(7, Feb, 2021),
            new(20, Feb, 2021),
            new(25, April, 2021),
            new(8, May, 2021),
            new(18, September, 2021),
            new(26, September, 2021),
            new(9, October, 2021),

            // China Inter Bank working weekends list in the year 2022
            new(29, Jan, 2022),
            new(30, Jan, 2022),
            new(2, April, 2022),
            new(24, April, 2022),
            new(7, May, 2022),
            new(8, October, 2022),
            new(9, October, 2022),

            // China Inter Bank working weekends list in the year 2023
            new(28, Jan, 2023),
            new(29, Jan, 2023),
            new(23, April, 2023),
            new(6, May, 2023),
            new(25, June, 2023),
            new(7, October, 2023),
            new(8, October, 2023),

            // China Inter Bank working weekends list in the year 2024
            new(4, Feb, 2024),
            new(18, Feb, 2024),
            new(7, April, 2024),
            new(28, April, 2024),
            new(11, May, 2024),
            new(14, Sep, 2024),
            new(29, Sep, 2024),
            new(12, October, 2024),

            // China Inter Bank working weekends list in the year 2025
            new(26, Jan, 2025),
            new(8, Feb, 2025),
            new(27, Apr, 2025),
            new(28, Sep, 2025),
            new(11, Oct, 2025),

            // China Inter Bank working weekends list in the year 2026
            new(4, Jan, 2026),
            new(14, Feb, 2026),
            new(28, Feb, 2026),
            new(9, May, 2026),
            new(20, Sep, 2026),
            new(10, Oct, 2026),
        ];

        Calendar c = new China(ChinaMarket.IB);
        var start = new Date(1, Jan, 2014);
        var end = new Date(31, Dec, 2026);

        int k = 0;

        while (start <= end)
        {
            if (c.IsBusinessDay(start) && c.IsWeekend(start.Weekday))
            {
                if (expectedWorkingWeekEnds[k] != start)
                {
                    Assert.Fail(
                        $"expected working weekend was {expectedWorkingWeekEnds[k]} while calculated working weekend is "
                        + $"{start}");
                }

                ++k;
            }

            ++start;
        }

        if (k != expectedWorkingWeekEnds.Length)
        {
            Assert.Fail(
                $"there were {expectedWorkingWeekEnds.Length} expected working weekends, while there are {k} calculated "
                + "working weekends");
        }
    }

    [Fact]
    public void TestMexicoInaugurationDay()
    {
        // Testing Mexican Inauguration Day holiday...

        // The first five Inauguration Days 2024 and later
        Date[] expectedHol =
        [
            new(1, Oct, 2024),
            new(1, Oct, 2030),
            new(1, Oct, 2036),
            new(1, Oct, 2042),
            new(1, Oct, 2048),
        ];

        // Some years of non-Inaugurations
        Date[] expectedWorkingDays =
        [
            new(1, Oct, 2018),
            new(1, Oct, 2025),
            new(1, Oct, 2026),
            new(1, Oct, 2027),
            // 2028 falls on a weekend
            new(1, Oct, 2029),
            new(1, Oct, 2031),
            new(1, Oct, 2032),
            // 2033 and 2034 fall on weekends
            new(1, Oct, 2035),
        ];

        Calendar mexico = new Mexico();
        foreach (Date holiday in expectedHol)
        {
            if (!mexico.IsHoliday(holiday))
            {
                Assert.Fail($"Expected to have an Inauguration Day holiday in the Mexican calendar for date {holiday}");
            }
        }

        foreach (Date workingDay in expectedWorkingDays)
        {
            if (!mexico.IsBusinessDay(workingDay))
            {
                Assert.Fail($"Did not expect to have a holiday in the Mexican calendar for date {workingDay}");
            }
        }
    }

    [Fact]
    public void TestNewZealand()
    {
        // Testing a few holiday rules for New Zealand...

        var auckland = new NewZealand(NewZealandMarket.Auckland);
        var wellington = new NewZealand(NewZealandMarket.Wellington);

        foreach (NewZealand calendar in new[] { auckland, wellington })
        {
            // mid-week New Year's day
            Assert.True(calendar.IsHoliday(new Date(1, January, 2025)));
            Assert.True(calendar.IsHoliday(new Date(2, January, 2025)));
            Assert.True(calendar.IsBusinessDay(new Date(3, January, 2025)));
            // New Year's day on Sunday
            Assert.True(calendar.IsHoliday(new Date(1, January, 2023)));
            Assert.True(calendar.IsHoliday(new Date(2, January, 2023)));
            Assert.True(calendar.IsHoliday(new Date(3, January, 2023)));
            Assert.True(calendar.IsBusinessDay(new Date(4, January, 2023)));
            // New Year's day on Saturday
            Assert.True(calendar.IsHoliday(new Date(1, January, 2022)));
            Assert.True(calendar.IsHoliday(new Date(2, January, 2022)));
            Assert.True(calendar.IsHoliday(new Date(3, January, 2022)));
            Assert.True(calendar.IsHoliday(new Date(4, January, 2022)));
            Assert.True(calendar.IsBusinessDay(new Date(5, January, 2022)));
            // New Year's day on Friday
            Assert.True(calendar.IsHoliday(new Date(1, January, 2027)));
            Assert.True(calendar.IsHoliday(new Date(2, January, 2027)));
            Assert.True(calendar.IsHoliday(new Date(3, January, 2027)));
            Assert.True(calendar.IsHoliday(new Date(4, January, 2027)));
            Assert.True(calendar.IsBusinessDay(new Date(5, January, 2027)));

            // mid-week Christmas day
            Assert.True(calendar.IsHoliday(new Date(25, December, 2024)));
            Assert.True(calendar.IsHoliday(new Date(26, December, 2024)));
            Assert.True(calendar.IsBusinessDay(new Date(27, December, 2024)));
            // Christmas day on Sunday
            Assert.True(calendar.IsHoliday(new Date(25, December, 2022)));
            Assert.True(calendar.IsHoliday(new Date(26, December, 2022)));
            Assert.True(calendar.IsHoliday(new Date(27, December, 2022)));
            Assert.True(calendar.IsBusinessDay(new Date(28, December, 2022)));
            // Christmas day on Saturday
            Assert.True(calendar.IsHoliday(new Date(25, December, 2021)));
            Assert.True(calendar.IsHoliday(new Date(26, December, 2021)));
            Assert.True(calendar.IsHoliday(new Date(27, December, 2021)));
            Assert.True(calendar.IsHoliday(new Date(28, December, 2021)));
            Assert.True(calendar.IsBusinessDay(new Date(29, December, 2021)));
            // Christmas day on Friday
            Assert.True(calendar.IsHoliday(new Date(25, December, 2026)));
            Assert.True(calendar.IsHoliday(new Date(26, December, 2026)));
            Assert.True(calendar.IsHoliday(new Date(27, December, 2026)));
            Assert.True(calendar.IsHoliday(new Date(28, December, 2026)));
            Assert.True(calendar.IsBusinessDay(new Date(29, December, 2026)));

            // Waitangi Day is moved to Monday but only since 2013
            Assert.True(calendar.IsHoliday(new Date(8, February, 2021)));
            Assert.True(calendar.IsHoliday(new Date(7, February, 2022)));
            Assert.True(calendar.IsBusinessDay(new Date(8, February, 2010)));
            Assert.True(calendar.IsBusinessDay(new Date(7, February, 2011)));

            // The same goes for ANZAC Day
            Assert.True(calendar.IsHoliday(new Date(27, April, 2020)));
            Assert.True(calendar.IsHoliday(new Date(26, April, 2021)));
            Assert.True(calendar.IsBusinessDay(new Date(27, April, 2009)));
            Assert.True(calendar.IsBusinessDay(new Date(26, April, 2010)));
        }

        // different Anniversary Day for the two calendars
        Assert.True(auckland.IsBusinessDay(new Date(22, January, 2024)));
        Assert.True(wellington.IsHoliday(new Date(22, January, 2024)));
        Assert.True(auckland.IsHoliday(new Date(29, January, 2024)));
        Assert.True(wellington.IsBusinessDay(new Date(29, January, 2024)));
        Assert.True(auckland.IsBusinessDay(new Date(19, January, 2026)));
        Assert.True(wellington.IsHoliday(new Date(19, January, 2026)));
        Assert.True(auckland.IsHoliday(new Date(26, January, 2026)));
        Assert.True(wellington.IsBusinessDay(new Date(26, January, 2026)));
        Assert.True(auckland.IsBusinessDay(new Date(25, January, 2027)));
        Assert.True(wellington.IsHoliday(new Date(25, January, 2027)));
        Assert.True(auckland.IsHoliday(new Date(1, February, 2027)));
        Assert.True(wellington.IsBusinessDay(new Date(1, February, 2027)));
    }

    [Fact]
    public void TestTASECalendar()
    {
        // Testing TASE calendar...

        Calendar c = new Israel(IsraelMarket.TASE);

        static bool IsWeekend(Date d) =>
            d >= new Date(5, January, 2026)
                ? d.Weekday == Weekday.Saturday || d.Weekday == Weekday.Sunday
                : d.Weekday == Weekday.Friday || d.Weekday == Weekday.Saturday;

        // using checkHolidays with includeWeekEnds = false doesn't work in this case;
        // weekdays changed in 2026.

        Date[] expected2013 =
        [
            new(24, February, 2013),  new(25, March, 2013),     new(26, March, 2013),     new(31, March, 2013),
            new(1, April, 2013),      new(15, April, 2013),     new(16, April, 2013),     new(14, May, 2013),
            new(15, May, 2013),       new(16, July, 2013),      new(4, September, 2013),  new(5, September, 2013),
            new(18, September, 2013), new(19, September, 2013), new(25, September, 2013), new(26, September, 2013),
        ];

        Date[] holidays =
            [.. c.HolidayList(new Date(1, January, 2013), new Date(31, December, 2013), true).Where(d => !IsWeekend(d))];
        CheckHolidays(holidays, expected2013);

        Date[] expected2025 =
        [
            new(13, April, 2025),   new(30, April, 2025),     new(1, May, 2025),        new(2, June, 2025),
            new(3, August, 2025),   new(23, September, 2025), new(24, September, 2025), new(1, October, 2025),
            new(2, October, 2025),  new(6, October, 2025),    new(7, October, 2025),    new(13, October, 2025),
            new(14, October, 2025),
        ];

        holidays =
            [.. c.HolidayList(new Date(1, January, 2025), new Date(31, December, 2025), true).Where(d => !IsWeekend(d))];
        CheckHolidays(holidays, expected2025);
    }

    [Fact]
    public void TestSHIRCalendar()
    {
        // Testing SHIR calendar...

        Date[] expected =
        [
            new Date(5, May, 2022),        new Date(3, June, 2022),       new Date(26, September, 2022),
            new Date(27, September, 2022), new Date(4, October, 2022),    new Date(5, October, 2022),
            new Date(10, October, 2022),   new Date(17, October, 2022),   new Date(1, November, 2022),
            new Date(26, December, 2022),  new Date(2, January, 2023),    new Date(7, March, 2023),
            new Date(8, March, 2023),      new Date(5, April, 2023),      new Date(6, April, 2023),
            new Date(7, April, 2023),      new Date(10, April, 2023),     new Date(12, April, 2023),
            new Date(26, April, 2023),     new Date(26, May, 2023),       new Date(29, May, 2023),
            new Date(27, July, 2023),      new Date(15, September, 2023), new Date(25, September, 2023),
            new Date(25, December, 2023),  new Date(26, December, 2023),  new Date(1, January, 2024),
            new Date(27, February, 2024),  new Date(25, March, 2024),     new Date(29, March, 2024),
            new Date(22, April, 2024),     new Date(23, April, 2024),     new Date(29, April, 2024),
            new Date(14, May, 2024),       new Date(27, May, 2024),       new Date(12, June, 2024),
            new Date(13, August, 2024),    new Date(2, October, 2024),    new Date(3, October, 2024),
            new Date(4, October, 2024),    new Date(11, October, 2024),   new Date(17, October, 2024),
            new Date(24, October, 2024),   new Date(25, December, 2024),  new Date(26, December, 2024),
            new Date(1, January, 2025),    new Date(14, March, 2025),     new Date(18, April, 2025),
            new Date(1, May, 2025),        new Date(26, May, 2025),       new Date(2, June, 2025),
            new Date(22, September, 2025), new Date(23, September, 2025), new Date(24, September, 2025),
            new Date(1, October, 2025),    new Date(2, October, 2025),    new Date(7, October, 2025),
            new Date(14, October, 2025),   new Date(25, December, 2025),  new Date(26, December, 2025),
        ];

        Calendar c = new Israel(IsraelMarket.SHIR);
        CheckHolidays(c.HolidayList(new Date(5, May, 2022), new Date(31, December, 2025)), expected);
    }

    [Fact]
    public void TestTelborCalendar()
    {
        // Testing Telbor calendar...

        Date[] expected =
        [
            new(5, May, 2022),        new(30, May, 2022),       new(26, September, 2022), new(27, September, 2022),
            new(5, October, 2022),    new(10, October, 2022),   new(17, October, 2022),   new(26, December, 2022),
            new(7, March, 2023),      new(8, March, 2023),      new(5, April, 2023),      new(6, April, 2023),
            new(12, April, 2023),     new(26, April, 2023),     new(26, May, 2023),       new(29, May, 2023),
            new(27, July, 2023),      new(25, September, 2023), new(25, December, 2023),  new(26, December, 2023),
            new(1, January, 2024),    new(25, March, 2024),     new(22, April, 2024),     new(23, April, 2024),
            new(29, April, 2024),     new(14, May, 2024),       new(27, May, 2024),       new(12, June, 2024),
            new(13, August, 2024),    new(3, October, 2024),    new(4, October, 2024),    new(17, October, 2024),
            new(24, October, 2024),   new(25, December, 2024),  new(26, December, 2024),  new(1, January, 2025),
            new(14, March, 2025),     new(1, May, 2025),        new(26, May, 2025),       new(2, June, 2025),
            new(23, September, 2025), new(24, September, 2025), new(2, October, 2025),    new(7, October, 2025),
            new(14, October, 2025),   new(25, December, 2025),  new(26, December, 2025),
        ];

        Calendar c = new Israel(IsraelMarket.Telbor);
        CheckHolidays(c.HolidayList(new Date(5, May, 2022), new Date(31, December, 2025)), expected);
    }

    [Fact]
    public void TestStartOfMonth()
    {
        Calendar c = new TARGET(); // any calendar would be OK

        Date som, counter = Date.MinDate + (2 * TimeUnit.Months);
        Date last = Date.MaxDate;

        while (counter < last)
        {
            som = c.StartOfMonth(counter);

            // check that som is som
            if (!c.IsStartOfMonth(som))
            {
                Assert.Fail($"\n  {som.Weekday} {som} is not the first business day in {DateFunctions.ToString(som.Month)} "
                            + $"{som.Year} according to {c.Name}");
            }

            // check that som is in the same month as counter
            if (som.Month != counter.Month)
            {
                Assert.Fail($"\n  {som} is not in the same month as {counter}");
            }

            // Check that previous business day is in a different month
            if (c.Advance(som, -1, TimeUnit.Days, BusinessDayConvention.Unadjusted).Month == som.Month)
            {
                Assert.Fail($"\n  {c.Advance(som, -1, TimeUnit.Days, BusinessDayConvention.Unadjusted)} is in the same month as {som}");
            }

            counter = counter + 1;
        }
    }

    [Fact]
    public void TestEndOfMonth()
    {
        Calendar c = new TARGET(); // any calendar would be OK

        Date eom, counter = Date.MinDate;
        Date last = Date.MaxDate - (2 * TimeUnit.Months);

        while (counter <= last)
        {
            eom = c.EndOfMonth(counter);

            // check that eom is eom
            if (!c.IsEndOfMonth(eom))
            {
                Assert.Fail($"\n  {eom.Weekday} {eom} is not the last business day in {DateFunctions.ToString(eom.Month)} "
                            + $"{eom.Year} according to {c.Name}");
            }

            // check that eom is in the same month as counter
            if (eom.Month != counter.Month)
            {
                Assert.Fail($"\n  {eom} is not in the same month as {counter}");
            }

            // Check that next business day is in a different month
            if (c.Advance(eom, 1, TimeUnit.Days, BusinessDayConvention.Unadjusted).Month == eom.Month)
            {
                Assert.Fail($"\n  {c.Advance(eom, 1, TimeUnit.Days, BusinessDayConvention.Unadjusted)} is in the same month as {eom}");
            }

            counter = counter + 1;
        }
    }

    [Fact]
    public void TestBusinessDaysBetween()
    {
        // Testing calculation of business days between dates...
        Date[] testDates =
        [
            new(1, February, 2002), // isBusinessDay = true
            new(4, February, 2002), // isBusinessDay = true
            new(16, May, 2003), // isBusinessDay = true
            new(17, December, 2003), // isBusinessDay = true
            new(17, December, 2004), // isBusinessDay = true
            new(19, December, 2005), // isBusinessDay = true
            new(2, January, 2006), // isBusinessDay = true
            new(13, March, 2006), // isBusinessDay = true
            new(15, May, 2006), // isBusinessDay = true
            new(17, March, 2006), // isBusinessDay = true
            new(15, May, 2006), // isBusinessDay = true
            new(26, July, 2006), // isBusinessDay = true
            new(26, July, 2006), // isBusinessDay = true
            new(27, July, 2006), // isBusinessDay = true
            new(29, July, 2006), // isBusinessDay = false
            new(29, July, 2006), // isBusinessDay = false
        ];

        // default params: from date included, to excluded
        int[] expected = [1, 321, 152, 251, 252, 10, 48, 42, -38, 38, 51, 0, 1, 2, 0];

        // exclude from, include to
        int[] expectedIncludeTo = [1, 321, 152, 251, 252, 10, 48, 42, -38, 38, 51, 0, 1, 1, 0];

        // include both from and to
        int[] expectedIncludeAll = [2, 322, 153, 252, 253, 11, 49, 43, -39, 39, 52, 1, 2, 2, 0];

        // exclude both from and to
        int[] expectedExcludeAll = [0, 320, 151, 250, 251, 9, 47, 41, -37, 37, 50, 0, 0, 1, 0];

        Calendar calendar = new Brazil();

        for (int i = 1; i < testDates.Length; i++)
        {
            int calculated = calendar.BusinessDaysBetween(testDates[i - 1], testDates[i], true, false);
            Assert.True(
                calculated == expected[i - 1],
                $"from {testDates[i - 1]} included to {testDates[i]} excluded:\n"
                + $"    calculated: {calculated}\n    expected:   {expected[i - 1]}");

            calculated = calendar.BusinessDaysBetween(testDates[i - 1], testDates[i], false, true);
            Assert.True(
                calculated == expectedIncludeTo[i - 1],
                $"from {testDates[i - 1]} excluded to {testDates[i]} included:\n"
                + $"    calculated: {calculated}\n    expected:   {expectedIncludeTo[i - 1]}");

            calculated = calendar.BusinessDaysBetween(testDates[i - 1], testDates[i], true, true);
            Assert.True(
                calculated == expectedIncludeAll[i - 1],
                $"from {testDates[i - 1]} included to {testDates[i]} included:\n"
                + $"    calculated: {calculated}\n    expected:   {expectedIncludeAll[i - 1]}");

            calculated = calendar.BusinessDaysBetween(testDates[i - 1], testDates[i], false, false);
            Assert.True(
                calculated == expectedExcludeAll[i - 1],
                $"from {testDates[i - 1]} excluded to {testDates[i]} excluded:\n"
                + $"    calculated: {calculated}\n    expected:   {expectedExcludeAll[i - 1]}");
        }
    }

    // Adapted: a bespoke calendar's edits return a modified copy, so a2, QuantLib's copy "linked to a1" (line 3973),
    // is a1 with the edits made through it, and a1 doesn't see them. Kept: every check of a1 before the copy and of a2
    // after each edit. Dropped: lines 3977-3984 and 3997-4004, which check that a1 sees a2's edits;
    // Conventions/BespokeCalendarValueTests.cs tests nanoql's behaviour in their place.
    [Fact]
    public void TestBespokeCalendars()
    {
        // Testing bespoke calendars...
        var a1 = new BespokeCalendar();
        var b1 = new BespokeCalendar();

        Date testDate1 = new(4, October, 2008); // Saturday
        Date testDate2 = new(5, October, 2008); // Sunday
        Date testDate3 = new(6, October, 2008); // Monday
        Date testDate4 = new(7, October, 2008); // Tuesday

        CheckBusinessDay(a1, testDate1);
        CheckBusinessDay(a1, testDate2);
        CheckBusinessDay(a1, testDate3);
        CheckBusinessDay(a1, testDate4);

        CheckBusinessDay(b1, testDate1);
        CheckBusinessDay(b1, testDate2);
        CheckBusinessDay(b1, testDate3);
        CheckBusinessDay(b1, testDate4);

        a1 = a1.AddWeekend(Weekday.Sunday);

        CheckBusinessDay(a1, testDate1);
        CheckHoliday(a1, testDate2, "(Sunday) not detected as weekend");
        CheckBusinessDay(a1, testDate3);
        CheckBusinessDay(a1, testDate4);

        CheckBusinessDay(b1, testDate1);
        CheckBusinessDay(b1, testDate2);
        CheckBusinessDay(b1, testDate3);
        CheckBusinessDay(b1, testDate4);

        a1 = (BespokeCalendar)a1.AddHoliday(testDate3);

        CheckBusinessDay(a1, testDate1);
        CheckHoliday(a1, testDate2, "(Sunday) not detected as weekend");
        CheckHoliday(a1, testDate3, "(marked as holiday) not detected");
        CheckBusinessDay(a1, testDate4);

        CheckBusinessDay(b1, testDate1);
        CheckBusinessDay(b1, testDate2);
        CheckBusinessDay(b1, testDate3);
        CheckBusinessDay(b1, testDate4);

        BespokeCalendar a2 = a1;

        a2 = a2.AddWeekend(Weekday.Saturday);

        CheckHoliday(a2, testDate1, "(Saturday) not detected as weekend");
        CheckHoliday(a2, testDate2, "(Sunday) not detected as weekend");
        CheckHoliday(a2, testDate3, "(marked as holiday) not detected");
        CheckBusinessDay(a2, testDate4);

        a2 = (BespokeCalendar)a2.AddHoliday(testDate4);

        CheckHoliday(a2, testDate1, "(Saturday) not detected as weekend");
        CheckHoliday(a2, testDate2, "(Sunday) not detected as weekend");
        CheckHoliday(a2, testDate3, "(marked as holiday) not detected");
        CheckHoliday(a2, testDate4, "(marked as holiday) not detected");

        static void CheckBusinessDay(Calendar c, Date d) =>
            Assert.True(c.IsBusinessDay(d), $"{d} erroneously detected as holiday");

        static void CheckHoliday(Calendar c, Date d, string what) => Assert.False(c.IsBusinessDay(d), $"{d} {what}");
    }

    [Fact]
    public void TestDayLists()
    {
        // Testing holidayList and businessDaysList...
        Calendar germany = new Germany();
        Date firstDate = RunnerDate, endDate = firstDate + (1 * TimeUnit.Years);

        // Test that a crossed range returns an empty vector
        Assert.Empty(germany.HolidayList(endDate, firstDate, true));
        Assert.Empty(germany.BusinessDayList(endDate, firstDate));

        // Test that the range is inclusive on both sides
        Assert.Equal(germany.IsHoliday(firstDate) ? 1 : 0, germany.HolidayList(firstDate, firstDate, true).Length);
        Assert.Equal(germany.IsBusinessDay(firstDate) ? 1 : 0, germany.BusinessDayList(firstDate, firstDate).Length);

        ImmutableArray<Date> holidays = germany.HolidayList(firstDate, endDate, true);
        ImmutableArray<Date> businessDays = germany.BusinessDayList(firstDate, endDate);

        int itHolidays = 0, itBusinessDays = 0;
        for (Date d = firstDate; d <= endDate; d++)
        {
            if (itHolidays != holidays.Length && itBusinessDays != businessDays.Length
                && d == holidays[itHolidays] && d == businessDays[itBusinessDays])
            {
                Assert.Fail($"Date {d}is both holiday and business day.");
            }
            else if (itHolidays != holidays.Length && d == holidays[itHolidays])
            {
                ++itHolidays;
            }
            else if (itBusinessDays != businessDays.Length && d == businessDays[itBusinessDays])
            {
                ++itBusinessDays;
            }
            else
            {
                Assert.Fail($"Date {d}is neither holiday nor business day.");
            }
        }
    }

    [Fact]
    public void TestMalta()
    {
        // Testing Malta holiday list...

        Date[] expectedHol =
        [
            new(1, January, 2026),
            new(10, February, 2026),
            new(19, March, 2026),
            new(31, March, 2026),
            new(6, April, 2026),
            new(29, June, 2026),
            new(8, September, 2026),
            new(21, September, 2026),
            new(8, December, 2026),
            new(13, December, 2026),
            new(24, December, 2026),
            new(31, December, 2026),

            new(10, February, 2027),
            new(29, March, 2027),
            new(31, March, 2027),
            new(29, June, 2027),
            new(15, August, 2027),
            new(8, September, 2027),
            new(21, September, 2027),
            new(8, December, 2027),
            new(13, December, 2027),
            new(26, December, 2027),
        ];

        Calendar c = new Malta();

        // Moved: the check of line 4173 on Fridays and Sundays, whose weekend QuantLib's Friday-and-Saturday override
        // swaps, moves to Divergences/MaltaWeekendTests.cs; the rest of the list stands.
        static bool Kept(Date d) => d.Weekday is not (Weekday.Friday or Weekday.Sunday);
        CheckHolidays(
            c.HolidayList(new Date(1, January, 2026), new Date(31, December, 2027)).Where(Kept),
            expectedHol.Where(Kept));
    }

    [Fact]
    public void TestMontenegro()
    {
        // Testing Montenegro holiday list...

        Date[] expectedHol =
        [
            new(1, January, 2026),
            new(2, January, 2026),
            new(1, May, 2026),
            new(21, May, 2026),
            new(22, May, 2026),
            new(13, July, 2026),
            new(14, July, 2026),
            new(13, November, 2026),

            new(1, January, 2027),
            new(21, May, 2027),
            new(13, July, 2027),
            new(14, July, 2027),
        ];

        Calendar c = new Montenegro();
        CheckHolidays(c.HolidayList(new Date(1, January, 2026), new Date(31, December, 2027)), expectedHol);
    }

    [Fact]
    public void TestNorthMacedonia()
    {
        // Testing North Macedonia holiday list...

        Date[] expectedHol =
        [
            new(1, January, 2026),
            new(7, January, 2026),
            new(20, March, 2026),
            new(13, April, 2026),
            new(1, May, 2026),
            new(27, May, 2026),
            new(8, September, 2026),
            new(23, October, 2026),
            new(8, December, 2026),

            new(1, January, 2027),
            new(7, January, 2027),
            new(10, March, 2027),
            new(3, May, 2027),
            new(17, May, 2027),
            new(24, May, 2027),
            new(2, August, 2027),
            new(8, September, 2027),
            new(11, October, 2027),
            new(8, December, 2027),
        ];

        Calendar c = new NorthMacedonia();
        CheckHolidays(c.HolidayList(new Date(1, January, 2026), new Date(31, December, 2027)), expectedHol);
    }

    [Fact]
    public void TestNorthMacedoniaRamazanBajram()
    {
        // Testing North Macedonia Ramazan Bajram (Eid al-Fitr) dates...

        Calendar c = new NorthMacedonia();

        Assert.True(c.IsHoliday(new Date(20, March, 2026)));
        Assert.True(c.IsHoliday(new Date(10, March, 2027)));
        Assert.True(c.IsHoliday(new Date(27, February, 2028)));
        Assert.True(c.IsHoliday(new Date(15, February, 2029)));
        Assert.True(c.IsHoliday(new Date(5, February, 2030)));
        Assert.True(c.IsHoliday(new Date(25, January, 2031)));
        Assert.True(c.IsHoliday(new Date(14, January, 2032)));
        Assert.True(c.IsHoliday(new Date(2, January, 2033)));
        Assert.True(c.IsHoliday(new Date(23, December, 2033)));
        Assert.True(c.IsHoliday(new Date(12, December, 2034)));
        Assert.True(c.IsHoliday(new Date(1, December, 2035)));
        Assert.True(c.IsHoliday(new Date(19, November, 2036)));
    }

    [Fact]
    public void TestNorthMacedoniaKurbanBajram()
    {
        // Testing North Macedonia Kurban Bajram (Eid al-Adha) dates...

        Calendar c = new NorthMacedonia();

        Assert.True(c.IsHoliday(new Date(27, May, 2026)));
        Assert.True(c.IsHoliday(new Date(17, May, 2027)));
        Assert.True(c.IsHoliday(new Date(5, May, 2028)));
        Assert.True(c.IsHoliday(new Date(24, April, 2029)));
        Assert.True(c.IsHoliday(new Date(13, April, 2030)));
        Assert.True(c.IsHoliday(new Date(3, April, 2031)));
        Assert.True(c.IsHoliday(new Date(22, March, 2032)));
        Assert.True(c.IsHoliday(new Date(11, March, 2033)));
        Assert.True(c.IsHoliday(new Date(28, February, 2034)));
        Assert.True(c.IsHoliday(new Date(18, February, 2035)));
        Assert.True(c.IsHoliday(new Date(7, February, 2036)));
        Assert.True(c.IsHoliday(new Date(27, January, 2037)));
        Assert.True(c.IsHoliday(new Date(16, January, 2038)));
        Assert.True(c.IsHoliday(new Date(5, January, 2039)));
        Assert.True(c.IsHoliday(new Date(26, December, 2039)));
        Assert.True(c.IsHoliday(new Date(15, December, 2040)));
    }

    [Fact]
    public void TestSerbia()
    {
        // Testing Serbia holiday list...

        Date[] expectedHol =
        [
            new(1, January, 2026),
            new(2, January, 2026),
            new(7, January, 2026),
            new(16, February, 2026),
            new(3, April, 2026),
            new(6, April, 2026),
            new(1, May, 2026),
            new(11, November, 2026),
            new(31, December, 2026),

            new(1, January, 2027),
            new(7, January, 2027),
            new(15, February, 2027),
            new(16, February, 2027),
            new(26, March, 2027),
            new(29, March, 2027),
            new(11, November, 2027),
            new(31, December, 2027),
        ];

        Calendar c = new Serbia();

        // Moved: the check of line 4301 on the Western and the Orthodox Good Friday and Easter Monday, which follow
        // Orthodox Easter in nanoql, moves to Divergences/SerbiaEasterTests.cs; the rest of the list stands.
        Date[] easter =
        [
            new(3, April, 2026), new(6, April, 2026), new(10, April, 2026), new(13, April, 2026),
            new(26, March, 2027), new(29, March, 2027), new(30, April, 2027), new(3, May, 2027),
        ];
        bool Kept(Date d) => !easter.Contains(d);
        CheckHolidays(
            c.HolidayList(new Date(1, January, 2026), new Date(31, December, 2027)).Where(Kept),
            expectedHol.Where(Kept));
    }

    [Fact]
    public void TestSaudiArabiaFoundingDayAndEidHolidays()
    {
        // Testing Saudi Arabia Founding Day and Eid holidays...

        Calendar saudi = new SaudiArabia();

        // Founding Day observed on Sunday 23 Feb 2025 (22 Feb is Saturday)
        Assert.True(saudi.IsHoliday(new Date(23, February, 2025)));

        // Founding Day on Sunday 22 Feb 2026
        Assert.True(saudi.IsHoliday(new Date(22, February, 2026)));
        Assert.True(saudi.IsBusinessDay(new Date(19, February, 2026)));

        // Eid Al-Fitr 2015 (exchange: 18-21 Jul)
        Assert.True(saudi.IsHoliday(new Date(21, July, 2015)));
        Assert.True(saudi.IsBusinessDay(new Date(16, July, 2015)));
        Assert.True(saudi.IsBusinessDay(new Date(22, July, 2015)));

        // Eid Al-Adha 2015 (exchange: 22-27 Sep)
        Assert.True(saudi.IsHoliday(new Date(22, September, 2015)));
        Assert.True(saudi.IsBusinessDay(new Date(21, September, 2015)));
        Assert.True(saudi.IsBusinessDay(new Date(28, September, 2015)));

        // Eid Al-Adha 2025 (Saudi Exchange: 5-10 Jun, resume 11 Jun)
        Assert.True(saudi.IsHoliday(new Date(5, June, 2025)));
        Assert.True(saudi.IsHoliday(new Date(10, June, 2025)));
        Assert.True(saudi.IsBusinessDay(new Date(4, June, 2025)));
        Assert.True(saudi.IsBusinessDay(new Date(11, June, 2025)));

        // Eid Al-Fitr 2026 (Saudi Exchange: 17-23 Mar, resume 24 Mar)
        Assert.True(saudi.IsHoliday(new Date(20, March, 2026)));
        Assert.True(saudi.IsBusinessDay(new Date(16, March, 2026)));
        Assert.True(saudi.IsBusinessDay(new Date(24, March, 2026)));

        // Eid Al-Adha 2026 (Saudi Exchange: 22-30 May, resume 31 May)
        Assert.True(saudi.IsHoliday(new Date(25, May, 2026)));
        Assert.True(saudi.IsBusinessDay(new Date(21, May, 2026)));
        Assert.True(saudi.IsBusinessDay(new Date(31, May, 2026)));
    }

    [Fact]
    public void TestSlovenia()
    {
        // Testing Slovenia holiday list...

        Date[] expectedHol =
        [
            new(1, January, 2026),
            new(2, January, 2026),
            new(3, April, 2026),
            new(6, April, 2026),
            new(1, May, 2026),
            new(25, June, 2026),
            new(24, December, 2026),
            new(25, December, 2026),
            new(31, December, 2026),

            new(1, January, 2027),
            new(26, March, 2027),
            new(29, March, 2027),
            new(25, June, 2027),
            new(24, December, 2027),
            new(31, December, 2027),
        ];

        Calendar c = new Slovenia();
        CheckHolidays(c.HolidayList(new Date(1, January, 2026), new Date(31, December, 2027)), expectedHol);
    }

    [Fact]
    public void TestCroatia()
    {
        // Testing Croatia holiday list...

        Date[] expectedHol =
        [
            new(1, January, 2026),
            new(6, January, 2026),
            new(3, April, 2026),
            new(6, April, 2026),
            new(1, May, 2026),
            new(4, June, 2026),
            new(22, June, 2026),
            new(5, August, 2026),
            new(18, November, 2026),
            new(24, December, 2026),
            new(25, December, 2026),
            new(31, December, 2026),

            new(1, January, 2027),
            new(6, January, 2027),
            new(26, March, 2027),
            new(29, March, 2027),
            new(27, May, 2027),
            new(22, June, 2027),
            new(5, August, 2027),
            new(18, November, 2027),
            new(24, December, 2027),
            new(31, December, 2027),
        ];

        Calendar c = new Croatia();
        CheckHolidays(c.HolidayList(new Date(1, January, 2026), new Date(31, December, 2027)), expectedHol);
    }

    [Fact]
    public void TestMalaysia()
    {
        // Testing Malaysia holiday list...

        Date[] expectedHol =
        [
            // 2024
            new(1, January, 2024), // New Year's Day
            new(25, January, 2024), // Thaipusam
            new(1, February, 2024), // Federal Territory Day
            new(12, February, 2024), // Chinese New Year (substitute)
            new(28, March, 2024), // Nuzul Al-Quran
            new(10, April, 2024), // Hari Raya Aidilfitri
            new(11, April, 2024), // Hari Raya Aidilfitri
            new(1, May, 2024), // Labour Day
            new(22, May, 2024), // Wesak Day
            new(3, June, 2024), // Agong's Birthday
            new(17, June, 2024), // Hari Raya Haji
            new(8, July, 2024), // Awal Muharram (substitute)
            new(16, September, 2024), // Malaysia Day / Maulidur Rasul
            new(31, October, 2024), // Deepavali
            new(25, December, 2024), // Christmas Day

            // 2025
            new(1, January, 2025), // New Year's Day
            new(29, January, 2025), // Chinese New Year
            new(30, January, 2025), // Chinese New Year
            new(11, February, 2025), // Thaipusam
            new(18, March, 2025), // Nuzul Al-Quran
            new(31, March, 2025), // Hari Raya Aidilfitri
            new(1, April, 2025), // Hari Raya Aidilfitri
            new(1, May, 2025), // Labour Day
            new(12, May, 2025), // Wesak Day
            new(2, June, 2025), // Agong's Birthday
            new(27, June, 2025), // Awal Muharram
            new(1, September, 2025), // National Day (substitute)
            new(5, September, 2025), // Maulidur Rasul
            new(15, September, 2025), // Malaysia Day (additional)
            new(16, September, 2025), // Malaysia Day
            new(20, October, 2025), // Deepavali
            new(25, December, 2025), // Christmas Day
        ];

        Calendar c = new Malaysia();
        CheckHolidays(c.HolidayList(new Date(1, January, 2024), new Date(31, December, 2025)), expectedHol);
    }

    [Fact]
    public void TestMalaysiaVariableHolidays()
    {
        // Testing Malaysia variable holidays across the tabulated range...

        Calendar c = new Malaysia();

        // One festival per tabulated year, so that a date added out of order in the
        // holiday table is caught: the table is looked up with a binary search, and
        // an unsorted entry makes its neighbours unfindable.
        Assert.True(c.IsHoliday(new Date(3, February, 2015))); // Thaipusam
        Assert.True(c.IsHoliday(new Date(24, January, 2016)));
        Assert.True(c.IsHoliday(new Date(9, February, 2017)));
        Assert.True(c.IsHoliday(new Date(31, January, 2018)));
        Assert.True(c.IsHoliday(new Date(21, January, 2019)));
        Assert.True(c.IsHoliday(new Date(9, February, 2020)));
        Assert.True(c.IsHoliday(new Date(28, January, 2021)));
        Assert.True(c.IsHoliday(new Date(18, January, 2022)));
        Assert.True(c.IsHoliday(new Date(5, February, 2023)));
        Assert.True(c.IsHoliday(new Date(25, January, 2024)));
        Assert.True(c.IsHoliday(new Date(11, February, 2025)));
        Assert.True(c.IsHoliday(new Date(22, January, 2027)));

        Assert.True(c.IsHoliday(new Date(19, February, 2015))); // Chinese New Year
        Assert.True(c.IsHoliday(new Date(8, February, 2016)));
        Assert.True(c.IsHoliday(new Date(28, January, 2017)));
        Assert.True(c.IsHoliday(new Date(16, February, 2018)));
        Assert.True(c.IsHoliday(new Date(5, February, 2019)));
        Assert.True(c.IsHoliday(new Date(25, January, 2020)));
        Assert.True(c.IsHoliday(new Date(12, February, 2021)));
        Assert.True(c.IsHoliday(new Date(1, February, 2022)));
        Assert.True(c.IsHoliday(new Date(22, January, 2023)));
        Assert.True(c.IsHoliday(new Date(10, February, 2024)));
        Assert.True(c.IsHoliday(new Date(29, January, 2025)));
        Assert.True(c.IsHoliday(new Date(17, February, 2026)));
        Assert.True(c.IsHoliday(new Date(6, February, 2027)));
    }

    [Fact]
    public void TestMalaysiaFixedHolidaysOutsideTabulatedRange()
    {
        // Testing Malaysia fixed holidays outside the tabulated range...

        Calendar c = new Malaysia();

        // The variable festivals are tabulated only for 2015-2027, but the
        // fixed-date federal holidays are rule-based and must hold in any year.
        Assert.True(c.IsHoliday(new Date(1, January, 2035))); // New Year's Day
        Assert.True(c.IsHoliday(new Date(1, February, 2035))); // Federal Territory Day
        Assert.True(c.IsHoliday(new Date(1, May, 2035))); // Labour Day
        Assert.True(c.IsHoliday(new Date(31, August, 2035))); // National Day
        Assert.True(c.IsHoliday(new Date(25, December, 2035))); // Christmas Day

        // Malaysia Day only became a federal holiday in 2010.
        Assert.True(c.IsHoliday(new Date(16, September, 2035)));
        Assert.True(c.IsBusinessDay(new Date(16, September, 2009)));
    }

    [Fact]
    public void TestPhilippines()
    {
        // Testing Philippine holiday list...

        Date[] expectedHol =
        [
            // 2024
            new(1, January, 2024), // New Year's Day
            new(9, February, 2024), // Additional special (non-working) day
            // Saturday: {10, February, 2024},  // Chinese New Year
            new(28, March, 2024), // Maundy Thursday
            new(29, March, 2024), // Good Friday
            // Saturday: {30, March, 2024},     // Black Saturday
            new(9, April, 2024), // Araw ng Kagitingan
            new(10, April, 2024), // Eid'l Fitr
            new(1, May, 2024), // Labor Day
            new(12, June, 2024), // Independence Day
            new(17, June, 2024), // Eid'l Adha
            new(24, July, 2024), // Exchange closed by Typhoon Carina
            new(23, August, 2024), // Ninoy Aquino Day, moved from August 21st
            new(26, August, 2024), // National Heroes Day
            new(1, November, 2024), // All Saints' Day
            // Saturday: {2, November, 2024},   // All Souls' Day
            // Saturday: {30, November, 2024},  // Bonifacio Day
            // Sunday: {8, December, 2024},     // Immaculate Conception
            new(24, December, 2024), // Christmas Eve
            new(25, December, 2024), // Christmas Day
            new(30, December, 2024), // Rizal Day
            new(31, December, 2024), // Last Day of the Year

            // 2025
            new(1, January, 2025), // New Year's Day
            new(29, January, 2025), // Chinese New Year
            new(1, April, 2025), // Eid'l Fitr
            new(9, April, 2025), // Araw ng Kagitingan
            new(17, April, 2025), // Maundy Thursday
            new(18, April, 2025), // Good Friday
            // Saturday: {19, April, 2025},     // Black Saturday
            new(1, May, 2025), // Labor Day
            new(12, May, 2025), // National and local elections
            new(6, June, 2025), // Eid'l Adha
            new(12, June, 2025), // Independence Day
            // Sunday: {27, July, 2025},        // Iglesia ni Cristo anniversary
            new(21, August, 2025), // Ninoy Aquino Day
            new(25, August, 2025), // National Heroes Day
            new(31, October, 2025), // Additional special (non-working) day
            // Saturday: {1, November, 2025},   // All Saints' Day
            // Sunday: {30, November, 2025},    // Bonifacio Day
            new(8, December, 2025), // Immaculate Conception
            new(24, December, 2025), // Christmas Eve
            new(25, December, 2025), // Christmas Day
            new(30, December, 2025), // Rizal Day
            new(31, December, 2025), // Last Day of the Year

            // 2026
            new(1, January, 2026), // New Year's Day
            new(17, February, 2026), // Chinese New Year
            new(20, March, 2026), // Eid'l Fitr
            new(2, April, 2026), // Maundy Thursday
            new(3, April, 2026), // Good Friday
            // Saturday: {4, April, 2026},      // Black Saturday
            new(9, April, 2026), // Araw ng Kagitingan
            new(1, May, 2026), // Labor Day
            new(27, May, 2026), // Eid'l Adha
            new(12, June, 2026), // Independence Day
            new(21, August, 2026), // Ninoy Aquino Day
            new(31, August, 2026), // National Heroes Day
            // Sunday: {1, November, 2026},     // All Saints' Day
            new(2, November, 2026), // All Souls' Day
            new(30, November, 2026), // Bonifacio Day
            new(8, December, 2026), // Immaculate Conception
            new(24, December, 2026), // Christmas Eve
            new(25, December, 2026), // Christmas Day
            new(30, December, 2026), // Rizal Day
            new(31, December, 2026), // Last Day of the Year
        ];

        Calendar c = new Philippines();
        CheckHolidays(c.HolidayList(new Date(1, January, 2024), new Date(31, December, 2026)), expectedHol);
    }

    [Fact]
    public void TestPhilippinesTransferredHolidays()
    {
        // Testing Philippine transferred and withdrawn holidays...

        Calendar c = new Philippines();

        // Araw ng Kagitingan was moved from April 9th (a Sunday) to April 10th
        // in 2023, and Bonifacio Day from November 30th to November 27th.
        Assert.True(c.IsHoliday(new Date(10, April, 2023)));
        Assert.True(c.IsBusinessDay(new Date(30, November, 2023)));
        Assert.True(c.IsHoliday(new Date(27, November, 2023)));

        // Ninoy Aquino Day was moved from August 21st to August 23rd in 2024.
        Assert.True(c.IsBusinessDay(new Date(21, August, 2024)));
        Assert.True(c.IsHoliday(new Date(23, August, 2024)));

        // The Last Day of the Year was declared a special working day in 2021.
        Assert.True(c.IsBusinessDay(new Date(31, December, 2021)));
        Assert.True(c.IsHoliday(new Date(31, December, 2020)));

        // The EDSA anniversary was a special non-working day up to 2023 and a
        // special working day from 2025.
        Assert.True(c.IsHoliday(new Date(25, February, 2022)));
        Assert.True(c.IsHoliday(new Date(24, February, 2023)));
        Assert.True(c.IsBusinessDay(new Date(25, February, 2025)));

        // The exchange was closed by the COVID-19 trading suspension.
        Assert.True(c.IsHoliday(new Date(17, March, 2020)));
        Assert.True(c.IsHoliday(new Date(18, March, 2020)));
        Assert.True(c.IsBusinessDay(new Date(19, March, 2020)));
    }

    [Fact]
    public void TestPhilippinesFixedHolidaysOutsideTabulatedRange()
    {
        // Testing Philippine fixed holidays outside the tabulated range...

        Calendar c = new Philippines();

        // The proclaimed festivals are tabulated only for 2020-2027, but the
        // rule-based holidays must hold in any year.
        Assert.True(c.IsHoliday(new Date(1, January, 2035))); // New Year's Day
        Assert.True(c.IsHoliday(new Date(9, April, 2035))); // Araw ng Kagitingan
        Assert.True(c.IsHoliday(new Date(1, May, 2035))); // Labor Day
        Assert.True(c.IsHoliday(new Date(12, June, 2035))); // Independence Day
        Assert.True(c.IsHoliday(new Date(21, August, 2035))); // Ninoy Aquino Day
        Assert.True(c.IsHoliday(new Date(27, August, 2035))); // National Heroes Day
        Assert.True(c.IsHoliday(new Date(1, November, 2035))); // All Saints' Day
        Assert.True(c.IsHoliday(new Date(30, November, 2035))); // Bonifacio Day
        Assert.True(c.IsHoliday(new Date(25, December, 2035))); // Christmas Day
        Assert.True(c.IsHoliday(new Date(31, December, 2035))); // Last Day of the Year

        // National Heroes Day is the last Monday of August, not a fixed date.
        Assert.True(c.IsBusinessDay(new Date(20, August, 2035)));

        // Ninoy Aquino Day became a holiday under RA 9256 in 2004, and the Feast
        // of the Immaculate Conception under RA 10966 in 2018.
        Assert.True(c.IsBusinessDay(new Date(21, August, 2003)));
        Assert.True(c.IsBusinessDay(new Date(8, December, 2015)));
    }

    [Fact]
    public void TestUzbekistan()
    {
        // Testing Uzbekistan holiday list...

        Date[] expectedHol =
        [
            new(1, January, 2026),
            new(20, March, 2026),
            new(27, May, 2026),
            new(1, September, 2026),
            new(1, October, 2026),
            new(8, December, 2026),
            new(1, January, 2027),
            new(8, March, 2027),
            new(10, March, 2027),
            new(17, May, 2027),
            new(1, September, 2027),
            new(1, October, 2027),
            new(8, December, 2027),
        ];

        Calendar c = new Uzbekistan();
        CheckHolidays(c.HolidayList(new Date(1, January, 2026), new Date(31, December, 2027)), expectedHol);
    }

    [Fact]
    public void TestUzbekistanRamazonHayit()
    {
        // Testing Uzbekistan Ramazon Hayit (Eid al-Fitr) dates...

        Calendar c = new Uzbekistan();

        Assert.True(c.IsHoliday(new Date(20, March, 2026)));
        Assert.True(c.IsHoliday(new Date(10, March, 2027)));
        Assert.True(c.IsHoliday(new Date(27, February, 2028)));
        Assert.True(c.IsHoliday(new Date(15, February, 2029)));
        Assert.True(c.IsHoliday(new Date(5, February, 2030)));
        Assert.True(c.IsHoliday(new Date(25, January, 2031)));
        Assert.True(c.IsHoliday(new Date(14, January, 2032)));
        Assert.True(c.IsHoliday(new Date(2, January, 2033)));
        Assert.True(c.IsHoliday(new Date(23, December, 2033)));
        Assert.True(c.IsHoliday(new Date(12, December, 2034)));
        Assert.True(c.IsHoliday(new Date(1, December, 2035)));
        Assert.True(c.IsHoliday(new Date(19, November, 2036)));
    }

    [Fact]
    public void TestUzbekistanQurbonHayit()
    {
        // Testing Uzbekistan Qurbon Hayit (Eid al-Adha) dates...

        Calendar c = new Uzbekistan();

        Assert.True(c.IsHoliday(new Date(27, May, 2026)));
        Assert.True(c.IsHoliday(new Date(17, May, 2027)));
        Assert.True(c.IsHoliday(new Date(5, May, 2028)));
        Assert.True(c.IsHoliday(new Date(24, April, 2029)));
        Assert.True(c.IsHoliday(new Date(13, April, 2030)));
        Assert.True(c.IsHoliday(new Date(3, April, 2031)));
        Assert.True(c.IsHoliday(new Date(22, March, 2032)));
        Assert.True(c.IsHoliday(new Date(11, March, 2033)));
        Assert.True(c.IsHoliday(new Date(28, February, 2034)));
        Assert.True(c.IsHoliday(new Date(18, February, 2035)));
        Assert.True(c.IsHoliday(new Date(7, February, 2036)));
        Assert.True(c.IsHoliday(new Date(27, January, 2037)));
        Assert.True(c.IsHoliday(new Date(16, January, 2038)));
        Assert.True(c.IsHoliday(new Date(5, January, 2039)));
        Assert.True(c.IsHoliday(new Date(26, December, 2039)));
        Assert.True(c.IsHoliday(new Date(15, December, 2040)));
    }

    [Fact]
    public void TestIndia()
    {
        // Testing India (NSE) clearing holiday list...

        Date[] expectedHol =
        [
            // 2026
            // Municipal Corporation Election - Maharashtra
            new Date(15, January, 2026),
            // Republic Day
            new Date(26, January, 2026),
            // Chatrapati Shivaji Jayanti
            new Date(19, February, 2026),
            // Holi
            new Date(3, March, 2026),
            // Gudi Padwa
            new Date(19, March, 2026),
            // Ram Navami
            new Date(26, March, 2026),
            // Mahavir Jayanti
            new Date(31, March, 2026),
            // Annual Bank Closing
            new Date(1, April, 2026),
            // Good Friday
            new Date(3, April, 2026),
            // Ambedkar Jayanti
            new Date(14, April, 2026),
            // May Day
            new Date(1, May, 2026),
            // Bakri Id
            new Date(28, May, 2026),
            // Muharram
            new Date(26, June, 2026),
            // Id-E-Milad
            new Date(26, August, 2026),
            // Ganesh Chaturthi
            new Date(14, September, 2026),
            // Gandhi Jayanti
            new Date(2, October, 2026),
            // Dussehra
            new Date(20, October, 2026),
            // Diwali - Balipratipada
            new Date(10, November, 2026),
            // Gurunank Jayanti
            new Date(24, November, 2026),
            // Christmas
            new Date(25, December, 2026),
        ];

        // The following 2026 holidays fall on weekends and are
        // therefore not included above:
        // Mahashivratri:      15-Feb-2026 (Sunday)
        // Id-Ul-Fitr:         21-Mar-2026 (Saturday)
        // Independence Day:   15-Aug-2026 (Saturday)
        // Diwali Laxmi Pujan: 08-Nov-2026 (Sunday)

        Calendar c = new India();
        CheckHolidays(c.HolidayList(new Date(1, January, 2026), new Date(31, December, 2026)), expectedHol);

        Date[] historical =
        [
            new(21, January, 2005), new(11, January, 2006), new(1, January, 2007),
            new(6, March, 2008), new(8, January, 2009), new(1, January, 2010),
            new(2, March, 2011), new(20, February, 2012), new(27, March, 2013),
            new(27, February, 2014), new(19, February, 2019), new(19, February, 2020),
            new(19, February, 2021), new(1, March, 2022), new(7, March, 2023),
            new(22, January, 2024), new(19, February, 2025)
        ];
        foreach (Date date in historical)
            Assert.True(c.IsHoliday(date), $"{date}");
    }

    // QuantLib's checkHolidays: the calculated and expected holidays compared as sets, in both directions.
    private static void CheckHolidays(IEnumerable<Date> calculated, IEnumerable<Date> expected)
    {
        var calc = new SortedSet<Date>(calculated);
        var exp = new SortedSet<Date>(expected);
        var errors = new List<string>();

        foreach (Date d in calculated)
        {
            if (!exp.Contains(d))
            {
                errors.Add($"{d} calculated but not expected");
            }
        }

        foreach (Date d in expected)
        {
            if (!calc.Contains(d))
            {
                errors.Add($"{d} expected but not calculated");
            }
        }

        Assert.Empty(errors);
    }
}
