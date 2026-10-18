// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2006 Piter Dias
// SPDX-FileCopyrightText: Copyright (C) 2012 Simon Shakeshaft
// SPDX-FileCopyrightText: Copyright (c) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/daycounters.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testSimple, testOne, testBusiness252, testThirty365, testIntraday, testActualActualOutOfScheduleRange, testAct366, testAct36525, testYearFraction2DateBulk, testYearFraction2DateRounding, testYearFractionToDateForLinearDayCounter

using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

/// <summary>
/// QuantLib's day counter tests, <c>test-suite/daycounters.cpp</c>, for the day counters nanoql ports.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DayCountersTests
{
    [Fact]
    public void TestActualActual()
    {
        // Testing actual/actual day counters...
        SingleCase[] testCases =
        [
            // first example
            new(ActualActualConvention.ISDA,
                new Date(1, November, 2003), new Date(1, May, 2004),
                0.497724380567),
            new(ActualActualConvention.ISMA,
                new Date(1, November, 2003), new Date(1, May, 2004),
                new Date(1, November, 2003), new Date(1, May, 2004),
                0.500000000000),
            new(ActualActualConvention.AFB,
                new Date(1, November, 2003), new Date(1, May, 2004),
                0.497267759563),

            // short first calculation period (first period)
            new(ActualActualConvention.ISDA,
                new Date(1, February, 1999), new Date(1, July, 1999),
                0.410958904110),
            new(ActualActualConvention.ISMA,
                new Date(1, February, 1999), new Date(1, July, 1999),
                new Date(1, July, 1998), new Date(1, July, 1999),
                0.410958904110),
            new(ActualActualConvention.AFB,
                new Date(1, February, 1999), new Date(1, July, 1999),
                0.410958904110),

            // short first calculation period (second period)
            new(ActualActualConvention.ISDA,
                new Date(1, July, 1999), new Date(1, July, 2000),
                1.001377348600),
            new(ActualActualConvention.ISMA,
                new Date(1, July, 1999), new Date(1, July, 2000),
                new Date(1, July, 1999), new Date(1, July, 2000),
                1.000000000000),
            new(ActualActualConvention.AFB,
                new Date(1, July, 1999), new Date(1, July, 2000),
                1.000000000000),

            // long first calculation period (first period)
            new(ActualActualConvention.ISDA,
                new Date(15, August, 2002), new Date(15, July, 2003),
                0.915068493151),
            new(ActualActualConvention.ISMA,
                new Date(15, August, 2002), new Date(15, July, 2003),
                new Date(15, January, 2003), new Date(15, July, 2003),
                0.915760869565),
            new(ActualActualConvention.AFB,
                new Date(15, August, 2002), new Date(15, July, 2003),
                0.915068493151),

            // long first calculation period (second period)
            /* Warning: the ISDA case is in disagreement with mktc1198.pdf */
            new(ActualActualConvention.ISDA,
                new Date(15, July, 2003), new Date(15, January, 2004),
                0.504004790778),
            new(ActualActualConvention.ISMA,
                new Date(15, July, 2003), new Date(15, January, 2004),
                new Date(15, July, 2003), new Date(15, January, 2004),
                0.500000000000),
            new(ActualActualConvention.AFB,
                new Date(15, July, 2003), new Date(15, January, 2004),
                0.504109589041),

            // short final calculation period (penultimate period)
            new(ActualActualConvention.ISDA,
                new Date(30, July, 1999), new Date(30, January, 2000),
                0.503892506924),
            new(ActualActualConvention.ISMA,
                new Date(30, July, 1999), new Date(30, January, 2000),
                new Date(30, July, 1999), new Date(30, January, 2000),
                0.500000000000),
            new(ActualActualConvention.AFB,
                new Date(30, July, 1999), new Date(30, January, 2000),
                0.504109589041),

            // short final calculation period (final period)
            new(ActualActualConvention.ISDA,
                new Date(30, January, 2000), new Date(30, June, 2000),
                0.415300546448),
            new(ActualActualConvention.ISMA,
                new Date(30, January, 2000), new Date(30, June, 2000),
                new Date(30, January, 2000), new Date(30, July, 2000),
                0.417582417582),
            new(ActualActualConvention.AFB,
                new Date(30, January, 2000), new Date(30, June, 2000),
                0.41530054644),
        ];

        Size n = testCases.Length;
        for (Size i = 0; i < n; i++)
        {
            var dayCounter = new ActualActual(testCases[i].Convention);
            Date d1 = testCases[i].Start,
                d2 = testCases[i].End;
            Date? rd1 = testCases[i].RefStart,
                rd2 = testCases[i].RefEnd;
            double calculated = dayCounter.YearFraction(d1, d2, rd1, rd2);

            if (Math.Abs(calculated - testCases[i].Result) > 1.0e-10)
            {
                string period = $"period: {d1} to {d2}";
                string refPeriod = testCases[i].Convention == ActualActualConvention.ISMA
                    ? $"referencePeriod: {Show(rd1)} to {Show(rd2)}"
                    : string.Empty;
                Assert.Fail(
                    $"{dayCounter.Name}:\n{period}\n{refPeriod}\n"
                    + $"    calculated: {calculated}\n"
                    + $"    expected:   {testCases[i].Result}");
            }
        }
    }

    [Fact]
    public void TestActualActualIsma()
    {
        // Testing actual/actual (ISMA) with odd last period...
        bool isEndOfMonth = false;
        Frequency frequency = Frequency.Semiannual;
        var interestAccrualDate = new Date(30, Jan, 1999);
        var maturityDate = new Date(30, Jun, 2000);
        var firstCouponDate = new Date(30, Jul, 1999);
        var penultimateCouponDate = new Date(30, Jan, 2000);
        var d1 = new Date(30, Jan, 2000);
        var d2 = new Date(30, Jun, 2000);

        double expected = 152.0 / (182.0 * 2);

        Schedule schedule = new MakeSchedule()
            .From(interestAccrualDate)
            .To(maturityDate)
            .WithFrequency(frequency)
            .WithFirstDate(firstCouponDate)
            .WithNextToLastDate(penultimateCouponDate)
            .EndOfMonth(isEndOfMonth)
            .ToSchedule();

        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISMA, schedule);

        Real calculated = dayCounter.YearFraction(d1, d2);

        if (Math.Abs(calculated - expected) > 1.0e-10)
        {
            Assert.Fail(IsmaMessage(dayCounter, d1, d2, firstCouponDate, penultimateCouponDate, calculated, expected));
        }

        //////////////////////////////////

        isEndOfMonth = true;
        frequency = Frequency.Quarterly;
        interestAccrualDate = new Date(31, May, 1999);
        maturityDate = new Date(30, Apr, 2000);
        firstCouponDate = new Date(31, Aug, 1999);
        penultimateCouponDate = new Date(30, Nov, 1999);
        d1 = new Date(30, Nov, 1999);
        d2 = new Date(30, Apr, 2000);

        expected = (91.0 / (91.0 * 4)) + (61.0 / (92.0 * 4));

        schedule = new MakeSchedule()
            .From(interestAccrualDate)
            .To(maturityDate)
            .WithFrequency(frequency)
            .WithFirstDate(firstCouponDate)
            .WithNextToLastDate(penultimateCouponDate)
            .EndOfMonth(isEndOfMonth)
            .ToSchedule();

        dayCounter = new ActualActual(ActualActualConvention.ISMA, schedule);

        calculated = dayCounter.YearFraction(d1, d2);

        if (Math.Abs(calculated - expected) > 1.0e-10)
        {
            Assert.Fail(IsmaMessage(dayCounter, d1, d2, firstCouponDate, penultimateCouponDate, calculated, expected));
        }

        //////////////////////////////////

        isEndOfMonth = false;
        frequency = Frequency.Quarterly;
        interestAccrualDate = new Date(31, May, 1999);
        maturityDate = new Date(30, Apr, 2000);
        firstCouponDate = new Date(31, Aug, 1999);
        penultimateCouponDate = new Date(30, Nov, 1999);
        d1 = new Date(30, Nov, 1999);
        d2 = new Date(30, Apr, 2000);

        expected = (91.0 / (91.0 * 4)) + (61.0 / (90.0 * 4));

        schedule = new MakeSchedule()
            .From(interestAccrualDate)
            .To(maturityDate)
            .WithFrequency(frequency)
            .WithFirstDate(firstCouponDate)
            .WithNextToLastDate(penultimateCouponDate)
            .EndOfMonth(isEndOfMonth)
            .ToSchedule();

        dayCounter = new ActualActual(ActualActualConvention.ISMA, schedule);

        calculated = dayCounter.YearFraction(d1, d2);

        if (Math.Abs(calculated - expected) > 1.0e-10)
        {
            Assert.Fail(IsmaMessage(dayCounter, d1, d2, firstCouponDate, penultimateCouponDate, calculated, expected));
        }
    }

    [Fact]
    public void TestActualActualWithSemiannualSchedule()
    {
        // Testing actual/actual with schedule for undefined semiannual reference periods...
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        var fromDate = new Date(10, January, 2017);
        var firstCoupon = new Date(31, August, 2017);
        var quasiCoupon = new Date(28, February, 2017);
        var quasiCoupon2 = new Date(31, August, 2016);

        Schedule schedule = new MakeSchedule()
            .From(fromDate)
            .WithFirstDate(firstCoupon)
            .To(new Date(28, February, 2026))
            .WithFrequency(Frequency.Semiannual)
            .WithCalendar(calendar)
            .WithConvention(BusinessDayConvention.Unadjusted)
            .Backwards().EndOfMonth(true)
            .ToSchedule();

        Date testDate = schedule.Date(1);
        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISMA, schedule);
        DayCounter dayCounterNoSchedule = new ActualActual(ActualActualConvention.ISMA);

        Date referencePeriodStart = schedule.Date(1);
        Date referencePeriodEnd = schedule.Date(2);

        // Test
        Assert.True(
            dayCounter.YearFraction(referencePeriodStart, referencePeriodStart) == 0.0,
            "This should be zero.");
        Assert.True(
            dayCounterNoSchedule.YearFraction(referencePeriodStart, referencePeriodStart) == 0.0,
            "This should be zero");
        Assert.True(
            dayCounterNoSchedule.YearFraction(
                referencePeriodStart, referencePeriodStart, referencePeriodStart, referencePeriodStart) == 0.0,
            "This should be zero");
        Assert.True(
            dayCounter.YearFraction(referencePeriodStart, referencePeriodEnd) == 0.5,
            $"This should be exact using schedule; {referencePeriodStart} to {referencePeriodEnd}Should be 0.5");
        Assert.True(
            dayCounterNoSchedule.YearFraction(
                referencePeriodStart, referencePeriodEnd, referencePeriodStart, referencePeriodEnd) == 0.5,
            "This should be exact for explicit reference periods with no schedule");

        while (testDate < referencePeriodEnd)
        {
            double difference =
                dayCounter.YearFraction(testDate, referencePeriodEnd, referencePeriodStart, referencePeriodEnd)
                - dayCounter.YearFraction(testDate, referencePeriodEnd);
            if (Math.Abs(difference) > 1.0e-10)
            {
                Assert.Fail("Failed to correctly use the schedule to find the reference period for Act/Act");
            }

            testDate = calendar.Advance(testDate, 1, TimeUnit.Days);
        }

        // Test long first coupon
        Real calculatedYearFraction =
            dayCounter.YearFraction(fromDate, firstCoupon);
        Real expectedYearFraction =
            0.5 + (((Real)dayCounter.DayCount(fromDate, quasiCoupon))
                / (2 * dayCounter.DayCount(quasiCoupon2, quasiCoupon)));

        Assert.True(
            Math.Abs(calculatedYearFraction - expectedYearFraction) < 1.0e-10,
            "Failed to compute the expected year fraction "
            + $"\n expected:   {expectedYearFraction}"
            + $"\n calculated: {calculatedYearFraction}");

        // test multiple periods

        schedule = new MakeSchedule()
            .From(new Date(10, January, 2017))
            .WithFirstDate(new Date(31, August, 2017))
            .To(new Date(28, February, 2026))
            .WithFrequency(Frequency.Semiannual)
            .WithCalendar(calendar)
            .WithConvention(BusinessDayConvention.Unadjusted)
            .Backwards().EndOfMonth(false)
            .ToSchedule();

        Date periodStartDate = schedule.Date(1);
        Date periodEndDate = schedule.Date(2);

        dayCounter = new ActualActual(ActualActualConvention.ISMA, schedule);

        while (periodEndDate < schedule.Date(schedule.Size - 2))
        {
            double expected =
                ActualActualDaycountComputation(
                    schedule,
                    periodStartDate,
                    periodEndDate);
            double calculated = dayCounter.YearFraction(
                periodStartDate,
                periodEndDate);

            if (Math.Abs(expected - calculated) > 1e-8)
            {
                Assert.Fail(
                    "Failed to compute the correct year fraction given a schedule: "
                    + $"{periodStartDate} to {periodEndDate}"
                    + $"\n expected: {expected} calculated: {calculated}");
            }

            periodEndDate = calendar.Advance(periodEndDate, 1, TimeUnit.Days);
        }
    }

    [Fact]
    public void TestActualActualWithAnnualSchedule()
    {
        // Testing actual/actual with schedule for undefined annual reference periods...

        // Now do an annual schedule
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        Schedule schedule = new MakeSchedule()
            .From(new Date(10, January, 2017))
            .WithFirstDate(new Date(31, August, 2017))
            .To(new Date(28, February, 2026))
            .WithFrequency(Frequency.Annual)
            .WithCalendar(calendar)
            .WithConvention(BusinessDayConvention.Unadjusted)
            .Backwards().EndOfMonth(false)
            .ToSchedule();

        Date referencePeriodStart = schedule.Date(1);
        Date referencePeriodEnd = schedule.Date(2);

        Date testDate = schedule.Date(1);
        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISMA, schedule);

        while (testDate < referencePeriodEnd)
        {
            double difference =
                ISMAYearFractionWithReferenceDates(
                    dayCounter, testDate, referencePeriodEnd, referencePeriodStart, referencePeriodEnd)
                - dayCounter.YearFraction(testDate, referencePeriodEnd);
            if (Math.Abs(difference) > 1.0e-10)
            {
                Assert.Fail(
                    "Failed to correctly use the schedule to find the reference period for Act/Act:\n"
                    + $"{testDate} to {referencePeriodEnd}"
                    + $"\n Ref: {referencePeriodStart} to {referencePeriodEnd}");
            }

            testDate = calendar.Advance(testDate, 1, TimeUnit.Days);
        }
    }

    [Fact]
    public void TestActualActualWithSchedule()
    {
        // Testing actual/actual day counter with schedule...

        // long first coupon
        var issueDateExpected = new Date(17, January, 2017);
        var firstCouponDateExpected = new Date(31, August, 2017);

        Schedule schedule = new MakeSchedule()
            .From(issueDateExpected)
            .WithFirstDate(firstCouponDateExpected)
            .To(new Date(28, February, 2026))
            .WithFrequency(Frequency.Semiannual)
            .WithCalendar(new Canada())
            .WithConvention(BusinessDayConvention.Unadjusted)
            .Backwards()
            .EndOfMonth()
            .ToSchedule();

        Date issueDate = schedule.Date(0);
        Assert.True(
            issueDate == issueDateExpected,
            $"This is not the expected issue date {issueDate} expected {issueDateExpected}");
        Date firstCouponDate = schedule.Date(1);
        Assert.True(
            firstCouponDate == firstCouponDateExpected,
            $"This is not the expected first coupon date {firstCouponDate} expected: {firstCouponDateExpected}");

        // Make thw quasi coupon dates:
        Date quasiCouponDate2 = schedule.Calendar!.Advance(
            firstCouponDate,
            -schedule.Tenor(),
            schedule.BusinessDayConvention,
            schedule.EndOfMonth());
        Date quasiCouponDate1 = schedule.Calendar!.Advance(
            quasiCouponDate2,
            -schedule.Tenor(),
            schedule.BusinessDayConvention,
            schedule.EndOfMonth());

        var quasiCouponDate1Expected = new Date(31, August, 2016);
        var quasiCouponDate2Expected = new Date(28, February, 2017);

        Assert.True(
            quasiCouponDate2 == quasiCouponDate2Expected,
            $"Expected {quasiCouponDate2Expected} as the later quasi coupon date but received {quasiCouponDate2}");
        Assert.True(
            quasiCouponDate1 == quasiCouponDate1Expected,
            $"Expected {quasiCouponDate1Expected} as the earlier quasi coupon date but received {quasiCouponDate1}");

        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISMA, schedule);

        // full coupon
        double t_with_reference = dayCounter.YearFraction(
            issueDate, firstCouponDate,
            quasiCouponDate2, firstCouponDate);
        double t_no_reference = dayCounter.YearFraction(
            issueDate,
            firstCouponDate);
        double t_total =
            ISMAYearFractionWithReferenceDates(
                dayCounter,
                issueDate, quasiCouponDate2,
                quasiCouponDate1, quasiCouponDate2)
            + 0.5;
        double expected = 0.6160220994;

        if (Math.Abs(t_total - expected) > 1.0e-10)
        {
            Assert.Fail(
                "Failed to reproduce expected time:\n"
                + $"    calculated: {t_total}\n"
                + $"    expected:   {expected}");
        }

        if (Math.Abs(t_with_reference - expected) > 1.0e-10)
        {
            Assert.Fail(
                "Failed to reproduce expected time:\n"
                + $"    calculated: {t_with_reference}\n"
                + $"    expected:   {expected}");
        }

        if (Math.Abs(t_no_reference - t_with_reference) > 1.0e-10)
        {
            Assert.Fail("Should produce the same time whether or not references are present");
        }

        // settlement date in the first quasi-period
        var settlementDate = new Date(29, January, 2017);

        t_with_reference = ISMAYearFractionWithReferenceDates(
            dayCounter,
            issueDate, settlementDate,
            quasiCouponDate1, quasiCouponDate2);
        t_no_reference = dayCounter.YearFraction(issueDate, settlementDate);
        double t_expected_first_qp = 0.03314917127071823; // 12.0/362
        if (Math.Abs(t_with_reference - t_expected_first_qp) > 1.0e-10)
        {
            Assert.Fail(
                "Failed to reproduce expected time:\n"
                + $"    calculated: {t_no_reference}\n"
                + $"    expected:   {t_expected_first_qp}");
        }

        if (Math.Abs(t_no_reference - t_with_reference) > 1.0e-10)
        {
            Assert.Fail("Should produce the same time whether or not references are present");
        }

        double t2 = dayCounter.YearFraction(settlementDate, firstCouponDate);
        if (Math.Abs(t_expected_first_qp + t2 - expected) > 1.0e-10)
        {
            Assert.Fail("Sum of quasiperiod2 split is not consistent");
        }

        // settlement date in the second quasi-period
        settlementDate = new Date(29, July, 2017);

        t_no_reference = dayCounter.YearFraction(issueDate, settlementDate);
        t_with_reference = ISMAYearFractionWithReferenceDates(
            dayCounter,
            issueDate, quasiCouponDate2,
            quasiCouponDate1, quasiCouponDate2)
            + ISMAYearFractionWithReferenceDates(
                dayCounter,
                quasiCouponDate2, settlementDate,
                quasiCouponDate2, firstCouponDate);
        if (Math.Abs(t_no_reference - t_with_reference) > 1.0e-10)
        {
            Assert.Fail("These two cases should be identical");
        }

        t2 = dayCounter.YearFraction(settlementDate, firstCouponDate);

        if (Math.Abs(t_total - (t_no_reference + t2)) > 1.0e-10)
        {
            Assert.Fail(
                "Failed to reproduce expected time:\n"
                + $"    calculated: {t_total}\n"
                + $"    expected:   {t_no_reference + t2}");
        }
    }

    [Fact]
    public void TestThirty360_BondBasis()
    {
        // Testing 30/360 day counter (Bond Basis)...

        // See https://www.isda.org/2008/12/22/30-360-day-count-conventions/

        DayCounter dayCounter = new Thirty360(Thirty360Convention.BondBasis);

        Thirty360Case[] data =
        [
            // Example 1: End dates do not involve the last day of February
            new(new Date(20, August, 2006),    new Date(20, February, 2007), 180),
            new(new Date(20, February, 2007),  new Date(20, August, 2007),   180),
            new(new Date(20, August, 2007),    new Date(20, February, 2008), 180),
            new(new Date(20, February, 2008),  new Date(20, August, 2008),   180),
            new(new Date(20, August, 2008),    new Date(20, February, 2009), 180),
            new(new Date(20, February, 2009),  new Date(20, August, 2009),   180),

            // Example 2: End dates include some end-February dates
            new(new Date(31, August, 2006),    new Date(28, February, 2007), 178),
            new(new Date(28, February, 2007),  new Date(31, August, 2007),   183),
            new(new Date(31, August, 2007),    new Date(29, February, 2008), 179),
            new(new Date(29, February, 2008),  new Date(31, August, 2008),   182),
            new(new Date(31, August, 2008),    new Date(28, February, 2009), 178),
            new(new Date(28, February, 2009),  new Date(31, August, 2009),   183),

            // Example 3: Miscellaneous calculations
            new(new Date(31, January, 2006),   new Date(28, February, 2006),  28),
            new(new Date(30, January, 2006),   new Date(28, February, 2006),  28),
            new(new Date(28, February, 2006),  new Date(3, March, 2006),       5),
            new(new Date(14, February, 2006),  new Date(28, February, 2006),  14),
            new(new Date(30, September, 2006), new Date(31, October, 2006),   30),
            new(new Date(31, October, 2006),   new Date(28, November, 2006),  28),
            new(new Date(31, August, 2007),    new Date(28, February, 2008), 178),
            new(new Date(28, February, 2008),  new Date(28, August, 2008),   180),
            new(new Date(28, February, 2008),  new Date(30, August, 2008),   182),
            new(new Date(28, February, 2008),  new Date(31, August, 2008),   183),
            new(new Date(26, February, 2007),  new Date(28, February, 2008), 362),
            new(new Date(26, February, 2007),  new Date(29, February, 2008), 363),
            new(new Date(29, February, 2008),  new Date(28, February, 2009), 359),
            new(new Date(28, February, 2008),  new Date(30, March, 2008),     32),
            new(new Date(28, February, 2008),  new Date(31, March, 2008),     33),
        ];

        CheckDayCounts(dayCounter, data);
    }

    [Fact]
    public void TestThirty360_EurobondBasis()
    {
        // Testing 30/360 day counter (Eurobond Basis)...

        // See https://www.isda.org/2008/12/22/30-360-day-count-conventions/

        DayCounter dayCounter = new Thirty360(Thirty360Convention.EurobondBasis);

        Thirty360Case[] data =
        [
            // Example 1: End dates do not involve the last day of February
            new(new Date(20, August, 2006),    new Date(20, February, 2007), 180),
            new(new Date(20, February, 2007),  new Date(20, August, 2007),   180),
            new(new Date(20, August, 2007),    new Date(20, February, 2008), 180),
            new(new Date(20, February, 2008),  new Date(20, August, 2008),   180),
            new(new Date(20, August, 2008),    new Date(20, February, 2009), 180),
            new(new Date(20, February, 2009),  new Date(20, August, 2009),   180),

            // Example 2: End dates include some end-February dates
            new(new Date(28, February, 2006),  new Date(31, August, 2006),   182),
            new(new Date(31, August, 2006),    new Date(28, February, 2007), 178),
            new(new Date(28, February, 2007),  new Date(31, August, 2007),   182),
            new(new Date(31, August, 2007),    new Date(29, February, 2008), 179),
            new(new Date(29, February, 2008),  new Date(31, August, 2008),   181),
            new(new Date(31, August, 2008),    new Date(28, Feb, 2009),      178),
            new(new Date(28, February, 2009),  new Date(31, August, 2009),   182),
            new(new Date(31, August, 2009),    new Date(28, Feb, 2010),      178),
            new(new Date(28, February, 2010),  new Date(31, August, 2010),   182),
            new(new Date(31, August, 2010),    new Date(28, Feb, 2011),      178),
            new(new Date(28, February, 2011),  new Date(31, August, 2011),   182),
            new(new Date(31, August, 2011),    new Date(29, Feb, 2012),      179),

            // Example 3: Miscellaneous calculations
            new(new Date(31, January, 2006),   new Date(28, February, 2006),  28),
            new(new Date(30, January, 2006),   new Date(28, February, 2006),  28),
            new(new Date(28, February, 2006),  new Date(3, March, 2006),       5),
            new(new Date(14, February, 2006),  new Date(28, February, 2006),  14),
            new(new Date(30, September, 2006), new Date(31, October, 2006),   30),
            new(new Date(31, October, 2006),   new Date(28, November, 2006),  28),
            new(new Date(31, August, 2007),    new Date(28, February, 2008), 178),
            new(new Date(28, February, 2008),  new Date(28, August, 2008),   180),
            new(new Date(28, February, 2008),  new Date(30, August, 2008),   182),
            new(new Date(28, February, 2008),  new Date(31, August, 2008),   182),
            new(new Date(26, February, 2007),  new Date(28, February, 2008), 362),
            new(new Date(26, February, 2007),  new Date(29, February, 2008), 363),
            new(new Date(29, February, 2008),  new Date(28, February, 2009), 359),
            new(new Date(28, February, 2008),  new Date(30, March, 2008),     32),
            new(new Date(28, February, 2008),  new Date(31, March, 2008),     32),
        ];

        CheckDayCounts(dayCounter, data);
    }

    [Fact]
    public void TestThirty360_USA()
    {
        // Testing 30/360 day counter (USA)...

        // See https://en.wikipedia.org/wiki/Day_count_convention#30/360_US

        DayCounter dayCounter = new Thirty360(Thirty360Convention.USA);

        Thirty360Case[] data =
        [
            // Example 1: End dates do not involve the last day of February
            new(new Date(20, August, 2006),    new Date(20, February, 2007), 180),
            new(new Date(20, February, 2007),  new Date(20, August, 2007),   180),
            new(new Date(20, August, 2007),    new Date(20, February, 2008), 180),
            new(new Date(20, February, 2008),  new Date(20, August, 2008),   180),
            new(new Date(20, August, 2008),    new Date(20, February, 2009), 180),
            new(new Date(20, February, 2009),  new Date(20, August, 2009),   180),

            // Example 2: End dates include some end-February dates
            new(new Date(31, August, 2006),    new Date(28, February, 2007), 178),
            new(new Date(28, February, 2007),  new Date(31, August, 2007),   180),
            new(new Date(31, August, 2007),    new Date(29, February, 2008), 179),
            new(new Date(29, February, 2008),  new Date(31, August, 2008),   180),
            new(new Date(31, August, 2008),    new Date(28, February, 2009), 178),
            new(new Date(28, February, 2009),  new Date(31, August, 2009),   180),

            // Example 3: Miscellaneous calculations
            new(new Date(31, January, 2006),   new Date(28, February, 2006),  28),
            new(new Date(30, January, 2006),   new Date(28, February, 2006),  28),
            new(new Date(28, February, 2006),  new Date(3, March, 2006),       3),
            new(new Date(14, February, 2006),  new Date(28, February, 2006),  14),
            new(new Date(30, September, 2006), new Date(31, October, 2006),   30),
            new(new Date(31, October, 2006),   new Date(28, November, 2006),  28),
            new(new Date(31, August, 2007),    new Date(28, February, 2008), 178),
            new(new Date(28, February, 2008),  new Date(28, August, 2008),   180),
            new(new Date(28, February, 2008),  new Date(30, August, 2008),   182),
            new(new Date(28, February, 2008),  new Date(31, August, 2008),   183),
            new(new Date(26, February, 2007),  new Date(28, February, 2008), 362),
            new(new Date(26, February, 2007),  new Date(29, February, 2008), 363),
            new(new Date(29, February, 2008),  new Date(28, February, 2009), 360),
            new(new Date(28, February, 2008),  new Date(30, March, 2008),     32),
            new(new Date(28, February, 2008),  new Date(31, March, 2008),     33),
        ];

        CheckDayCounts(dayCounter, data);
    }

    [Fact]
    public void TestThirty360_ISDA()
    {
        // Testing 30/360 day counter (ISDA)...

        // See https://www.isda.org/2008/12/22/30-360-day-count-conventions/

        Thirty360Case[] data1 =
        [
            // Example 1: End dates do not involve the last day of February
            new(new Date(20, August, 2006),    new Date(20, February, 2007), 180),
            new(new Date(20, February, 2007),  new Date(20, August, 2007),   180),
            new(new Date(20, August, 2007),    new Date(20, February, 2008), 180),
            new(new Date(20, February, 2008),  new Date(20, August, 2008),   180),
            new(new Date(20, August, 2008),    new Date(20, February, 2009), 180),
            new(new Date(20, February, 2009),  new Date(20, August, 2009),   180),
        ];

        var terminationDate = new Date(20, August, 2009);
        var dayCounter = new Thirty360(Thirty360Convention.ISDA, terminationDate);

        CheckDayCounts(dayCounter, data1);

        Thirty360Case[] data2 =
        [
            // Example 2: End dates include some end-February dates
            new(new Date(28, February, 2006),  new Date(31, August, 2006),   180),
            new(new Date(31, August, 2006),    new Date(28, February, 2007), 180),
            new(new Date(28, February, 2007),  new Date(31, August, 2007),   180),
            new(new Date(31, August, 2007),    new Date(29, February, 2008), 180),
            new(new Date(29, February, 2008),  new Date(31, August, 2008),   180),
            new(new Date(31, August, 2008),    new Date(28, February, 2009), 180),
            new(new Date(28, February, 2009),  new Date(31, August, 2009),   180),
            new(new Date(31, August, 2009),    new Date(28, February, 2010), 180),
            new(new Date(28, February, 2010),  new Date(31, August, 2010),   180),
            new(new Date(31, August, 2010),    new Date(28, February, 2011), 180),
            new(new Date(28, February, 2011),  new Date(31, August, 2011),   180),
            new(new Date(31, August, 2011),    new Date(29, February, 2012), 179),
        ];

        terminationDate = new Date(29, February, 2012);
        dayCounter = new Thirty360(Thirty360Convention.ISDA, terminationDate);

        CheckDayCounts(dayCounter, data2);

        Thirty360Case[] data3 =
        [
            // Example 3: Miscellaneous calculations
            new(new Date(31, January, 2006),   new Date(28, February, 2006),  30),
            new(new Date(30, January, 2006),   new Date(28, February, 2006),  30),
            new(new Date(28, February, 2006),  new Date(3, March, 2006),       3),
            new(new Date(14, February, 2006),  new Date(28, February, 2006),  16),
            new(new Date(30, September, 2006), new Date(31, October, 2006),   30),
            new(new Date(31, October, 2006),   new Date(28, November, 2006),  28),
            new(new Date(31, August, 2007),    new Date(28, February, 2008), 178),
            new(new Date(28, February, 2008),  new Date(28, August, 2008),   180),
            new(new Date(28, February, 2008),  new Date(30, August, 2008),   182),
            new(new Date(28, February, 2008),  new Date(31, August, 2008),   182),
            new(new Date(28, February, 2007),  new Date(28, February, 2008), 358),
            new(new Date(28, February, 2007),  new Date(29, February, 2008), 359),
            new(new Date(29, February, 2008),  new Date(28, February, 2009), 360),
            new(new Date(29, February, 2008),  new Date(30, March, 2008),     30),
            new(new Date(29, February, 2008),  new Date(31, March, 2008),     30),
        ];

        terminationDate = new Date(29, February, 2008);
        dayCounter = new Thirty360(Thirty360Convention.ISDA, terminationDate);

        CheckDayCounts(dayCounter, data3);
    }

    [Fact]
    public void TestActual365_Canadian()
    {
        // Testing that Actual/365 (Canadian) throws when needed...

        var dayCounter = new Actual365Fixed(Actual365FixedConvention.Canadian);

        // no reference period
        CheckThrowsError(() =>
            dayCounter.YearFraction(
                new Date(10, September, 2018),
                new Date(10, September, 2019)));

        // reference period shorter than a month
        CheckThrowsError(() =>
            dayCounter.YearFraction(
                new Date(10, September, 2018),
                new Date(12, September, 2018),
                new Date(10, September, 2018),
                new Date(15, September, 2018)));

        // reference period longer than a year
        CheckThrowsError(() =>
            dayCounter.YearFraction(
                new Date(8, January, 2025),
                new Date(8, January, 2027),
                new Date(8, January, 2025),
                new Date(8, January, 2027)));
    }

    // Adapted: only the two Actual360 checks, lines 1309 and 1312, with the counters and year fractions they read
    // (lines 1291, 1295-1296, 1300 and 1303-1304). Dropped: the Actual366, Actual364 and Actual36525 counters, which
    // aren't ported, with their year fractions and checks (lines 1292-1294, 1301-1302, 1305, 1307-1308 and 1310-1311);
    // and the high-resolution dates (lines 1273-1276 and 1284-1288), which the default build leaves out.
    [Fact]
    public void TestActualConsistency()
    {
        // Testing consistency between different actual day-counters...

        Date[] todayDates =
        [
            new(12, January, 2022),
        ];

        Date[] testDates =
        [
            new(1, February, 2023), new(4, February, 2023), new(16, May, 2024),
            new(17, December, 2024), new(17, December, 2025), new(19, December, 2026),
            new(2, January, 2027), new(13, March, 2028), new(15, May, 2028),
            new(26, July, 2036),
        ];

        DayCounter actual365 = new Actual365Fixed();
        DayCounter actual360 = new Actual360();
        DayCounter actual360incl = new Actual360(true);

        foreach (Date today in todayDates)
        {
            foreach (Date d in testDates)
            {
                double t365 = actual365.YearFraction(today, d);
                double t360 = actual360.YearFraction(today, d);
                double t360incl = actual360incl.YearFraction(today, d);

                CheckSmall((t365 * 365 / 360.0) - t360, 1e-14);
                CheckSmall((t365 * 365 / 360.0) - (((t360incl * 360) - 1) / 360), 1e-14);
            }
        }
    }

    private static double ISMAYearFractionWithReferenceDates(
        DayCounter dayCounter, Date start, Date end, Date refStart, Date refEnd)
    {
        Real referenceDayCount = dayCounter.DayCount(refStart, refEnd);

        // guess how many coupon periods per year:
        var couponsPerYear = (Integer)Math.Round(365.0 / referenceDayCount, MidpointRounding.AwayFromZero);

        // the above is good enough for annual or semi annual payments.
        return dayCounter.DayCount(start, end)
            / (referenceDayCount * couponsPerYear);
    }

    private static double ActualActualDaycountComputation(Schedule schedule, Date start, Date end)
    {
        DayCounter daycounter = new ActualActual(ActualActualConvention.ISMA, schedule);
        double yearFraction = 0.0;

        for (Size i = 1; i < schedule.Size - 1; i++)
        {
            Date referenceStart = schedule.Date(i);
            Date referenceEnd = schedule.Date(i + 1);
            if (start < referenceEnd && end > referenceStart)
            {
                yearFraction += ISMAYearFractionWithReferenceDates(
                    daycounter,
                    (start > referenceStart) ? start : referenceStart,
                    (end < referenceEnd) ? end : referenceEnd,
                    referenceStart,
                    referenceEnd);
            }
        }

        return yearFraction;
    }

    private static void CheckDayCounts(DayCounter dayCounter, Thirty360Case[] data)
    {
        foreach (Thirty360Case x in data)
        {
            int calculated = dayCounter.DayCount(x.Start, x.End);
            if (calculated != x.Expected)
            {
                Assert.Fail(
                    $"from {x.Start} to {x.End}:\n"
                    + $"    calculated: {calculated}\n"
                    + $"    expected:   {x.Expected}");
            }
        }
    }

    // BOOST_CHECK_THROW(..., Error): QuantLib's Error is an ArgumentException or a NanoQLException in nanoql.
    private static void CheckThrowsError(Action action)
    {
        Exception? e = Record.Exception(action);
        Assert.True(
            e is ArgumentException or NanoQLException,
            $"expected an ArgumentException or a NanoQLException, got {e?.GetType().Name ?? "no exception"}");
    }

    // QL_CHECK_SMALL: |fpv| <= tolerance.
    private static void CheckSmall(double fpv, double tolerance) =>
        Assert.True(Math.Abs(fpv) <= tolerance, $"|{fpv}| exceeds {tolerance}");

    private static string IsmaMessage(
        DayCounter dayCounter,
        Date d1,
        Date d2,
        Date firstCouponDate,
        Date penultimateCouponDate,
        double calculated,
        double expected) =>
        $"{dayCounter.Name}:\n"
        + $"period:                {d1} to {d2}\n"
        + $"firstCouponDate:       {firstCouponDate}\n"
        + $"penultimateCouponDate: {penultimateCouponDate}\n"
        + $"    calculated: {calculated}\n"
        + $"    expected:   {expected}";

    private static string Show(Date? d) => d?.ToString() ?? "null date";

    private sealed record SingleCase(
        ActualActualConvention Convention, Date Start, Date End, Date? RefStart, Date? RefEnd, double Result)
    {
        public SingleCase(ActualActualConvention convention, Date start, Date end, double result)
            : this(convention, start, end, null, null, result)
        {
        }
    }

    private readonly record struct Thirty360Case(Date Start, Date End, int Expected);
}
