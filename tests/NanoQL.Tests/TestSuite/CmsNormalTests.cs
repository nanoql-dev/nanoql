// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2023 Andre Miemiec
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/cms_normal.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

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
public class CmsNormalTests
{
    // Adapted: setPricer on the coupon (lines 310 and 314) returns a copy holding the pricer; setSwaptionVolatility
    // (lines 309 and 313) returns a copy of the pricer; CommonVars' relinkable handle is the curve itself, built first;
    // the evaluation date it sets is the context the coupon's rate reads.
    [Fact]
    public void TestFairRate()
    {
        // Testing Hagan-pricer flat-vol equivalence for coupons (normal case)...

        var vars = new CommonVars();

        SwapIndex swapIndex = CommonVars.Cms10Y(vars);

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
            Spread tol = 2.0e-4; // The tolerance used before was 2bp. Semms very low for a coupon with pmt in 20 Years

            // seems a more appropriate comparison to me instead of  difference > tol
            if (Math.Round(10.0 * (difference - tol), MidpointRounding.AwayFromZero) / 10.0 > 0.0)
            {
                Assert.Fail($"\nCoupon payment date: {paymentDate}"
                            + $"\nCoupon start date:   {startDate}"
                            + $"\nCoupon gearing:      {gearing}"
                            + $"\nCoupon swap index:   {swapIndex.Name}"
                            + $"\nCoupon spread:       {spread}"
                            + $"\nYieldCurve Model:    {vars.YieldCurveModels[j]}"
                            + $"\nNumerical Pricer:    {rate0}"
                            + $"\nAnalytic Pricer:     {rate1}"
                            + $"\ndifference:          {difference}"
                            + $"\ntolerance:           {tol}");
            }
        }
    }

    // Adapted: as TestFairRate; MakeCms's conversion (line 363) is ToSwap(context); setCouponPricer on the swap's first
    // leg (lines 372 and 374) returns a copy of the swap; and NPV() (lines 373 and 375) is the static
    // DiscountingSwapEngine.Calculate on the swap index's forwarding curve, the engine MakeCms attaches.
    [Fact]
    public void TestCmsSwap()
    {
        // Testing Hagan-pricer flat-vol equivalence for swaps (normal case)...

        var vars = new CommonVars();

        SwapIndex swapIndex = CommonVars.Cms10Y(vars);

        // FIXME
        // ext::shared_ptr<SwapIndex> swapIndex(new
        //    EuriborSwapIsdaFixA(10*Years, vars.iborIndex->termStructure()));
        Spread spread = 0.0;
        int[] swapLengths = [1, 5, 6, 10]; // ?is an off-gridpoint a good test?
        int n = swapLengths.Length;
        var cms = new Swap[n];
        for (int i = 0; i < n; ++i)
        {
            // no cap, floor
            // no gearing, spread
            cms[i] = new MakeCms(new Period(swapLengths[i], TimeUnit.Years), swapIndex, vars.IborIndex)
                .WithIborSpread(spread)
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

                // seems a more appropriate comparison to me instead of  difference > tol
                if (Math.Round(10.0 * (difference - tol), MidpointRounding.AwayFromZero) / 10.0 > 0.0)
                {
                    Assert.Fail($"\nLength in Years:  {swapLengths[sl]}"
                                + $"\nswap index:       {swapIndex.Name}"
                                + $"\nibor index:       {vars.IborIndex.Name}"
                                + $"\nspread:           {spread}"
                                + $"\nYieldCurve Model: {vars.YieldCurveModels[j]}"
                                + $"\nNumerical Pricer: {priceNum}"
                                + $"\nAnalytic Pricer:  {priceAn}"
                                + $"\ndifference:       {difference}"
                                + $"\ntolerance:        {tol}");
                }
            }
        }
    }

    // Adapted: as TestFairRate; price(termStructure) (lines 468-470) is FloatingRateCoupon.Price on the curve and the
    // context.
    [Fact]
    public void TestParity()
    {
        // Testing put-call parity for capped-floored CMS coupons (normal case)...

        var vars = new CommonVars();

        SwaptionVolatilityStructure[] swaptionVols = [vars.AtmVol, vars.SabrVolCube1, vars.SabrVolCube2];

        SwapIndex swapIndex = CommonVars.Cms10Y(vars);

        Date startDate = vars.TermStructure.ReferenceDate + (20 * TimeUnit.Years);
        Date paymentDate = startDate + (1 * TimeUnit.Years);
        Date endDate = paymentDate;
        Real nominal = 1.0;
        Rate? infiniteCap = null;
        Rate? infiniteFloor = null;
        Real gearing = 1.0;
        Spread spread = 0.0;
        DiscountFactor discount = vars.TermStructure.Discount(paymentDate);
        var cpn_plain = new CappedFlooredCmsCoupon(
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
        for (Rate strike = -0.005; strike <= 0.035; strike += 0.01)
        {
            var cpn_capped = new CappedFlooredCmsCoupon(
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
            var cpn_floored = new CappedFlooredCmsCoupon(
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
                        Real cpn_plain_Price = cpn_plain.SetPricer(pricers[k]).Price(vars.TermStructure, vars.Context);
                        Real cpn_capped_Price = cpn_capped.SetPricer(pricers[k]).Price(vars.TermStructure, vars.Context);
                        Real cpn_floored_Price = cpn_floored.SetPricer(pricers[k]).Price(vars.TermStructure, vars.Context);
                        Real difference = Math.Abs(cpn_capped_Price + cpn_floored_Price - cpn_plain_Price
                                                   - (nominal * strike * cpn_plain.AccrualPeriod() * discount));
                        Real tol = 4.0e-5;

                        if (difference > tol)
                        {
                            failures.Add($"\nDiscount Factor:     {discount}"
                                         + $"\nCoupon payment date: {paymentDate}"
                                         + $"\nCoupon start date:   {startDate}"
                                         + $"\nCoupon gearing:      {gearing}"
                                         + $"\nCoupon swap index:   {swapIndex.Name}"
                                         + $"\nCoupon spread:       {spread}"
                                         + $"\nstrike:              {strike}"
                                         + $"\nYieldCurve Model:    {vars.YieldCurveModels[j]}"
                                         + $"\nPricerType:          {(k == 0 ? "Numerical Pricer" : "Analytic Pricer")}"
                                         + $"\nPlain Coupon price:       {cpn_plain_Price}"
                                         + $"\nCapped Coupon price:        {cpn_capped_Price}"
                                         + $"\nFloored Coupon price:      {cpn_floored_Price}"
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

            TermStructure = new FlatForward(referenceDate, 0.02, new Actual365Fixed());

            // ATM Volatility structure
            Period[] atmOptionTenors =
            [
                1 * TimeUnit.Months, 6 * TimeUnit.Months, 1 * TimeUnit.Years, 5 * TimeUnit.Years, 10 * TimeUnit.Years,
                30 * TimeUnit.Years,
            ];

            Period[] atmSwapTenors = [1 * TimeUnit.Years, 5 * TimeUnit.Years, 10 * TimeUnit.Years, 30 * TimeUnit.Years];

            var m = new Matrix(new[,]
            {
                { 0.0085, 0.0120, 0.0102, 0.0095 },
                { 0.0106, 0.0104, 0.0095, 0.0092 },
                { 0.0104, 0.0099, 0.0092, 0.0088 },
                { 0.0091, 0.0086, 0.0080, 0.0070 },
                { 0.0077, 0.0073, 0.0068, 0.0060 },
                { 0.0057, 0.0055, 0.0050, 0.0039 },
            });

            AtmVol = SwaptionVolatilityMatrix.Create(
                calendar,
                BusinessDayConvention.Following,
                atmOptionTenors,
                atmSwapTenors,
                m,
                new Actual365Fixed(),
                Context,
                false,
                VolatilityType.Normal);

            // Vol cubes
            Period[] optionTenors = [1 * TimeUnit.Years, 10 * TimeUnit.Years, 30 * TimeUnit.Years];
            Period[] swapTenors = [2 * TimeUnit.Years, 10 * TimeUnit.Years, 30 * TimeUnit.Years];
            Spread[] strikeSpreads = [-0.020, -0.005, 0.000, 0.005, 0.020];

            Real[][] volSpreads =
            [
                [-0.0016, -0.0008, 0.0000, 0.0009, 0.0038],
                [0.0009, -0.0003, 0.0000, 0.0007, 0.0035],
                [0.0025, 0.0002, 0.0000, 0.0002, 0.0024],
                [-0.0009, -0.0003, 0.0000, 0.0003, 0.0013],
                [-0.0001, -0.0001, 0.0000, 0.0001, 0.0007],
                [0.0003, 0.0000, 0.0000, 0.0001, 0.0005],
                [-0.0004, -0.0001, 0.0000, 0.0001, 0.0006],
                [-0.0001, 0.0000, 0.0000, 0.0000, 0.0002],
                [-0.0002, -0.0001, 0.0000, 0.0001, 0.0002],
            ];

            IborIndex = new Euribor6M(TermStructure);
            /*
              ext::shared_ptr<SwapIndex> swapIndexBase(new
                    EuriborSwapIsdaFixA(10*Years, termStructure,termStructure));
              ext::shared_ptr<SwapIndex> shortSwapIndexBase(new
                    EuriborSwapIsdaFixA(2*Years, termStructure,termStructure));
            */

            var swapIndexBase = new SwapIndex(
                "swapIndexBase",
                2 * TimeUnit.Years,
                IborIndex.FixingDays,
                IborIndex.Currency,
                IborIndex.FixingCalendar,
                1 * TimeUnit.Years,
                BusinessDayConvention.ModifiedFollowing,
                new Thirty360(Thirty360Convention.EurobondBasis), // EUR
                IborIndex,
                TermStructure);
            var shortSwapIndexBase = new SwapIndex(
                "shortSwapIndexBase",
                1 * TimeUnit.Years,
                IborIndex.FixingDays,
                IborIndex.Currency,
                IborIndex.FixingCalendar,
                1 * TimeUnit.Years,
                BusinessDayConvention.ModifiedFollowing,
                new Thirty360(Thirty360Convention.EurobondBasis), // EUR
                IborIndex,
                TermStructure);

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

            Real[][] guess = [.. Enumerable.Range(0, optionTenors.Length * swapTenors.Length).Select(_ => new[] { 0.01, 0.0, 0.3, 0.5 })];
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
            ];

            Real zeroMeanRev = 0.0;

            var numericalPricers = new List<CmsCouponPricer>();
            var analyticPricers = new List<CmsCouponPricer>();
            foreach (GFunctionFactoryYieldCurveModel yieldCurveModel in YieldCurveModels)
            {
                numericalPricers.Add(new NumericHaganPricer(AtmVol, yieldCurveModel, zeroMeanRev));

                analyticPricers.Add(new AnalyticHaganPricer(AtmVol, yieldCurveModel, zeroMeanRev));
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

        // The tests' CMS10Y index (lines 280-289, 343-352, 409-418), on the curve for both forwarding and discounting.
        public static SwapIndex Cms10Y(CommonVars vars) => new(
            "CMS10Y",
            10 * TimeUnit.Years,
            vars.IborIndex.FixingDays,
            vars.IborIndex.Currency,
            vars.IborIndex.FixingCalendar,
            1 * TimeUnit.Years,
            BusinessDayConvention.ModifiedFollowing,
            new Thirty360(Thirty360Convention.EurobondBasis), // EUR
            vars.IborIndex,
            vars.TermStructure);
    }
}
