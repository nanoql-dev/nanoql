// SPDX-FileCopyrightText: Copyright (C) 2023 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/equitytotalreturnswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

// Adapted throughout: QuantLib's fixture links relinkable handles that the indexes hold; here the indexes are built on
// the fixture's curves and quote, and the fixture's evaluation date and fixings are its context. The swap's engine,
// QuantLib's attached DiscountingSwapEngine, is called on it, and the leg inspectors read its results.
[Trait("Oracle", "QuantLib")]
public class EquityTotalReturnSwapTests
{
    [Fact]
    public void TestFairMargin()
    {
        // Testing fair margin...

        // Check TRS vs Libor-type index
        CheckFairMarginCalculation(SwapType.Receiver, new Date(5, January, 2023), new Date(5, April, 2023), false);
        CheckFairMarginCalculation(SwapType.Payer, new Date(5, January, 2023), new Date(5, April, 2023), false, 0.01);
        CheckFairMarginCalculation(SwapType.Payer, new Date(5, January, 2023), new Date(5, April, 2023), false, 0.0, 0.0);
        CheckFairMarginCalculation(
            SwapType.Receiver, new Date(31, January, 2023), new Date(30, April, 2023), false, -0.005, 1.0, 2);

        // Check TRS vs overnight index
        CheckFairMarginCalculation(SwapType.Receiver, new Date(5, January, 2023), new Date(5, April, 2023), true);
        CheckFairMarginCalculation(SwapType.Payer, new Date(5, January, 2023), new Date(5, April, 2023), true, 0.01);
        CheckFairMarginCalculation(
            SwapType.Receiver, new Date(31, January, 2023), new Date(30, April, 2023), true, -0.005, 1.0, 2);
    }

