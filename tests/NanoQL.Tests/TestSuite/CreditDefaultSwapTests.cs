// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/creditdefaultswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testCachedValue's integral-engine checks (lines 119-165), which wait for B6.2's IntegralCdsEngine

using NanoQL.Coupons;
using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

/// <summary>QuantLib's credit-default-swap tests, <c>test-suite/creditdefaultswap.cpp</c>.</summary>
[Trait("Oracle", "QuantLib")]
public class CreditDefaultSwapTests
{
    // Adapted: the swap is priced by calling the engine. The integral engine's checks wait (the Partial line).
    [Fact]
    public void TestCachedValue()
    {
        // Testing credit-default swap against cached values...

        // Initialize curves
        var context = new EvaluationContext { EvaluationDate = new Date(9, June, 2006) };
        Date today = context.EvaluationDate;
        Calendar calendar = new TARGET();

        FlatHazardRate probabilityCurve = FlatHazardRate.Create(0, calendar, 0.01234, new Actual360(), context);

        var discountCurve = new FlatForward(today, 0.06, new Actual360());

        // Build the schedule
        Date issueDate = calendar.Advance(today, -1, TimeUnit.Years);
        Date maturity = calendar.Advance(issueDate, 10, TimeUnit.Years);
        Frequency frequency = Frequency.Semiannual;
        BusinessDayConvention convention = BusinessDayConvention.ModifiedFollowing;

        var schedule = new Schedule(
            issueDate, maturity, new Period(frequency), calendar, convention, convention, DateGenerationRule.Forward, false, context);

        // Build the CDS
        double fixedRate = 0.0120;
        DayCounter dayCount = new Actual360();
        double notional = 10000.0;
        double recoveryRate = 0.4;

        var cds = new CreditDefaultSwap(ProtectionSide.Seller, notional, fixedRate, schedule, convention, dayCount, true, true);
        CreditDefaultSwapResults results =
            MidPointCdsEngine.Calculate(cds, context, probabilityCurve, recoveryRate, discountCurve);

        double npv = 295.0153398;
        double fairRate = 0.007517539081;

        double calculatedNpv = results.NPV!.Value;
        double calculatedFairRate = results.FairSpread!.Value;
        double tolerance = 1.0e-7;

        Assert.False(
            Math.Abs(calculatedNpv - npv) > tolerance,
            "Failed to reproduce NPV with mid-point engine\n"
            + $"    calculated NPV: {calculatedNpv:G10}\n    expected NPV:   {npv:G10}");

        Assert.False(
            Math.Abs(calculatedFairRate - fairRate) > tolerance,
            "Failed to reproduce fair rate with mid-point engine\n"
            + $"    calculated fair rate: {calculatedFairRate:G10}\n    expected fair rate:   {fairRate:G10}");
    }

    [Fact]
    public void TestCachedMarketValue()
    {
        // Testing credit-default swap against cached market values...

        var context = new EvaluationContext { EvaluationDate = new Date(9, June, 2006) };
        Date evalDate = context.EvaluationDate;
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);

        Date[] discountDates =
        [
            evalDate,
            calendar.Advance(evalDate, 1, TimeUnit.Weeks, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 1, TimeUnit.Months, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 2, TimeUnit.Months, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 3, TimeUnit.Months, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 6, TimeUnit.Months, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 12, TimeUnit.Months, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 2, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 3, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 4, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 5, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 6, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 7, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 8, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 9, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 10, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 15, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing),
        ];

        double[] dfs =
        [
            1.0,
            0.9990151375768731,
            0.99570502636871183,
            0.99118260474528685,
            0.98661167950906203,
            0.9732592953359388,
            0.94724424481038083,
            0.89844996737120875,
            0.85216647839921411,
            0.80775477692556874,
            0.76517289234200347,
            0.72401019553182933,
            0.68503909569219212,
            0.64797499814013748,
            0.61263171936255534,
            0.5791942350748791,
            0.43518868769953606,
        ];

        DayCounter curveDayCounter = new Actual360();

        var discountCurve = new DiscountCurve(discountDates, dfs, curveDayCounter);

        DayCounter dayCounter = new Thirty360(Thirty360Convention.BondBasis);
        Date[] dates =
        [
            evalDate,
            calendar.Advance(evalDate, 6, TimeUnit.Months, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 1, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 2, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 3, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 4, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 5, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 7, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing),
            calendar.Advance(evalDate, 10, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing),
        ];

        double[] defaultProbabilities = [0.0000, 0.0047, 0.0093, 0.0286, 0.0619, 0.0953, 0.1508, 0.2288, 0.3666];

