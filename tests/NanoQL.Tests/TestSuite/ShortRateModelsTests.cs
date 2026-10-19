// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/shortratemodels.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testSwaps, which waits for B3.4's TreeVanillaSwapEngine; testExtendedCoxIngersollRossUpdatesWhenTermStructureRelinks and testExtendedCoxIngersollRossDiscountFactor, which wait for B3.1's ExtendedCoxIngersollRoss
// Replaced: testHullWhiteUpdatesR0WhenTermStructureRelinks (Conventions/ModelRebuildTests.cs)

using NanoQL.Indexes;
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
