// SPDX-FileCopyrightText: Copyright (C) 2025 Shubham Gaur
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/floatfloatswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

// QuantLib's fairSpread1() and fairSpread2() throw "fair spread 1 not available" and "fair spread 2 not available" on
// a null result: the results here are null, which the cases assert.
[Trait("Oracle", "QuantLib")]
public class FloatFloatSwapTests
{
    // QuantLib's test runner's evaluation date (quantlibglobalfixture.cpp:62-63)
    private static readonly Date RunnerDate = new(16, Month.September, 2015);

    [Fact]
    public void TestFairSpread1()
    {
        // Testing float-float swap calculation of fair spread on leg 1...

        var vars = new CommonVars();

        SwapType[] types = [SwapType.Payer, SwapType.Receiver];
        double[] spread2Values = [-0.002, 0.0, 0.002, 0.005];

        foreach (SwapType type in types)
        {
            foreach (double spread2 in spread2Values)
            {
                FloatFloatSwap swap = vars.MakeSwap(type, 0.0, spread2);
                double fair = vars.Price(swap).FairSpread1!.Value;

                FloatFloatSwap swap2 = vars.MakeSwap(type, fair, spread2);
                double npv = vars.Price(swap2).NPV!.Value;
                Assert.True(
                    Math.Abs(npv) <= 1.0e-10,
                    $"recalculating with fair spread on leg 1: type {type}, spread on leg 2 {spread2}, fair spread on leg 1 {fair}, swap NPV {npv}");
            }
        }
    }

    [Fact]
    public void TestFairSpread2()
    {
        // Testing float-float swap calculation of fair spread on leg 2...

        var vars = new CommonVars();

        SwapType[] types = [SwapType.Payer, SwapType.Receiver];
        double[] spread1Values = [-0.002, 0.0, 0.002, 0.005];

        foreach (SwapType type in types)
        {
            foreach (double spread1 in spread1Values)
            {
                FloatFloatSwap swap = vars.MakeSwap(type, spread1, 0.0);
                double fair = vars.Price(swap).FairSpread2!.Value;

                FloatFloatSwap swap2 = vars.MakeSwap(type, spread1, fair);
                double npv = vars.Price(swap2).NPV!.Value;
                Assert.True(
                    Math.Abs(npv) <= 1.0e-10,
                    $"recalculating with fair spread on leg 2: type {type}, spread on leg 1 {spread1}, fair spread on leg 2 {fair}, swap NPV {npv}");
            }
        }
    }

    [Fact]
    public void TestPayerReceiverSymmetry()
    {
        // Testing float-float swap payer/receiver NPV symmetry...

        var vars = new CommonVars();

        double spread1 = 0.001;
        double spread2 = 0.003;

        double payer = vars.Price(vars.MakeSwap(SwapType.Payer, spread1, spread2)).NPV!.Value;
        double receiver = vars.Price(vars.MakeSwap(SwapType.Receiver, spread1, spread2)).NPV!.Value;

        double tolerance = 1.0e-10;
        Assert.True(
            Math.Abs(payer + receiver) <= tolerance,
            $"payer and receiver NPVs do not cancel: payer NPV {payer}, receiver NPV {receiver}, sum {payer + receiver}");
    }

    [Fact]
    public void TestFairSpreadPayerReceiverConsistency()
    {
        // Testing float-float swap fair spread consistency between payer and receiver...

        var vars = new CommonVars();

        double spread2 = 0.002;

        FloatFloatSwap payer = vars.MakeSwap(SwapType.Payer, 0.0, spread2);
        FloatFloatSwap receiver = vars.MakeSwap(SwapType.Receiver, 0.0, spread2);

        double fairPayer = vars.Price(payer).FairSpread1!.Value;
        double fairReceiver = vars.Price(receiver).FairSpread1!.Value;

        double tolerance = 1.0e-10;
        Assert.True(
            Math.Abs(fairPayer - fairReceiver) <= tolerance,
            $"fair spread on leg 1 differs between payer and receiver: payer {fairPayer}, receiver {fairReceiver}");

        FloatFloatSwap payer2 = vars.MakeSwap(SwapType.Payer, spread2, 0.0);
        FloatFloatSwap receiver2 = vars.MakeSwap(SwapType.Receiver, spread2, 0.0);

        fairPayer = vars.Price(payer2).FairSpread2!.Value;
        fairReceiver = vars.Price(receiver2).FairSpread2!.Value;

        Assert.True(
            Math.Abs(fairPayer - fairReceiver) <= tolerance,
            $"fair spread on leg 2 differs between payer and receiver: payer {fairPayer}, receiver {fairReceiver}");
    }

