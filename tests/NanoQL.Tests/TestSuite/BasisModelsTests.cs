// SPDX-FileCopyrightText: Copyright (C) 2018 Sebastian Schlenkrich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/basismodels.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

/// <summary>
/// QuantLib's tenor basis model cases: the swaption cash flows against the swap's leg NPVs, and the caplet and swaption
/// volatility transformations. QuantLib's cases read the global evaluation date, which QuantLib's test runner sets to
/// 16 September 2015 (playbook section 9.2); so do these, in their context.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BasisModelsTests
{
    // auxiliary data
    private static readonly Period[] Terms =
    [
        new(0, TimeUnit.Days), new(1, TimeUnit.Years), new(2, TimeUnit.Years), new(3, TimeUnit.Years),
        new(5, TimeUnit.Years), new(7, TimeUnit.Years), new(10, TimeUnit.Years), new(15, TimeUnit.Years),
        new(20, TimeUnit.Years), new(61, TimeUnit.Years), // avoid extrapolation issues with 30y caplets
    ];

    private static readonly double[] DiscRates =
    [
        -0.00147407, -0.001761684, -0.001736745, -0.00119244, 0.000896055,
        0.003537077, 0.007213824, 0.011391278, 0.013334611, 0.013982809,
    ];

    private static readonly double[] Proj3mRates =
    [
        -0.000483439, -0.000578569, -0.000383832, 0.000272656, 0.002478699,
        0.005100113, 0.008750643, 0.012788095, 0.014534052, 0.014942896,
    ];

    private static readonly double[] Proj6mRates =
    [
        0.000233608, 0.000218862, 0.000504018, 0.001240556, 0.003554415,
        0.006153921, 0.009688264, 0.013521628, 0.015136391, 0.015377704,
    ];

    private static readonly Period[] CapletTerms =
    [
        new(1, TimeUnit.Years), new(2, TimeUnit.Years), new(3, TimeUnit.Years), new(5, TimeUnit.Years),
        new(7, TimeUnit.Years), new(10, TimeUnit.Years), new(15, TimeUnit.Years), new(20, TimeUnit.Years),
        new(25, TimeUnit.Years), new(30, TimeUnit.Years),
    ];

    private static readonly double[] CapletStrikes = [-0.0050, 0.0000, 0.0050, 0.0100, 0.0150, 0.0200, 0.0300, 0.0500];

    private static readonly Period[] SwaptionVTSTerms =
    [
        new(1, TimeUnit.Years), new(5, TimeUnit.Years), new(10, TimeUnit.Years), new(20, TimeUnit.Years),
        new(30, TimeUnit.Years),
    ];

    [Fact]
    public void TestSwaptioncfsContCompSpread()
    {
        // Testing deterministic tenor basis model with continuous compounded spreads...
        TestSwaptioncfs(true);
    }

    [Fact]
    public void TestSwaptioncfsSimpleCompSpread()
    {
        // Testing deterministic tenor basis model with simple compounded spreads...
        TestSwaptioncfs(false);
    }

    [Fact]
    public void TestTenoroptionletvts()
    {
        // Testing volatility transformation for caplets/floorlets...
        EvaluationContext context = RunnerContext;

        // market data and floating rate index
        const double Spread = 0.01;
        YieldTermStructure proj3mYTS = GetYTS(Terms, Proj3mRates, context);
        YieldTermStructure proj6mYTS = GetYTS(Terms, Proj3mRates, context, Spread);
        IborIndex euribor3m = new Euribor6M(proj3mYTS);
        IborIndex euribor6m = new Euribor6M(proj6mYTS);

        // 3m optionlet VTS
        OptionletVolatilityStructure optionletVTS3m = GetOptionletTS(context);
        {
            // we need a correlation structure
            TenorOptionletVTSCorrelationStructure corr = Correlation(0.3, 0.9);

            // now we can set up the new volTS and calculate volatilities
            OptionletVolatilityStructure optionletVTS6m = new TenorOptionletVTS(
                optionletVTS3m, euribor3m, euribor6m, corr, context);
            foreach (Period capletTerm in CapletTerms)
            {
                foreach (double capletStrike in CapletStrikes)
                {
                    double vol3m = optionletVTS3m.Volatility(capletTerm, capletStrike, true);
                    double vol6m = optionletVTS6m.Volatility(capletTerm, capletStrike, true);
                    double vol6mShifted = optionletVTS6m.Volatility(capletTerm, capletStrike + Spread, true);

                    // De-correlation yields that larger tenor shifted vols are smaller then shorter
                    // tenor vols; we leave 1bp tolerance due to simplified spread calculation
                    Assert.True(
                        vol6mShifted - vol3m <= 0.0001,
                        $"Shifted 6m vol significantly larger then 3m vol at expiry term: {capletTerm}, strike: "
                        + $"{capletStrike}; vol3m: {vol3m}, vol6m: {vol6m}, vol6mShifted: {vol6mShifted}");
                }
            }
        }

        {
            // we need a correlation structure
            TenorOptionletVTSCorrelationStructure corr = Correlation(0.0, 0.0);

            // now we can set up the new volTS and calculate volatilities
            OptionletVolatilityStructure optionletVTS6m = new TenorOptionletVTS(
                optionletVTS3m, euribor3m, euribor6m, corr, context);
            for (int i = 0; i < CapletTerms.Length; ++i)
            {
                foreach (double capletStrike in CapletStrikes)
                {
                    double vol3m = optionletVTS3m.Volatility(CapletTerms[i], capletStrike, true);
                    double vol6m = optionletVTS6m.Volatility(CapletTerms[i], capletStrike, true);
                    double vol6mShifted = optionletVTS6m.Volatility(CapletTerms[i], capletStrike + Spread, true);

                    // for perfect correlation shifted 6m vols should coincide with 3m vols
                    double tol = i < 3 ? 0.001 : 0.0001; // 10bp tol for smaller tenors and 1bp tol for larger tenors
                    Assert.True(
                        Math.Abs(vol6mShifted - vol3m) <= tol,
                        $"Shifted 6m vol does not match 3m vol for perfect correlation at expiry term: {CapletTerms[i]}, "
                        + $"strike: {capletStrike}; vol3m: {vol3m}, vol6m: {vol6m}, vol6mShifted: {vol6mShifted}");
                }
            }
        }
    }

    [Fact]
    public void TestTenorswaptionvts()
    {
        // Testing volatility transformation for swaptions...
        EvaluationContext context = RunnerContext;

        // market data and floating rate index
        const double Spread = 0.01;
        YieldTermStructure discYTS = GetYTS(Terms, DiscRates, context);
        YieldTermStructure proj3mYTS = GetYTS(Terms, Proj3mRates, context);
        YieldTermStructure proj6mYTS = GetYTS(Terms, Proj3mRates, context, Spread);
        IborIndex euribor3m = new Euribor6M(proj3mYTS);
        IborIndex euribor6m = new Euribor6M(proj6mYTS);

        // Euribor6m ATM vols
        SwaptionVolatilityStructure euribor6mSwVTS = GetSwaptionVTS(context);
        var bondBasis = new Thirty360(Thirty360Convention.BondBasis);
        {
            var euribor3mSwVTS = new TenorSwaptionVTS(
                euribor6mSwVTS, discYTS, euribor6m, euribor3m, new Period(1, TimeUnit.Years),
                new Period(1, TimeUnit.Years), bondBasis, bondBasis, context);

            // 6m vols should be slightly larger then 3m vols due to basis
            foreach (Period i in SwaptionVTSTerms)
            {
                foreach (Period j in SwaptionVTSTerms)
                {
                    double vol6m = euribor6mSwVTS.Volatility(i, j, 0.01, true);
                    double vol3m = euribor3mSwVTS.Volatility(i, j, 0.01, true);
                    Assert.False(
                        vol3m > vol6m,
                        $"Euribor 6m must be larger than 3m vol at expiry term: {i}, swap term: {j}; vol3m: {vol3m}, "
                        + $"vol6m: {vol6m}");
                }
            }
        }

        {
            var euribor6mSwVTS2 = new TenorSwaptionVTS(
                euribor6mSwVTS, discYTS, euribor6m, euribor6m, new Period(1, TimeUnit.Years),
                new Period(1, TimeUnit.Years), bondBasis, bondBasis, context);

            // 6m vols to 6m vols should yield initiial vols
            foreach (Period i in SwaptionVTSTerms)
            {
                foreach (Period j in SwaptionVTSTerms)
                {
                    double vol6m = euribor6mSwVTS.Volatility(i, j, 0.01, true);
                    double vol6m2 = euribor6mSwVTS2.Volatility(i, j, 0.01, true);
                    const double Tol = 1.0e-8;
                    Assert.True(
                        Math.Abs(vol6m2 - vol6m) <= Tol,
                        $"Euribor 6m to 6m vols should not change at expiry term: {i}, swap term: {j}; vol6m: {vol6m}, "
                        + $"vol6m2: {vol6m2}, variance: {vol6m2 - vol6m}");
                }
            }
        }
    }

    // The evaluation date QuantLib's test runner sets globally (playbook section 9.2).
    private static readonly EvaluationContext RunnerContext = new() { EvaluationDate = new Date(16, Month.September, 2015) };

    internal static YieldTermStructure GetYTS(
        Period[] terms, double[] rates, EvaluationContext context, double spread = 0.0)
    {
        Date today = context.EvaluationDate;
        var dates = new List<Date>(terms.Length);
        foreach (Period term in terms)
        {
            dates.Add(new NullCalendar().Advance(today, term, BusinessDayConvention.Unadjusted));
        }

        double[] ratesPlusSpread = [.. rates.Select(k => k + spread)];
        return new InterpolatedZeroCurve<Cubic>(dates, ratesPlusSpread, new Actual365Fixed(), new NullCalendar());
    }

    internal static OptionletVolatilityStructure GetOptionletTS(EvaluationContext context)
    {
        Date today = context.EvaluationDate;
        var dates = new List<Date>(CapletTerms.Length);
        foreach (Period capletTerm in CapletTerms)
        {
            dates.Add(new TARGET().Advance(today, capletTerm, BusinessDayConvention.Following));
        }

        // set up vol data manually
        double[][] capletVols =
        [
            [0.003010094, 0.002628065, 0.00456118, 0.006731268, 0.008678572, 0.010570881, 0.014149552, 0.021000638],
            [0.004173715, 0.003727039, 0.004180263, 0.005726083, 0.006905876, 0.008263514, 0.010555395, 0.014976523],
            [0.005870143, 0.005334526, 0.005599775, 0.006633987, 0.007773317, 0.009036581, 0.011474391, 0.016277549],
            [0.007458597, 0.007207522, 0.007263995, 0.007308727, 0.007813586, 0.008274858, 0.009743988, 0.012555171],
            [0.007711531, 0.007608826, 0.007572816, 0.007684107, 0.007971932, 0.008283118, 0.009268828, 0.011574083],
            [0.007619605, 0.007639059, 0.007719825, 0.007823373, 0.00800813, 0.008113384, 0.008616374, 0.009785436],
            [0.007312199, 0.007352993, 0.007369116, 0.007468333, 0.007515657, 0.00767695, 0.008020447, 0.009072769],
            [0.006905851, 0.006966315, 0.007056413, 0.007116494, 0.007259661, 0.00733308, 0.007667563, 0.008419696],
            [0.006529553, 0.006630731, 0.006749022, 0.006858027, 0.007001959, 0.007139097, 0.007390404, 0.008036255],
            [0.006225482, 0.006404012, 0.00651594, 0.006642273, 0.006640887, 0.006885713, 0.007093024, 0.00767373],
        ];
        YieldTermStructure curve3m = GetYTS(Terms, Proj3mRates, context);
        IborIndex index = new Euribor3M(curve3m);
        var tmp1 = new StrippedOptionlet(
            2, new TARGET(), BusinessDayConvention.Following, index, dates, CapletStrikes, capletVols,
            new Actual365Fixed(), context, VolatilityType.Normal, 0.0);
        return StrippedOptionletAdapter.Create(tmp1, context);
    }

    internal static SwaptionVolatilityStructure GetSwaptionVTS(EvaluationContext context)
    {
        var swaptionVols = new Matrix(new double[,]
        {
            { 0.002616, 0.00468, 0.0056, 0.005852, 0.005823 },
            { 0.006213, 0.00643, 0.006622, 0.006124, 0.005958 },
            { 0.006658, 0.006723, 0.006602, 0.005802, 0.005464 },
            { 0.005728, 0.005814, 0.005663, 0.004689, 0.004276 },
            { 0.005041, 0.005059, 0.004746, 0.003927, 0.003608 },
        });
        return SwaptionVolatilityMatrix.Create(
            new TARGET(), BusinessDayConvention.Following, SwaptionVTSTerms, SwaptionVTSTerms, swaptionVols,
            new Actual365Fixed(), context, true, VolatilityType.Normal);
    }

    private static TenorOptionletVTSCorrelationStructure Correlation(double rhoInf, double beta)
    {
        double[] corrTimes = [0.0, 50.0];
        double[] rhoInfData = [rhoInf, rhoInf];
        double[] betaData = [beta, beta];
        Interpolation rho = default(Linear).Interpolate(corrTimes, rhoInfData);
        Interpolation b = default(Linear).Interpolate(corrTimes, betaData);
        return new TwoParameterCorrelation(rho, b);
    }

    private static void TestSwaptioncfs(bool contTenorSpread)
    {
        EvaluationContext context = RunnerContext;
        bool usingAtParCoupons = context.UseAtParCoupons;

        // market data and floating rate index
        YieldTermStructure discYTS = GetYTS(Terms, DiscRates, context);
        YieldTermStructure proj6mYTS = GetYTS(Terms, Proj6mRates, context);
        IborIndex euribor6m = new Euribor6M(proj6mYTS);

        // Vanilla swap details
        Date today = context.EvaluationDate;
        var target = new TARGET();
        Date swapStart = target.Advance(today, new Period(5, TimeUnit.Years), BusinessDayConvention.Following);
        Date swapEnd = target.Advance(swapStart, new Period(10, TimeUnit.Years), BusinessDayConvention.Following);
        Date exerciseDate = target.Advance(swapStart, new Period(-2, TimeUnit.Days), BusinessDayConvention.Preceding);
        var fixedSchedule = new Schedule(
            swapStart, swapEnd, new Period(1, TimeUnit.Years), target, BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Backward, false, context);
        var floatSchedule = new Schedule(
            swapStart, swapEnd, new Period(6, TimeUnit.Months), target, BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Backward, false, context);
        var swap = new VanillaSwap(
            SwapType.Payer, 10000.0, fixedSchedule, 0.03, new Thirty360(Thirty360Convention.BondBasis), floatSchedule,
            euribor6m, 0.0, euribor6m.DayCounter, context);
        FixedVsFloatingSwapResults swapResults = DiscountingSwapEngine.Calculate(swap, context, discYTS);

        // European exercise and swaption
        var europeanExercise = new EuropeanExercise(exerciseDate);
        var swaption = new Swaption(swap, europeanExercise, SettlementType.Physical);

        // calculate basis model swaption cash flows, discount and conmpare with swap
        var cashFlows = new SwaptionCashFlows(swaption, discYTS, context, contTenorSpread);

        // model time is always Act365Fixed
        double exerciseTime = new Actual365Fixed().YearFraction(discYTS.ReferenceDate, swaption.Exercise.Dates[0]);
        Assert.True(
            exerciseTime == cashFlows.ExerciseTimes[0],
            "Swaption cash flow exercise time does not coincide with manual calculation");

        // there might be rounding errors
        const double Tol = 1.0e-8;

        // (discounted) fixed leg coupons must match swap fixed leg NPV
        double fixedLeg = 0.0;
        for (int k = 0; k < cashFlows.FixedTimes.Length; ++k)
        {
            fixedLeg += cashFlows.FixedWeights[k] * discYTS.Discount(cashFlows.FixedTimes[k]);
        }

        double fixedLegNPV = swapResults.FixedLegNPV!.Value;
        Assert.True(
            Math.Abs(fixedLeg - (-fixedLegNPV)) <= Tol, // note, '-1' because payer swap
            $"Swaption cash flow fixed leg NPV does not match Vanillaswap fixed leg NPV SwaptionCashFlows: {fixedLeg}, "
            + $"swap->fixedLegNPV: {fixedLegNPV}, Variance: {fixedLegNPV - fixedLeg}");

        // (discounted) floating leg coupons must match swap floating leg NPV
        double floatLeg = 0.0;
        for (int k = 0; k < cashFlows.FloatTimes.Length; ++k)
        {
            floatLeg += cashFlows.FloatWeights[k] * discYTS.Discount(cashFlows.FloatTimes[k]);
        }

        double floatingLegNPV = swapResults.FloatingLegNPV!.Value;
        Assert.True(
            Math.Abs(floatLeg - floatingLegNPV) <= Tol,
            "Swaption cash flow floating leg NPV does not match Vanillaswap floating leg NPV. SwaptionCashFlows: "
            + $"{floatLeg}, swap->floatingLegNPV: {floatingLegNPV}, Variance: {floatingLegNPV - floatLeg}");

        // There should not be spread coupons in a single-curve setting.
        // However, if indexed coupons are used the floating leg is not at par,
        // so we need to relax the tolerance to a level at which it will only
        // catch large errors.
        double tol2 = usingAtParCoupons ? Tol : 0.02;
        var singleCurveCashFlows = new SwaptionCashFlows(swaption, proj6mYTS, context, contTenorSpread);
        for (int k = 1; k < singleCurveCashFlows.FloatWeights.Length - 1; ++k)
        {
            Assert.True(
                Math.Abs(singleCurveCashFlows.FloatWeights[k]) <= tol2,
                "Swaption cash flow floating leg spread does not vanish in single-curve setting. Cash flow index k: "
                + $"{k}, floatWeights: {singleCurveCashFlows.FloatWeights[k]}");
        }
    }
}