    [Fact]
    public void TestErrorWhenNegativeNominal()
    {
        // Testing error when negative nominal...
        var vars = new CommonVars();
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => vars.CreateTRS(SwapType.Receiver, new Date(5, January, 2023), new Date(5, April, 2023), false, 0.0, -1.0e7));
        Assert.Equal("nominal", e.ParamName);
        Assert.StartsWith("Nominal cannot be negative", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TestErrorWhenNoPaymentCalendar()
    {
        // Testing error when payment calendar is missing...
        var vars = new CommonVars();
        var sch = new Schedule(
            new Date(5, January, 2023), new Date(5, April, 2023), new Period(3, TimeUnit.Months), null,
            BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted, DateGenerationRule.Backward, false,
            vars.Context);
        NanoQLException e = Assert.Throws<NanoQLException>(() => vars.CreateTRS(SwapType.Receiver, sch, false));
        Assert.Equal("Calendar in schedule cannot be empty", e.Message);
    }

    [Fact]
    public void TestEquityLegNPV()
    {
        // Testing equity leg NPV replication...
        var vars = new CommonVars();
        const Real tolerance = 1.0e-8;
        var start = new Date(5, January, 2023);
        var end = new Date(5, April, 2023);
        EquityTotalReturnSwap trs = vars.CreateTRS(SwapType.Receiver, start, end, false);
        Real actualEquityLegNPV = EquityTotalReturnSwap.EquityLegNPV(vars.Price(trs))!.Value;
        EquityIndex eqIdx = trs.EquityIndex;
        Real discount = vars.InterestHandle.Discount(end);
        Real expectedEquityLegNPV =
            ((eqIdx.Fixing(end, vars.Context) / eqIdx.Fixing(start, vars.Context)) - 1.0) * trs.Nominal * discount;
        Assert.False(
            Math.Abs(actualEquityLegNPV - expectedEquityLegNPV) > tolerance,
            $"unable to replicate equity leg NPV\n    actual NPV:    {actualEquityLegNPV}\n"
            + $"    expected NPV:    {expectedEquityLegNPV}\n");
    }

    [Fact]
    public void TestTRSNPV()
    {
        // Testing TRS NPV...

        // Check TRS vs Libor-type index
        CheckNPVCalculation(SwapType.Receiver, new Date(5, January, 2023), new Date(5, April, 2023), false);
        CheckNPVCalculation(SwapType.Payer, new Date(5, January, 2023), new Date(5, April, 2023), false, 0.01);
        CheckNPVCalculation(SwapType.Payer, new Date(5, January, 2023), new Date(5, April, 2023), false, 0.0, 0.0);
        CheckNPVCalculation(SwapType.Receiver, new Date(31, January, 2023), new Date(30, April, 2023), false, -0.005, 1.0, 2);

        //// Check TRS vs overnight index
        CheckNPVCalculation(SwapType.Receiver, new Date(5, January, 2023), new Date(5, April, 2023), true);
        CheckNPVCalculation(SwapType.Payer, new Date(5, January, 2023), new Date(5, April, 2023), true, 0.01);
        CheckNPVCalculation(SwapType.Receiver, new Date(31, January, 2023), new Date(30, April, 2023), true, -0.005, 1.0, 2);
    }

    private static void CheckFairMarginCalculation(
        SwapType type, Date start, Date end, bool useOvernightIndex, Rate margin = 0.0, Real gearing = 1.0, int paymentDelay = 0)
    {
        var vars = new CommonVars();
        const Real tolerance = 1.0e-8;
        const Real nominal = 1.0e7;
        EquityTotalReturnSwap trs = vars.CreateTRS(type, start, end, useOvernightIndex, margin, nominal, gearing, paymentDelay);
        Real fairMargin = trs.FairMargin(vars.Price(trs));
        EquityTotalReturnSwap parTrs = vars.CreateTRS(
            type, start, end, useOvernightIndex, fairMargin, nominal, gearing, paymentDelay);
        Real parNpv = vars.Price(parTrs).NPV!.Value;
        Assert.False(
            Math.Abs(parNpv) > tolerance,
            $"unable to imply a fair margin\n    actual NPV:    {parNpv}\n    expected NPV:    0.0 \n"
            + $"    fair margin:    {fairMargin}\n    IR index name:    {trs.InterestRateIndex.Name}\n");
    }

    private static Real LegNPV(ImmutableArray<CashFlow> leg, YieldTermStructure ts, EvaluationContext context)
    {
        Real npv = 0.0;
        foreach (CashFlow cf in leg)
        {
            npv += cf.Amount(context) * ts.Discount(cf.Date);
        }

        return npv;
    }

    private static void CheckNPVCalculation(
        SwapType type, Date start, Date end, bool useOvernightIndex, Rate margin = 0.0, Real gearing = 1.0, int paymentDelay = 0)
    {
        var vars = new CommonVars();
        const Real tolerance = 1.0e-2;
        const Real nominal = 1.0e7;
        EquityTotalReturnSwap trs = vars.CreateTRS(type, start, end, useOvernightIndex, margin, nominal, gearing, paymentDelay);
        SwapResults results = vars.Price(trs);
        Real npv = results.NPV!.Value;
        Real scaling = type == SwapType.Receiver ? 1.0 : -1.0;
        Real equityLegNPV = EquityTotalReturnSwap.EquityLegNPV(results)!.Value;
        Real replicatedEquityLegNPV = scaling * LegNPV(trs.EquityLeg, vars.InterestHandle, vars.Context);
        Assert.False(
            Math.Abs(equityLegNPV - replicatedEquityLegNPV) > tolerance,
            $"incorrect NPV of the equity leg\n    actual NPV:    {equityLegNPV}\n    expected NPV:    {replicatedEquityLegNPV}\n");

        Real interestLegNPV = EquityTotalReturnSwap.InterestRateLegNPV(results)!.Value;
        Real replicatedInterestLegNPV = -scaling * LegNPV(trs.InterestRateLeg, vars.InterestHandle, vars.Context);
        Assert.False(
            Math.Abs(interestLegNPV - replicatedInterestLegNPV) > tolerance,
            $"incorrect NPV of the interest leg\n    actual NPV:    {interestLegNPV}\n    expected NPV:    {replicatedInterestLegNPV}\n");

        Assert.False(
            Math.Abs(npv - (equityLegNPV + interestLegNPV)) > tolerance,
            $"summing legs NPV does not replicate the instrument NPV\n    actual NPV:    {npv}\n"
            + $"    NPV from summing legs:    {equityLegNPV + interestLegNPV}\n");
    }

    private static YieldTermStructure FlatRate(Real forward, DayCounter dc, EvaluationContext context) =>
        FlatForward.Create(0, new NullCalendar(), forward, dc, context);

    private sealed class CommonVars
    {
        public CommonVars()
        {
            Calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
            DayCount = new Actual365Fixed();
            Today = Calendar.Adjust(new Date(27, January, 2023));
            var context = new EvaluationContext { EvaluationDate = Today };

            InterestHandle = FlatRate(0.0375, DayCount, context);
            YieldTermStructure dividendHandle = FlatRate(0.005, DayCount, context);
            EquityIndex = new EquityIndex("eqIndex", Calendar, new USDCurrency(), InterestHandle, dividendHandle, 8700.0);
            Sofr = new Sofr(InterestHandle);
            UsdLibor = new USDLibor(new Period(3, TimeUnit.Months), InterestHandle);

            IndexFixings fixings = context.Fixings
                .AddFixing(EquityIndex, new Date(5, January, 2023), 9010.0)
                .AddFixing(EquityIndex, Today, 8690.0)
                .AddFixing(Sofr, new Date(3, January, 2023), 0.03)
                .AddFixing(Sofr, new Date(4, January, 2023), 0.031)
                .AddFixing(Sofr, new Date(5, January, 2023), 0.031)
                .AddFixing(Sofr, new Date(6, January, 2023), 0.031)
                .AddFixing(Sofr, new Date(9, January, 2023), 0.032)
                .AddFixing(Sofr, new Date(10, January, 2023), 0.033)
                .AddFixing(Sofr, new Date(11, January, 2023), 0.033)
                .AddFixing(Sofr, new Date(12, January, 2023), 0.033)
                .AddFixing(Sofr, new Date(13, January, 2023), 0.033)
                .AddFixing(Sofr, new Date(17, January, 2023), 0.033)
                .AddFixing(Sofr, new Date(18, January, 2023), 0.034)
                .AddFixing(Sofr, new Date(19, January, 2023), 0.034)
                .AddFixing(Sofr, new Date(20, January, 2023), 0.034)
                .AddFixing(Sofr, new Date(23, January, 2023), 0.034)
                .AddFixing(Sofr, new Date(24, January, 2023), 0.034)
                .AddFixing(Sofr, new Date(25, January, 2023), 0.034)
                .AddFixing(Sofr, new Date(26, January, 2023), 0.034)
                .AddFixing(UsdLibor, new Date(3, January, 2023), 0.035);
            Context = context with { Fixings = fixings };
        }

        public Date Today { get; }

        public Calendar Calendar { get; }

        public DayCounter DayCount { get; }

        public EquityIndex EquityIndex { get; }

        public IborIndex UsdLibor { get; }

        public OvernightIndex Sofr { get; }

        public YieldTermStructure InterestHandle { get; }

        public EvaluationContext Context { get; }

        // QuantLib's discountEngine, attached to each swap
        public SwapResults Price(EquityTotalReturnSwap swap) => DiscountingSwapEngine.Calculate(swap, Context, InterestHandle);

        public EquityTotalReturnSwap CreateTRS(
            SwapType type,
            Schedule schedule,
            bool useOvernightIndex,
            Rate margin = 0.0,
            Real nominal = 1.0e7,
            Real gearing = 1.0,
            int paymentDelay = 0) =>
            useOvernightIndex
                ? new EquityTotalReturnSwap(
                    type, nominal, schedule, EquityIndex, Sofr, DayCount, margin, gearing, schedule.Calendar,
                    BusinessDayConvention.Following, paymentDelay)
                : new EquityTotalReturnSwap(
                    type, nominal, schedule, EquityIndex, UsdLibor, DayCount, margin, Context, gearing, schedule.Calendar,
                    BusinessDayConvention.Following, paymentDelay);

        public EquityTotalReturnSwap CreateTRS(
            SwapType type,
            Date start,
            Date end,
            bool useOvernightIndex,
            Rate margin = 0.0,
            Real nominal = 1.0e7,
            Real gearing = 1.0,
            int paymentDelay = 0)
        {
            Schedule schedule = new MakeSchedule()
                .From(start)
                .To(end)
                .WithTenor(new Period(3, TimeUnit.Months))
                .WithCalendar(Calendar)
                .WithConvention(BusinessDayConvention.Following)
                .Backwards()
                .ToSchedule();
            return CreateTRS(type, schedule, useOvernightIndex, margin, nominal, gearing, paymentDelay);
        }
    }
}
