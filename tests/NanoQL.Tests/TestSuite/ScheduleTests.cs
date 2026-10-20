// SPDX-FileCopyrightText: Copyright (C) 2012 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/schedule.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class ScheduleTests
{
    // QuantLib's test runner's global evaluation date. The rule-based constructor takes the context, which it reads
    // only for a null effective date; no case here passes one.
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(16, Month.September, 2015) };

    [Fact]
    public void TestDailySchedule()
    {
        // Testing schedule with daily frequency...
        var startDate = new Date(17, Month.January, 2012);

        Schedule s = new MakeSchedule().From(startDate).To(startDate + 7)
            .WithCalendar(new TARGET())
            .WithFrequency(Frequency.Daily)
            .WithConvention(BusinessDayConvention.Preceding)
            .ToSchedule();

        // The schedule should skip Saturday 21st and Sunday 22rd. Previously, it would adjust them to Friday 20th,
        // resulting in three copies of the same date.
        Date[] expected =
        [
            new(17, Month.January, 2012),
            new(18, Month.January, 2012),
            new(19, Month.January, 2012),
            new(20, Month.January, 2012),
            new(23, Month.January, 2012),
            new(24, Month.January, 2012),
        ];
        CheckDates(s, expected);
    }

    [Fact]
    public void TestEomAdjustment()
    {
        // Testing end-of-month adjustment...
        var startDate = new Date(29, Month.February, 2024);
        Date endDate = startDate + new Period(1, TimeUnit.Years);

        Schedule s1 = new MakeSchedule().From(startDate).To(endDate)
            .WithCalendar(new TARGET())
            .WithFrequency(Frequency.Monthly)
            .WithConvention(BusinessDayConvention.Unadjusted)
            .EndOfMonth()
            .ToSchedule();

        CheckDates(s1,
        [
            new(29, Month.February, 2024),
            new(31, Month.March, 2024),
            new(30, Month.April, 2024),
            new(31, Month.May, 2024),
            new(30, Month.June, 2024),
            new(31, Month.July, 2024),
            new(31, Month.August, 2024),
            new(30, Month.September, 2024),
            new(31, Month.October, 2024),
            new(30, Month.November, 2024),
            new(31, Month.December, 2024),
            new(31, Month.January, 2025),
            new(28, Month.February, 2025),
        ]);

        Schedule s2 = new MakeSchedule().From(startDate).To(endDate)
            .WithCalendar(new TARGET())
            .WithFrequency(Frequency.Monthly)
            .WithConvention(BusinessDayConvention.Following)
            .EndOfMonth()
            .ToSchedule();

        CheckDates(s2,
        [
            new(29, Month.February, 2024),
            new(2, Month.April, 2024),
            new(30, Month.April, 2024),
            new(31, Month.May, 2024),
            new(1, Month.July, 2024),
            new(31, Month.July, 2024),
            new(2, Month.September, 2024),
            new(30, Month.September, 2024),
            new(31, Month.October, 2024),
            new(2, Month.December, 2024),
            new(31, Month.December, 2024),
            new(31, Month.January, 2025),
            new(28, Month.February, 2025),
        ]);

        Schedule s3 = new MakeSchedule().From(startDate).To(endDate)
            .WithCalendar(new TARGET())
            .WithFrequency(Frequency.Monthly)
            .WithConvention(BusinessDayConvention.ModifiedPreceding)
            .EndOfMonth()
            .ToSchedule();

        CheckDates(s3,
        [
            new(29, Month.February, 2024),
            new(28, Month.March, 2024),
            new(30, Month.April, 2024),
            new(31, Month.May, 2024),
            new(28, Month.June, 2024),
            new(31, Month.July, 2024),
            new(30, Month.August, 2024),
            new(30, Month.September, 2024),
            new(31, Month.October, 2024),
            new(29, Month.November, 2024),
            new(31, Month.December, 2024),
            new(31, Month.January, 2025),
            new(28, Month.February, 2025),
        ]);
    }

    [Fact]
    public void TestEndDateWithEomAdjustment()
    {
        // Testing end date for schedule with end-of-month adjustment...
        Schedule s = new MakeSchedule().From(new Date(30, Month.September, 2009))
            .To(new Date(15, Month.June, 2012))
            .WithCalendar(new Japan())
            .WithTenor(new Period(6, TimeUnit.Months))
            .WithConvention(BusinessDayConvention.ModifiedFollowing)
            .WithTerminationDateConvention(BusinessDayConvention.ModifiedFollowing)
            .Forwards()
            .EndOfMonth()
            .ToSchedule();

        Date[] expected =
        [
            new(30, Month.September, 2009),
            new(31, Month.March, 2010),
            new(30, Month.September, 2010),
            new(31, Month.March, 2011),
            new(30, Month.September, 2011),
            new(30, Month.March, 2012),
            new(15, Month.June, 2012),
        ];

        CheckDates(s, expected);
    }

    [Fact]
    public void TestDatesPastEndDateWithEomAdjustment()
    {
        // Testing that no dates are past the end date with EOM adjustment...
        Schedule s = new MakeSchedule().From(new Date(28, Month.March, 2013))
            .To(new Date(30, Month.March, 2015))
            .WithCalendar(new TARGET())
            .WithTenor(new Period(1, TimeUnit.Years))
            .WithConvention(BusinessDayConvention.Unadjusted)
            .WithTerminationDateConvention(BusinessDayConvention.Unadjusted)
            .Forwards()
            .EndOfMonth()
            .ToSchedule();

        Date[] expected =
        [
            new(28, Month.March, 2013),
            new(31, Month.March, 2014),

            // March 31st 2015, coming from the EOM adjustment of March 28th, should be discarded as past the end date.
            new(30, Month.March, 2015),
        ];
        CheckDates(s, expected);

        // also, the last period should not be regular.
        Assert.False(s.IsRegular(2), "last period should not be regular");
    }

    [Fact]
    public void TestDatesSameAsEndDateWithEomAdjustment()
    {
        // Testing that next-to-last date same as end date is removed...
        Schedule s = new MakeSchedule().From(new Date(28, Month.March, 2013))
            .To(new Date(31, Month.March, 2015))
            .WithCalendar(new TARGET())
            .WithTenor(new Period(1, TimeUnit.Years))
            .WithConvention(BusinessDayConvention.Unadjusted)
            .WithTerminationDateConvention(BusinessDayConvention.Unadjusted)
            .Forwards()
            .EndOfMonth()
            .ToSchedule();

        Date[] expected =
        [
            new(28, Month.March, 2013),
            new(31, Month.March, 2014),

            // March 31st 2015, coming from the EOM adjustment of March 28th, should be discarded as the same as the
            // end date.
            new(31, Month.March, 2015),
        ];
        CheckDates(s, expected);

        // also, the last period should be regular.
        Assert.True(s.IsRegular(2), "last period should be regular");
    }

    [Fact]
    public void TestForwardDatesWithEomAdjustment()
    {
        // Testing that the last date is not adjusted for EOM when termination date convention is unadjusted...
        Schedule s = new MakeSchedule().From(new Date(31, Month.August, 1996))
            .To(new Date(15, Month.September, 1997))
            .WithCalendar(new UnitedStates(UnitedStatesMarket.GovernmentBond))
            .WithTenor(new Period(6, TimeUnit.Months))
            .WithConvention(BusinessDayConvention.Unadjusted)
            .WithTerminationDateConvention(BusinessDayConvention.Unadjusted)
            .Forwards()
            .EndOfMonth()
            .ToSchedule();

        Date[] expected =
        [
            new(31, Month.August, 1996),
            new(28, Month.February, 1997),
            new(31, Month.August, 1997),
            new(15, Month.September, 1997),
        ];
        CheckDates(s, expected);
    }

    [Fact]
    public void TestBackwardDatesWithEomAdjustment()
    {
        // Testing that the first date is not adjusted for EOM going backward when termination date convention is
        // unadjusted...
        Schedule s = new MakeSchedule().From(new Date(22, Month.August, 1996))
            .To(new Date(31, Month.August, 1997))
            .WithCalendar(new UnitedStates(UnitedStatesMarket.GovernmentBond))
            .WithTenor(new Period(6, TimeUnit.Months))
            .WithConvention(BusinessDayConvention.Unadjusted)
            .WithTerminationDateConvention(BusinessDayConvention.Unadjusted)
            .Backwards()
            .EndOfMonth()
            .ToSchedule();

        Date[] expected =
        [
            new(22, Month.August, 1996),
            new(31, Month.August, 1996),
            new(28, Month.February, 1997),
            new(31, Month.August, 1997),
        ];
        CheckDates(s, expected);
    }

    [Fact]
    public void TestDoubleFirstDateWithEomAdjustment()
    {
        // Testing that the first date is not duplicated due to EOM convention when going backwards...
        Schedule s = new MakeSchedule().From(new Date(22, Month.August, 1996))
            .To(new Date(31, Month.August, 1997))
            .WithCalendar(new UnitedStates(UnitedStatesMarket.GovernmentBond))
            .WithTenor(new Period(6, TimeUnit.Months))
            .WithConvention(BusinessDayConvention.ModifiedFollowing)
            .WithTerminationDateConvention(BusinessDayConvention.Following)
            .Backwards()
            .EndOfMonth()
            .ToSchedule();

        Date[] expected =
        [
            new(22, Month.August, 1996),
            new(30, Month.August, 1996),
            new(28, Month.February, 1997),
            new(2, Month.September, 1997),
        ];
        CheckDates(s, expected);
    }

    [Fact]
    public void TestFirstDateWithEomAdjustment()
    {
        // Testing schedule with first date and EOM adjustment...
        Schedule schedule = new MakeSchedule()
            .From(new Date(10, Month.August, 1996))
            .To(new Date(10, Month.August, 1998))
            .WithFirstDate(new Date(28, Month.February, 1997))
            .WithCalendar(new UnitedStates(UnitedStatesMarket.GovernmentBond))
            .WithTenor(new Period(6, TimeUnit.Months))
            .WithConvention(BusinessDayConvention.ModifiedFollowing)
            .WithTerminationDateConvention(BusinessDayConvention.ModifiedFollowing)
            .Forwards()
            .EndOfMonth()
            .ToSchedule();

        Date[] expected =
        [
            new(12, Month.August, 1996),
            new(28, Month.February, 1997),
            new(29, Month.August, 1997),
            new(27, Month.February, 1998),
            new(10, Month.August, 1998),
        ];
        CheckDates(schedule, expected);
    }

    [Fact]
    public void TestNextToLastWithEomAdjustment()
    {
        // Testing schedule with next-to-last date and EOM adjustment...
        Schedule schedule = new MakeSchedule()
            .From(new Date(10, Month.August, 1996))
            .To(new Date(10, Month.August, 1998))
            .WithNextToLastDate(new Date(28, Month.February, 1998))
            .WithCalendar(new UnitedStates(UnitedStatesMarket.GovernmentBond))
            .WithTenor(new Period(6, TimeUnit.Months))
            .WithConvention(BusinessDayConvention.ModifiedFollowing)
            .WithTerminationDateConvention(BusinessDayConvention.ModifiedFollowing)
            .Backwards()
            .EndOfMonth()
            .ToSchedule();

        Date[] expected =
        [
            new(12, Month.August, 1996),
            new(30, Month.August, 1996),
            new(28, Month.February, 1997),
            new(29, Month.August, 1997),
            new(27, Month.February, 1998),
            new(10, Month.August, 1998),
        ];
        CheckDates(schedule, expected);
    }

    [Fact]
    public void TestEffectiveDateWithEomAdjustment()
    {
        // Testing forward schedule with EOM adjustment and effective date and first date in the same month...
        Schedule s = new MakeSchedule().From(new Date(16, Month.January, 2023))
            .To(new Date(16, Month.March, 2023))
            .WithFirstDate(new Date(31, Month.January, 2023))
            .WithCalendar(new NullCalendar())
            .WithTenor(new Period(1, TimeUnit.Months))
            .WithConvention(BusinessDayConvention.Unadjusted)
            .WithTerminationDateConvention(BusinessDayConvention.Unadjusted)
            .Forwards()
            .EndOfMonth()
            .ToSchedule();

        // check that the effective date is not moved at the end of the month
        Date[] expected =
        [
            new(16, Month.January, 2023),
            new(31, Month.January, 2023),
            new(28, Month.February, 2023),
            new(16, Month.March, 2023),
        ];
        CheckDates(s, expected);
    }

    [Fact]
    public void TestCDS2015Convention()
    {
        // Testing CDS2015 semi-annual rolling convention...

        DateGenerationRule rule = DateGenerationRule.CDS2015;
        var tenor = new Period(5, TimeUnit.Years);

        // From September 20th 2016 to March 19th 2017 of the next year, end date is December 20th 2021 for a 5 year
        // CDS. To get the correct schedule, you can first use the cdsMaturity function to get the maturity from the
        // tenor.
        var tradeDate = new Date(12, Month.December, 2016);
        Date? maturity = CreditDefaultSwapFunctions.CdsMaturity(tradeDate, tenor, rule);
        var expStart = new Date(20, Month.September, 2016);
        var expMaturity = new Date(20, Month.December, 2021);
        Assert.Equal(expMaturity, maturity);
        Schedule s = MakeCdsSchedule(tradeDate, maturity!.Value, rule);
        Assert.Equal(expStart, s.StartDate());
        Assert.Equal(expMaturity, s.EndDate());

        // If we just use 12 Dec 2016 + 5Y = 12 Dec 2021 as termination date in the schedule, the schedule constructor
        // can use any of the allowable CDS dates i.e. 20 Mar, Jun, Sep and Dec. In the constructor, we just use the
        // next one here i.e. 20 Dec 2021. We get the same results as above.
        maturity = tradeDate + tenor;
        s = MakeCdsSchedule(tradeDate, maturity.Value, rule);
        Assert.Equal(expStart, s.StartDate());
        Assert.Equal(expMaturity, s.EndDate());

        // We do the same tests but with a trade date of 1 Mar 2017. Using cdsMaturity to get maturity date from 5Y
        // tenor, we get the same maturity as above.
        tradeDate = new Date(1, Month.March, 2017);
        maturity = CreditDefaultSwapFunctions.CdsMaturity(tradeDate, tenor, rule);
        Assert.Equal(expMaturity, maturity);
        s = MakeCdsSchedule(tradeDate, maturity!.Value, rule);
        expStart = new Date(20, Month.December, 2016);
        Assert.Equal(expStart, s.StartDate());
        Assert.Equal(expMaturity, s.EndDate());

        // Using 1 Mar 2017 + 5Y = 1 Mar 2022 as termination date in the schedule, the constructor just uses the next
        // allowable CDS date i.e. 20 Mar 2022. We must update the expected maturity.
        maturity = tradeDate + tenor;
        s = MakeCdsSchedule(tradeDate, maturity.Value, rule);
        Assert.Equal(expStart, s.StartDate());
        expMaturity = new Date(20, Month.March, 2022);
        Assert.Equal(expMaturity, s.EndDate());

        // From March 20th 2017 to September 19th 2017, end date is June 20th 2022 for a 5 year CDS.
        tradeDate = new Date(20, Month.March, 2017);
        maturity = CreditDefaultSwapFunctions.CdsMaturity(tradeDate, tenor, rule);
        expStart = new Date(20, Month.March, 2017);
        expMaturity = new Date(20, Month.June, 2022);
        Assert.Equal(expMaturity, maturity);
        s = MakeCdsSchedule(tradeDate, maturity!.Value, rule);
        Assert.Equal(expStart, s.StartDate());
        Assert.Equal(expMaturity, s.EndDate());
    }

    [Fact]
    public void TestCDS2015ConventionGrid()
    {
        // Testing against section 11 of ISDA doc FAQs Amending when Single Name CDS roll to new on-the-run contracts
        // December 20, 2015 Go-Live
        // Testing CDS2015 convention against ISDA doc...

        // Test inputs and expected outputs
        // Each row is the trade date and the CDS tenor, then the expected start and end date.
        // The trade dates are from the transition dates in the doc i.e. 20th Mar, Jun, Sep and Dec in 2016 and a day
        // either side. The tenors are selected tenors from the doc i.e. short quarterly tenors less than 1Y, 1Y and 5Y.
        (Date From, Period Tenor, Date ExpStart, Date ExpEnd)[] inputs =
        [
                (new Date(19, Month.March, 2016), new Period(3, TimeUnit.Months), new Date(21, Month.December, 2015), new Date(20, Month.March, 2016)),
                (new Date(20, Month.March, 2016), new Period(3, TimeUnit.Months), new Date(21, Month.December, 2015), new Date(20, Month.September, 2016)),
                (new Date(21, Month.March, 2016), new Period(3, TimeUnit.Months), new Date(21, Month.March, 2016), new Date(20, Month.September, 2016)),
                (new Date(19, Month.June, 2016), new Period(3, TimeUnit.Months), new Date(21, Month.March, 2016), new Date(20, Month.September, 2016)),
                (new Date(20, Month.June, 2016), new Period(3, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.September, 2016)),
                (new Date(21, Month.June, 2016), new Period(3, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.September, 2016)),
                (new Date(19, Month.September, 2016), new Period(3, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.September, 2016)),
                (new Date(20, Month.September, 2016), new Period(3, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.March, 2017)),
                (new Date(21, Month.September, 2016), new Period(3, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.March, 2017)),
                (new Date(19, Month.December, 2016), new Period(3, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.March, 2017)),
                (new Date(20, Month.December, 2016), new Period(3, TimeUnit.Months), new Date(20, Month.December, 2016), new Date(20, Month.March, 2017)),
                (new Date(21, Month.December, 2016), new Period(3, TimeUnit.Months), new Date(20, Month.December, 2016), new Date(20, Month.March, 2017)),
                (new Date(19, Month.March, 2016), new Period(6, TimeUnit.Months), new Date(21, Month.December, 2015), new Date(20, Month.June, 2016)),
                (new Date(20, Month.March, 2016), new Period(6, TimeUnit.Months), new Date(21, Month.December, 2015), new Date(20, Month.December, 2016)),
                (new Date(21, Month.March, 2016), new Period(6, TimeUnit.Months), new Date(21, Month.March, 2016), new Date(20, Month.December, 2016)),
                (new Date(19, Month.June, 2016), new Period(6, TimeUnit.Months), new Date(21, Month.March, 2016), new Date(20, Month.December, 2016)),
                (new Date(20, Month.June, 2016), new Period(6, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.December, 2016)),
                (new Date(21, Month.June, 2016), new Period(6, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.December, 2016)),
                (new Date(19, Month.September, 2016), new Period(6, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.December, 2016)),
                (new Date(20, Month.September, 2016), new Period(6, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.June, 2017)),
                (new Date(21, Month.September, 2016), new Period(6, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.June, 2017)),
                (new Date(19, Month.December, 2016), new Period(6, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.June, 2017)),
                (new Date(20, Month.December, 2016), new Period(6, TimeUnit.Months), new Date(20, Month.December, 2016), new Date(20, Month.June, 2017)),
                (new Date(21, Month.December, 2016), new Period(6, TimeUnit.Months), new Date(20, Month.December, 2016), new Date(20, Month.June, 2017)),
                (new Date(19, Month.March, 2016), new Period(9, TimeUnit.Months), new Date(21, Month.December, 2015), new Date(20, Month.September, 2016)),
                (new Date(20, Month.March, 2016), new Period(9, TimeUnit.Months), new Date(21, Month.December, 2015), new Date(20, Month.March, 2017)),
                (new Date(21, Month.March, 2016), new Period(9, TimeUnit.Months), new Date(21, Month.March, 2016), new Date(20, Month.March, 2017)),
                (new Date(19, Month.June, 2016), new Period(9, TimeUnit.Months), new Date(21, Month.March, 2016), new Date(20, Month.March, 2017)),
                (new Date(20, Month.June, 2016), new Period(9, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.March, 2017)),
                (new Date(21, Month.June, 2016), new Period(9, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.March, 2017)),
                (new Date(19, Month.September, 2016), new Period(9, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.March, 2017)),
                (new Date(20, Month.September, 2016), new Period(9, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.September, 2017)),
                (new Date(21, Month.September, 2016), new Period(9, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.September, 2017)),
                (new Date(19, Month.December, 2016), new Period(9, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.September, 2017)),
                (new Date(20, Month.December, 2016), new Period(9, TimeUnit.Months), new Date(20, Month.December, 2016), new Date(20, Month.September, 2017)),
                (new Date(21, Month.December, 2016), new Period(9, TimeUnit.Months), new Date(20, Month.December, 2016), new Date(20, Month.September, 2017)),
                (new Date(19, Month.March, 2016), new Period(1, TimeUnit.Years), new Date(21, Month.December, 2015), new Date(20, Month.December, 2016)),
                (new Date(20, Month.March, 2016), new Period(1, TimeUnit.Years), new Date(21, Month.December, 2015), new Date(20, Month.June, 2017)),
                (new Date(21, Month.March, 2016), new Period(1, TimeUnit.Years), new Date(21, Month.March, 2016), new Date(20, Month.June, 2017)),
                (new Date(19, Month.June, 2016), new Period(1, TimeUnit.Years), new Date(21, Month.March, 2016), new Date(20, Month.June, 2017)),
                (new Date(20, Month.June, 2016), new Period(1, TimeUnit.Years), new Date(20, Month.June, 2016), new Date(20, Month.June, 2017)),
                (new Date(21, Month.June, 2016), new Period(1, TimeUnit.Years), new Date(20, Month.June, 2016), new Date(20, Month.June, 2017)),
                (new Date(19, Month.September, 2016), new Period(1, TimeUnit.Years), new Date(20, Month.June, 2016), new Date(20, Month.June, 2017)),
                (new Date(20, Month.September, 2016), new Period(1, TimeUnit.Years), new Date(20, Month.September, 2016), new Date(20, Month.December, 2017)),
                (new Date(21, Month.September, 2016), new Period(1, TimeUnit.Years), new Date(20, Month.September, 2016), new Date(20, Month.December, 2017)),
                (new Date(19, Month.December, 2016), new Period(1, TimeUnit.Years), new Date(20, Month.September, 2016), new Date(20, Month.December, 2017)),
                (new Date(20, Month.December, 2016), new Period(1, TimeUnit.Years), new Date(20, Month.December, 2016), new Date(20, Month.December, 2017)),
                (new Date(21, Month.December, 2016), new Period(1, TimeUnit.Years), new Date(20, Month.December, 2016), new Date(20, Month.December, 2017)),
                (new Date(19, Month.March, 2016), new Period(5, TimeUnit.Years), new Date(21, Month.December, 2015), new Date(20, Month.December, 2020)),
                (new Date(20, Month.March, 2016), new Period(5, TimeUnit.Years), new Date(21, Month.December, 2015), new Date(20, Month.June, 2021)),
                (new Date(21, Month.March, 2016), new Period(5, TimeUnit.Years), new Date(21, Month.March, 2016), new Date(20, Month.June, 2021)),
                (new Date(19, Month.June, 2016), new Period(5, TimeUnit.Years), new Date(21, Month.March, 2016), new Date(20, Month.June, 2021)),
                (new Date(20, Month.June, 2016), new Period(5, TimeUnit.Years), new Date(20, Month.June, 2016), new Date(20, Month.June, 2021)),
                (new Date(21, Month.June, 2016), new Period(5, TimeUnit.Years), new Date(20, Month.June, 2016), new Date(20, Month.June, 2021)),
                (new Date(19, Month.September, 2016), new Period(5, TimeUnit.Years), new Date(20, Month.June, 2016), new Date(20, Month.June, 2021)),
                (new Date(20, Month.September, 2016), new Period(5, TimeUnit.Years), new Date(20, Month.September, 2016), new Date(20, Month.December, 2021)),
                (new Date(21, Month.September, 2016), new Period(5, TimeUnit.Years), new Date(20, Month.September, 2016), new Date(20, Month.December, 2021)),
                (new Date(19, Month.December, 2016), new Period(5, TimeUnit.Years), new Date(20, Month.September, 2016), new Date(20, Month.December, 2021)),
                (new Date(20, Month.December, 2016), new Period(5, TimeUnit.Years), new Date(20, Month.December, 2016), new Date(20, Month.December, 2021)),
                (new Date(21, Month.December, 2016), new Period(5, TimeUnit.Years), new Date(20, Month.December, 2016), new Date(20, Month.December, 2021)),
                (new Date(20, Month.March, 2016), new Period(0, TimeUnit.Months), new Date(21, Month.December, 2015), new Date(20, Month.June, 2016)),
                (new Date(21, Month.March, 2016), new Period(0, TimeUnit.Months), new Date(21, Month.March, 2016), new Date(20, Month.June, 2016)),
                (new Date(19, Month.June, 2016), new Period(0, TimeUnit.Months), new Date(21, Month.March, 2016), new Date(20, Month.June, 2016)),
                (new Date(20, Month.September, 2016), new Period(0, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.December, 2016)),
                (new Date(21, Month.September, 2016), new Period(0, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.December, 2016)),
                (new Date(19, Month.December, 2016), new Period(0, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.December, 2016)),
        ];

        TestCdsConventions(inputs, DateGenerationRule.CDS2015);
    }

    [Fact]
    public void TestCDSConventionGrid()
    {
        // Testing against section 11 of ISDA doc FAQs Amending when Single Name CDS roll to new on-the-run contracts
        // December 20, 2015 Go-Live. Amended the dates in the doc to the pre-2015 expected maturity dates.
        // Testing CDS convention against ISDA doc...

        // Test inputs and expected outputs
        // Each row is the trade date and the CDS tenor, then the expected start and end date.
        // The trade dates are from the transition dates in the doc i.e. 20th Mar, Jun, Sep and Dec in 2016 and a day
        // either side. The tenors are selected tenors from the doc i.e. short quarterly tenors less than 1Y, 1Y and 5Y.
        (Date From, Period Tenor, Date ExpStart, Date ExpEnd)[] inputs =
        [
                (new Date(19, Month.March, 2016), new Period(3, TimeUnit.Months), new Date(21, Month.December, 2015), new Date(20, Month.June, 2016)),
                (new Date(20, Month.March, 2016), new Period(3, TimeUnit.Months), new Date(21, Month.December, 2015), new Date(20, Month.September, 2016)),
                (new Date(21, Month.March, 2016), new Period(3, TimeUnit.Months), new Date(21, Month.March, 2016), new Date(20, Month.September, 2016)),
                (new Date(19, Month.June, 2016), new Period(3, TimeUnit.Months), new Date(21, Month.March, 2016), new Date(20, Month.September, 2016)),
                (new Date(20, Month.June, 2016), new Period(3, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.December, 2016)),
                (new Date(21, Month.June, 2016), new Period(3, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.December, 2016)),
                (new Date(19, Month.September, 2016), new Period(3, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.December, 2016)),
                (new Date(20, Month.September, 2016), new Period(3, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.March, 2017)),
                (new Date(21, Month.September, 2016), new Period(3, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.March, 2017)),
                (new Date(19, Month.December, 2016), new Period(3, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.March, 2017)),
                (new Date(20, Month.December, 2016), new Period(3, TimeUnit.Months), new Date(20, Month.December, 2016), new Date(20, Month.June, 2017)),
                (new Date(21, Month.December, 2016), new Period(3, TimeUnit.Months), new Date(20, Month.December, 2016), new Date(20, Month.June, 2017)),
                (new Date(19, Month.March, 2016), new Period(6, TimeUnit.Months), new Date(21, Month.December, 2015), new Date(20, Month.September, 2016)),
                (new Date(20, Month.March, 2016), new Period(6, TimeUnit.Months), new Date(21, Month.December, 2015), new Date(20, Month.December, 2016)),
                (new Date(21, Month.March, 2016), new Period(6, TimeUnit.Months), new Date(21, Month.March, 2016), new Date(20, Month.December, 2016)),
                (new Date(19, Month.June, 2016), new Period(6, TimeUnit.Months), new Date(21, Month.March, 2016), new Date(20, Month.December, 2016)),
                (new Date(20, Month.June, 2016), new Period(6, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.March, 2017)),
                (new Date(21, Month.June, 2016), new Period(6, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.March, 2017)),
                (new Date(19, Month.September, 2016), new Period(6, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.March, 2017)),
                (new Date(20, Month.September, 2016), new Period(6, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.June, 2017)),
                (new Date(21, Month.September, 2016), new Period(6, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.June, 2017)),
                (new Date(19, Month.December, 2016), new Period(6, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.June, 2017)),
                (new Date(20, Month.December, 2016), new Period(6, TimeUnit.Months), new Date(20, Month.December, 2016), new Date(20, Month.September, 2017)),
                (new Date(21, Month.December, 2016), new Period(6, TimeUnit.Months), new Date(20, Month.December, 2016), new Date(20, Month.September, 2017)),
                (new Date(19, Month.March, 2016), new Period(9, TimeUnit.Months), new Date(21, Month.December, 2015), new Date(20, Month.December, 2016)),
                (new Date(20, Month.March, 2016), new Period(9, TimeUnit.Months), new Date(21, Month.December, 2015), new Date(20, Month.March, 2017)),
                (new Date(21, Month.March, 2016), new Period(9, TimeUnit.Months), new Date(21, Month.March, 2016), new Date(20, Month.March, 2017)),
                (new Date(19, Month.June, 2016), new Period(9, TimeUnit.Months), new Date(21, Month.March, 2016), new Date(20, Month.March, 2017)),
                (new Date(20, Month.June, 2016), new Period(9, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.June, 2017)),
                (new Date(21, Month.June, 2016), new Period(9, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.June, 2017)),
                (new Date(19, Month.September, 2016), new Period(9, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.June, 2017)),
                (new Date(20, Month.September, 2016), new Period(9, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.September, 2017)),
                (new Date(21, Month.September, 2016), new Period(9, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.September, 2017)),
                (new Date(19, Month.December, 2016), new Period(9, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.September, 2017)),
                (new Date(20, Month.December, 2016), new Period(9, TimeUnit.Months), new Date(20, Month.December, 2016), new Date(20, Month.December, 2017)),
                (new Date(21, Month.December, 2016), new Period(9, TimeUnit.Months), new Date(20, Month.December, 2016), new Date(20, Month.December, 2017)),
                (new Date(19, Month.March, 2016), new Period(1, TimeUnit.Years), new Date(21, Month.December, 2015), new Date(20, Month.March, 2017)),
                (new Date(20, Month.March, 2016), new Period(1, TimeUnit.Years), new Date(21, Month.December, 2015), new Date(20, Month.June, 2017)),
                (new Date(21, Month.March, 2016), new Period(1, TimeUnit.Years), new Date(21, Month.March, 2016), new Date(20, Month.June, 2017)),
                (new Date(19, Month.June, 2016), new Period(1, TimeUnit.Years), new Date(21, Month.March, 2016), new Date(20, Month.June, 2017)),
                (new Date(20, Month.June, 2016), new Period(1, TimeUnit.Years), new Date(20, Month.June, 2016), new Date(20, Month.September, 2017)),
                (new Date(21, Month.June, 2016), new Period(1, TimeUnit.Years), new Date(20, Month.June, 2016), new Date(20, Month.September, 2017)),
                (new Date(19, Month.September, 2016), new Period(1, TimeUnit.Years), new Date(20, Month.June, 2016), new Date(20, Month.September, 2017)),
                (new Date(20, Month.September, 2016), new Period(1, TimeUnit.Years), new Date(20, Month.September, 2016), new Date(20, Month.December, 2017)),
                (new Date(21, Month.September, 2016), new Period(1, TimeUnit.Years), new Date(20, Month.September, 2016), new Date(20, Month.December, 2017)),
                (new Date(19, Month.December, 2016), new Period(1, TimeUnit.Years), new Date(20, Month.September, 2016), new Date(20, Month.December, 2017)),
                (new Date(20, Month.December, 2016), new Period(1, TimeUnit.Years), new Date(20, Month.December, 2016), new Date(20, Month.March, 2018)),
                (new Date(21, Month.December, 2016), new Period(1, TimeUnit.Years), new Date(20, Month.December, 2016), new Date(20, Month.March, 2018)),
                (new Date(19, Month.March, 2016), new Period(5, TimeUnit.Years), new Date(21, Month.December, 2015), new Date(20, Month.March, 2021)),
                (new Date(20, Month.March, 2016), new Period(5, TimeUnit.Years), new Date(21, Month.December, 2015), new Date(20, Month.June, 2021)),
                (new Date(21, Month.March, 2016), new Period(5, TimeUnit.Years), new Date(21, Month.March, 2016), new Date(20, Month.June, 2021)),
                (new Date(19, Month.June, 2016), new Period(5, TimeUnit.Years), new Date(21, Month.March, 2016), new Date(20, Month.June, 2021)),
                (new Date(20, Month.June, 2016), new Period(5, TimeUnit.Years), new Date(20, Month.June, 2016), new Date(20, Month.September, 2021)),
                (new Date(21, Month.June, 2016), new Period(5, TimeUnit.Years), new Date(20, Month.June, 2016), new Date(20, Month.September, 2021)),
                (new Date(19, Month.September, 2016), new Period(5, TimeUnit.Years), new Date(20, Month.June, 2016), new Date(20, Month.September, 2021)),
                (new Date(20, Month.September, 2016), new Period(5, TimeUnit.Years), new Date(20, Month.September, 2016), new Date(20, Month.December, 2021)),
                (new Date(21, Month.September, 2016), new Period(5, TimeUnit.Years), new Date(20, Month.September, 2016), new Date(20, Month.December, 2021)),
                (new Date(19, Month.December, 2016), new Period(5, TimeUnit.Years), new Date(20, Month.September, 2016), new Date(20, Month.December, 2021)),
                (new Date(20, Month.December, 2016), new Period(5, TimeUnit.Years), new Date(20, Month.December, 2016), new Date(20, Month.March, 2022)),
                (new Date(21, Month.December, 2016), new Period(5, TimeUnit.Years), new Date(20, Month.December, 2016), new Date(20, Month.March, 2022)),
                (new Date(19, Month.March, 2016), new Period(0, TimeUnit.Months), new Date(21, Month.December, 2015), new Date(20, Month.March, 2016)),
                (new Date(20, Month.March, 2016), new Period(0, TimeUnit.Months), new Date(21, Month.December, 2015), new Date(20, Month.June, 2016)),
                (new Date(21, Month.March, 2016), new Period(0, TimeUnit.Months), new Date(21, Month.March, 2016), new Date(20, Month.June, 2016)),
                (new Date(19, Month.June, 2016), new Period(0, TimeUnit.Months), new Date(21, Month.March, 2016), new Date(20, Month.June, 2016)),
                (new Date(20, Month.June, 2016), new Period(0, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.September, 2016)),
                (new Date(21, Month.June, 2016), new Period(0, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.September, 2016)),
                (new Date(19, Month.September, 2016), new Period(0, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.September, 2016)),
                (new Date(20, Month.September, 2016), new Period(0, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.December, 2016)),
                (new Date(21, Month.September, 2016), new Period(0, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.December, 2016)),
                (new Date(19, Month.December, 2016), new Period(0, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.December, 2016)),
                (new Date(20, Month.December, 2016), new Period(0, TimeUnit.Months), new Date(20, Month.December, 2016), new Date(20, Month.March, 2017)),
                (new Date(21, Month.December, 2016), new Period(0, TimeUnit.Months), new Date(20, Month.December, 2016), new Date(20, Month.March, 2017)),
        ];

        TestCdsConventions(inputs, DateGenerationRule.CDS);
    }

    [Fact]
    public void TestOldCDSConventionGrid()
    {
        // Testing against section 11 of ISDA doc FAQs Amending when Single Name CDS roll to new on-the-run contracts
        // December 20, 2015 Go-Live. Amended the dates in the doc to the pre-2009 expected start and maturity dates.
        // Testing old CDS convention...

        // Test inputs and expected outputs
        // Each row is the trade date and the CDS tenor, then the expected start and end date.
        // The trade dates are from the transition dates in the doc i.e. 20th Mar, Jun, Sep and Dec in 2016 and a day
        // either side. The tenors are selected tenors from the doc i.e. short quarterly tenors less than 1Y, 1Y and 5Y.
        (Date From, Period Tenor, Date ExpStart, Date ExpEnd)[] inputs =
        [
                (new Date(19, Month.March, 2016), new Period(3, TimeUnit.Months), new Date(19, Month.March, 2016), new Date(20, Month.June, 2016)),
                (new Date(20, Month.March, 2016), new Period(3, TimeUnit.Months), new Date(20, Month.March, 2016), new Date(20, Month.September, 2016)),
                (new Date(21, Month.March, 2016), new Period(3, TimeUnit.Months), new Date(21, Month.March, 2016), new Date(20, Month.September, 2016)),
                (new Date(19, Month.June, 2016), new Period(3, TimeUnit.Months), new Date(19, Month.June, 2016), new Date(20, Month.September, 2016)),
                (new Date(20, Month.June, 2016), new Period(3, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.December, 2016)),
                (new Date(21, Month.June, 2016), new Period(3, TimeUnit.Months), new Date(21, Month.June, 2016), new Date(20, Month.December, 2016)),
                (new Date(19, Month.September, 2016), new Period(3, TimeUnit.Months), new Date(19, Month.September, 2016), new Date(20, Month.December, 2016)),
                (new Date(20, Month.September, 2016), new Period(3, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.March, 2017)),
                (new Date(21, Month.September, 2016), new Period(3, TimeUnit.Months), new Date(21, Month.September, 2016), new Date(20, Month.March, 2017)),
                (new Date(19, Month.December, 2016), new Period(3, TimeUnit.Months), new Date(19, Month.December, 2016), new Date(20, Month.March, 2017)),
                (new Date(20, Month.December, 2016), new Period(3, TimeUnit.Months), new Date(20, Month.December, 2016), new Date(20, Month.June, 2017)),
                (new Date(21, Month.December, 2016), new Period(3, TimeUnit.Months), new Date(21, Month.December, 2016), new Date(20, Month.June, 2017)),
                (new Date(19, Month.March, 2016), new Period(6, TimeUnit.Months), new Date(19, Month.March, 2016), new Date(20, Month.September, 2016)),
                (new Date(20, Month.March, 2016), new Period(6, TimeUnit.Months), new Date(20, Month.March, 2016), new Date(20, Month.December, 2016)),
                (new Date(21, Month.March, 2016), new Period(6, TimeUnit.Months), new Date(21, Month.March, 2016), new Date(20, Month.December, 2016)),
                (new Date(19, Month.June, 2016), new Period(6, TimeUnit.Months), new Date(19, Month.June, 2016), new Date(20, Month.December, 2016)),
                (new Date(20, Month.June, 2016), new Period(6, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.March, 2017)),
                (new Date(21, Month.June, 2016), new Period(6, TimeUnit.Months), new Date(21, Month.June, 2016), new Date(20, Month.March, 2017)),
                (new Date(19, Month.September, 2016), new Period(6, TimeUnit.Months), new Date(19, Month.September, 2016), new Date(20, Month.March, 2017)),
                (new Date(20, Month.September, 2016), new Period(6, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.June, 2017)),
                (new Date(21, Month.September, 2016), new Period(6, TimeUnit.Months), new Date(21, Month.September, 2016), new Date(20, Month.June, 2017)),
                (new Date(19, Month.December, 2016), new Period(6, TimeUnit.Months), new Date(19, Month.December, 2016), new Date(20, Month.June, 2017)),
                (new Date(20, Month.December, 2016), new Period(6, TimeUnit.Months), new Date(20, Month.December, 2016), new Date(20, Month.September, 2017)),
                (new Date(21, Month.December, 2016), new Period(6, TimeUnit.Months), new Date(21, Month.December, 2016), new Date(20, Month.September, 2017)),
                (new Date(19, Month.March, 2016), new Period(9, TimeUnit.Months), new Date(19, Month.March, 2016), new Date(20, Month.December, 2016)),
                (new Date(20, Month.March, 2016), new Period(9, TimeUnit.Months), new Date(20, Month.March, 2016), new Date(20, Month.March, 2017)),
                (new Date(21, Month.March, 2016), new Period(9, TimeUnit.Months), new Date(21, Month.March, 2016), new Date(20, Month.March, 2017)),
                (new Date(19, Month.June, 2016), new Period(9, TimeUnit.Months), new Date(19, Month.June, 2016), new Date(20, Month.March, 2017)),
                (new Date(20, Month.June, 2016), new Period(9, TimeUnit.Months), new Date(20, Month.June, 2016), new Date(20, Month.June, 2017)),
                (new Date(21, Month.June, 2016), new Period(9, TimeUnit.Months), new Date(21, Month.June, 2016), new Date(20, Month.June, 2017)),
                (new Date(19, Month.September, 2016), new Period(9, TimeUnit.Months), new Date(19, Month.September, 2016), new Date(20, Month.June, 2017)),
                (new Date(20, Month.September, 2016), new Period(9, TimeUnit.Months), new Date(20, Month.September, 2016), new Date(20, Month.September, 2017)),
                (new Date(21, Month.September, 2016), new Period(9, TimeUnit.Months), new Date(21, Month.September, 2016), new Date(20, Month.September, 2017)),
                (new Date(19, Month.December, 2016), new Period(9, TimeUnit.Months), new Date(19, Month.December, 2016), new Date(20, Month.September, 2017)),
                (new Date(20, Month.December, 2016), new Period(9, TimeUnit.Months), new Date(20, Month.December, 2016), new Date(20, Month.December, 2017)),
                (new Date(21, Month.December, 2016), new Period(9, TimeUnit.Months), new Date(21, Month.December, 2016), new Date(20, Month.December, 2017)),
                (new Date(19, Month.March, 2016), new Period(1, TimeUnit.Years), new Date(19, Month.March, 2016), new Date(20, Month.March, 2017)),
                (new Date(20, Month.March, 2016), new Period(1, TimeUnit.Years), new Date(20, Month.March, 2016), new Date(20, Month.June, 2017)),
                (new Date(21, Month.March, 2016), new Period(1, TimeUnit.Years), new Date(21, Month.March, 2016), new Date(20, Month.June, 2017)),
                (new Date(19, Month.June, 2016), new Period(1, TimeUnit.Years), new Date(19, Month.June, 2016), new Date(20, Month.June, 2017)),
                (new Date(20, Month.June, 2016), new Period(1, TimeUnit.Years), new Date(20, Month.June, 2016), new Date(20, Month.September, 2017)),
                (new Date(21, Month.June, 2016), new Period(1, TimeUnit.Years), new Date(21, Month.June, 2016), new Date(20, Month.September, 2017)),
                (new Date(19, Month.September, 2016), new Period(1, TimeUnit.Years), new Date(19, Month.September, 2016), new Date(20, Month.September, 2017)),
                (new Date(20, Month.September, 2016), new Period(1, TimeUnit.Years), new Date(20, Month.September, 2016), new Date(20, Month.December, 2017)),
                (new Date(21, Month.September, 2016), new Period(1, TimeUnit.Years), new Date(21, Month.September, 2016), new Date(20, Month.December, 2017)),
                (new Date(19, Month.December, 2016), new Period(1, TimeUnit.Years), new Date(19, Month.December, 2016), new Date(20, Month.December, 2017)),
                (new Date(20, Month.December, 2016), new Period(1, TimeUnit.Years), new Date(20, Month.December, 2016), new Date(20, Month.March, 2018)),
                (new Date(21, Month.December, 2016), new Period(1, TimeUnit.Years), new Date(21, Month.December, 2016), new Date(20, Month.March, 2018)),
                (new Date(19, Month.March, 2016), new Period(5, TimeUnit.Years), new Date(19, Month.March, 2016), new Date(20, Month.March, 2021)),
                (new Date(20, Month.March, 2016), new Period(5, TimeUnit.Years), new Date(20, Month.March, 2016), new Date(20, Month.June, 2021)),
                (new Date(21, Month.March, 2016), new Period(5, TimeUnit.Years), new Date(21, Month.March, 2016), new Date(20, Month.June, 2021)),
                (new Date(19, Month.June, 2016), new Period(5, TimeUnit.Years), new Date(19, Month.June, 2016), new Date(20, Month.June, 2021)),
                (new Date(20, Month.June, 2016), new Period(5, TimeUnit.Years), new Date(20, Month.June, 2016), new Date(20, Month.September, 2021)),
                (new Date(21, Month.June, 2016), new Period(5, TimeUnit.Years), new Date(21, Month.June, 2016), new Date(20, Month.September, 2021)),
                (new Date(19, Month.September, 2016), new Period(5, TimeUnit.Years), new Date(19, Month.September, 2016), new Date(20, Month.September, 2021)),
                (new Date(20, Month.September, 2016), new Period(5, TimeUnit.Years), new Date(20, Month.September, 2016), new Date(20, Month.December, 2021)),
                (new Date(21, Month.September, 2016), new Period(5, TimeUnit.Years), new Date(21, Month.September, 2016), new Date(20, Month.December, 2021)),
                (new Date(19, Month.December, 2016), new Period(5, TimeUnit.Years), new Date(19, Month.December, 2016), new Date(20, Month.December, 2021)),
                (new Date(20, Month.December, 2016), new Period(5, TimeUnit.Years), new Date(20, Month.December, 2016), new Date(20, Month.March, 2022)),
                (new Date(21, Month.December, 2016), new Period(5, TimeUnit.Years), new Date(21, Month.December, 2016), new Date(20, Month.March, 2022)),
        ];

        TestCdsConventions(inputs, DateGenerationRule.OldCDS);
    }

    [Fact]
    public void TestCDS2015ConventionSampleDates()
    {
        // Testing all dates in sample CDS schedule(s) for rule CDS2015...

        DateGenerationRule rule = DateGenerationRule.CDS2015;
        var tenor = new Period(1, TimeUnit.Years);

        // trade date = Fri 18 Sep 2015.
        var tradeDate = new Date(18, Month.September, 2015);
        Date maturity = CreditDefaultSwapFunctions.CdsMaturity(tradeDate, tenor, rule)!.Value;
        Schedule s = MakeCdsSchedule(tradeDate, maturity, rule);
        List<Date> expDates =
        [
            new(22, Month.June, 2015), new(21, Month.September, 2015), new(21, Month.December, 2015),
            new(21, Month.March, 2016), new(20, Month.June, 2016),
        ];
        CheckDates(s, [.. expDates]);

        // trade date = Sat 19 Sep 2015, no change.
        tradeDate = new Date(19, Month.September, 2015);
        maturity = CreditDefaultSwapFunctions.CdsMaturity(tradeDate, tenor, rule)!.Value;
        s = MakeCdsSchedule(tradeDate, maturity, rule);
        CheckDates(s, [.. expDates]);

        // trade date = Sun 20 Sep 2015. Roll to new maturity. Trade date still before next coupon payment
        // date of Mon 21 Sep 2015, so keep the first period from 22 Jun 2015 to 21 Sep 2015 in schedule.
        tradeDate = new Date(20, Month.September, 2015);
        maturity = CreditDefaultSwapFunctions.CdsMaturity(tradeDate, tenor, rule)!.Value;
        s = MakeCdsSchedule(tradeDate, maturity, rule);
        expDates.Add(new Date(20, Month.September, 2016));
        expDates.Add(new Date(20, Month.December, 2016));
        CheckDates(s, [.. expDates]);

        // trade date = Mon 21 Sep 2015, first period drops out of schedule.
        tradeDate = new Date(21, Month.September, 2015);
        maturity = CreditDefaultSwapFunctions.CdsMaturity(tradeDate, tenor, rule)!.Value;
        s = MakeCdsSchedule(tradeDate, maturity, rule);
        expDates.RemoveAt(0);
        CheckDates(s, [.. expDates]);

        // Another sample trade date, Sat 20 Jun 2009.
        tradeDate = new Date(20, Month.June, 2009);
        maturity = new Date(20, Month.December, 2009);
        s = MakeCdsSchedule(tradeDate, maturity, rule);
        expDates =
        [
            new(20, Month.March, 2009), new(22, Month.June, 2009), new(21, Month.September, 2009),
            new(20, Month.December, 2009),
        ];
        CheckDates(s, [.. expDates]);

        // Move forward to Sun 21 Jun 2009
        tradeDate = new Date(21, Month.June, 2009);
        s = MakeCdsSchedule(tradeDate, maturity, rule);
        CheckDates(s, [.. expDates]);

        // Move forward to Mon 22 Jun 2009
        tradeDate = new Date(22, Month.June, 2009);
        s = MakeCdsSchedule(tradeDate, maturity, rule);
        expDates.RemoveAt(0);
        CheckDates(s, [.. expDates]);
    }

    [Fact]
    public void TestCDSConventionSampleDates()
    {
        // Testing all dates in sample CDS schedule(s) for rule CDS...

        DateGenerationRule rule = DateGenerationRule.CDS;
        var tenor = new Period(1, TimeUnit.Years);

        // trade date = Fri 18 Sep 2015.
        var tradeDate = new Date(18, Month.September, 2015);
        Date maturity = CreditDefaultSwapFunctions.CdsMaturity(tradeDate, tenor, rule)!.Value;
        Schedule s = MakeCdsSchedule(tradeDate, maturity, rule);
        List<Date> expDates =
        [
            new(22, Month.June, 2015), new(21, Month.September, 2015), new(21, Month.December, 2015),
            new(21, Month.March, 2016), new(20, Month.June, 2016), new(20, Month.September, 2016),
        ];
        CheckDates(s, [.. expDates]);

        // trade date = Sat 19 Sep 2015, no change.
        tradeDate = new Date(19, Month.September, 2015);
        maturity = CreditDefaultSwapFunctions.CdsMaturity(tradeDate, tenor, rule)!.Value;
        s = MakeCdsSchedule(tradeDate, maturity, rule);
        CheckDates(s, [.. expDates]);

        // trade date = Sun 20 Sep 2015. Roll to new maturity. Trade date still before next coupon payment
        // date of Mon 21 Sep 2015, so keep the first period from 22 Jun 2015 to 21 Sep 2015 in schedule.
        tradeDate = new Date(20, Month.September, 2015);
        maturity = CreditDefaultSwapFunctions.CdsMaturity(tradeDate, tenor, rule)!.Value;
        s = MakeCdsSchedule(tradeDate, maturity, rule);
        expDates.Add(new Date(20, Month.December, 2016));
        CheckDates(s, [.. expDates]);

        // trade date = Mon 21 Sep 2015, first period drops out of schedule.
        tradeDate = new Date(21, Month.September, 2015);
        maturity = CreditDefaultSwapFunctions.CdsMaturity(tradeDate, tenor, rule)!.Value;
        s = MakeCdsSchedule(tradeDate, maturity, rule);
        expDates.RemoveAt(0);
        CheckDates(s, [.. expDates]);

        // Another sample trade date, Sat 20 Jun 2009.
        tradeDate = new Date(20, Month.June, 2009);
        maturity = new Date(20, Month.December, 2009);
        s = MakeCdsSchedule(tradeDate, maturity, rule);
        expDates =
        [
            new(20, Month.March, 2009), new(22, Month.June, 2009), new(21, Month.September, 2009),
            new(20, Month.December, 2009),
        ];
        CheckDates(s, [.. expDates]);

        // Move forward to Sun 21 Jun 2009
        tradeDate = new Date(21, Month.June, 2009);
        s = MakeCdsSchedule(tradeDate, maturity, rule);
        CheckDates(s, [.. expDates]);

        // Move forward to Mon 22 Jun 2009
        tradeDate = new Date(22, Month.June, 2009);
        s = MakeCdsSchedule(tradeDate, maturity, rule);
        expDates.RemoveAt(0);
        CheckDates(s, [.. expDates]);
    }

    [Fact]
    public void TestOldCDSConventionSampleDates()
    {
        // Testing all dates in sample CDS schedule(s) for rule OldCDS...

        DateGenerationRule rule = DateGenerationRule.OldCDS;
        var tenor = new Period(1, TimeUnit.Years);

        // trade date plus 1D = Fri 18 Sep 2015.
        var tradeDatePlusOne = new Date(18, Month.September, 2015);
        Date maturity = CreditDefaultSwapFunctions.CdsMaturity(tradeDatePlusOne, tenor, rule)!.Value;
        Schedule s = MakeCdsSchedule(tradeDatePlusOne, maturity, rule);
        List<Date> expDates =
        [
            new(18, Month.September, 2015), new(21, Month.December, 2015),
            new(21, Month.March, 2016), new(20, Month.June, 2016), new(20, Month.September, 2016),
        ];
        CheckDates(s, [.. expDates]);

        // trade date plus 1D = Sat 19 Sep 2015, no change.
        // OldCDS, schedule start date is not adjusted (kept this).
        expDates[0] = tradeDatePlusOne = new Date(19, Month.September, 2015);
        maturity = CreditDefaultSwapFunctions.CdsMaturity(tradeDatePlusOne, tenor, rule)!.Value;
        s = MakeCdsSchedule(tradeDatePlusOne, maturity, rule);
        CheckDates(s, [.. expDates]);

        // trade date plus 1D = Sun 20 Sep 2015, roll.
        expDates[0] = tradeDatePlusOne = new Date(20, Month.September, 2015);
        maturity = CreditDefaultSwapFunctions.CdsMaturity(tradeDatePlusOne, tenor, rule)!.Value;
        s = MakeCdsSchedule(tradeDatePlusOne, maturity, rule);
        expDates.Add(new Date(20, Month.December, 2016));
        CheckDates(s, [.. expDates]);

        // trade date plus 1D = Mon 21 Sep 2015, no change.
        expDates[0] = tradeDatePlusOne = new Date(21, Month.September, 2015);
        maturity = CreditDefaultSwapFunctions.CdsMaturity(tradeDatePlusOne, tenor, rule)!.Value;
        s = MakeCdsSchedule(tradeDatePlusOne, maturity, rule);
        CheckDates(s, [.. expDates]);

        // Check the 30 day stub rule by moving closer to the first coupon payment date of Mon 21 Dec 2015.
        // The test here requires long first stub when trade date plus 1D = 21 Nov 2015. The condition in the schedule
        // generation code is if: effective date + 30D > next 20th _unadjusted_. Not sure if we should refer to the
        // actual coupon payment date here i.e. the next 20th _adjusted_ when making the decision.

        // 19 Nov 2015 + 30D = 19 Dec 2015 <= 20 Dec 2015 => short front stub.
        expDates[0] = tradeDatePlusOne = new Date(19, Month.November, 2015);
        s = MakeCdsSchedule(tradeDatePlusOne, maturity, rule);
        CheckDates(s, [.. expDates]);

        // 20 Nov 2015 + 30D = 20 Dec 2015 <= 20 Dec 2015 => short front stub.
        expDates[0] = tradeDatePlusOne = new Date(20, Month.November, 2015);
        s = MakeCdsSchedule(tradeDatePlusOne, maturity, rule);
        CheckDates(s, [.. expDates]);

        // 21 Nov 2015 + 30D = 21 Dec 2015 > 20 Dec 2015 => long front stub.
        // Note that if we reffered to the next coupon payment date of 21 Dec 2015, it would still be short front.
        expDates[0] = tradeDatePlusOne = new Date(21, Month.November, 2015);
        s = MakeCdsSchedule(tradeDatePlusOne, maturity, rule);
        expDates.RemoveAt(1);
        CheckDates(s, [.. expDates]);
    }

    // Adapted: QuantLib's Date() is null here.
    [Fact]
    public void TestCDS2015ZeroMonthsMatured()
    {
        // Testing 0M tenor for CDS2015 where matured...

        DateGenerationRule rule = DateGenerationRule.CDS2015;
        var tenor = new Period(0, TimeUnit.Months);

        // Move through selected trade dates from 20 Dec 2015 to 20 Dec 2016 checking that the 0M CDS is matured.
        Date[] inputs =
        [
            new(20, Month.December, 2015),
            new(15, Month.February, 2016),
            new(19, Month.March, 2016),
            new(20, Month.June, 2016),
            new(15, Month.August, 2016),
            new(19, Month.September, 2016),
            new(20, Month.December, 2016),
        ];

        foreach (Date input in inputs)
        {
            Assert.Null(CreditDefaultSwapFunctions.CdsMaturity(input, tenor, rule));
        }
    }

    [Fact]
    public void TestDateConstructor()
    {
        // Testing the constructor taking a vector of dates and possibly additional meta information...
        Date[] dates =
        [
            new(16, Month.May, 2015),
            new(18, Month.May, 2015),
            new(18, Month.May, 2016),
            new(31, Month.December, 2017),
        ];

        // schedule without any additional information
        var schedule1 = new Schedule(dates);
        Assert.True(schedule1.Size == dates.Length, $"schedule1 has size {schedule1.Size}, expected {dates.Length}");
        for (int i = 0; i < dates.Length; ++i)
        {
            Assert.True(schedule1[i] == dates[i], $"schedule1 has {schedule1[i]} at position {i}, expected {dates[i]}");
        }

        Assert.False(schedule1.Calendar != new NullCalendar(), $"schedule1 has calendar {schedule1.Calendar.Name}, expected null calendar");
        Assert.False(schedule1.BusinessDayConvention != BusinessDayConvention.Unadjusted, $"schedule1 has convention {schedule1.BusinessDayConvention}, expected unadjusted");

        // schedule with metadata
        bool[] regular = [false, true, false];
        var schedule2 = new Schedule(
            dates,
            new TARGET(),
            BusinessDayConvention.Following,
            BusinessDayConvention.ModifiedPreceding,
            new Period(1, TimeUnit.Years),
            DateGenerationRule.Backward,
            true,
            regular);
        for (int i = 1; i < dates.Length; ++i)
        {
            Assert.True(
                schedule2.IsRegular(i) == regular[i - 1],
                $"schedule2 has a {(schedule2.IsRegular(i) ? "regular" : "irregular")} period at position {i}, expected {(regular[i - 1] ? "regular" : "irregular")}");
        }

        Assert.False(schedule2.Calendar != new TARGET(), $"schedule1 has calendar {schedule2.Calendar.Name}, expected TARGET");
        Assert.False(schedule2.BusinessDayConvention != BusinessDayConvention.Following, $"schedule2 has convention {schedule2.BusinessDayConvention}, expected Following");
        Assert.False(schedule2.TerminationDateBusinessDayConvention() != BusinessDayConvention.ModifiedPreceding, $"schedule2 has convention {schedule2.TerminationDateBusinessDayConvention()}, expected Modified Preceding");
        Assert.False(schedule2.Tenor() != new Period(1, TimeUnit.Years), $"schedule2 has tenor {schedule2.Tenor()}, expected 1Y");
        Assert.False(schedule2.Rule() != DateGenerationRule.Backward, $"schedule2 has rule {schedule2.Rule()}, expected Backward");
        Assert.True(schedule2.EndOfMonth(), "schedule2 has end of month flag false, expected true");
    }

    [Fact]
    public void TestFourWeeksTenor()
    {
        // Testing that a four-weeks tenor works...
        try
        {
            _ = new MakeSchedule().From(new Date(13, Month.January, 2016))
                .To(new Date(4, Month.May, 2016))
                .WithCalendar(new TARGET())
                .WithTenor(new Period(4, TimeUnit.Weeks))
                .WithConvention(BusinessDayConvention.Following)
                .Forwards()
                .ToSchedule();
        }
        catch (Exception e) when (e is NanoQLException or ArgumentException)
        {
            Assert.Fail($"A four-weeks tenor caused an exception: {e.Message}");
        }
    }

    [Fact]
    public void TestOnceFrequency()
    {
        // Testing that Once frequency works...
        Schedule s = new MakeSchedule().From(new Date(13, Month.January, 2016))
            .To(new Date(13, Month.January, 2019))
            .WithFrequency(Frequency.Once)
            .Forwards()
            .ToSchedule();

        Assert.True(s.Size == 2);
        Assert.True(s[0] == new Date(13, Month.January, 2016));
        Assert.True(s[1] == new Date(13, Month.January, 2019));
    }

    [Fact]
    public void TestScheduleAlwaysHasAStartDate()
    {
        // Testing that variations of MakeSchedule always produce a schedule with a start date...

        // Attempt to establish whether the first coupon payment date is always the second element of the
        // constructor.
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        Schedule schedule = new MakeSchedule()
            .From(new Date(10, Month.January, 2017))
            .WithFirstDate(new Date(31, Month.August, 2017))
            .To(new Date(28, Month.February, 2026))
            .WithFrequency(Frequency.Semiannual)
            .WithCalendar(calendar)
            .WithConvention(BusinessDayConvention.Unadjusted)
            .Backwards().EndOfMonth(false)
            .ToSchedule();
        Assert.True(schedule.Date(0) == new Date(10, Month.January, 2017), "The first element should always be the start date");

        schedule = new MakeSchedule()
            .From(new Date(10, Month.January, 2017))
            .To(new Date(28, Month.February, 2026))
            .WithFrequency(Frequency.Semiannual)
            .WithCalendar(calendar)
            .WithConvention(BusinessDayConvention.Unadjusted)
            .Backwards().EndOfMonth(false)
            .ToSchedule();
        Assert.True(schedule.Date(0) == new Date(10, Month.January, 2017), "The first element should always be the start date");

        schedule = new MakeSchedule()
            .From(new Date(31, Month.August, 2017))
            .To(new Date(28, Month.February, 2026))
            .WithFrequency(Frequency.Semiannual)
            .WithCalendar(calendar)
            .WithConvention(BusinessDayConvention.Unadjusted)
            .Backwards().EndOfMonth(false)
            .ToSchedule();
        Assert.True(schedule.Date(0) == new Date(31, Month.August, 2017), "The first element should always be the start date");
    }

    [Fact]
    public void TestShortEomSchedule()
    {
        // Testing short end-of-month schedule...
        Schedule s = new MakeSchedule()
            .From(new Date(21, Month.Feb, 2019))
            .To(new Date(28, Month.Feb, 2019))
            .WithCalendar(new TARGET())
            .WithTenor(new Period(1, TimeUnit.Years))
            .WithConvention(BusinessDayConvention.ModifiedFollowing)
            .WithTerminationDateConvention(BusinessDayConvention.ModifiedFollowing)
            .Backwards()
            .EndOfMonth(true)
            .ToSchedule();

        Assert.Equal(2, s.Size);
        Assert.True(s[0] == new Date(21, Month.Feb, 2019));
        Assert.True(s[1] == new Date(28, Month.Feb, 2019));
    }

    [Fact]
    public void TestFirstDateOnMaturity()
    {
        // Testing schedule with first date on maturity...
        Schedule schedule = new MakeSchedule()
            .From(new Date(20, Month.September, 2016))
            .To(new Date(20, Month.December, 2016))
            .WithFirstDate(new Date(20, Month.December, 2016))
            .WithFrequency(Frequency.Quarterly)
            .WithCalendar(new UnitedStates(UnitedStatesMarket.GovernmentBond))
            .WithConvention(BusinessDayConvention.Unadjusted)
            .Backwards()
            .ToSchedule();

        Date[] expected = [new(20, Month.September, 2016), new(20, Month.December, 2016)];
        CheckDates(schedule, expected);

        schedule = new MakeSchedule()
            .From(new Date(20, Month.September, 2016))
            .To(new Date(20, Month.December, 2016))
            .WithFirstDate(new Date(20, Month.December, 2016))
            .WithFrequency(Frequency.Quarterly)
            .WithCalendar(new UnitedStates(UnitedStatesMarket.GovernmentBond))
            .WithConvention(BusinessDayConvention.Unadjusted)
            .Forwards()
            .ToSchedule();
        CheckDates(schedule, expected);
    }

    [Fact]
    public void TestNextToLastDateOnStart()
    {
        // Testing schedule with next-to-last date on start date...
        Schedule schedule = new MakeSchedule()
            .From(new Date(20, Month.September, 2016))
            .To(new Date(20, Month.December, 2016))
            .WithNextToLastDate(new Date(20, Month.September, 2016))
            .WithFrequency(Frequency.Quarterly)
            .WithCalendar(new UnitedStates(UnitedStatesMarket.GovernmentBond))
            .WithConvention(BusinessDayConvention.Unadjusted)
            .Backwards()
            .ToSchedule();

        Date[] expected = [new(20, Month.September, 2016), new(20, Month.December, 2016)];
        CheckDates(schedule, expected);

        schedule = new MakeSchedule()
            .From(new Date(20, Month.September, 2016))
            .To(new Date(20, Month.December, 2016))
            .WithNextToLastDate(new Date(20, Month.September, 2016))
            .WithFrequency(Frequency.Quarterly)
            .WithCalendar(new UnitedStates(UnitedStatesMarket.GovernmentBond))
            .WithConvention(BusinessDayConvention.Unadjusted)
            .Backwards()
            .ToSchedule();
        CheckDates(schedule, expected);
    }

    [Fact]
    public void TestTruncation()
    {
        // Testing schedule truncation...
        Schedule s = new MakeSchedule().From(new Date(30, Month.September, 2009))
            .To(new Date(15, Month.June, 2020))
            .WithCalendar(new Japan())
            .WithTenor(new Period(6, TimeUnit.Months))
            .WithConvention(BusinessDayConvention.ModifiedFollowing)
            .WithTerminationDateConvention(BusinessDayConvention.ModifiedFollowing)
            .Forwards()
            .EndOfMonth()
            .ToSchedule();

        Schedule t;
        Date[] expected;

        // Until
        t = s.Until(new Date(1, Month.January, 2014));
        expected =
        [
            new(30, Month.September, 2009),
            new(31, Month.March, 2010),
            new(30, Month.September, 2010),
            new(31, Month.March, 2011),
            new(30, Month.September, 2011),
            new(30, Month.March, 2012),
            new(28, Month.September, 2012),
            new(29, Month.March, 2013),
            new(30, Month.September, 2013),
            new(1, Month.January, 2014),
        ];
        CheckDates(t, expected);
        Assert.True(t.IsRegular()[^1] == false);

        // Until, with truncation date falling on a schedule date
        t = s.Until(new Date(30, Month.September, 2013));
        expected =
        [
            new(30, Month.September, 2009),
            new(31, Month.March, 2010),
            new(30, Month.September, 2010),
            new(31, Month.March, 2011),
            new(30, Month.September, 2011),
            new(30, Month.March, 2012),
            new(28, Month.September, 2012),
            new(29, Month.March, 2013),
            new(30, Month.September, 2013),
        ];
        CheckDates(t, expected);
        Assert.True(t.IsRegular()[^1] == true);

        // After
        t = s.After(new Date(1, Month.January, 2014));
        expected =
        [
            new(1, Month.January, 2014),
            new(31, Month.March, 2014),
            new(30, Month.September, 2014),
            new(31, Month.March, 2015),
            new(30, Month.September, 2015),
            new(31, Month.March, 2016),
            new(30, Month.September, 2016),
            new(31, Month.March, 2017),
            new(29, Month.September, 2017),
            new(30, Month.March, 2018),
            new(28, Month.September, 2018),
            new(29, Month.March, 2019),
            new(30, Month.September, 2019),
            new(31, Month.March, 2020),
            new(15, Month.June, 2020),
        ];
        CheckDates(t, expected);
        Assert.True(t.IsRegular()[0] == false);

        // After, with truncation date falling on a schedule date
        t = s.After(new Date(28, Month.September, 2018));
        expected =
        [
            new(28, Month.September, 2018),
            new(29, Month.March, 2019),
            new(30, Month.September, 2019),
            new(31, Month.March, 2020),
            new(15, Month.June, 2020),
        ];
        CheckDates(t, expected);
        Assert.True(t.IsRegular()[0] == true);
    }

    [Fact]
    public void TestBackwardRegularFirstPeriodWithFirstDate()
    {
        // Testing that backward schedule marks regular first period correctly when the first date is provided...

        // Reproduces issue #405: Backward generation with firstDate and endOfMonth incorrectly marked the first
        // period as irregular even when effectiveDate + tenor == firstDate.
        var calendar = new NullCalendar();

        // Regular first period: Sep 30 + 6M = Mar 31 (with endOfMonth)
        var backward = new Schedule(
            new Date(30, Month.September, 2017), // effectiveDate
            new Date(30, Month.September, 2024), // terminationDate
            new Period(Frequency.Semiannual),
            calendar,
            BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Backward,
            true,
            Context,
            new Date(31, Month.March, 2018)); // firstDate

        // First period should be regular
        Assert.True(backward.IsRegular(1), "First period should be regular (effectiveDate + 6M == firstDate)");

        // Forward generation should agree
        var forward = new Schedule(
            new Date(30, Month.September, 2017),
            new Date(30, Month.September, 2024),
            new Period(Frequency.Semiannual),
            calendar,
            BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Forward,
            true,
            Context,
            new Date(31, Month.March, 2018));
        Assert.True(forward.IsRegular(1), "Forward first period should also be regular");

        // Genuinely irregular first period: effective date does NOT align with firstDate by one tenor
        var irregular = new Schedule(
            new Date(3, Month.September, 2017), // effectiveDate (not end-of-month)
            new Date(30, Month.September, 2024),
            new Period(Frequency.Semiannual),
            calendar,
            BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Backward,
            true,
            Context,
            new Date(31, Month.March, 2018));
        Assert.False(irregular.IsRegular(1), "First period should be irregular (effectiveDate + 6M != firstDate)");

        // Forward nextToLastDate: off-grid nextToLastDate triggers the insertion path and should be marked irregular
        var forwardNTLIrreg = new Schedule(
            new Date(30, Month.September, 2017),
            new Date(30, Month.September, 2024),
            new Period(Frequency.Semiannual),
            calendar,
            BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Forward,
            true,
            Context,
            null,
            new Date(15, Month.March, 2024)); // off-grid, not one tenor from neighbors
        Size n = forwardNTLIrreg.Size;
        Assert.False(forwardNTLIrreg.IsRegular(n - 2), "Period ending at off-grid nextToLastDate should be irregular");
    }

    // QuantLib's CdsTests::makeCdsSchedule.
    private static Schedule MakeCdsSchedule(Date from, Date to, DateGenerationRule rule) =>
        new MakeSchedule()
            .From(from)
            .To(to)
            .WithCalendar(new WeekendsOnly())
            .WithTenor(new Period(3, TimeUnit.Months))
            .WithConvention(BusinessDayConvention.Following)
            .WithTerminationDateConvention(BusinessDayConvention.Unadjusted)
            .WithRule(rule)
            .ToSchedule();

    // QuantLib's CdsTests::testCDSConventions: the generated start and end date against the expected start and end
    // date.
    private static void TestCdsConventions((Date From, Period Tenor, Date ExpStart, Date ExpEnd)[] inputs, DateGenerationRule rule)
    {
        foreach ((Date from, Period tenor, Date expStart, Date expEnd) in inputs)
        {
            Date? maturity = CreditDefaultSwapFunctions.CdsMaturity(from, tenor, rule);
            Assert.Equal(expEnd, maturity);

            Schedule s = MakeCdsSchedule(from, maturity!.Value, rule);

            Date start = s.StartDate();
            Date end = s.EndDate();
            Assert.Equal(expStart, start);
            Assert.Equal(expEnd, end);
        }
    }

    private static void CheckDates(Schedule s, Date[] expected)
    {
        if (s.Size != expected.Length)
        {
            Assert.Fail($"expected {expected.Length} dates, found {s.Size}");
        }

        var errors = new List<string>();
        for (int i = 0; i < expected.Length; ++i)
        {
            if (s[i] != expected[i])
            {
                errors.Add($"expected {expected[i]} at index {i}, found {s[i]}");
            }
        }

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }
}