        var hazardRates = new List<double> { 0.0 };
        for (int i = 1; i < dates.Length; ++i)
        {
            double t1 = dayCounter.YearFraction(dates[0], dates[i - 1]);
            double t2 = dayCounter.YearFraction(dates[0], dates[i]);
            double s1 = 1.0 - defaultProbabilities[i - 1];
            double s2 = 1.0 - defaultProbabilities[i];
            hazardRates.Add(Math.Log(s1 / s2) / (t2 - t1));
        }

        var piecewiseFlatHazardRate = new InterpolatedHazardRateCurve<BackwardFlat>(
            dates, hazardRates, new Thirty360(Thirty360Convention.BondBasis));

        // Testing credit default swap

        // Build the schedule
        var issueDate = new Date(20, March, 2006);
        var maturity = new Date(20, June, 2013);
        Frequency cdsFrequency = Frequency.Semiannual;
        BusinessDayConvention cdsConvention = BusinessDayConvention.ModifiedFollowing;

        var schedule = new Schedule(
            issueDate,
            maturity,
            new Period(cdsFrequency),
            calendar,
            cdsConvention,
            cdsConvention,
            DateGenerationRule.Forward,
            false,
            context);

        // Build the CDS
        double recoveryRate = 0.25;
        double fixedRate = 0.0224;
        DayCounter dayCount = new Actual360();
        double cdsNotional = 100.0;

        var cds = new CreditDefaultSwap(
            ProtectionSide.Seller, cdsNotional, fixedRate, schedule, cdsConvention, dayCount, true, true);
        CreditDefaultSwapResults results =
            MidPointCdsEngine.Calculate(cds, context, piecewiseFlatHazardRate, recoveryRate, discountCurve);

        double calculatedNpv = results.NPV!.Value;
        double calculatedFairRate = results.FairSpread!.Value;

        double npv = -1.364048777; // from Bloomberg we have 98.15598868 - 100.00;
        double fairRate = 0.0248429452; // from Bloomberg we have 0.0258378;

        double tolerance = 1e-9;

        Assert.False(
            Math.Abs(npv - calculatedNpv) > tolerance,
            "Failed to reproduce the npv for the given credit-default swap\n"
            + $"    computed NPV:  {calculatedNpv:G10}\n    Given NPV:     {npv:G10}");

