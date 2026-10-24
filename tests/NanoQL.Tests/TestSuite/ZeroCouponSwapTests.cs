// SPDX-FileCopyrightText: Copyright (C) 2021 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/zerocouponswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

// Adapted throughout: the fixture's relinkable handle is the curve itself, built first, and its index fixing and
// evaluation date are its context; a swap's engine is the static DiscountingSwapEngine.Calculate on the curve, its
// ZeroCouponSwap overload, whose results carry the fair payment, and QuantLib's fairFixedRate takes those results.
[Trait("Oracle", "QuantLib")]
public class ZeroCouponSwapTests
{
    [Fact]
    public void TestInstrumentValuation()
    {
        // Testing zero coupon swap valuation...

        // Ongoing instrument
        CheckReplicationOfZeroCouponSwapNPV(new Date(12, February, 2021), new Date(12, February, 2041), SwapType.Receiver);

        // Forward starting instrument
        CheckReplicationOfZeroCouponSwapNPV(new Date(15, April, 2021), new Date(12, February, 2041), SwapType.Payer);

        // Expired instrument
        CheckReplicationOfZeroCouponSwapNPV(new Date(12, February, 2000), new Date(12, February, 2020));
    }

    [Fact]
    public void TestFairFixedPayment()
    {
        // Testing fair fixed payment...

        // Ongoing instrument
        CheckFairFixedPayment(new Date(12, February, 2021), new Date(12, February, 2041), SwapType.Receiver);

        // Spot starting instrument
        CheckFairFixedPayment(new Date(17, March, 2021), new Date(12, February, 2041), SwapType.Payer);
    }

    [Fact]
    public void TestFairFixedRate()
    {
        // Testing fair fixed rate...

        // Ongoing instrument
        CheckFairFixedRate(new Date(12, February, 2021), new Date(12, February, 2041), SwapType.Receiver);

        // Spot starting instrument
        CheckFairFixedRate(new Date(17, March, 2021), new Date(12, February, 2041), SwapType.Payer);
    }

    [Fact]
    public void TestFixedPaymentFromRate()
    {
        // Testing fixed payment calculation from rate...
        var vars = new CommonVars();
        const Real tolerance = 1.0e-8;
        const Rate fixedRate = 0.01;

        var start = new Date(12, February, 2021);
        var end = new Date(12, February, 2041);

        ZeroCouponSwap zcSwap = vars.CreateZCSwap(start, end, fixedRate);
        Real actualFxdPmt = zcSwap.FixedPayment(vars.Context);

        Real t = vars.DayCount.YearFraction(start, end);
        Real expectedFxdPmt = zcSwap.BaseNominal * (Math.Pow(1.0 + fixedRate, t) - 1.0);

        Assert.False(
            Math.Abs(actualFxdPmt - expectedFxdPmt) > tolerance,
            $"unable to replicate fixed payment from rate\n    actual fixed payment:    {actualFxdPmt}\n"
            + $"    expected fixed payment:    {expectedFxdPmt}\n    start:    {start}\n    end:    {end}\n");
    }

    [Fact]
    public void TestArgumentsValidation()
    {
        // Testing arguments validation...
        var vars = new CommonVars();

        var start = new Date(12, February, 2021);
        var end = new Date(12, February, 2041);

        // Negative base nominal
        Assert.Throws<ArgumentException>(() => vars.CreateZCSwap(SwapType.Payer, start, end, -1.0e6, 1.0e6));

        // Start date after end date
        Assert.Throws<ArgumentException>(() => vars.CreateZCSwap(end, start, 0.01));
    }

