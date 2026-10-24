// SPDX-FileCopyrightText: Copyright (C) 2021 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/multipleresetscoupons.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

// Adapted throughout: the fixture's relinkable handle is the curve itself, built first, and its index fixings and
// evaluation date are its context; a coupon's pricer is given at construction, where QuantLib sets it; a leg's
// builder builds through ToLeg; and BOOST_CHECK_THROW(..., Error) is an ArgumentException or a NanoQLException.
[Trait("Oracle", "QuantLib")]
public class MultipleResetsCouponsTests
{
    [Fact]
    public void TestCompoundedCouponWithMultipleResets()
    {
        // Testing coupon with multiple compounded resets...
        var vars = new CommonVars();

        Date start = vars.Today - new Period(2, TimeUnit.Months);
        Date end = start + new Period(6, TimeUnit.Months);

        const Spread spread = 0.001;

        Schedule schedule = vars.CreateSchedule(start, end);

        ImmutableArray<CashFlow> iborLeg = vars.CreateIborLeg(schedule, spread);
        CashFlow testCpn = vars.CreateMultipleResetsCoupon(schedule, spread, RateAveragingType.Compound);

        const Real tolerance = 1.0e-14;

        Real actualPayment = testCpn.Amount(vars.Context);

        Real compound = 1.0;
        foreach (CashFlow cf in iborLeg)
        {
            var cpn = (IborCoupon)cf;
            Real yearFraction = cpn.AccrualPeriod();
            Rate fixing = vars.Euribor.Fixing(cpn.FixingDate(), vars.Context);
            compound *= 1.0 + (yearFraction * (fixing + cpn.Spread));
        }

        Real expectedPayment = compound - 1.0;

        Assert.False(
            Math.Abs(actualPayment - expectedPayment) > tolerance,
            $"unable to replicate compounded multiple-resets coupon payment\n    calculated:    {actualPayment:G5}\n"
            + $"    expected:    {expectedPayment:G5}\n    start:    {start}\n    end:    {end}\n");
    }

    [Fact]
    public void TestAveragedCouponWithMultipleResets()
    {
        // Testing coupon with multiple averaged resets...
        var vars = new CommonVars();

        Date start = vars.Today - new Period(2, TimeUnit.Months);
        Date end = start + new Period(6, TimeUnit.Months);

        const Spread spread = 0.001;

        Schedule schedule = vars.CreateSchedule(start, end);

        ImmutableArray<CashFlow> iborLeg = vars.CreateIborLeg(schedule, spread);
        CashFlow testCpn = vars.CreateMultipleResetsCoupon(schedule, spread, RateAveragingType.Simple);

        const Real tolerance = 1.0e-14;

        Real actualPayment = testCpn.Amount(vars.Context);

        Real expectedPayment = 0.0;
        foreach (CashFlow cf in iborLeg)
        {
            var cpn = (IborCoupon)cf;
            Real yearFraction = cpn.AccrualPeriod();
            Rate fixing = vars.Euribor.Fixing(cpn.FixingDate(), vars.Context);
            expectedPayment += yearFraction * (fixing + cpn.Spread);
        }

        Assert.False(
            Math.Abs(actualPayment - expectedPayment) > tolerance,
            $"unable to replicate averaged multiple-resets coupon payment\n    calculated:    {actualPayment:G5}\n"
            + $"    expected:    {expectedPayment:G5}\n    start:    {start}\n    end:    {end}\n");
    }

    [Fact]
    public void TestExCouponCashFlow()
    {
        // Testing ex-coupon cash flow...
        var vars = new CommonVars();

        Date start = vars.Calendar.Advance(vars.Today, -6, TimeUnit.Months);
        Date end = vars.Today;
        Schedule schedule = vars.CreateSchedule(start, end);

        Calendar paymentCalendar = vars.Euribor.FixingCalendar;
        Date paymentDate = paymentCalendar.Advance(end, 2, TimeUnit.Days);
        Date exCouponDate = paymentCalendar.Advance(end, -2, TimeUnit.Days);

        var cpn = new MultipleResetsCoupon(
            paymentDate,
            1.0,
            schedule,
            2,
            vars.Euribor,
            1.0,
            0.0,
            0.0,
            null,
            null,
            null,
            exCouponDate,
            new CompoundingMultipleResetsPricer());

        Real npv = CashFlows.Npv([cpn], vars.EuriborCurve, vars.Context, false, vars.Today, vars.Today);

        const Real tolerance = 1.0e-14;

        Assert.False(
            Math.Abs(npv) > tolerance,
            $"cash flow was expected to go ex-coupon\n    calculated:    {npv:G5}\n    expected:    {0.0}\n"
            + $"    start:    {start}\n    end:    {end}\n");
    }

    [Fact]
    public void TestMultipleResetsLegConsistencyChecks()
    {
        // Testing multiple-resets leg consistency checks...
        var vars = new CommonVars();

        var start = new Date(18, March, 2021);
        var end = new Date(18, March, 2031);

        ImmutableArray<CashFlow> validLeg = vars.CreateMultipleResetsLeg(start, end).ToLeg();
        int n = validLeg.Length;

        AssertError(() => vars.CreateMultipleResetsLeg(start, end).WithNotionals(Array.Empty<Real>()).ToLeg());

        AssertError(() => vars.CreateMultipleResetsLeg(start, end).WithNotionals(Enumerable.Repeat(1.0, n + 1)).ToLeg());

        AssertError(() => vars.CreateMultipleResetsLeg(start, end).WithFixingDays(Enumerable.Repeat(2, n + 1)).ToLeg());

        AssertError(() => vars.CreateMultipleResetsLeg(start, end).WithGearings(0.0).ToLeg());

        AssertError(() => vars.CreateMultipleResetsLeg(start, end).WithGearings(Enumerable.Repeat(1.0, n + 1)).ToLeg());

        AssertError(() => vars.CreateMultipleResetsLeg(start, end).WithCouponSpreads(Enumerable.Repeat(0.0, n + 1)).ToLeg());

        AssertError(() => vars.CreateMultipleResetsLeg(start, end).WithRateSpreads(Enumerable.Repeat(0.0, n + 1)).ToLeg());
    }

