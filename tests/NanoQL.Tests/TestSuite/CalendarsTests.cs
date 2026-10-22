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
// Partial: testGermanyFrankfurt, testGermanyEurex, testGermanyXetra, testGermanySettlementCalendar, testGermanyEuwaxCalendar, testItaly, testRussia, testDenmark, testSouthKoreanSettlement, testKoreaStockExchange, testHongKong, testArgentinaCalendar, testIndonesiaCalendar, testTaiwanCalendar, testThailandCalendar, testBotswanaCalendar, testCzechRepublicCalendar, testFinlandCalendar, testHungaryCalendar, testIcelandCalendar, testNorwayCalendar, testPolandCalendars, testSlovakiaCalendar, testSouthAfricaCalendar, testSwedenCalendar, testUkraineCalendar, testSingaporeCalendar, testAustriaCalendars, testFranceCalendars, testRomaniaCalendars, testAustraliaCalendars, testChileCalendar, testChinaSSE, testChinaIB, testMexicoInaugurationDay, testTASECalendar, testSHIRCalendar, testTelborCalendar, testDayLists, testMalta, testMontenegro, testNorthMacedonia, testNorthMacedoniaRamazanBajram, testNorthMacedoniaKurbanBajram, testSerbia, testSaudiArabiaFoundingDayAndEidHolidays, testSlovenia, testCroatia, testMalaysia, testMalaysiaVariableHolidays, testMalaysiaFixedHolidaysOutsideTabulatedRange, testPhilippines, testPhilippinesTransferredHolidays, testPhilippinesFixedHolidaysOutsideTabulatedRange, testUzbekistan, testUzbekistanRamazonHayit, testUzbekistanQurbonHayit, testIndia
// Not ported: testIntradayAddHolidays (QL_HIGH_RESOLUTION_DATE, which the default build leaves out)

using System.Collections.Immutable;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

/// <summary>QuantLib's calendar tests, <c>test-suite/calendars.cpp</c>, for the calendars nanoql ports.</summary>
[Trait("Oracle", "QuantLib")]
public class CalendarsTests
{
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

    // Adapted: without its Germany join (lines 237-241) and what only it uses: c5 (line 175), the vector of calendars
    // (177-183), cvh (191) and b5 (199). Germany isn't ported yet.
    [Fact]
    public void TestJointCalendars()
    {
        Calendar c1 = new TARGET(), c2 = new UnitedKingdom(), c3 = new UnitedStates(UnitedStatesMarket.NYSE),
                 c4 = new Japan();

        Calendar c12h = new JointCalendar(c1, c2, JointCalendarRule.JoinHolidays),
                 c12b = new JointCalendar(c1, c2, JointCalendarRule.JoinBusinessDays),
                 c123h = new JointCalendar(c1, c2, c3, JointCalendarRule.JoinHolidays),
                 c123b = new JointCalendar(c1, c2, c3, JointCalendarRule.JoinBusinessDays),
                 c1234h = new JointCalendar(c1, c2, c3, c4, JointCalendarRule.JoinHolidays),
                 c1234b = new JointCalendar(c1, c2, c3, c4, JointCalendarRule.JoinBusinessDays);

        // test one year, starting today
        Date firstDate = Date.TodaysDate(), endDate = firstDate + (1 * TimeUnit.Years);

        for (Date d = firstDate; d < endDate; d++)
        {
            bool b1 = c1.IsBusinessDay(d), b2 = c2.IsBusinessDay(d), b3 = c3.IsBusinessDay(d),
                 b4 = c4.IsBusinessDay(d);

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
