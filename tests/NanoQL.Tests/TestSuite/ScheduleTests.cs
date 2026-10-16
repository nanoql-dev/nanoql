// SPDX-FileCopyrightText: Copyright (C) 2012 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/schedule.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testEndDateWithEomAdjustment, testTruncation (Japan); testCDS2015Convention, testCDS2015ConventionGrid, testCDSConventionGrid, testOldCDSConventionGrid, testCDS2015ConventionSampleDates, testCDSConventionSampleDates, testOldCDSConventionSampleDates, testCDS2015ZeroMonthsMatured (cdsMaturity)

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