        Assert.False(
            Math.Abs(fairRate - calculatedFairRate) > tolerance,
            "Failed to reproduce the fair rate for the given credit-default swap\n"
            + $"    computed fair rate:  {calculatedFairRate:G10}\n    Given fair rate:     {fairRate:G10}");
    }

    [Fact]
    public void TestImpliedHazardRate()
    {
        // Testing implied hazard-rate for credit-default swaps...

        // Initialize curves
        Calendar calendar = new TARGET();
        Date today = calendar.Adjust(Date.TodaysDate());
        var context = new EvaluationContext { EvaluationDate = today };

        double h1 = 0.30, h2 = 0.40;
        DayCounter dayCounter = new Actual365Fixed();

        Date[] dates = [today, today + new Period(5, TimeUnit.Years), today + new Period(10, TimeUnit.Years)];
        double[] hazardRates = [h1, h1, h2];

        var probabilityCurve = new InterpolatedHazardRateCurve<BackwardFlat>(dates, hazardRates, dayCounter);

        var discountCurve = new FlatForward(today, 0.03, new Actual360());

        Frequency frequency = Frequency.Semiannual;
        BusinessDayConvention convention = BusinessDayConvention.ModifiedFollowing;

        Date issueDate = calendar.Advance(today, -6, TimeUnit.Months);
        double fixedRate = 0.0120;
        DayCounter cdsDayCount = new Actual360();
        double notional = 10000.0;
        double recoveryRate = 0.4;

        double? latestRate = null;
        for (int n = 6; n <= 10; ++n)
        {
            Date maturity = calendar.Advance(issueDate, n, TimeUnit.Years);
            var schedule = new Schedule(
                issueDate, maturity, new Period(frequency), calendar, convention, convention, DateGenerationRule.Forward, false, context);

            var cds = new CreditDefaultSwap(
                ProtectionSide.Seller, notional, fixedRate, schedule, convention, cdsDayCount, true, true);

            double npv = MidPointCdsEngine.Calculate(cds, context, probabilityCurve, recoveryRate, discountCurve).NPV!.Value;
            double flatRate = cds.ImpliedHazardRate(npv, discountCurve, dayCounter, context, recoveryRate);

            Assert.False(
                flatRate < h1 || flatRate > h2,
                $"implied hazard rate outside expected range\n    maturity: {n} years\n    expected minimum: {h1}\n"
                + $"    expected maximum: {h2}\n    implied rate:     {flatRate}");

            Assert.False(
                n > 6 && flatRate < latestRate,
                $"implied hazard rate decreasing with swap maturity\n    maturity: {n} years\n"
                + $"    previous rate: {latestRate}\n    implied rate:  {flatRate}");

            latestRate = flatRate;

            var probability = new FlatHazardRate(today, flatRate, dayCounter);

            var cds2 = new CreditDefaultSwap(
                ProtectionSide.Seller, notional, fixedRate, schedule, convention, cdsDayCount, true, true);

            double npv2 = MidPointCdsEngine.Calculate(cds2, context, probability, recoveryRate, discountCurve).NPV!.Value;
            double tolerance = 1.0;
            Assert.False(
                Math.Abs(npv - npv2) > tolerance,
                $"failed to reproduce NPV with implied rate\n    expected:   {npv}\n    calculated: {npv2}");
        }
    }

    [Fact]
    public void TestFairSpread()
    {
        // Testing fair-spread calculation for credit-default swaps...

        // Initialize curves
        Calendar calendar = new TARGET();
        Date today = calendar.Adjust(Date.TodaysDate());
        var context = new EvaluationContext { EvaluationDate = today };

        FlatHazardRate probabilityCurve = FlatHazardRate.Create(0, calendar, 0.01234, new Actual360(), context);

        var discountCurve = new FlatForward(today, 0.06, new Actual360());

        // Build the schedule
        Date issueDate = calendar.Advance(today, -1, TimeUnit.Years);
        Date maturity = calendar.Advance(issueDate, 10, TimeUnit.Years);
        BusinessDayConvention convention = BusinessDayConvention.Following;

        Schedule schedule = new MakeSchedule()
            .From(issueDate)
            .To(maturity)
            .WithFrequency(Frequency.Quarterly)
            .WithCalendar(calendar)
            .WithTerminationDateConvention(convention)
            .WithRule(DateGenerationRule.TwentiethIMM)
            .ToSchedule();

        // Build the CDS
        double fixedRate = 0.001;
        DayCounter dayCount = new Actual360();
        double notional = 10000.0;
        double recoveryRate = 0.4;

        CreditDefaultSwapResults Engine(CreditDefaultSwap c) =>
            MidPointCdsEngine.Calculate(c, context, probabilityCurve, recoveryRate, discountCurve);

        var cds = new CreditDefaultSwap(ProtectionSide.Seller, notional, fixedRate, schedule, convention, dayCount, true, true);

        double fairRate = Engine(cds).FairSpread!.Value;

        var fairCds = new CreditDefaultSwap(ProtectionSide.Seller, notional, fairRate, schedule, convention, dayCount, true, true);

        double fairNPV = Engine(fairCds).NPV!.Value;
        double tolerance = 1e-9;

        Assert.False(
            Math.Abs(fairNPV) > tolerance,
            "Failed to reproduce null NPV with calculated fair spread\n"
            + $"    calculated spread: {fairRate}\n    calculated NPV:    {fairNPV}");
    }

    [Fact]
    public void TestFairUpfront()
    {
        // Testing fair-upfront calculation for credit-default swaps...

        // Initialize curves
        Calendar calendar = new TARGET();
        Date today = calendar.Adjust(Date.TodaysDate());
        var context = new EvaluationContext { EvaluationDate = today };

        FlatHazardRate probabilityCurve = FlatHazardRate.Create(0, calendar, 0.01234, new Actual360(), context);

        var discountCurve = new FlatForward(today, 0.06, new Actual360());

        // Build the schedule
        Date issueDate = today;
        Date maturity = calendar.Advance(issueDate, 10, TimeUnit.Years);
        BusinessDayConvention convention = BusinessDayConvention.Following;

        Schedule schedule = new MakeSchedule()
            .From(issueDate)
            .To(maturity)
            .WithFrequency(Frequency.Quarterly)
            .WithCalendar(calendar)
            .WithTerminationDateConvention(convention)
            .WithRule(DateGenerationRule.TwentiethIMM)
            .ToSchedule();

        // Build the CDS
        double fixedRate = 0.05;
        double upfront = 0.001;
        DayCounter dayCount = new Actual360();
        double notional = 10000.0;
        double recoveryRate = 0.4;

        CreditDefaultSwapResults Engine(CreditDefaultSwap c) =>
            MidPointCdsEngine.Calculate(c, context, probabilityCurve, recoveryRate, discountCurve, true);

        var cds = new CreditDefaultSwap(
            ProtectionSide.Seller, notional, upfront, fixedRate, schedule, convention, dayCount, true, true);

        double fairUpfront = Engine(cds).FairUpfront!.Value;

        var fairCds = new CreditDefaultSwap(
            ProtectionSide.Seller, notional, fairUpfront, fixedRate, schedule, convention, dayCount, true, true);

        double fairNPV = Engine(fairCds).NPV!.Value;
        double tolerance = 1e-9;

        Assert.False(
            Math.Abs(fairNPV) > tolerance,
            "Failed to reproduce null NPV with calculated fair upfront\n"
            + $"    calculated upfront: {fairUpfront}\n    calculated NPV:     {fairNPV}");

        // same with null upfront to begin with
        upfront = 0.0;
        var cds2 = new CreditDefaultSwap(
            ProtectionSide.Seller, notional, upfront, fixedRate, schedule, convention, dayCount, true, true);

        fairUpfront = Engine(cds2).FairUpfront!.Value;

        var fairCds2 = new CreditDefaultSwap(
            ProtectionSide.Seller, notional, fairUpfront, fixedRate, schedule, convention, dayCount, true, true);

        fairNPV = Engine(fairCds2).NPV!.Value;

        Assert.False(
            Math.Abs(fairNPV) > tolerance,
            "Failed to reproduce null NPV with calculated fair upfront\n"
            + $"    calculated upfront: {fairUpfront}\n    calculated NPV:     {fairNPV}");
    }

    // Adapted: the swaps are priced by calling the engine; the tolerance reads the context's at-par switch.
    [Fact]
    public void TestIsdaEngine()
    {
        // Testing ISDA engine calculations for credit-default swaps...

        var tradeDate = new Date(21, May, 2009);
        var context = new EvaluationContext { EvaluationDate = tradeDate };

        bool usingAtParCoupons = context.UseAtParCoupons;

        // build an ISDA compliant yield curve
        // data comes from Markit published rates
        var isdaRateHelpers = new List<BootstrapHelper<YieldTermStructure>>();
        int[] depTenors = [1, 2, 3, 6, 9, 12];
        double[] depQuotes = [0.003081, 0.005525, 0.007163, 0.012413, 0.014, 0.015488];

        for (int i = 0; i < depTenors.Length; i++)
        {
            isdaRateHelpers.Add(new DepositRateHelper(
                depQuotes[i],
                new Period(depTenors[i], TimeUnit.Months),
                2,
                new WeekendsOnly(),
                BusinessDayConvention.ModifiedFollowing,
                false,
                new Actual360(),
                context));
        }

        int[] swapTenors = [2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 15, 20, 25, 30];
        double[] swapQuotes =
        [
            0.011907,
            0.01699,
            0.021198,
            0.02444,
            0.026937,
            0.028967,
            0.030504,
            0.031719,
            0.03279,
            0.034535,
            0.036217,
            0.036981,
            0.037246,
            0.037605,
        ];

        var isdaIbor = new IborIndex(
            "IsdaIbor",
            new Period(3, TimeUnit.Months),
            2,
            new USDCurrency(),
            new WeekendsOnly(),
            BusinessDayConvention.ModifiedFollowing,
            false,
            new Actual360());
        for (int i = 0; i < swapTenors.Length; i++)
        {
            isdaRateHelpers.Add(new SwapRateHelper(
                swapQuotes[i],
                new Period(swapTenors[i], TimeUnit.Years),
                new WeekendsOnly(),
                Frequency.Semiannual,
                BusinessDayConvention.ModifiedFollowing,
                new Thirty360(Thirty360Convention.BondBasis),
                isdaIbor,
                context));
        }

        PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap> discountCurve =
            PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>.Create(
                0, new WeekendsOnly(), isdaRateHelpers, new Actual365Fixed(), context);

        Date[] termDates =
        [
            new(20, June, 2010),
            new(20, June, 2011),
            new(20, June, 2012),
            new(20, June, 2016),
            new(20, June, 2019),
        ];
        double[] spreads = [0.001, 0.1];
        double[] recoveries = [0.2, 0.4];

        double[] markitValues =
        [
            -97798.29358, // 0.001
            -97776.11889, // 0.001
            914971.5977, // 0.1
            894985.6298, // 0.1
            -186921.3594, // 0.001
            -186839.8148, // 0.001
            1646623.672, // 0.1
            1579803.626, // 0.1
            -274298.9203,
            -274122.4725,
            2279730.93,
            2147972.527,
            -592420.2297,
            -591571.2294,
            3993550.206,
            3545843.418,
            -797501.1422,
            -795915.9787,
            4702034.688,
            4042340.999,
        ];

        // When using indexes coupons, the risk-free curve is a bit off.
        // We might skip the tests altogether and rely on running them
        // with indexed coupons disabled, but leaving them can be useful anyway.
        double tolerance = usingAtParCoupons ? 1.0e-6 : 1.0e-3;

        int l = 0;

        foreach (Date termDate in termDates)
        {
            foreach (double spread in spreads)
            {
                foreach (double recovery in recoveries)
                {
                    CreditDefaultSwap quotedTrade =
                        new MakeCreditDefaultSwap(termDate, spread).WithNominal(10000000.0).ToCreditDefaultSwap(context);

                    double h = quotedTrade.ImpliedHazardRate(
                        0.0, discountCurve, new Actual365Fixed(), context, recovery, 1e-10, CreditDefaultSwapPricingModel.ISDA);

                    FlatHazardRate probabilityCurve = FlatHazardRate.Create(0, new WeekendsOnly(), h, new Actual365Fixed(), context);

                    CreditDefaultSwapResults Engine(CreditDefaultSwap c) => IsdaCdsEngine.Calculate(
                        c,
                        context,
                        probabilityCurve,
                        recovery,
                        discountCurve,
                        null,
                        IsdaCdsEngineNumericalFix.Taylor,
                        IsdaCdsEngineAccrualBias.HalfDayBias,
                        IsdaCdsEngineForwardsInCouponPeriod.Piecewise);

                    CreditDefaultSwap conventionalTrade =
                        new MakeCreditDefaultSwap(termDate, 0.01).WithNominal(10000000.0).ToCreditDefaultSwap(context);
                    double fairUpfront = Engine(conventionalTrade).FairUpfront!.Value;

                    CheckClose(conventionalTrade.Notional * fairUpfront, markitValues[l], tolerance);

                    // Now testing that with the calculated fair-upfront, both Buyer and Seller sides
                    // price close to zero
                    CreditDefaultSwap conventionalTradeBuy = new MakeCreditDefaultSwap(termDate, 0.01)
                        .WithNominal(10000000.0)
                        .WithUpfrontRate(fairUpfront)
                        .WithSide(ProtectionSide.Buyer)
                        .ToCreditDefaultSwap(context);

                    CheckSmall(Engine(conventionalTradeBuy).NPV!.Value, tolerance);

                    CreditDefaultSwap conventionalTradeSell = new MakeCreditDefaultSwap(termDate, 0.01)
                        .WithNominal(10000000.0)
                        .WithUpfrontRate(fairUpfront)
                        .WithSide(ProtectionSide.Seller)
                        .ToCreditDefaultSwap(context);

                    CheckSmall(Engine(conventionalTradeSell).NPV!.Value, tolerance);

                    l++;
                }
            }
        }
    }

    [Fact]
    public void TestAccrualRebateAmounts()
    {
        // Testing accrual rebate amounts on credit default swaps...

        // The accrual values are taken from various test results on the ISDA CDS model website
        // https://www.cdsmodel.com/cdsmodel/documentation.html.

        // Inputs
        double notional = 10000000;
        double spread = 0.0100;
        var maturity = new Date(20, June, 2014);

        // key is trade date and value is expected accrual
        (Date TradeDate, double Accrual)[] inputs =
        [
            (new Date(18, March, 2009), 24166.67),
            (new Date(19, March, 2009), 0.00),
            (new Date(20, March, 2009), 277.78),
            (new Date(23, March, 2009), 1111.11),
            (new Date(19, June, 2009), 25555.56),
            (new Date(20, June, 2009), 25833.33),
            (new Date(21, June, 2009), 0.00),
            (new Date(22, June, 2009), 277.78),
            (new Date(18, June, 2014), 25277.78),
            (new Date(19, June, 2014), 25555.56),
        ];

        foreach ((Date tradeDate, double accrual) in inputs)
        {
            var context = new EvaluationContext { EvaluationDate = tradeDate };
            CreditDefaultSwap cds = new MakeCreditDefaultSwap(maturity, spread).WithNominal(notional).ToCreditDefaultSwap(context);
            CheckSmall(accrual - cds.AccrualRebate!.Amount(context), 0.01);
        }
    }

    // Adapted: the swap is priced by calling the engine.
    [Fact]
    public void TestIsdaCalculatorReconcileSingleQuote()
    {
        // Testing ISDA engine calculations for a single credit-default swap record (reconciliation)...

        var tradeDate = new Date(26, July, 2021);
        var context = new EvaluationContext { EvaluationDate = tradeDate };

        PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap> discountCurve = EurIsdaCurve(context);

        var instrumentMaturity = new Date(20, June, 2026);
        double coupon = 0.01, conventionalSpread = 0.006713, recovery = 0.4;
        double nominal = 1e6, markitValue = -16070.7, expectedAccrual = 1000, tolerance = 1.0e-3;

        CreditDefaultSwap quotedTrade =
            new MakeCreditDefaultSwap(instrumentMaturity, conventionalSpread).WithNominal(nominal).ToCreditDefaultSwap(context);

        double h = quotedTrade.ImpliedHazardRate(
            0.0, discountCurve, new Actual365Fixed(), context, recovery, 1e-10, CreditDefaultSwapPricingModel.ISDA);

        FlatHazardRate probabilityCurve = FlatHazardRate.Create(0, new WeekendsOnly(), h, new Actual365Fixed(), context);

        CreditDefaultSwap conventionalTrade =
            new MakeCreditDefaultSwap(instrumentMaturity, coupon).WithNominal(nominal).ToCreditDefaultSwap(context);
        CreditDefaultSwapResults results = IsdaCdsEngine.Calculate(
            conventionalTrade,
            context,
            probabilityCurve,
            recovery,
            discountCurve,
            null,
            IsdaCdsEngineNumericalFix.Taylor,
            IsdaCdsEngineAccrualBias.HalfDayBias,
            IsdaCdsEngineForwardsInCouponPeriod.Piecewise);

        double npv = results.NPV!.Value;
        double calculatedUpfront = conventionalTrade.Notional * results.FairUpfront!.Value;
        double df = calculatedUpfront / npv; // to take into account of the discount to cash settlement
        double derivedAccrual = df * (npv - results.DefaultLegNPV!.Value - results.CouponLegNPV!.Value);

        double calculatedAccrual = conventionalTrade.AccrualRebate!.Amount(context);

        Date settlementDate = conventionalTrade.AccrualRebate.Date;

        CheckClose(npv, markitValue, tolerance);

        CheckClose(calculatedUpfront, df * markitValue, tolerance);

        CheckClose(derivedAccrual, expectedAccrual, tolerance);

        CheckClose(calculatedAccrual, expectedAccrual, tolerance);

        Assert.Equal(new WeekendsOnly().Advance(tradeDate, 3, TimeUnit.Days), settlementDate);
    }

    // Adapted: the swap is priced by calling the engine.
    [Fact]
    public void TestIsdaCalculatorReconcileSingleWithIssueDateInThePast()
    {
        // Testing ISDA engine calculations for a single credit-default swap with issue date in the past...

        var valueDate = new Date(26, July, 2021);
        var context = new EvaluationContext { EvaluationDate = valueDate };

        // this is not IMM date but the settlement date is in the past so the accrual rebate
        // should not be part of the NPV
        var tradeDate = new Date(20, July, 2019);

        PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap> discountCurve = EurIsdaCurve(context);

        var instrumentMaturity = new Date(20, June, 2026);
        double coupon = 0.01, conventionalSpread = 0.006713, recovery = 0.4;

        // because there is no accrual involved, the markit value is decreased as compared to the
        // previous test (old_markit_value - old_accrual or -16070.7 - 1000)
        double nominal = 1e6, markitValue = -17070.77, expectedAccrual = 0, tolerance = 1.0e-3;

        CreditDefaultSwap quotedTrade =
            new MakeCreditDefaultSwap(instrumentMaturity, conventionalSpread).WithNominal(nominal).ToCreditDefaultSwap(context);

        double h = quotedTrade.ImpliedHazardRate(
            0.0, discountCurve, new Actual365Fixed(), context, recovery, 1e-10, CreditDefaultSwapPricingModel.ISDA);

        FlatHazardRate probabilityCurve = FlatHazardRate.Create(0, new WeekendsOnly(), h, new Actual365Fixed(), context);

        CreditDefaultSwap conventionalTrade = new MakeCreditDefaultSwap(instrumentMaturity, coupon)
            .WithNominal(nominal)
            .WithTradeDate(tradeDate)
            .ToCreditDefaultSwap(context);
        CreditDefaultSwapResults results = IsdaCdsEngine.Calculate(
            conventionalTrade,
            context,
            probabilityCurve,
            recovery,
            discountCurve,
            null,
            IsdaCdsEngineNumericalFix.Taylor,
            IsdaCdsEngineAccrualBias.HalfDayBias,
            IsdaCdsEngineForwardsInCouponPeriod.Piecewise);

        double npv = results.NPV!.Value;
        double calculatedAccrual = npv - results.DefaultLegNPV!.Value - results.CouponLegNPV!.Value;

        CheckClose(npv, markitValue, tolerance);

        CheckClose(calculatedAccrual, expectedAccrual, tolerance);
    }

    [Fact]
    public void TestDefaultConventions()
    {
        // Testing default conventions for CDS factory class...

        var today = new Date(6, March, 2026); // a Friday

        var context = new EvaluationContext { EvaluationDate = today };

        CreditDefaultSwap cds = new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01).ToCreditDefaultSwap(context);

        Assert.Equal(0.01, cds.RunningSpread); // not actually a default

        Assert.Equal(1.0, cds.Notional);
        Assert.True(cds.Upfront.HasValue);
        Assert.Equal(0.0, cds.Upfront!.Value);

        Assert.Equal(today, cds.TradeDate);
        Assert.Equal(3, cds.CashSettlementDays);
        Assert.Equal(today + 5, cds.UpfrontPayment.Date); // 3 cash settlement days plus the weekend
        Assert.Equal(today, cds.ProtectionStartDate);
        Assert.Equal(
            CreditDefaultSwapFunctions.CdsMaturity(today, new Period(5, TimeUnit.Years), DateGenerationRule.CDS),
            cds.ProtectionEndDate);

        Assert.Equal(21, cds.Coupons.Length); // 5Y quarterly, modulo CDS conventions

        Assert.True(cds.SettlesAccrual);
        Assert.True(cds.PaysAtDefaultTime);
        Assert.True(cds.RebatesAccrual);

        var first = (Coupon)cds.Coupons[0];
        var last = (Coupon)cds.Coupons[^1];

        Assert.Equal("Actual/360", first.DayCounter.Name);
        Assert.Equal("Actual/360 (inc)", last.DayCounter.Name);

        Date termDate = CreditDefaultSwapFunctions.CdsMaturity(today, new Period(3, TimeUnit.Years), DateGenerationRule.CDS2015)!.Value;
        cds = new MakeCreditDefaultSwap(termDate, 0.01).ToCreditDefaultSwap(context);
        Assert.Equal(termDate, cds.ProtectionEndDate);

        termDate = CreditDefaultSwapFunctions.CdsMaturity(today - 4, new Period(10, TimeUnit.Years), DateGenerationRule.CDS2015)!.Value;
        var schedule = new Schedule(
            today - 4,
            termDate,
            new Period(3, TimeUnit.Months),
            new WeekendsOnly(),
            BusinessDayConvention.Following,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.CDS2015,
            false,
            context);
        cds = new MakeCreditDefaultSwap(schedule, 0.01).ToCreditDefaultSwap(context);
        Assert.Equal(schedule.Front(), cds.ProtectionStartDate);
        Assert.Equal(schedule.Back(), cds.ProtectionEndDate);

        // override the defaults

        cds = new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01)
            .WithNominal(10000.0)
            .WithUpfrontRate(0.02)
            .ToCreditDefaultSwap(context);

        Assert.Equal(10000.0, cds.Notional);
        first = (Coupon)cds.Coupons[0];
        Assert.Equal(10000.0, first.Nominal(context));

        Assert.True(cds.Upfront.HasValue);
        Assert.Equal(0.02, cds.Upfront!.Value);
        Assert.Equal(200.0, cds.UpfrontPayment.Amount(context));

        cds = new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01)
            .WithCashSettlementDays(2)
            .ToCreditDefaultSwap(context);
        Assert.Equal(2, cds.CashSettlementDays);
        Assert.Equal(today + 4, cds.UpfrontPayment.Date);

        cds = new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01)
            .WithCashSettlementDays(2)
            .WithUpfrontDate(today + 7)
            .ToCreditDefaultSwap(context);
        Assert.Equal(2, cds.CashSettlementDays);
        Assert.Equal(today + 7, cds.UpfrontPayment.Date);

        cds = new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01)
            .WithProtectionStart(today + 2)
            .ToCreditDefaultSwap(context);
        Assert.Equal(today + 2, cds.ProtectionStartDate);

        cds = new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01)
            .WithCouponTenor(new Period(6, TimeUnit.Months))
            .ToCreditDefaultSwap(context);
        Assert.Equal(11, cds.Coupons.Length); // 5Y semiannually, modulo CDS conventions

        cds = new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01)
            .WithTradeDate(today + 3)
            .ToCreditDefaultSwap(context);

        Assert.Equal(today + 3, cds.TradeDate);
        Assert.Equal(3, cds.CashSettlementDays);
        Assert.Equal(today + 6, cds.UpfrontPayment.Date);
        Assert.Equal(today + 3, cds.ProtectionStartDate);

        cds = new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01)
            .SettleAccrual(false)
            .ToCreditDefaultSwap(context);
        Assert.False(cds.SettlesAccrual);

        cds = new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01)
            .PayAtDefaultTime(false)
            .ToCreditDefaultSwap(context);
        Assert.False(cds.PaysAtDefaultTime);

        cds = new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01)
            .RebateAccrual(false)
            .ToCreditDefaultSwap(context);
        Assert.False(cds.RebatesAccrual);

        cds = new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01)
            .WithDayCounter(new Actual365Fixed())
            .ToCreditDefaultSwap(context);

        first = (Coupon)cds.Coupons[0];
        last = (Coupon)cds.Coupons[^1];

        Assert.Equal("Actual/365 (Fixed)", first.DayCounter.Name);
        Assert.Equal("Actual/360 (inc)", last.DayCounter.Name);

        cds = new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01)
            .WithLastPeriodDayCounter(new Actual365Fixed())
            .ToCreditDefaultSwap(context);

        first = (Coupon)cds.Coupons[0];
        last = (Coupon)cds.Coupons[^1];

        Assert.Equal("Actual/360", first.DayCounter.Name);
        Assert.Equal("Actual/365 (Fixed)", last.DayCounter.Name);
    }

    // The two reconciliation cases' curve: an ISDA-compliant EUR curve from Markit's published rates of 26 July 2021.
    private static PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap> EurIsdaCurve(EvaluationContext context)
    {
        // build an ISDA compliant yield curve
        // data comes from Markit published rates
        var isdaRateHelpers = new List<BootstrapHelper<YieldTermStructure>>();
        int[] depTenors = [1, 3, 6, 12];
        double[] depQuotes = [-0.0056, -0.005440, -0.005190, -0.004930];

        for (int i = 0; i < depTenors.Length; i++)
        {
            isdaRateHelpers.Add(new DepositRateHelper(
                depQuotes[i],
                new Period(depTenors[i], TimeUnit.Months),
                2,
                new WeekendsOnly(),
                BusinessDayConvention.ModifiedFollowing,
                false,
                new Actual360(),
                context));
        }

        int[] swapTenors = [2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 15, 20, 30];
        double[] swapQuotes =
        [
            -0.004820,
            -0.004420,
            -0.003990,
            -0.003520,
            -0.002970,
            -0.002370,
            -0.001760,
            -0.001140,
            -0.000540,
            0.000570,
            0.001880,
            0.002940,
            0.002820,
        ];

        var isdaIbor = new IborIndex(
            "IsdaIbor",
            new Period(6, TimeUnit.Months),
            2,
            new EURCurrency(),
            new WeekendsOnly(),
            BusinessDayConvention.ModifiedFollowing,
            false,
            new Actual360());
        for (int i = 0; i < swapTenors.Length; i++)
        {
            isdaRateHelpers.Add(new SwapRateHelper(
                swapQuotes[i],
                new Period(swapTenors[i], TimeUnit.Years),
                new WeekendsOnly(),
                Frequency.Annual,
                BusinessDayConvention.ModifiedFollowing,
                new Thirty360(Thirty360Convention.BondBasis),
                isdaIbor,
                context));
        }

        return PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>.Create(
            0, new WeekendsOnly(), isdaRateHelpers, new Actual365Fixed(), context);
    }

    // QL_CHECK_CLOSE, Boost's BOOST_CHECK_CLOSE: |a - b| within the tolerance, in percent, of both values.
    private static void CheckClose(double a, double b, double tolerancePercent)
    {
        double diff = Math.Abs(a - b);
        Assert.True(
            diff <= tolerancePercent / 100.0 * Math.Abs(a) && diff <= tolerancePercent / 100.0 * Math.Abs(b),
            $"difference between {a} and {b} exceeds {tolerancePercent}%");
    }

    // QL_CHECK_SMALL, Boost's BOOST_CHECK_SMALL: |value| within the tolerance.
    private static void CheckSmall(double value, double tolerance) =>
        Assert.True(Math.Abs(value) <= tolerance, $"{value} is not small, tolerance {tolerance}");
}
