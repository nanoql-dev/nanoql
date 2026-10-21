// SPDX-FileCopyrightText: Copyright (C) 2009, 2012 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/cashflows.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Currencies;
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
    // it to HasOccurred and CashFlows.Npv.
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
        var noDiscount = new InterestRate(0.0, new Actual365Fixed(), Compounding.Continuous, Frequency.Annual);
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

    // Adapted: QuantLib catches its Error, a proper exception; here a refusal is a NanoQLException or an
    // ArgumentException. The volatility and the coupon's pricer take the context.
    [Fact]
    public void TestAccessViolation()
    {
        // Testing dynamic cast of coupon in Black pricer...

        var todaysDate = new Date(7, April, 2010);
        var settlementDate = new Date(9, April, 2010);
        var context = new EvaluationContext { EvaluationDate = todaysDate };
        Calendar calendar = new TARGET();

        YieldTermStructure rhTermStructure = new FlatForward(settlementDate, 0.04875825, new Actual365Fixed());

        Volatility volatility = 0.10;
        OptionletVolatilityStructure vol = ConstantOptionletVolatility.Create(
            2, calendar, BusinessDayConvention.ModifiedFollowing, volatility, new Actual365Fixed(), context);

        IborIndex index3m = new USDLibor(3 * TimeUnit.Months, rhTermStructure);

        var payDate = new Date(20, December, 2013);
        var startDate = new Date(20, September, 2013);
        var endDate = new Date(20, December, 2013);
        Rate spread = 0.0115;
        IborCouponPricer pricer = new BlackIborCouponPricer(context, vol);
        FloatingRateCoupon coupon = new FloatingRateCoupon(
            payDate, 100, startDate, endDate, 2, index3m, 1.0, spread / 100).SetPricer(pricer);

        try
        {
            // this caused an access violation in version 1.0
            _ = coupon.Amount(context);
        }
        catch (Exception e) when (e is NanoQLException or ArgumentException)
        {
            // ok; proper exception thrown
        }
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

    // Adapted: QuantLib's precondition, at-par coupons, is the context's default; the conversion to a leg (lines
    // 269-273) takes the context.
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

        IborIndex index = new USDLibor(6 * TimeUnit.Months);
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

    // Adapted: the floating legs' conversions take the context.
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

        // same check as above for a floating leg
        IborIndex iborIndex = new USDLibor(3 * TimeUnit.Months);
        Leg legf = new IborLeg(schedule, iborIndex)
                       .WithNotionals(100.0)
                       .WithPaymentDayCounter(new ActualActual(ActualActualConvention.ISMA))
                       .ToLeg(Context);
        Leg legf2 = new IborLeg(schedule2, iborIndex)
                        .WithNotionals(100.0)
                        .WithPaymentDayCounter(new ActualActual(ActualActualConvention.ISMA))
                        .ToLeg(Context);
        Leg legf3 = new IborLeg(schedule3, iborIndex)
                        .WithNotionals(100.0)
                        .WithPaymentDayCounter(new ActualActual(ActualActualConvention.ISMA))
                        .ToLeg(Context);

        var firstCpnF = legf[0] as FloatingRateCoupon;
        var lastCpnF = legf[^1] as FloatingRateCoupon;
        Assert.NotNull(firstCpnF);
        Assert.NotNull(lastCpnF);
        Assert.Equal(new Date(25, Mar, 2017), firstCpnF.ReferencePeriodStart);
        Assert.Equal(new Date(25, Sep, 2017), firstCpnF.ReferencePeriodEnd);
        Assert.Equal(new Date(25, Sep, 2020), lastCpnF.ReferencePeriodStart);
        Assert.Equal(new Date(25, Mar, 2021), lastCpnF.ReferencePeriodEnd);

        var firstCpnF2 = legf2[0] as FloatingRateCoupon;
        var lastCpnF2 = legf2[^1] as FloatingRateCoupon;
        Assert.NotNull(firstCpnF2);
        Assert.NotNull(lastCpnF2);
        Assert.Equal(new Date(25, Mar, 2017), firstCpnF2.ReferencePeriodStart);
        Assert.Equal(new Date(25, Sep, 2017), firstCpnF2.ReferencePeriodEnd);
        Assert.Equal(new Date(25, Sep, 2020), lastCpnF2.ReferencePeriodStart);
        Assert.Equal(new Date(25, Mar, 2021), lastCpnF2.ReferencePeriodEnd);

        var firstCpnF3 = legf3[0] as FloatingRateCoupon;
        var lastCpnF3 = legf3[^1] as FloatingRateCoupon;
        Assert.NotNull(firstCpnF3);
        Assert.NotNull(lastCpnF3);
        Assert.Equal(new Date(15, Sep, 2017), firstCpnF3.ReferencePeriodStart);
        Assert.Equal(new Date(25, Sep, 2017), firstCpnF3.ReferencePeriodEnd);
        Assert.Equal(new Date(25, Sep, 2020), lastCpnF3.ReferencePeriodStart);
        Assert.Equal(new Date(30, Sep, 2020), lastCpnF3.ReferencePeriodEnd);
    }

    // Adapted: the fixing added to the index (line 549) is stored in the context, and the coupon's pricer (line 555),
    // built with QuantLib's defaults, reads the context's at-par switch.
    [Fact]
    public void TestFixedIborCouponWithoutForecastCurve()
    {
        // Testing past ibor coupon without forecast curve...

        Date today = Context.EvaluationDate;

        var index = new USDLibor(6 * TimeUnit.Months);
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

    // Adapted: the conversions to legs take a context with today as the evaluation date.
    [Fact]
    public void TestInterpolatedIborStubCoupon()
    {
        // Testing interpolation of an irregular Ibor coupon...

        var today = new Date(27, May, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure twoMonthCurve = new FlatForward(today, 0.02, new Actual365Fixed());
        YieldTermStructure threeMonthCurve = new FlatForward(today, 0.04, new Actual365Fixed());

        Calendar calendar = new NewZealand();
        var bkbm2m = new IborIndex(
            "Bkbm", 2 * TimeUnit.Months, 0, new NZDCurrency(), calendar, BusinessDayConvention.ModifiedFollowing, false,
            new Actual365Fixed(), twoMonthCurve);
        var bkbm3m = new IborIndex(
            "Bkbm", 3 * TimeUnit.Months, 0, new NZDCurrency(), calendar, BusinessDayConvention.ModifiedFollowing, false,
            new Actual365Fixed(), threeMonthCurve);
        var start = new Date(29, May, 2026);
        var maturity = new Date(28, February, 2027);

        Schedule schedule = new MakeSchedule()
                                .From(start)
                                .To(maturity)
                                .WithTenor(3 * TimeUnit.Months)
                                .WithCalendar(calendar)
                                .WithConvention(BusinessDayConvention.ModifiedFollowing)
                                .Backwards()
                                .ToSchedule();
        Assert.False(schedule.IsRegular(1));
        Assert.Equal(new Date(28, August, 2026), schedule.Date(1));

        var convention = new StubIndexSelection(StubIndexSelectionConvention.Interpolated, [bkbm2m, bkbm3m]);
        Leg leg = new IborLeg(schedule, bkbm3m)
                      .WithNotionals(1.0)
                      .WithIndexedCoupons(true)
                      .WithStubIndexSelection(convention)
                      .ToLeg(context);

        var coupon = Assert.IsType<StubIborCoupon>(leg[0]);
        Assert.IsNotType<StubIborCoupon>(leg[1]);

        Date fixingDate = coupon.FixingDate();
        Date shortMaturity = bkbm2m.MaturityDate(start);
        Date longMaturity = bkbm3m.MaturityDate(start);
        Assert.Equal(new Date(29, July, 2026), shortMaturity);
        Assert.Equal(new Date(31, August, 2026), longMaturity);

        Rate shortRate = bkbm2m.Fixing(fixingDate, context);
        Rate longRate = bkbm3m.Fixing(fixingDate, context);
        Real weight = (double)(coupon.AccrualEndDate - shortMaturity) / (longMaturity - shortMaturity);
        Rate expected = shortRate + ((longRate - shortRate) * weight);
        Assert.True(Math.Abs(coupon.IndexFixing(context) - expected) <= 1.0e-14);

        var closest = new StubIndexSelection(StubIndexSelectionConvention.ClosestIndex, [bkbm2m, bkbm3m]);
        Leg closestLeg = new IborLeg(schedule, bkbm3m)
                             .WithNotionals(1.0)
                             .WithIndexedCoupons(true)
                             .WithStubIndexSelection(closest)
                             .ToLeg(context);
        var closestCoupon = Assert.IsType<StubIborCoupon>(closestLeg[0]);
        Assert.True(Math.Abs(closestCoupon.IndexFixing(context) - longRate) <= 1.0e-14);

        // stub index conventions require indexed coupons...
        Assert.Throws<NanoQLException>(() => new IborLeg(schedule, bkbm3m)
                                                 .WithNotionals(1.0)
                                                 .WithIndexedCoupons(false)
                                                 .WithStubIndexSelection(convention)
                                                 .ToLeg(context));

        // ...and candidates with distinct maturities
        var duplicate = new StubIndexSelection(StubIndexSelectionConvention.Interpolated, [bkbm2m, bkbm3m, bkbm3m]);
        Assert.Throws<ArgumentException>(() => new IborLeg(schedule, bkbm3m)
                                                   .WithNotionals(1.0)
                                                   .WithIndexedCoupons(true)
                                                   .WithStubIndexSelection(duplicate)
                                                   .ToLeg(context));

        // a default-constructed configuration is empty and leaves the leg alone
        Assert.True(new StubIndexSelection().Empty);
        Leg defaultLeg = new IborLeg(schedule, bkbm3m)
                             .WithNotionals(1.0)
                             .WithIndexedCoupons(true)
                             .WithStubIndexSelection(new StubIndexSelection())
                             .ToLeg(context);
        Assert.IsNotType<StubIborCoupon>(defaultLeg[0]);

        // configurations without usable candidates are rejected on construction
        Assert.Throws<ArgumentException>(() => new StubIndexSelection(StubIndexSelectionConvention.Interpolated, []));
        Assert.Throws<ArgumentException>(
            () => new StubIndexSelection(StubIndexSelectionConvention.Interpolated, [bkbm2m, null!]));
    }

    // Adapted: as TestInterpolatedIborStubCoupon.
    [Fact]
    public void TestIborStubInterpolationEndpointsAndRegularSchedule()
    {
        // Testing Ibor stub endpoints and unchanged regular coupons...

        var today = new Date(27, May, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure twoMonthCurve = new FlatForward(today, 0.02, new Actual365Fixed());
        YieldTermStructure threeMonthCurve = new FlatForward(today, 0.04, new Actual365Fixed());
        var bkbm2m = new Bkbm2M(twoMonthCurve);
        var bkbm3m = new Bkbm3M(threeMonthCurve);
        var start = new Date(29, May, 2026);
        var convention = new StubIndexSelection(StubIndexSelectionConvention.Interpolated, [bkbm2m, bkbm3m]);

        foreach (IborIndex endpoint in new IborIndex[] { bkbm2m, bkbm3m })
        {
            Date end = endpoint.MaturityDate(start);
            var endpointSchedule = new Schedule(
                [start, end], endpoint.FixingCalendar, BusinessDayConvention.ModifiedFollowing, null,
                3 * TimeUnit.Months, DateGenerationRule.Backward, true, [false]);
            Leg leg = new IborLeg(endpointSchedule, bkbm3m)
                          .WithNotionals(1.0)
                          .WithIndexedCoupons(true)
                          .WithStubIndexSelection(convention)
                          .ToLeg(context);
            var coupon = Assert.IsType<StubIborCoupon>(leg[0]);
            Assert.True(
                Math.Abs(coupon.IndexFixing(context) - endpoint.Fixing(coupon.FixingDate(), context)) <= 1.0e-14);
        }

        Date regularEnd = bkbm3m.MaturityDate(start);
        var regularSchedule = new Schedule(
            [start, regularEnd], bkbm3m.FixingCalendar, BusinessDayConvention.ModifiedFollowing, null,
            3 * TimeUnit.Months, DateGenerationRule.Backward, true, [true]);
        Leg defaultLeg = new IborLeg(regularSchedule, bkbm3m)
                             .WithNotionals(1.0)
                             .WithIndexedCoupons(true)
                             .ToLeg(context);
        Leg configuredLeg = new IborLeg(regularSchedule, bkbm3m)
                                .WithNotionals(1.0)
                                .WithIndexedCoupons(true)
                                .WithStubIndexSelection(convention)
                                .ToLeg(context);
        Assert.IsNotType<StubIborCoupon>(configuredLeg[0]);
        var defaultCoupon = Assert.IsAssignableFrom<IborCoupon>(defaultLeg[0]);
        var configuredCoupon = Assert.IsAssignableFrom<IborCoupon>(configuredLeg[0]);
        Assert.True(Math.Abs(defaultCoupon.Rate(context) - configuredCoupon.Rate(context)) <= 1.0e-14);
    }

    // Adapted: as TestInterpolatedIborStubCoupon.
    [Fact]
    public void TestFinalAndLongIborStubCoupons()
    {
        // Testing final and long Ibor stub coupons...

        var today = new Date(27, May, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        Calendar calendar = new NewZealand();

        YieldTermStructure oneMonthCurve = new FlatForward(today, 0.01, new Actual365Fixed());
        YieldTermStructure threeMonthCurve = new FlatForward(today, 0.03, new Actual365Fixed());
        YieldTermStructure sixMonthCurve = new FlatForward(today, 0.06, new Actual365Fixed());
        var bkbm1m = new Bkbm1M(oneMonthCurve);
        var bkbm3m = new Bkbm3M(threeMonthCurve);
        var bkbm6m = new Bkbm6M(sixMonthCurve);

        var finalStubSchedule = new Schedule(
            [new Date(29, May, 2026), new Date(31, August, 2026), new Date(30, October, 2026)],
            calendar, BusinessDayConvention.ModifiedFollowing, null, 3 * TimeUnit.Months, DateGenerationRule.Forward,
            true, [true, false]);
        var finalConvention = new StubIndexSelection(StubIndexSelectionConvention.Interpolated, [bkbm1m, bkbm3m]);
        Leg finalStubLeg = new IborLeg(finalStubSchedule, bkbm3m)
                               .WithNotionals(1.0)
                               .WithIndexedCoupons(true)
                               .WithStubIndexSelection(finalConvention)
                               .ToLeg(context);
        Assert.IsNotType<StubIborCoupon>(finalStubLeg[0]);
        var finalCoupon = Assert.IsType<StubIborCoupon>(finalStubLeg[^1]);

        Date finalStart = finalCoupon.AccrualStartDate;
        Date oneMonthMaturity = bkbm1m.MaturityDate(finalStart);
        Date threeMonthMaturity = bkbm3m.MaturityDate(finalStart);
        Real finalWeight = (double)(finalCoupon.AccrualEndDate - oneMonthMaturity)
            / (threeMonthMaturity - oneMonthMaturity);
        Rate expectedFinal =
            bkbm1m.Fixing(finalCoupon.FixingDate(), context)
            + ((bkbm3m.Fixing(finalCoupon.FixingDate(), context) - bkbm1m.Fixing(finalCoupon.FixingDate(), context))
                * finalWeight);
        Assert.True(Math.Abs(finalCoupon.IndexFixing(context) - expectedFinal) <= 1.0e-14);

        var longStart = new Date(29, May, 2026);
        var longEnd = new Date(30, October, 2026);
        var longStubSchedule = new Schedule(
            [longStart, longEnd], calendar, BusinessDayConvention.ModifiedFollowing, null, 3 * TimeUnit.Months,
            DateGenerationRule.Backward, true, [false]);
        var longConvention = new StubIndexSelection(StubIndexSelectionConvention.Interpolated, [bkbm3m, bkbm6m]);
        Leg longStubLeg = new IborLeg(longStubSchedule, bkbm3m)
                              .WithNotionals(1.0)
                              .WithIndexedCoupons(true)
                              .WithStubIndexSelection(longConvention)
                              .ToLeg(context);
        var longCoupon = Assert.IsType<StubIborCoupon>(longStubLeg[0]);
        Date threeMonthLongMaturity = bkbm3m.MaturityDate(longStart);
        Date sixMonthMaturity = bkbm6m.MaturityDate(longStart);
        Real longWeight = (double)(longEnd - threeMonthLongMaturity) / (sixMonthMaturity - threeMonthLongMaturity);
        Rate expectedLong =
            bkbm3m.Fixing(longCoupon.FixingDate(), context)
            + ((bkbm6m.Fixing(longCoupon.FixingDate(), context) - bkbm3m.Fixing(longCoupon.FixingDate(), context))
                * longWeight);
        Assert.True(Math.Abs(longCoupon.IndexFixing(context) - expectedLong) <= 1.0e-14);
    }

    // Adapted: QuantLib relinks the two-month curve and checks that the coupon notifies its observers and reprices;
    // here no object observes another, so a coupon built on a two-month index on the relinked curve prices differently.
    // The fixings added to the indexes (lines 862-863) are stored in the context of 1 June, and cleared with it.
    [Fact]
    public void TestIborStubCouponHistoricalFixingsAndRelinking()
    {
        // Testing Ibor stub historical fixings and forwarding-curve relinking...

        var fixingDate = new Date(29, May, 2026);
        Date start = fixingDate;
        var end = new Date(28, August, 2026);

        var bkbm2m = new Bkbm2M(new FlatForward(fixingDate, 0.02, new Actual365Fixed()));
        var bkbm3m = new Bkbm3M(new FlatForward(fixingDate, 0.04, new Actual365Fixed()));
        var convention = new StubIndexSelection(StubIndexSelectionConvention.Interpolated, [bkbm2m, bkbm3m]);
        var schedule = new Schedule(
            [start, end], bkbm3m.FixingCalendar, BusinessDayConvention.ModifiedFollowing, null, 3 * TimeUnit.Months,
            DateGenerationRule.Backward, true, [false]);

        var context = new EvaluationContext { EvaluationDate = new Date(27, May, 2026) };
        StubIborCoupon CouponOn(StubIndexSelection selection) =>
            Assert.IsType<StubIborCoupon>(new IborLeg(schedule, bkbm3m)
                                              .WithNotionals(1.0)
                                              .WithIndexedCoupons(true)
                                              .WithStubIndexSelection(selection)
                                              .ToLeg(context)[0]);
        StubIborCoupon coupon = CouponOn(convention);

        Rate initialRate = coupon.Rate(context);
        var relinked = new Bkbm2M(new FlatForward(fixingDate, 0.03, new Actual365Fixed()));
        StubIborCoupon repriced = CouponOn(
            new StubIndexSelection(StubIndexSelectionConvention.Interpolated, [relinked, bkbm3m]));
        Assert.True(Math.Abs(repriced.Rate(context) - initialRate) > 1.0e-6);

        var afterFixing = new EvaluationContext
        {
            EvaluationDate = new Date(1, June, 2026),
            Fixings = IndexFixings.Empty.AddFixing(bkbm2m, fixingDate, 0.05).AddFixing(bkbm3m, fixingDate, 0.08),
        };
        Date shortMaturity = bkbm2m.MaturityDate(start);
        Date longMaturity = bkbm3m.MaturityDate(start);
        Real weight = (double)(end - shortMaturity) / (longMaturity - shortMaturity);
        Rate expected = 0.05 + ((0.08 - 0.05) * weight);
        Assert.True(Math.Abs(coupon.IndexFixing(afterFixing) - expected) <= 1.0e-14);
    }

    // Adapted: the fixings added to the indexes (lines 904 and 908) build each check's own context.
    [Fact]
    public void TestIborStubCouponHasFixedUsesSelectedIndices()
    {
        // Testing that a stub coupon has fixed only once its selected indices have...

        var fixingDate = new Date(29, May, 2026);
        Date start = fixingDate;
        var end = new Date(28, August, 2026);
        var context = new EvaluationContext { EvaluationDate = fixingDate };

        var bkbm2m = new Bkbm2M(new FlatForward(fixingDate, 0.02, new Actual365Fixed()));
        var bkbm3m = new Bkbm3M(new FlatForward(fixingDate, 0.04, new Actual365Fixed()));
        var convention = new StubIndexSelection(StubIndexSelectionConvention.Interpolated, [bkbm2m, bkbm3m]);
        var schedule = new Schedule(
            [start, end], bkbm3m.FixingCalendar, BusinessDayConvention.ModifiedFollowing, null, 3 * TimeUnit.Months,
            DateGenerationRule.Backward, true, [false]);
        Leg leg = new IborLeg(schedule, bkbm3m)
                      .WithNotionals(1.0)
                      .WithIndexedCoupons(true)
                      .WithStubIndexSelection(convention)
                      .ToLeg(context);
        var coupon = Assert.IsType<StubIborCoupon>(leg[0]);
        Assert.Equal(fixingDate, coupon.FixingDate());

        Assert.False(coupon.HasFixed(context));

        // The leg index's own print must not flip the stub to fixed: the coupon
        // reads the bracketing candidates, and the 2M print is still missing.
        // Checked through a base-class pointer so the virtual dispatch is covered.
        EvaluationContext threeMonthPrint = context with
        {
            Fixings = context.Fixings.AddFixing(bkbm3m, fixingDate, 0.08),
        };
        IborCoupon asIborCoupon = coupon;
        Assert.False(asIborCoupon.HasFixed(threeMonthPrint));

        EvaluationContext bothPrints = threeMonthPrint with
        {
            Fixings = threeMonthPrint.Fixings.AddFixing(bkbm2m, fixingDate, 0.05),
        };
        Assert.True(asIborCoupon.HasFixed(bothPrints));

        Date shortMaturity = bkbm2m.MaturityDate(start);
        Date longMaturity = bkbm3m.MaturityDate(start);
        Real weight = (double)(end - shortMaturity) / (longMaturity - shortMaturity);
        Assert.True(Math.Abs(coupon.IndexFixing(bothPrints) - (0.05 + ((0.08 - 0.05) * weight))) <= 1.0e-14);
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
