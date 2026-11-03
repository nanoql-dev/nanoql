// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/shortratemodels.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Replaced: testHullWhiteUpdatesR0WhenTermStructureRelinks (Conventions/ModelRebuildTests.cs); testExtendedCoxIngersollRossUpdatesWhenTermStructureRelinks (Conventions/ModelRebuildTests.cs)

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class ShortRateModelsTests
{
    // QuantLib's test runner's evaluation date (quantlibglobalfixture.cpp:62-63)
    private static readonly Date RunnerDate = new(16, September, 2015);

    private static readonly (Integer Start, Integer Length, Volatility Volatility)[] CalibrationData =
    [
        (1, 5, 0.1148),
        (2, 4, 0.1108),
        (3, 3, 0.1070),
        (4, 2, 0.1021),
        (5, 1, 0.1000),
    ];

    // Adapted: the end criteria and the parameters are read from the calibration's result (lines 144 and 155), and the
    // values in the failure message from the calibrated model it holds (lines 156 and 160), which stays as calibrated.
    [Fact]
    public void TestCachedHullWhite()
    {
        // Testing Hull-White calibration against cached values using swaptions with start delay...

        var context = new EvaluationContext { EvaluationDate = new Date(15, February, 2002) };
        bool usingAtParCoupons = context.UseAtParCoupons;

        Date settlement = new(19, February, 2002);
        YieldTermStructure termStructure = new FlatForward(settlement, 0.04875825, new Actual365Fixed());
        var model = new HullWhite(termStructure);
        IborIndex index = new Euribor6M(termStructure);

        var swaptions = new List<CalibrationHelper>();
        foreach ((Integer start, Integer length, Volatility volatility) in CalibrationData)
        {
            SwaptionHelper helper = new SwaptionHelper(
                    new Period(start, TimeUnit.Years),
                    new Period(length, TimeUnit.Years),
                    volatility,
                    index,
                    new Period(1, TimeUnit.Years),
                    new Thirty360(Thirty360Convention.BondBasis),
                    new Actual360(),
                    termStructure,
                    context)
                .SetPricingEngine<HullWhite>((s, c, m) => JamshidianSwaptionEngine.Calculate(s, c, m));
            swaptions.Add(helper);
        }

        // Set up the optimization problem
        // Real simplexLambda = 0.1;
        // Simplex optimizationMethod(simplexLambda);
        var optimizationMethod = new LevenbergMarquardt(1.0e-8, 1.0e-8, 1.0e-8);
        var endCriteria = new EndCriteria(10000, 100, 1e-6, 1e-8, 1e-8);

        // Optimize
        CalibrationResults<HullWhite> calibration = model.Calibrate(swaptions, optimizationMethod, endCriteria, context);
        EndCriteriaType ecType = calibration.EndCriteria;

        // Check and print out results
        Real cachedA, cachedSigma;
        if (!usingAtParCoupons)
        {
            (cachedA, cachedSigma) = (0.0463679, 0.00579831);
        }
        else
        {
            (cachedA, cachedSigma) = (0.0464041, 0.00579912);
        }

        Real tolerance = 1.3e-5;
        CheckCachedCalibration(calibration.Model, swaptions, context, cachedA, cachedSigma, tolerance, ecType);
    }

    // Adapted: the end criteria and the parameters are read from the calibration's result (lines 218 and 229), and the
    // values in the failure message from the calibrated model it holds (lines 230 and 234), which stays as calibrated.
    [Fact]
    public void TestCachedHullWhiteFixedReversion()
    {
        // Testing Hull-White calibration with fixed reversion against cached values...

        var context = new EvaluationContext { EvaluationDate = new Date(15, February, 2002) };
        bool usingAtParCoupons = context.UseAtParCoupons;

        Date settlement = new(19, February, 2002);
        YieldTermStructure termStructure = new FlatForward(settlement, 0.04875825, new Actual365Fixed());
        var model = new HullWhite(termStructure, 0.05, 0.01);
        IborIndex index = new Euribor6M(termStructure);

        var swaptions = new List<CalibrationHelper>();
        foreach ((Integer start, Integer length, Volatility volatility) in CalibrationData)
        {
            SwaptionHelper helper = new SwaptionHelper(
                    new Period(start, TimeUnit.Years),
                    new Period(length, TimeUnit.Years),
                    volatility,
                    index,
                    new Period(1, TimeUnit.Years),
                    new Thirty360(Thirty360Convention.BondBasis),
                    new Actual360(),
                    termStructure,
                    context)
                .SetPricingEngine<HullWhite>((s, c, m) => JamshidianSwaptionEngine.Calculate(s, c, m));
            swaptions.Add(helper);
        }

        // Set up the optimization problem
        // Real simplexLambda = 0.1;
        // Simplex optimizationMethod(simplexLambda);
        var optimizationMethod = new LevenbergMarquardt(); // (1.0e-18,1.0e-18,1.0e-18);
        var endCriteria = new EndCriteria(1000, 500, 1E-8, 1E-8, 1E-8);

        // Optimize
        CalibrationResults<HullWhite> calibration = model.Calibrate(
            swaptions, optimizationMethod, endCriteria, context, null, [], HullWhite.FixedReversion().AsSpan());
        EndCriteriaType ecType = calibration.EndCriteria;

        // Check and print out results
        Real cachedA, cachedSigma;
        if (!usingAtParCoupons)
        {
            (cachedA, cachedSigma) = (0.05, 0.00585835);
        }
        else
        {
            (cachedA, cachedSigma) = (0.05, 0.00585858);
        }

        Real tolerance = 1.0e-5;
        CheckCachedCalibration(calibration.Model, swaptions, context, cachedA, cachedSigma, tolerance, ecType);
    }

    // Adapted: the end criteria and the parameters are read from the calibration's result (lines 295 and 308), and the
    // values in the failure message from the calibrated model it holds (lines 309 and 313), which stays as calibrated.
    [Fact]
    public void TestCachedHullWhite2()
    {
        // Testing Hull-White calibration against cached values using swaptions without start delay...

        var context = new EvaluationContext { EvaluationDate = new Date(15, February, 2002) };
        bool usingAtParCoupons = context.UseAtParCoupons;

        Date settlement = new(19, February, 2002);
        YieldTermStructure termStructure = new FlatForward(settlement, 0.04875825, new Actual365Fixed());
        var model = new HullWhite(termStructure);
        IborIndex index = new Euribor6M(termStructure);

        // Euribor 6m with zero fixing days
        var index0 = new IborIndex(
            index.FamilyName,
            index.Tenor,
            0,
            index.Currency,
            index.FixingCalendar,
            index.BusinessDayConvention,
            index.EndOfMonth,
            index.DayCounter,
            termStructure);

        var swaptions = new List<CalibrationHelper>();
        foreach ((Integer start, Integer length, Volatility volatility) in CalibrationData)
        {
            SwaptionHelper helper = new SwaptionHelper(
                    new Period(start, TimeUnit.Years),
                    new Period(length, TimeUnit.Years),
                    volatility,
                    index0,
                    new Period(1, TimeUnit.Years),
                    new Thirty360(Thirty360Convention.BondBasis),
                    new Actual360(),
                    termStructure,
                    context)
                .SetPricingEngine<HullWhite>((s, c, m) => JamshidianSwaptionEngine.Calculate(s, c, m));
            swaptions.Add(helper);
        }

        // Set up the optimization problem
        // Real simplexLambda = 0.1;
        // Simplex optimizationMethod(simplexLambda);
        var optimizationMethod = new LevenbergMarquardt(1.0e-8, 1.0e-8, 1.0e-8);
        var endCriteria = new EndCriteria(10000, 100, 1e-6, 1e-8, 1e-8);

        // Optimize
        CalibrationResults<HullWhite> calibration = model.Calibrate(swaptions, optimizationMethod, endCriteria, context);
        EndCriteriaType ecType = calibration.EndCriteria;

        // Check and print out results
        // The cached values were produced with an older version of the
        // JamshidianEngine not accounting for the delay between option
        // expiry and underlying start
        Real cachedA, cachedSigma;
        if (!usingAtParCoupons)
        {
            (cachedA, cachedSigma) = (0.0481608, 0.00582493);
        }
        else
        {
            (cachedA, cachedSigma) = (0.0482063, 0.00582687);
        }

        Real tolerance = 1.0e-5;
        CheckCachedCalibration(calibration.Model, swaptions, context, cachedA, cachedSigma, tolerance, ecType);
    }

    // Adapted: the evaluation date the case reads (line 335) is the test runner's, the context's; the fixing added to the
    // index (line 384) goes into the context's fixings, which every later swap reads, as QuantLib's index's history.
    [Fact]
    public void TestSwaps()
    {
        // Testing Hull-White swap pricing against known values...

        Date today = RunnerDate;
        Calendar calendar = new TARGET();
        today = calendar.Adjust(today);
        var context = new EvaluationContext { EvaluationDate = today };
        bool usingAtParCoupons = context.UseAtParCoupons;

        Date settlement = calendar.Advance(today, 2, TimeUnit.Days);

        Date[] dates =
        [
            settlement,
            calendar.Advance(settlement, 1, TimeUnit.Weeks),
            calendar.Advance(settlement, 1, TimeUnit.Months),
            calendar.Advance(settlement, 3, TimeUnit.Months),
            calendar.Advance(settlement, 6, TimeUnit.Months),
            calendar.Advance(settlement, 9, TimeUnit.Months),
            calendar.Advance(settlement, 1, TimeUnit.Years),
            calendar.Advance(settlement, 2, TimeUnit.Years),
            calendar.Advance(settlement, 3, TimeUnit.Years),
            calendar.Advance(settlement, 5, TimeUnit.Years),
            calendar.Advance(settlement, 10, TimeUnit.Years),
            calendar.Advance(settlement, 15, TimeUnit.Years),
        ];
        DiscountFactor[] discounts =
        [
            1.0,
            0.999258,
            0.996704,
            0.990809,
            0.981798,
            0.972570,
            0.963430,
            0.929532,
            0.889267,
            0.803693,
            0.596903,
            0.433022,
        ];

        YieldTermStructure termStructure = new DiscountCurve(dates, discounts, new Actual365Fixed());

        var model = new HullWhite(termStructure);

        Integer[] start = [-3, 0, 3];
        Integer[] length = [2, 5, 10];
        Rate[] rates = [0.02, 0.04, 0.06];
        IborIndex euribor = new Euribor6M(termStructure);

        Real tolerance = usingAtParCoupons ? 1.0e-8 : 4.0e-3;

        for (int i = 0; i < start.Length; i++)
        {
            Date startDate = calendar.Advance(settlement, start[i], TimeUnit.Months);
            if (startDate < today)
            {
                Date fixingDate = calendar.Advance(startDate, -2, TimeUnit.Days);
                context = context with { Fixings = context.Fixings.AddFixing(euribor, fixingDate, 0.03) };
            }

            for (int j = 0; j < length.Length; j++)
            {
                Date maturity = calendar.Advance(startDate, length[i], TimeUnit.Years);
                var fixedSchedule = new Schedule(
                    startDate, maturity, new Period(Frequency.Annual), calendar, BusinessDayConvention.Unadjusted,
                    BusinessDayConvention.Unadjusted, DateGenerationRule.Forward, false, context);
                var floatSchedule = new Schedule(
                    startDate, maturity, new Period(Frequency.Semiannual), calendar, BusinessDayConvention.Following,
                    BusinessDayConvention.Following, DateGenerationRule.Forward, false, context);
                foreach (Rate rate in rates)
                {
                    var swap = new VanillaSwap(
                        SwapType.Payer, 1000000.0, fixedSchedule, rate, new Thirty360(Thirty360Convention.BondBasis),
                        floatSchedule, euribor, 0.0, new Actual360(), context);
                    Real expected = DiscountingSwapEngine.Calculate(swap, context, termStructure).NPV!.Value;
                    Real calculated = TreeVanillaSwapEngine.Calculate(swap, context, model, 120).NPV!.Value;

                    Real error = Math.Abs((expected - calculated) / expected);
                    Assert.False(
                        error > tolerance,
                        $"Failed to reproduce swap NPV:\n    calculated: {calculated:F9}\n    expected:   {expected:F9}"
                        + $"\n    rel. error: {error:E}");
                }
            }
        }
    }

    [Fact]
    public void TestFuturesConvexityBias()
    {
        // Testing Hull-White futures convexity bias...

        // G. Kirikos, D. Novak, "Convexity Conundrums", Risk Magazine, March 1997
        Real futureQuote = 94.0;
        Real sigma = 0.015;
        double t = 5.0;
        Real tolerance = 0.0000001;

        foreach ((double T, Real a, Real expectedForward) in (ReadOnlySpan<(double, Real, Real)>)
                 [
                     (5.25, 0.03, 0.0573037),
                     (5.25, 1e-4, 0.0568627),
                     (5.25, 0.0, 0.0568611),
                     (5.001, 0.03, 0.0575736),
                     (5.0, 0.03, 0.0575747),
                 ])
        {
            Rate futureImpliedRate = (100.0 - futureQuote) / 100.0;
            Rate calculatedForward = futureImpliedRate - HullWhite.ConvexityBias(futureQuote, t, T, sigma, a);

            Real error = Math.Abs(calculatedForward - expectedForward);
            Assert.True(
                error < tolerance,
                $"Failed to reproduce convexity bias:\ncalculated: {calculatedForward}\n  expected: {expectedForward}"
                + $"\n     error: {error:E}\n tolerance: {tolerance:E}");
        }
    }

    // Adapted: the evaluation date the case reads (line 464) is the test runner's.
    [Fact]
    public void TestExtendedCoxIngersollRossDiscountFactor()
    {
        // Testing zero-bond pricing for extended CIR model...

        Date today = RunnerDate;

        const Rate rate = 0.1;
        YieldTermStructure rTS = new FlatForward(today, rate, new Actual365Fixed());

        const double now = 1.5;
        const double maturity = 2.5;

        var cirModel = new ExtendedCoxIngersollRoss(rTS, rate, 1.0, 1e-4, rate);

        Real expected = rTS.Discount(maturity) / rTS.Discount(now);
        Real calculated = cirModel.DiscountBond(now, maturity, rate);

        const Real tol = 1e-6;
        Real diff = Math.Abs(expected - calculated);

        Assert.False(
            diff > tol,
            $"Failed to reproduce zero bound price:\n  calculated: {calculated}\n  expected  : {expected}"
            + $"\n  difference: {diff:E}\n  tolerance : {tol:E}");
    }

    // Upstream: QuantLib's master's case for #2908.
    [Fact]
    public void TestCoxIngersollRossShortExpiryBondOptions()
    {
        // Testing short-expiry bond options for CIR model...

        // For short option expiries, the arguments of the non-central
        // chi-square distribution in the closed formula become very
        // large; it used to return 0, so that in-the-money calls were
        // priced at 0 and puts at minus their intrinsic value.
        var model = new CoxIngersollRoss(0.03, 0.04, 0.3, 0.1);
        const double bondMaturity = 1.0;

        // reference values computed with the closed formula and a
        // 40-digit evaluation of the non-central chi-square distribution
        (double Expiry, Real Strike, Real Call, Real Put)[] cases =
        [
            (1.0 / 52, 0.90, 0.0696854342991965, 0.0),
            (1.0 / 365, 0.97, 6.19091004229057e-5, 8.16319849046085e-4),
        ];

        const Real tolerance = 1e-12;
        foreach ((double expiry, Real strike, Real expectedCall, Real expectedPut) in cases)
        {
            Real call = model.DiscountBondOption(OptionType.Call, strike, expiry, bondMaturity);
            Real put = model.DiscountBondOption(OptionType.Put, strike, expiry, bondMaturity);

            Assert.False(
                Math.Abs(call - expectedCall) > tolerance || Math.Abs(put - expectedPut) > tolerance,
                "Failed to reproduce CIR bond option prices:"
                + $"\n  expiry    : {expiry}"
                + $"\n  strike    : {strike}"
                + $"\n  call      : {call:E}"
                + $"\n  expected  : {expectedCall:E}"
                + $"\n  put       : {put:E}"
                + $"\n  expected  : {expectedPut:E}"
                + $"\n  tolerance : {tolerance:E}");
        }
    }

    [Fact]
    public void TestVasicekDiscountFactorForSmallMeanReversion()
    {
        // Testing zero-bond pricing for Vasicek model with small mean reversion...

        const Rate r0 = 0.05;
        const Real a = 1e-12;
        const Real b = 0.05;
        const Volatility sigma = 0.01;
        const Real lambda = 0.0;
        const double now = 0.0;
        const double maturity = 1.0;

        var model = new Vasicek(r0, a, b, sigma, lambda);

        Real expected = Math.Exp((-r0 * maturity) + (sigma * sigma * maturity * maturity * maturity / 6.0));
        Real calculated = model.DiscountBond(now, maturity, r0);

        const Real tolerance = 1e-12;
        Real error = Math.Abs(expected - calculated);
        Assert.True(
            error <= tolerance,
            $"Failed to reproduce small-mean-reversion zero-bond price:\n  calculated: {calculated}\n  expected  : {expected}"
            + $"\n  error     : {error:E}\n  tolerance : {tolerance:E}");
    }

    // The three calibration cases' check and failure message, which each writes out in QuantLib.
    private static void CheckCachedCalibration(
        HullWhite model,
        List<CalibrationHelper> swaptions,
        EvaluationContext context,
        Real cachedA,
        Real cachedSigma,
        Real tolerance,
        EndCriteriaType ecType)
    {
        Vector xMinCalculated = model.Params();
        Real yMinCalculated = model.Value(xMinCalculated, swaptions, context);
        var xMinExpected = new Vector(2);
        xMinExpected[0] = cachedA;
        xMinExpected[1] = cachedSigma;
        Real yMinExpected = model.Value(xMinExpected, swaptions, context);
        Assert.False(
            Math.Abs(xMinCalculated[0] - cachedA) > tolerance || Math.Abs(xMinCalculated[1] - cachedSigma) > tolerance,
            "Failed to reproduce cached calibration results:\n"
            + $"calculated: a = {xMinCalculated[0]}, sigma = {xMinCalculated[1]}, f(a) = {yMinCalculated},\n"
            + $"expected:   a = {xMinExpected[0]}, sigma = {xMinExpected[1]}, f(a) = {yMinExpected},\n"
            + $"difference: a = {xMinCalculated[0] - xMinExpected[0]}, sigma = {xMinCalculated[1] - xMinExpected[1]}, "
            + $"f(a) = {yMinCalculated - yMinExpected},\n"
            + $"end criteria = {ecType}");
    }
}