    [Fact]
    public void TestExpectedCashFlowsInLegs()
    {
        // Testing expected cash flows in legs...
        var vars = new CommonVars();
        const Real tolerance = 1.0e-8;

        var start = new Date(12, February, 2021);
        var end = new Date(12, February, 2041);

        ZeroCouponSwap zcSwap = vars.CreateZCSwap(start, end, 0.01);
        CashFlow fixedCashFlow = zcSwap.FixedLeg[0];
        CashFlow floatingCashFlow = zcSwap.FloatingLeg[0];

        Date paymentDate = vars.Calendar.Advance(end, vars.PaymentDelay, TimeUnit.Days, vars.BusinessConvention);
        CashFlow subPeriodCpn = vars.CreateMultipleResetsCoupon(start, end);

        Assert.False(
            (Math.Abs(fixedCashFlow.Amount(vars.Context) - zcSwap.FixedPayment(vars.Context)) > tolerance)
            || (fixedCashFlow.Date != paymentDate),
            $"unable to replicate fixed leg\n    actual amount:    {fixedCashFlow.Amount(vars.Context)}\n"
            + $"    expected amount:    {zcSwap.FixedPayment(vars.Context)}\n    actual payment date:    {fixedCashFlow.Date}\n"
            + $"    expected payment date:    {paymentDate}\n");

        Assert.False(
            (Math.Abs(floatingCashFlow.Amount(vars.Context) - subPeriodCpn.Amount(vars.Context)) > tolerance)
            || (floatingCashFlow.Date != paymentDate),
            $"unable to replicate floating leg\n    actual amount:    {floatingCashFlow.Amount(vars.Context)}\n"
            + $"    expected amount:    {subPeriodCpn.Amount(vars.Context)}\n"
            + $"    actual payment date:    {floatingCashFlow.Date}\n    expected payment date:    {paymentDate}\n");
    }

    private static void CheckReplicationOfZeroCouponSwapNPV(Date start, Date end, SwapType type = SwapType.Receiver)
    {
        var vars = new CommonVars();
        const Real tolerance = 1.0e-8;

        ZeroCouponSwap zcSwap = vars.CreateZCSwap(type, start, end);
        ZeroCouponSwapResults results = vars.Npv(zcSwap);

        Real actualNPV = results.NPV!.Value;
        Real actualFixedLegNPV = results.FixedLegNPV!.Value;
        Real actualFloatLegNPV = results.FloatingLegNPV!.Value;

        Date paymentDate = vars.Calendar.Advance(end, vars.PaymentDelay, TimeUnit.Days, vars.BusinessConvention);
        Real discountAtPayment = paymentDate < vars.Settlement ? 0.0 : vars.EuriborCurve.Discount(paymentDate);
        Real expectedFixedLegNPV = -(int)type * discountAtPayment * vars.FinalPayment;

        CashFlow subPeriodCpn = vars.CreateMultipleResetsCoupon(start, end);
        Real expectedFloatLegNPV =
            paymentDate < vars.Settlement ? 0.0 : (int)type * discountAtPayment * subPeriodCpn.Amount(vars.Context);

        Real expectedNPV = expectedFloatLegNPV + expectedFixedLegNPV;

        Assert.False(
            (Math.Abs(actualNPV - expectedNPV) > tolerance)
            || (Math.Abs(actualFixedLegNPV - expectedFixedLegNPV) > tolerance)
            || (Math.Abs(actualFloatLegNPV - expectedFloatLegNPV) > tolerance),
            $"unable to replicate NPVs of zero coupon swap and its legs\n    actual NPV:    {actualNPV}\n"
            + $"    expected NPV:    {expectedNPV}\n    actual fixed leg NPV:    {actualFixedLegNPV}\n"
            + $"    expected fixed leg NPV:    {expectedFixedLegNPV}\n    actual float leg NPV:    {actualFloatLegNPV}\n"
            + $"    expected float leg NPV:    {expectedFloatLegNPV}\n    start:    {start}\n    end:    {end}\n"
            + $"    type:    {(int)type}\n");
    }

    private static void CheckFairFixedPayment(Date start, Date end, SwapType type)
    {
        var vars = new CommonVars();
        const Real tolerance = 1.0e-8;

        ZeroCouponSwap zcSwap = vars.CreateZCSwap(type, start, end);
        Real fairFixedPayment = vars.Npv(zcSwap).FairFixedPayment!.Value;
        ZeroCouponSwap parZCSwap = vars.CreateZCSwap(type, start, end, fairFixedPayment);
        Real parZCSwapNPV = vars.Npv(parZCSwap).NPV!.Value;

        Assert.False(
            Math.Abs(parZCSwapNPV) > tolerance,
            $"unable to replicate fair fixed payment\n    actual NPV:    {parZCSwapNPV}\n    expected NPV:    0.0\n"
            + $"    fair fixed payment:    {fairFixedPayment}\n    start:    {start}\n    end:    {end}\n"
            + $"    type:    {(int)type}\n");
    }