    [Fact]
    public void TestZeroBpsFairSpread()
    {
        // Testing float-float swap fair spread calculation with zero BPS...

        var vars = new CommonVars { Nominal = 0.0 };
        FloatFloatSwap swap = vars.MakeSwap(SwapType.Payer, 0.0, 0.0);

        Assert.False(swap.IsExpired(vars.Context));
        FloatFloatSwapResults results = vars.Price(swap);
        Assert.Equal(0.0, results.LegBPS[0]);
        Assert.Equal(0.0, results.LegBPS[1]);

        // QuantLib: "fair spread 1 not available", "fair spread 2 not available"
        Assert.Null(results.FairSpread1);
        Assert.Null(results.FairSpread2);
    }

    [Fact]
    public void TestExpiredSwapFairSpread()
    {
        // Testing float-float swap fair spread calculation for expired swap...

        var vars = new CommonVars();
        FloatFloatSwap swap = vars.MakeSwap(SwapType.Payer, 0.0, 0.0, 10);

        EvaluationContext later = vars.Context with { EvaluationDate = vars.Settlement + new Period(20, TimeUnit.Years) };

        Assert.True(swap.IsExpired(later));
        FloatFloatSwapResults results = DiscountingSwapEngine.Calculate(swap, later, vars.TermStructure);

        // QuantLib: "fair spread 1 not available", "fair spread 2 not available"
        Assert.Null(results.FairSpread1);
        Assert.Null(results.FairSpread2);
    }

    private sealed class CommonVars
    {
        public CommonVars()
        {
            SettlementDays = 2;
            Calendar = new TARGET();
            Today = Calendar.Adjust(RunnerDate);
            Context = new EvaluationContext { EvaluationDate = Today };
            Settlement = Calendar.Advance(Today, SettlementDays, TimeUnit.Days);
            TermStructure = new FlatForward(Settlement, 0.05, new Actual365Fixed());
            Index1 = new Euribor(new Period(3, TimeUnit.Months), TermStructure);
            Index2 = new Euribor(new Period(6, TimeUnit.Months), TermStructure);
        }

        public Date Today { get; }

        public Date Settlement { get; }

        public double Nominal { get; init; } = 100.0;

        public Calendar Calendar { get; }

        public YieldTermStructure TermStructure { get; }

        public IborIndex Index1 { get; }

        public IborIndex Index2 { get; }

        public int SettlementDays { get; }

        public EvaluationContext Context { get; }

        public FloatFloatSwap MakeSwap(SwapType type, double spread1, double spread2, int lengthInYears = 10)
        {
            Date maturity = Calendar.Advance(
                Settlement, lengthInYears, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing);

            var schedule1 = new Schedule(
                Settlement,
                maturity,
                Index1.Tenor,
                Calendar,
                BusinessDayConvention.ModifiedFollowing,
                BusinessDayConvention.ModifiedFollowing,
                DateGenerationRule.Forward,
                false,
                Context);

            var schedule2 = new Schedule(
                Settlement,
                maturity,
                Index2.Tenor,
                Calendar,
                BusinessDayConvention.ModifiedFollowing,
                BusinessDayConvention.ModifiedFollowing,
                DateGenerationRule.Forward,
                false,
                Context);

            var swap = new FloatFloatSwap(
                type,
                Nominal,
                Nominal,
                schedule1,
                Index1,
                Index1.DayCounter,
                schedule2,
                Index2,
                Index2.DayCounter,
                Context,
                false,
                false,
                1.0,
                spread1,
                null,
                null,
                1.0,
                spread2,
                null,
                null);

            var pricer = new BlackIborCouponPricer(Context);
            swap = CouponPricerFunctions.SetCouponPricer(swap, 0, pricer);
            return CouponPricerFunctions.SetCouponPricer(swap, 1, pricer);
        }

        public FloatFloatSwapResults Price(FloatFloatSwap swap) =>
            DiscountingSwapEngine.Calculate(swap, Context, TermStructure);
    }
}
