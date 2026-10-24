// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/cms.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testCmsMarketCalibration, testSabrCubeRecalibrationInitializesLazily (S-cms's CmsMarket, CmsMarketCalibration and the cube's recalibration members)

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class CmsTests
{
    // Adapted: setPricer on the coupon (lines 287 and 291) returns a copy holding the pricer; setSwaptionVolatility
    // (lines 286 and 290) returns a copy of the pricer; CommonVars' relinkable handle is the curve itself, built first;
    // the evaluation date it sets is the context the coupon's rate reads.
    [Fact]
    public void TestFairRate()
    {
        // Testing Hagan-pricer flat-vol equivalence for coupons (lognormal case)...

        var vars = new CommonVars();

        var swapIndex = new SwapIndex(
            "EuriborSwapIsdaFixA",
            10 * TimeUnit.Years,
            vars.IborIndex.FixingDays,
            vars.IborIndex.Currency,
            vars.IborIndex.FixingCalendar,
            1 * TimeUnit.Years,
            BusinessDayConvention.Unadjusted,
            vars.IborIndex.DayCounter, // ??
            vars.IborIndex);

        // FIXME
        // ext::shared_ptr<SwapIndex> swapIndex(new
        //    EuriborSwapIsdaFixA(10*Years, vars.iborIndex->termStructure()));
        Date startDate = vars.TermStructure.ReferenceDate + (20 * TimeUnit.Years);
        Date paymentDate = startDate + (1 * TimeUnit.Years);
        Date endDate = paymentDate;
        Real nominal = 1.0;
        Rate? infiniteCap = null;
        Rate? infiniteFloor = null;
        Real gearing = 1.0;
        Spread spread = 0.0;
        var coupon = new CappedFlooredCmsCoupon(
            paymentDate,
            nominal,
            startDate,
            endDate,
            swapIndex.FixingDays,
            swapIndex,
            gearing,
            spread,
            infiniteCap,
            infiniteFloor,
            startDate,
            endDate,
            vars.IborIndex.DayCounter);
        for (int j = 0; j < vars.YieldCurveModels.Length; ++j)
        {
            CmsCouponPricer numerical = vars.NumericalPricers[j].WithSwaptionVolatility(vars.AtmVol);
            Rate rate0 = coupon.SetPricer(numerical).Rate(vars.Context);

            CmsCouponPricer analytic = vars.AnalyticPricers[j].WithSwaptionVolatility(vars.AtmVol);
            Rate rate1 = coupon.SetPricer(analytic).Rate(vars.Context);

            Spread difference = Math.Abs(rate1 - rate0);
            Spread tol = 2.0e-4;
            bool linearTsr = j == vars.YieldCurveModels.Length - 1;

            if (difference > tol)
            {
                Assert.Fail($"\nCoupon payment date: {paymentDate}"
                            + $"\nCoupon start date:   {startDate}"
                            + $"\nCoupon gearing:      {gearing}"
                            + $"\nCoupon swap index:   {swapIndex.Name}"
                            + $"\nCoupon spread:       {spread}"
                            + $"\nYieldCurve Model:    {vars.YieldCurveModels[j]}"
                            + $"\nNumerical Pricer:    {rate0}" + (linearTsr ? " (Linear TSR Model)" : string.Empty)
                            + $"\nAnalytic Pricer:     {rate1}"
                            + $"\ndifference:          {difference}"
                            + $"\ntolerance:           {tol}");
            }
        }
    }

    // Adapted: as TestFairRate; MakeCms's conversion (line 341) is ToSwap(context); setCouponPricer on the swap's first
    // leg (lines 351 and 353) returns a copy of the swap; and NPV() (lines 352 and 354) is the static
    // DiscountingSwapEngine.Calculate on the swap index's forwarding curve, the engine MakeCms attaches.
    [Fact]
    public void TestCmsSwap()
    {
        // Testing Hagan-pricer flat-vol equivalence for swaps (lognormal case)...

        var vars = new CommonVars();

        var swapIndex = new SwapIndex(
            "EuriborSwapIsdaFixA",
            10 * TimeUnit.Years,
            vars.IborIndex.FixingDays,
            vars.IborIndex.Currency,
            vars.IborIndex.FixingCalendar,
            1 * TimeUnit.Years,
            BusinessDayConvention.Unadjusted,
            vars.IborIndex.DayCounter, // ??
            vars.IborIndex);

        // FIXME
        // ext::shared_ptr<SwapIndex> swapIndex(new
        //    EuriborSwapIsdaFixA(10*Years, vars.iborIndex->termStructure()));
        Spread spread = 0.0;
        int[] swapLengths = [1, 5, 6, 10];
        int n = swapLengths.Length;
        var cms = new Swap[n];
        for (int i = 0; i < n; ++i)
        {
            // no cap, floor
            // no gearing, spread
            cms[i] = new MakeCms(new Period(swapLengths[i], TimeUnit.Years), swapIndex, vars.IborIndex)
                .WithIborSpread(spread)
                .WithForwardStart(10 * TimeUnit.Days)
                .ToSwap(vars.Context);
        }

        Real Npv(Swap swap) =>
            DiscountingSwapEngine.Calculate(swap, vars.Context, swapIndex.ForwardingTermStructure!).NPV!.Value;

        for (int j = 0; j < vars.YieldCurveModels.Length; ++j)
        {
            CmsCouponPricer numerical = vars.NumericalPricers[j].WithSwaptionVolatility(vars.AtmVol);
            CmsCouponPricer analytic = vars.AnalyticPricers[j].WithSwaptionVolatility(vars.AtmVol);
            for (int sl = 0; sl < n; ++sl)
            {
                Real priceNum = Npv(CouponPricerFunctions.SetCouponPricer(cms[sl], 0, numerical));
                Real priceAn = Npv(CouponPricerFunctions.SetCouponPricer(cms[sl], 0, analytic));

                Real difference = Math.Abs(priceNum - priceAn);
                Real tol = 2.0e-4;
                bool linearTsr = j == vars.YieldCurveModels.Length - 1;
                if (difference > tol)
                {
                    Assert.Fail($"\nLength in Years:  {swapLengths[sl]}"
                                + $"\nswap index:       {swapIndex.Name}"
                                + $"\nibor index:       {vars.IborIndex.Name}"
                                + $"\nspread:           {spread}"
                                + $"\nYieldCurve Model: {vars.YieldCurveModels[j]}"
                                + $"\nNumerical Pricer: {priceNum}" + (linearTsr ? " (Linear TSR Model)" : string.Empty)
                                + $"\nAnalytic Pricer:  {priceAn}"
                                + $"\ndifference:       {difference}"
                                + $"\ntolerance:        {tol}");
                }
            }
        }
    }

    // Adapted: as TestFairRate; price(termStructure) (lines 436-439) is FloatingRateCoupon.Price on the curve and the
    // context.
    [Fact]
    public void TestParity()
    {
        // Testing put-call parity for capped-floored CMS coupons (lognormal case)...

        var vars = new CommonVars();

        SwaptionVolatilityStructure[] swaptionVols = [vars.AtmVol, vars.SabrVolCube1, vars.SabrVolCube2];

        SwapIndex swapIndex = new EuriborSwapIsdaFixA(10 * TimeUnit.Years, vars.IborIndex.ForwardingTermStructure);
        Date startDate = vars.TermStructure.ReferenceDate + (20 * TimeUnit.Years);
        Date paymentDate = startDate + (1 * TimeUnit.Years);
        Date endDate = paymentDate;
        Real nominal = 1.0;
        Rate? infiniteCap = null;
        Rate? infiniteFloor = null;
        Real gearing = 1.0;
        Spread spread = 0.0;
        DiscountFactor discount = vars.TermStructure.Discount(paymentDate);
        var swaplet = new CappedFlooredCmsCoupon(
            paymentDate,
            nominal,
            startDate,
            endDate,
            swapIndex.FixingDays,
            swapIndex,
            gearing,
            spread,
            infiniteCap,
            infiniteFloor,
            startDate,
            endDate,
            vars.IborIndex.DayCounter);
        var failures = new List<string>();
        for (Rate strike = .02; strike < .12; strike += 0.05)
        {
            var caplet = new CappedFlooredCmsCoupon(
                paymentDate,
                nominal,
                startDate,
                endDate,
                swapIndex.FixingDays,
                swapIndex,
                gearing,
                spread,
                strike,
                infiniteFloor,
                startDate,
                endDate,
                vars.IborIndex.DayCounter);
            var floorlet = new CappedFlooredCmsCoupon(
                paymentDate,
                nominal,
                startDate,
                endDate,
                swapIndex.FixingDays,
                swapIndex,
                gearing,
                spread,
                infiniteCap,
                strike,
                startDate,
                endDate,
                vars.IborIndex.DayCounter);

            foreach (SwaptionVolatilityStructure swaptionVol in swaptionVols)
            {
                for (int j = 0; j < vars.YieldCurveModels.Length; ++j)
                {
                    CmsCouponPricer[] pricers =
                    [
                        vars.NumericalPricers[j].WithSwaptionVolatility(swaptionVol),
                        vars.AnalyticPricers[j].WithSwaptionVolatility(swaptionVol),
                    ];
                    for (int k = 0; k < pricers.Length; ++k)
                    {
                        Real swapletPrice = swaplet.SetPricer(pricers[k]).Price(vars.TermStructure, vars.Context)
                            + (nominal * swaplet.AccrualPeriod() * strike * discount);
                        Real capletPrice = caplet.SetPricer(pricers[k]).Price(vars.TermStructure, vars.Context);
                        Real floorletPrice = floorlet.SetPricer(pricers[k]).Price(vars.TermStructure, vars.Context);
                        Real difference = Math.Abs(capletPrice + floorletPrice - swapletPrice);
                        Real tol = 2.0e-5;
                        bool linearTsr = k == 0 && j == vars.YieldCurveModels.Length - 1;
                        if (linearTsr)
                        {
                            tol = 1.0e-7;
                        }

                        if (difference > tol)
                        {
                            failures.Add($"\nCoupon payment date: {paymentDate}"
                                         + $"\nCoupon start date:   {startDate}"
                                         + $"\nCoupon gearing:      {gearing}"
                                         + $"\nCoupon swap index:   {swapIndex.Name}"
                                         + $"\nCoupon spread:       {spread}"
                                         + $"\nstrike:              {strike}"
                                         + $"\nYieldCurve Model:    {vars.YieldCurveModels[j]}"
                                         + (k == 0 ? "\nNumerical Pricer" : "\nAnalytic Pricer")
                                         + (linearTsr ? " (Linear TSR Model)" : string.Empty)
                                         + $"\nSwaplet price:       {swapletPrice}"
                                         + $"\nCaplet price:        {capletPrice}"
                                         + $"\nFloorlet price:      {floorletPrice}"
                                         + $"\ndifference:          {difference}"
                                         + $"\ntolerance:           {tol}");
                        }
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    private sealed class CommonVars
    {
        // setup
        public CommonVars()
        {
            Calendar calendar = new TARGET();

            Date referenceDate = calendar.Adjust(Date.TodaysDate());
            Context = new EvaluationContext { EvaluationDate = referenceDate };

            TermStructure = new FlatForward(referenceDate, 0.05, new Actual365Fixed());

            // ATM Volatility structure
            Period[] atmOptionTenors =
            [
                1 * TimeUnit.Months, 6 * TimeUnit.Months, 1 * TimeUnit.Years, 5 * TimeUnit.Years, 10 * TimeUnit.Years,
                30 * TimeUnit.Years,
            ];

            Period[] atmSwapTenors = [1 * TimeUnit.Years, 5 * TimeUnit.Years, 10 * TimeUnit.Years, 30 * TimeUnit.Years];

            var m = new Matrix(new[,]
            {
                { 0.1300, 0.1560, 0.1390, 0.1220 },
                { 0.1440, 0.1580, 0.1460, 0.1260 },
                { 0.1600, 0.1590, 0.1470, 0.1290 },
                { 0.1640, 0.1470, 0.1370, 0.1220 },
                { 0.1400, 0.1300, 0.1250, 0.1100 },
                { 0.1130, 0.1090, 0.1070, 0.0930 },
            });

            AtmVol = SwaptionVolatilityMatrix.Create(
                calendar, BusinessDayConvention.Following, atmOptionTenors, atmSwapTenors, m, new Actual365Fixed(), Context);

            // Vol cubes
            Period[] optionTenors = [1 * TimeUnit.Years, 10 * TimeUnit.Years, 30 * TimeUnit.Years];
            Period[] swapTenors = [2 * TimeUnit.Years, 10 * TimeUnit.Years, 30 * TimeUnit.Years];
            Spread[] strikeSpreads = [-0.020, -0.005, 0.000, 0.005, 0.020];

            Real[][] volSpreads =
            [
                [0.0599, 0.0049, 0.0000, -0.0001, 0.0127],
                [0.0729, 0.0086, 0.0000, -0.0024, 0.0098],
                [0.0738, 0.0102, 0.0000, -0.0039, 0.0065],
                [0.0465, 0.0063, 0.0000, -0.0032, -0.0010],
                [0.0558, 0.0084, 0.0000, -0.0050, -0.0057],
                [0.0576, 0.0083, 0.0000, -0.0043, -0.0014],
                [0.0437, 0.0059, 0.0000, -0.0030, -0.0006],
                [0.0533, 0.0078, 0.0000, -0.0045, -0.0046],
                [0.0545, 0.0079, 0.0000, -0.0042, -0.0020],
            ];

            IborIndex = new Euribor6M(TermStructure);
            SwapIndex swapIndexBase = new EuriborSwapIsdaFixA(10 * TimeUnit.Years, TermStructure);
            SwapIndex shortSwapIndexBase = new EuriborSwapIsdaFixA(2 * TimeUnit.Years, TermStructure);

            bool vegaWeightedSmileFit = false;

            SabrVolCube2 = new InterpolatedSwaptionVolatilityCube(
                AtmVol,
                optionTenors,
                swapTenors,
                strikeSpreads,
                volSpreads,
                swapIndexBase,
                shortSwapIndexBase,
                vegaWeightedSmileFit,
                Context).EnableExtrapolation();

            Real[][] guess = [.. Enumerable.Range(0, optionTenors.Length * swapTenors.Length).Select(_ => new[] { 0.2, 0.5, 0.4, 0.0 })];
            bool[] isParameterFixed = [false, true, false, false];

            // FIXME
            bool isAtmCalibrated = false;

            SabrVolCube1 = new SabrSwaptionVolatilityCube(
                AtmVol,
                optionTenors,
                swapTenors,
                strikeSpreads,
                volSpreads,
                swapIndexBase,
                shortSwapIndexBase,
                vegaWeightedSmileFit,
                guess,
                isParameterFixed,
                isAtmCalibrated,
                Context).EnableExtrapolation();

            YieldCurveModels =
            [
                GFunctionFactoryYieldCurveModel.Standard,
                GFunctionFactoryYieldCurveModel.ExactYield,
                GFunctionFactoryYieldCurveModel.ParallelShifts,
                GFunctionFactoryYieldCurveModel.NonParallelShifts,
                GFunctionFactoryYieldCurveModel.NonParallelShifts,
            ];

            Real zeroMeanRev = 0.0;

            var numericalPricers = new List<CmsCouponPricer>();
            var analyticPricers = new List<CmsCouponPricer>();
            for (int j = 0; j < YieldCurveModels.Length; ++j)
            {
                if (j < YieldCurveModels.Length - 1)
                {
                    numericalPricers.Add(new NumericHaganPricer(AtmVol, YieldCurveModels[j], zeroMeanRev));
                }
                else
                {
                    numericalPricers.Add(new LinearTsrPricer(AtmVol, zeroMeanRev));
                }

                analyticPricers.Add(new AnalyticHaganPricer(AtmVol, YieldCurveModels[j], zeroMeanRev));
            }

            NumericalPricers = [.. numericalPricers];
            AnalyticPricers = [.. analyticPricers];
        }

        // global data
        public EvaluationContext Context { get; }

        public YieldTermStructure TermStructure { get; }

        public IborIndex IborIndex { get; }

        public SwaptionVolatilityStructure AtmVol { get; }

        public SwaptionVolatilityStructure SabrVolCube1 { get; }

        public SwaptionVolatilityStructure SabrVolCube2 { get; }

        public GFunctionFactoryYieldCurveModel[] YieldCurveModels { get; }

        public ImmutableArray<CmsCouponPricer> NumericalPricers { get; }

        public ImmutableArray<CmsCouponPricer> AnalyticPricers { get; }
    }
}