    private static void CheckFairFixedRate(Date start, Date end, SwapType type)
    {
        var vars = new CommonVars();
        const Real tolerance = 1.0e-8;

        ZeroCouponSwap zcSwap = vars.CreateZCSwap(type, start, end);
        Rate fairFixedRate = zcSwap.FairFixedRate(vars.Npv(zcSwap), vars.DayCount);
        ZeroCouponSwap parZCSwap = vars.CreateZCSwap(start, end, fairFixedRate);
        Real parZCSwapNPV = vars.Npv(parZCSwap).NPV!.Value;

        Assert.False(
            Math.Abs(parZCSwapNPV) > tolerance,
            $"unable to replicate fair fixed rate\n    actual NPV:    {parZCSwapNPV}\n    expected NPV:    0.0\n"
            + $"    fair fixed rate:    {fairFixedRate}\n    start:    {start}\n    end:    {end}\n"
            + $"    type:    {(int)type}\n");
    }

    private sealed class CommonVars
    {
        // utilities
        public CommonVars()
        {
            SettlementDays = 2;
            PaymentDelay = 1;
            Calendar = new TARGET();
            DayCount = new Actual365Fixed();
            BusinessConvention = BusinessDayConvention.ModifiedFollowing;
            BaseNominal = 1.0e6;
            FinalPayment = 1.2e6;

            Today = Calendar.Adjust(new Date(15, March, 2021));
            Settlement = Calendar.Advance(Today, SettlementDays, TimeUnit.Days);

            EuriborCurve = new FlatForward(Settlement, 0.007, DayCount);
            Euribor = new Euribor6M(EuriborCurve);
            var context = new EvaluationContext { EvaluationDate = Today };
            Context = context with { Fixings = context.Fixings.AddFixing(Euribor, new Date(10, February, 2021), 0.0085) };
        }

        public Date Today { get; }

        public Date Settlement { get; }

        public Calendar Calendar { get; }

        public int SettlementDays { get; }

        public int PaymentDelay { get; }

        public DayCounter DayCount { get; }

        public BusinessDayConvention BusinessConvention { get; }

        public Real BaseNominal { get; }

        public Real FinalPayment { get; }

        public IborIndex Euribor { get; }

        public YieldTermStructure EuriborCurve { get; }

        public EvaluationContext Context { get; }

        public ZeroCouponSwapResults Npv(ZeroCouponSwap swap) => DiscountingSwapEngine.Calculate(swap, Context, EuriborCurve);

        public CashFlow CreateMultipleResetsCoupon(Date start, Date end)
        {
            Date paymentDate = Calendar.Advance(end, PaymentDelay, TimeUnit.Days, BusinessConvention);
            Schedule schedule = new MakeSchedule()
                .From(start).To(end)
                .WithTenor(Euribor.Tenor)
                .WithCalendar(Euribor.FixingCalendar)
                .ToSchedule();
            return new MultipleResetsCoupon(paymentDate, BaseNominal, schedule, SettlementDays, Euribor)
                .SetPricer(new CompoundingMultipleResetsPricer());
        }

        public ZeroCouponSwap CreateZCSwap(SwapType type, Date start, Date end, Real baseNominal, Real finalPayment) =>
            new(type, baseNominal, start, end, finalPayment, Euribor, Calendar, BusinessConvention, PaymentDelay);

        public ZeroCouponSwap CreateZCSwap(SwapType type, Date start, Date end, Real finalPayment) =>
            CreateZCSwap(type, start, end, BaseNominal, finalPayment);

        public ZeroCouponSwap CreateZCSwap(SwapType type, Date start, Date end) =>
            CreateZCSwap(type, start, end, FinalPayment);

        public ZeroCouponSwap CreateZCSwap(Date start, Date end, Rate fixedRate) =>
            new(SwapType.Receiver, BaseNominal, start, end, fixedRate, DayCount, Euribor, Calendar, BusinessConvention, PaymentDelay);
    }
}
