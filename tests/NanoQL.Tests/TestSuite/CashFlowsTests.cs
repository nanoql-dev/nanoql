// SPDX-FileCopyrightText: Copyright (C) 2009, 2012 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/cashflows.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testAccessViolation, which waits for batch 2's USDLibor; testInterpolatedIborStubCoupon, testIborStubInterpolationEndpointsAndRegularSchedule, testFinalAndLongIborStubCoupons, testIborStubCouponHistoricalFixingsAndRelinking and testIborStubCouponHasFixedUsesSelectedIndices, which wait for batch 2's NewZealand calendar and Bkbm indexes

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class CashFlowsTests
{
    // The evaluation date QuantLib's test runner sets globally (playbook §9.2).
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(16, September, 2015) };

    // Adapted: where QuantLib sets the global Settings before each block (lines 56, 75-76, 91-92, 108-109, 125-126,
    // 142-143, 172 and 177), each block builds its own evaluation context, with today as the evaluation date, and passes
    // it to HasOccurred and CashFlows.Npv. The NPV checks (lines 157-180) discount on a zero-rate FlatForward from
    // today, in place of the zero-rate InterestRate that CashFlows::npv's InterestRate overload takes, which isn't
    // ported yet; both discount by 1, so QuantLib's expected values stand.
    [Fact]
    public void TestSettings()
    {
        // Testing cash-flow settings...

        Date today = Date.TodaysDate();

        // cash flows at T+0, T+1, T+2
        var leg = new List<CashFlow>(3);
        for (Integer i = 0; i < 3; ++i)
        {
            leg.Add(new SimpleCashFlow(1.0, today + i));
        }

        void CheckInclusion(EvaluationContext context, int n, int days, bool expected)
        {
            if ((!leg[n].HasOccurred(context, today + days)) != expected)
            {
                Assert.Fail($"cashflow at T+{n} {(expected ? "not" : "")}included at T+{days}");
            }
        }

        // case 1: don't include reference-date payments, no override at
        //         today's date

        var settings = new EvaluationContext
        {
            EvaluationDate = today,
            IncludeReferenceDateEvents = false,
            IncludeTodaysCashFlows = null,
        };

        CheckInclusion(settings, 0, 0, false);
        CheckInclusion(settings, 0, 1, false);

        CheckInclusion(settings, 1, 0, true);
        CheckInclusion(settings, 1, 1, false);
        CheckInclusion(settings, 1, 2, false);

        CheckInclusion(settings, 2, 1, true);
        CheckInclusion(settings, 2, 2, false);
        CheckInclusion(settings, 2, 3, false);

        // case 2: same, but with explicit setting at today's date

        settings = new EvaluationContext
        {
            EvaluationDate = today,
            IncludeReferenceDateEvents = false,
            IncludeTodaysCashFlows = false,
        };

        CheckInclusion(settings, 0, 0, false);
        CheckInclusion(settings, 0, 1, false);

        CheckInclusion(settings, 1, 0, true);
        CheckInclusion(settings, 1, 1, false);
        CheckInclusion(settings, 1, 2, false);

        CheckInclusion(settings, 2, 1, true);
        CheckInclusion(settings, 2, 2, false);
        CheckInclusion(settings, 2, 3, false);

        // case 3: do include reference-date payments, no override at
        //         today's date

        settings = new EvaluationContext
        {
            EvaluationDate = today,
            IncludeReferenceDateEvents = true,
            IncludeTodaysCashFlows = null,
        };

        CheckInclusion(settings, 0, 0, true);
        CheckInclusion(settings, 0, 1, false);

        CheckInclusion(settings, 1, 0, true);
        CheckInclusion(settings, 1, 1, true);
        CheckInclusion(settings, 1, 2, false);

        CheckInclusion(settings, 2, 1, true);
        CheckInclusion(settings, 2, 2, true);
        CheckInclusion(settings, 2, 3, false);

        // case 4: do include reference-date payments, explicit (and same)
        //         setting at today's date

        settings = new EvaluationContext
        {
            EvaluationDate = today,
            IncludeReferenceDateEvents = true,
            IncludeTodaysCashFlows = true,
        };

        CheckInclusion(settings, 0, 0, true);
        CheckInclusion(settings, 0, 1, false);

        CheckInclusion(settings, 1, 0, true);
        CheckInclusion(settings, 1, 1, true);
        CheckInclusion(settings, 1, 2, false);

        CheckInclusion(settings, 2, 1, true);
        CheckInclusion(settings, 2, 2, true);
        CheckInclusion(settings, 2, 3, false);

        // case 5: do include reference-date payments, override at
        //         today's date

        settings = new EvaluationContext
        {
            EvaluationDate = today,
            IncludeReferenceDateEvents = true,
            IncludeTodaysCashFlows = false,
        };

        CheckInclusion(settings, 0, 0, false);
        CheckInclusion(settings, 0, 1, false);

        CheckInclusion(settings, 1, 0, true);
        CheckInclusion(settings, 1, 1, true);
        CheckInclusion(settings, 1, 2, false);

        CheckInclusion(settings, 2, 1, true);
        CheckInclusion(settings, 2, 2, true);
        CheckInclusion(settings, 2, 3, false);

        // no discount to make calculations easier
        var noDiscount = new FlatForward(today, 0.0, new Actual365Fixed(), Compounding.Continuous, Frequency.Annual);
        ImmutableArray<CashFlow> cashFlows = [.. leg];

        void CheckNpv(EvaluationContext context, bool includeRef, Real expected)
        {
            Real npv = CashFlows.Npv(cashFlows, noDiscount, context, includeRef, today);
            if (Math.Abs(npv - expected) > 1e-6)
            {
                Assert.Fail($"NPV mismatch:\n    calculated: {npv}\n    expected: {expected}");
            }
        }

        // no override
        settings = settings with { IncludeTodaysCashFlows = null };

        CheckNpv(settings, false, 2.0);
        CheckNpv(settings, true, 3.0);

        // override
        settings = settings with { IncludeTodaysCashFlows = false };

        CheckNpv(settings, false, 2.0);
        CheckNpv(settings, true, 2.0);
    }

    [Fact]
    public void TestDefaultSettlementDate()
    {
        // Testing default evaluation date in cashflows methods...
        Date today = Context.EvaluationDate;
        Schedule schedule =
            new MakeSchedule()
            .From(today - (2 * TimeUnit.Months)).To(today + (4 * TimeUnit.Months))
            .WithFrequency(Frequency.Semiannual)
            .WithCalendar(new TARGET())
            .WithConvention(BusinessDayConvention.Unadjusted)
            .Backwards()
            .ToSchedule();

        Leg leg = new FixedRateLeg(schedule)
            .WithNotionals(100.0)
            .WithCouponRates(0.03, new Actual360())
            .WithPaymentCalendar(new TARGET())
            .WithPaymentAdjustment(BusinessDayConvention.Following)
            .ToLeg();

        double accruedPeriod = CashFlows.AccruedPeriod(leg, Context, false);
        if (accruedPeriod == 0.0)
        {
            Assert.Fail("null accrued period with default settlement date");
        }

        int accruedDays = CashFlows.AccruedDays(leg, Context, false);
        if (accruedDays == 0)
        {
            Assert.Fail("no accrued days with default settlement date");
        }

        double accruedAmount = CashFlows.AccruedAmount(leg, Context, false);
        if (accruedAmount == 0.0)
        {
            Assert.Fail("null accrued amount with default settlement date");
        }
    }

    // Adapted: Euribor 6M in place of USDLibor 6M (line 268), which waits for batch 2; the case's index is incidental.
    // QuantLib's precondition, at-par coupons, is the context's default; the conversion to a leg (lines 269-273) takes
    // the context.
    [Fact]
    public void TestNullFixingDays()
    {
        // Testing ibor leg construction with null fixing days...
        Date today = Context.EvaluationDate;
        Schedule schedule =
            new MakeSchedule()
            .From(today - (2 * TimeUnit.Months)).To(today + (4 * TimeUnit.Months))
            .WithFrequency(Frequency.Semiannual)
            .WithCalendar(new TARGET())
            .WithConvention(BusinessDayConvention.Following)
            .Backwards()
            .ToSchedule();

        IborIndex index = new Euribor6M();
        Leg leg = new IborLeg(schedule, index)
            .WithNotionals(100.0)
            // this can happen with default values, and caused an
            // exception when the null was not managed properly
            .WithFixingDays((int?)null)
            .ToLeg(Context);
        Assert.False(leg.IsDefaultOrEmpty);
    }

    // Adapted: the Ibor legs' conversions (lines 298, 320 and 346) take a context with today as the evaluation date.
    [Fact]
    public void TestExCouponDates()
    {
        // Testing ex-coupon date calculation...

        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };
        Schedule schedule = new MakeSchedule()
            .From(today)
            .To(today + (5 * TimeUnit.Years))
            .WithFrequency(Frequency.Monthly)
            .WithCalendar(new TARGET())
            .WithConvention(BusinessDayConvention.Following)
            .ToSchedule();

        // no ex-coupon dates
        Leg l1 = new FixedRateLeg(schedule).WithNotionals(100.0).WithCouponRates(0.03, new Actual360()).ToLeg();
        foreach (CashFlow i in l1)
        {
            Coupon c = CouponCast(i);
            if (c.ExCouponDate is not null)
            {
                Assert.Fail("ex-coupon date found (none expected)");
            }
        }

        // same for floating legs
        IborIndex index = new Euribor3M();
        Leg l2 = new IborLeg(schedule, index).WithNotionals(100.0).ToLeg(context);
        foreach (CashFlow i in l2)
        {
            Coupon c = CouponCast(i);
            if (c.ExCouponDate is not null)
            {
                Assert.Fail("ex-coupon date found (none expected)");
            }
        }

        // calendar days
        Leg l5 = new FixedRateLeg(schedule)
            .WithNotionals(100.0)
            .WithCouponRates(0.03, new Actual360())
            .WithExCouponPeriod(
                new Period(2, TimeUnit.Days), new NullCalendar(), BusinessDayConvention.Unadjusted, false)
            .ToLeg();
        foreach (CashFlow i in l5)
        {
            Coupon c = CouponCast(i);
            Date expected = c.AccrualEndDate - 2;
            if (c.ExCouponDate != expected)
            {
                Assert.Fail($"ex-coupon date = {Show(c.ExCouponDate)} ({expected} expected)");
            }
        }

        Leg l6 = new IborLeg(schedule, index)
            .WithNotionals(100.0)
            .WithExCouponPeriod(
                new Period(2, TimeUnit.Days), new NullCalendar(), BusinessDayConvention.Unadjusted, false)
            .ToLeg(context);
        foreach (CashFlow i in l6)
        {
            Coupon c = CouponCast(i);
            Date expected = c.AccrualEndDate - 2;
            if (c.ExCouponDate != expected)
            {
                Assert.Fail($"ex-coupon date = {Show(c.ExCouponDate)} ({expected} expected)");
            }
        }

        // business days
        Leg l7 = new FixedRateLeg(schedule)
            .WithNotionals(100.0)
            .WithCouponRates(0.03, new Actual360())
            .WithExCouponPeriod(new Period(2, TimeUnit.Days), new TARGET(), BusinessDayConvention.Preceding, false)
            .ToLeg();
        foreach (CashFlow i in l7)
        {
            Coupon c = CouponCast(i);
            Date expected = new TARGET().Advance(c.AccrualEndDate, -2, TimeUnit.Days);
            if (c.ExCouponDate != expected)
            {
                Assert.Fail($"ex-coupon date = {Show(c.ExCouponDate)} ({expected} expected)");
            }
        }

        Leg l8 = new IborLeg(schedule, index)
            .WithNotionals(100.0)
            .WithExCouponPeriod(new Period(2, TimeUnit.Days), new TARGET(), BusinessDayConvention.Preceding, false)
            .ToLeg(context);
        foreach (CashFlow i in l8)
        {
            Coupon c = CouponCast(i);
            Date expected = new TARGET().Advance(c.AccrualEndDate, -2, TimeUnit.Days);
            if (c.ExCouponDate != expected)
            {
                Assert.Fail($"ex-coupon date = {Show(c.ExCouponDate)} ({expected} expected)");
            }
        }
    }

    [Fact]
    public void TestIrregularFirstCouponReferenceDatesAtEndOfMonth()
    {
        // Testing irregular first coupon reference dates with end of month enabled...
        Schedule schedule =
            new MakeSchedule()
            .From(new Date(17, January, 2017)).To(new Date(28, February, 2018))
            .WithFrequency(Frequency.Semiannual)
            .WithConvention(BusinessDayConvention.Unadjusted)
            .EndOfMonth()
            .Backwards()
            .ToSchedule();

        Leg leg = new FixedRateLeg(schedule)
            .WithNotionals(100.0)
            .WithCouponRates(0.01, new Actual360())
            .ToLeg();

        Coupon firstCoupon =
            CouponCast(leg[0]);

        if (firstCoupon.ReferencePeriodStart != new Date(31, August, 2016))
        {
            Assert.Fail("Expected reference start date at end of month, "
                        + $"got {firstCoupon.ReferencePeriodStart}");
        }
    }

    [Fact]
    public void TestIrregularFirstCouponReferenceDatesAtEndOfCalendarMonth()
    {
        // Testing irregular first coupon reference dates at end of calendar month with end of month enabled...
        Schedule schedule =
            new MakeSchedule()
            .WithCalendar(new UnitedStates(UnitedStatesMarket.GovernmentBond))
            .From(new Date(30, September, 2017)).To(new Date(30, September, 2022))
            .WithTenor(6 * TimeUnit.Months)
            .WithConvention(BusinessDayConvention.Unadjusted)
            .WithTerminationDateConvention(BusinessDayConvention.Unadjusted)
            .WithFirstDate(new Date(31, March, 2018))
            .WithNextToLastDate(new Date(31, March, 2022))
            .EndOfMonth()
            .Backwards()
            .ToSchedule();

        Leg leg = new FixedRateLeg(schedule)
            .WithNotionals(100.0)
            .WithCouponRates(0.01875, new ActualActual(ActualActualConvention.ISMA))
            .ToLeg();

        Coupon firstCoupon =
            CouponCast(leg[0]);
        if (firstCoupon.ReferencePeriodStart != new Date(30, September, 2017))
        {
            Assert.Fail("Expected reference start date at end of calendar day of the month, "
                        + $"got {firstCoupon.ReferencePeriodStart}");
        }

        // Expect first cashflow to be 0.9375
        Close(0.9375, firstCoupon.Amount(Context), 0.0001);
    }

    [Fact]
    public void TestIrregularLastCouponReferenceDatesAtEndOfMonth()
    {
        // Testing irregular last coupon reference dates with end of month enabled...
        Schedule schedule =
                new MakeSchedule()
                        .From(new Date(17, January, 2017)).To(new Date(15, September, 2018))
                        .WithNextToLastDate(new Date(28, February, 2018))
                        .WithFrequency(Frequency.Semiannual)
                        .WithConvention(BusinessDayConvention.Unadjusted)
                        .EndOfMonth()
                        .Backwards()
                        .ToSchedule();

        Leg leg = new FixedRateLeg(schedule)
                .WithNotionals(100.0)
                .WithCouponRates(0.01, new Actual360())
                .ToLeg();

        Coupon lastCoupon =
                CouponCast(leg[^1]);

        if (lastCoupon.ReferencePeriodEnd != new Date(31, August, 2018))
        {
            Assert.Fail("Expected reference end date at end of month, "
                        + $"got {lastCoupon.ReferencePeriodEnd}");
        }
    }

    // Adapted: the fixed-rate legs only. Dropped: the floating legs and their checks, lines 492-536, since USDLibor
    // waits for batch 2.
    [Fact]
    public void TestPartialScheduleLegConstruction()
    {
        // Testing leg construction with partial schedule...
        // schedule with irregular first and last period
        Schedule schedule = new MakeSchedule()
                                .From(new Date(15, September, 2017))
                                .To(new Date(30, September, 2020))
                                .WithNextToLastDate(new Date(25, September, 2020))
                                .WithFrequency(Frequency.Semiannual)
                                .Backwards()
                                .ToSchedule();

        // same schedule, date based, with metadata
        var schedule2 = new Schedule(schedule.Dates, new NullCalendar(), BusinessDayConvention.Unadjusted,
                                     BusinessDayConvention.Unadjusted, 6 * TimeUnit.Months, null,
                                     schedule.EndOfMonth(), schedule.IsRegular());

        // same schedule, date based, without metadata
        var schedule3 = new Schedule(schedule.Dates);

        // fixed rate legs based on the three schedule
        Leg leg = new FixedRateLeg(schedule).WithNotionals(100.0).WithCouponRates(
            0.01, new ActualActual(ActualActualConvention.ISMA)).ToLeg();
        Leg leg2 = new FixedRateLeg(schedule2).WithNotionals(100.0).WithCouponRates(
            0.01, new ActualActual(ActualActualConvention.ISMA)).ToLeg();
        Leg leg3 = new FixedRateLeg(schedule3).WithNotionals(100.0).WithCouponRates(
            0.01, new ActualActual(ActualActualConvention.ISMA)).ToLeg();

        // check reference period of first and last coupon in all variants
        // for the first two we expect a 6M reference period, for the
        // third it can not be constructed, so should be equal to the
        // respective schedule period
        var firstCpn = leg[0] as FixedRateCoupon;
        var lastCpn = leg[^1] as FixedRateCoupon;
        Assert.NotNull(firstCpn);
        Assert.NotNull(lastCpn);
        Assert.Equal(new Date(25, Mar, 2017), firstCpn.ReferencePeriodStart);
        Assert.Equal(new Date(25, Sep, 2017), firstCpn.ReferencePeriodEnd);
        Assert.Equal(new Date(25, Sep, 2020), lastCpn.ReferencePeriodStart);
        Assert.Equal(new Date(25, Mar, 2021), lastCpn.ReferencePeriodEnd);

        var firstCpn2 = leg2[0] as FixedRateCoupon;
        var lastCpn2 = leg2[^1] as FixedRateCoupon;
        Assert.NotNull(firstCpn2);
        Assert.NotNull(lastCpn2);
        Assert.Equal(new Date(25, Mar, 2017), firstCpn2.ReferencePeriodStart);
        Assert.Equal(new Date(25, Sep, 2017), firstCpn2.ReferencePeriodEnd);
        Assert.Equal(new Date(25, Sep, 2020), lastCpn2.ReferencePeriodStart);
        Assert.Equal(new Date(25, Mar, 2021), lastCpn2.ReferencePeriodEnd);

        var firstCpn3 = leg3[0] as FixedRateCoupon;
        var lastCpn3 = leg3[^1] as FixedRateCoupon;
        Assert.NotNull(firstCpn3);
        Assert.NotNull(lastCpn3);
        Assert.Equal(new Date(15, Sep, 2017), firstCpn3.ReferencePeriodStart);
        Assert.Equal(new Date(25, Sep, 2017), firstCpn3.ReferencePeriodEnd);
        Assert.Equal(new Date(25, Sep, 2020), lastCpn3.ReferencePeriodStart);
        Assert.Equal(new Date(30, Sep, 2020), lastCpn3.ReferencePeriodEnd);
    }

    // Adapted: Euribor 6M in place of USDLibor 6M (line 544), which waits for batch 2; the case's index is incidental.
    // The fixing added to the index (line 549) is stored in the context, and the coupon's pricer (line 555), built
    // with QuantLib's defaults, reads the context's at-par switch.
    [Fact]
    public void TestFixedIborCouponWithoutForecastCurve()
    {
        // Testing past ibor coupon without forecast curve...

        Date today = Context.EvaluationDate;

        var index = new Euribor6M();
        Calendar calendar = index.FixingCalendar;

        Date fixingDate = calendar.Advance(today, -2, TimeUnit.Months);
        Rate pastFixing = 0.01;
        EvaluationContext context = Context with
        {
            Fixings = Context.Fixings.AddFixing(index, fixingDate, pastFixing),
        };

        Date startDate = index.ValueDate(fixingDate);
        Date endDate = index.MaturityDate(fixingDate);

        IborCoupon coupon = new IborCoupon(endDate, 100.0, startDate, endDate, index.FixingDays, index)
            .SetPricer(new BlackIborCouponPricer(context));

        Assert.Null(Record.Exception(() => coupon.Amount(context)));

        // the main check is the one above, but let's check for consistency too:
        Real amount = coupon.Amount(context);
        Real expected = pastFixing * coupon.Nominal(context) * coupon.AccrualPeriod();
        if (Math.Abs(amount - expected) > 1e-8)
        {
            Assert.Fail("amount mismatch:"
                        + $"\n    calculated: {amount}"
                        + $"\n    expected: {expected}");
        }
    }

    // Adapted: each block's setting of enforcesTodaysHistoricFixings and its change to the index's fixings (lines
    // 589, 598-599, 605-606 and 612-613) build that block's own context; the coupons' pricers, built with QuantLib's
    // defaults, read the context's at-par switch.
    [Fact]
    public void TestIborCouponKnowsWhenitHasFixed()
    {
        // Testing that ibor coupon knows when it has fixed...

        Date today = Context.EvaluationDate;

        var index = new Euribor3M();
        Calendar calendar = index.FixingCalendar;

        {
            IborCoupon coupon = IborCouponForFixingDate(index, calendar.Advance(today, -1, TimeUnit.Days));
            EvaluationContext context = Context with { Fixings = IndexFixings.Empty };
            // this should not throw an exception if the fixing is missing
            Assert.True(coupon.HasFixed(context));
            // but this should
            Assert.ThrowsAny<NanoQLException>(() => coupon.Rate(context));
        }

        {
            IborCoupon coupon = IborCouponForFixingDate(index, today);
            EvaluationContext context = Context with
            {
                EnforcesTodaysHistoricFixings = false,
                Fixings = IndexFixings.Empty,
            };
            Assert.False(coupon.HasFixed(context));
        }

        {
            IborCoupon coupon = IborCouponForFixingDate(index, today);
            EvaluationContext context = Context with
            {
                EnforcesTodaysHistoricFixings = false,
                Fixings = IndexFixings.Empty.AddFixing(index, coupon.FixingDate(), 0.01),
            };
            Assert.True(coupon.HasFixed(context));
        }

        {
            IborCoupon coupon = IborCouponForFixingDate(index, today);
            EvaluationContext context = Context with
            {
                EnforcesTodaysHistoricFixings = true,
                Fixings = IndexFixings.Empty,
            };
            Assert.True(coupon.HasFixed(context));
            Assert.ThrowsAny<NanoQLException>(() => coupon.Rate(context));
        }

        {
            IborCoupon coupon = IborCouponForFixingDate(index, calendar.Advance(today, 1, TimeUnit.Days));
            EvaluationContext context = Context with { EnforcesTodaysHistoricFixings = true };
            Assert.False(coupon.HasFixed(context));
        }
    }

    private static IborCoupon IborCouponForFixingDate(IborIndex index, Date fixingDate)
    {
        Date startDate = index.ValueDate(fixingDate);
        Date endDate = index.MaturityDate(fixingDate);

        return new IborCoupon(endDate, 100.0, startDate, endDate, index.FixingDays, index)
            .SetPricer(new BlackIborCouponPricer(Context));
    }

    // coupon_cast: the cash flow as a coupon. QuantLib's returns null for any other cash flow, which these cases would
    // then dereference; the cast throws instead.
    private static Coupon CouponCast(CashFlow cf) => (Coupon)cf;

    // BOOST_TEST's tolerance check: the relative difference against each operand within the tolerance.
    private static void Close(Real expected, Real actual, Real tolerance)
    {
        Real diff = Math.Abs(actual - expected);
        Assert.True(
            diff <= tolerance * Math.Abs(expected) && diff <= tolerance * Math.Abs(actual),
            $"{actual:R} != {expected:R} within relative tolerance {tolerance}");
    }

    private static string Show(Date? d) => d?.ToString() ?? "null date";
}