    [Fact]
    public void TestMultipleResetsLegRegression()
    {
        // Testing number of fixing dates in multiple-resets coupons...
        Schedule schedule = new MakeSchedule()
            .From(new Date(1, August, 2024))
            .To(new Date(1, August, 2025))
            .WithFrequency(Frequency.Monthly)
            .WithCalendar(new TARGET())
            .ToSchedule();

        const int resetsPerCoupon = 3;
        ImmutableArray<CashFlow> leg = new MultipleResetsLeg(schedule, new Euribor1M(), resetsPerCoupon)
            .WithNotionals(100.0)
            .WithAveragingMethod(RateAveragingType.Compound)
            .ToLeg();

        foreach (CashFlow cf in leg)
        {
            var c = (MultipleResetsCoupon)cf;
            Assert.False(
                c.FixingDates.Length != 3,
                $"Unexpected number of fixing dates ({c.FixingDates.Length}) in coupon paying on {c.Date}");
        }
    }

    private static void AssertError(Action action)
    {
        Exception? e = Record.Exception(action);
        Assert.True(e is ArgumentException or NanoQLException, $"expected an error, got {e?.GetType().Name ?? "none"}");
    }

    private sealed class CommonVars
    {
        // utilities
        public CommonVars()
        {
            DayCount = new Actual365Fixed();
            BusinessConvention = BusinessDayConvention.ModifiedFollowing;

            Calendar = new TARGET();
            Today = Calendar.Adjust(new Date(15, March, 2021));
            EuriborCurve = new FlatForward(Today, 0.007, DayCount);
            Euribor = new Euribor1M(EuriborCurve);
            Calendar = Euribor.FixingCalendar;

            var context = new EvaluationContext { EvaluationDate = Today };
            IndexFixings fixings = context.Fixings
                .AddFixing(Euribor, new Date(13, January, 2021), 0.0077)
                .AddFixing(Euribor, new Date(11, February, 2021), 0.0075)
                .AddFixing(Euribor, new Date(11, March, 2021), 0.0073);
            Context = context with { Fixings = fixings };
        }

        public Date Today { get; }

        public Calendar Calendar { get; }

        public DayCounter DayCount { get; }

        public BusinessDayConvention BusinessConvention { get; }

        public IborIndex Euribor { get; }

        public YieldTermStructure EuriborCurve { get; }

        public EvaluationContext Context { get; }

        public Schedule CreateSchedule(Date start, Date end) =>
            new MakeSchedule()
                .From(start)
                .To(end)
                .WithTenor(Euribor.Tenor)
                .WithCalendar(Euribor.FixingCalendar)
                .WithConvention(Euribor.BusinessDayConvention)
                .ToSchedule();

        public ImmutableArray<CashFlow> CreateIborLeg(Schedule schedule, Spread spread) =>
            new IborLeg(schedule, Euribor)
                .WithNotionals(1.0)
                .WithSpreads(spread)
                .WithExCouponPeriod(new Period(2, TimeUnit.Days), Calendar, BusinessConvention)
                .WithPaymentLag(1)
                .WithFixingDays(Euribor.FixingDays)
                .ToLeg(Context);

        public CashFlow CreateMultipleResetsCoupon(
            Schedule schedule, Spread rateSpread = 0.0, RateAveragingType averaging = RateAveragingType.Compound)
        {
            Calendar paymentCalendar = Euribor.FixingCalendar;
            BusinessDayConvention paymentBdc = Euribor.BusinessDayConvention;
            Date paymentDate = paymentCalendar.Advance(schedule.Back(), 1, TimeUnit.Days, paymentBdc);
            Date exCouponDate = paymentCalendar.Advance(paymentDate, -2, TimeUnit.Days, paymentBdc);
            MultipleResetsPricer pricer = averaging == RateAveragingType.Compound
                ? new CompoundingMultipleResetsPricer()
                : new AveragingMultipleResetsPricer();
            return new MultipleResetsCoupon(
                paymentDate,
                1.0,
                schedule,
                Euribor.FixingDays,
                Euribor,
                1.0,
                0.0,
                rateSpread,
                null,
                null,
                null,
                exCouponDate,
                pricer);
        }

        public MultipleResetsLeg CreateMultipleResetsLeg(Date start, Date end)
        {
            Schedule s = CreateSchedule(start, end);
            return new MultipleResetsLeg(s, Euribor, 6)
                .WithNotionals(1.0)
                .WithExCouponPeriod(new Period(2, TimeUnit.Days), Calendar, BusinessConvention)
                .WithPaymentLag(1)
                .WithFixingDays(2)
                .WithRateSpreads(0.0)
                .WithCouponSpreads(0.0)
                .WithAveragingMethod(RateAveragingType.Compound);
        }
    }
}
