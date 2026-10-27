// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2012 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/marketmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testOneStepForwardsAndOptionlets, testOneStepNormalForwardsAndOptionlets, testInverseFloater, testAllMultiStepProducts, testPeriodAdapter, testCallableSwapNaif, testCallableSwapLS, testCallableSwapAnderson, testGreeks, testPathwiseGreeks, testPathwiseVegas, testPathwiseMarketVegas, testStochVolForwardsAndOptionlets (S-mmp)
// Not ported: testAbcdVolatilityCompare (it checks AbcdFunction against the legacy LIBOR model's LmExtLinearExponentialVolModel, which isn't ported)

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class MarketModelTests
{
    [Fact]
    public void TestAbcdVolatilityIntegration()
    {
        // Testing Abcd-volatility integration...
        double a = -0.0597;
        double b = 0.1677;
        double c = 0.5403;
        double d = 0.1710;

        const int N = 10;
        const double precision = 1e-04;

        var instVol = new AbcdFunction(a, b, c, d);
        var si = new SegmentIntegral(20000);
        var errors = new List<string>();
        for (int i = 0; i < N; i++)
        {
            double t1 = 0.5 * (1 + i); // expiry of forward 1: after T1 AbcdVol = 0
            for (int k = 0; k < N - i; k++)
            {
                double t2 = 0.5 * (1 + k); // expiry of forward 2: after T2 AbcdVol = 0

                // Integration
                for (int j = 0; j < N; j++)
                {
                    double xMin = 0.5 * j;
                    for (int l = 0; l < N - j; l++)
                    {
                        double xMax = xMin + (0.5 * l);
                        var abcd2 = new AbcdSquared(a, b, c, d, t1, t2);
                        double numerical = si.Integrate(abcd2.Evaluate, xMin, xMax);
                        double analytical = instVol.Covariance(xMin, xMax, t1, t2);
                        if (Math.Abs(analytical - numerical) > precision)
                        {
                            errors.Add($"     T1={t1},T2={t2},\t\txMin={xMin},xMax={xMax},\t\tanalytical: {analytical},\tnumerical:   {numerical}");
                        }

                        if (t1 == t2)
                        {
                            double variance = instVol.Variance(xMin, xMax, t1);
                            if (Math.Abs(analytical - variance) > 1e-14)
                            {
                                errors.Add($"     T1={t1},T2={t2},\t\txMin={xMin},xMax={xMax},\t\tvariance: {variance},\tanalytical: {analytical}");
                            }
                        }
                    }
                }
            }
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void TestAbcdVolatilityFit()
    {
        // Testing Abcd-volatility fit...
        var vars = new CommonVars();
        double[] fixingTimes = vars.RateTimes[..^1];
        var instVol = new AbcdCalibration(fixingTimes, vars.BlackVols);
        double a0 = instVol.A;
        double b0 = instVol.B;
        double c0 = instVol.C;
        double d0 = instVol.D;
        double error0 = instVol.Error();

        instVol = instVol.Compute();

        EndCriteriaType ec = instVol.EndCriteria;
        double a1 = instVol.A;
        double b1 = instVol.B;
        double c1 = instVol.C;
        double d1 = instVol.D;
        double error1 = instVol.Error();

        Assert.False(
            error1 >= error0,
            $"Parameters:\na:     {a0} ---> {a1}\nb:     {b0} ---> {b1}\nc:     {c0} ---> {c1}\nd:     {d0} ---> {d1}\nerror: {error0} ---> {error1}");

        var abcd = new AbcdFunction(a1, b1, c1, d1);
        double[] k = [.. instVol.K(fixingTimes, vars.BlackVols)];
        const double tol = 3.0e-4;
        for (int i = 0; i < vars.BlackVols.Length; i++)
        {
            if (Math.Abs(k[i] - 1.0) > tol)
            {
                double modelVol = abcd.Volatility(0.0, vars.RateTimes[i], vars.RateTimes[i]);
                Assert.Fail(
                    $"\n EndCriteria = {ec}\n Fixing Time = {vars.RateTimes[i]}\n MktVol      = {vars.BlackVols[i]}\n ModVol      = {modelVol}\n k           = {k[i]}\n error       = {Math.Abs(k[i] - 1.0)}\n tol         = {tol}");
            }
        }
    }

    [Fact]
    public void TestDriftCalculator()
    {
        // Test full factor drift equivalence between compute() and computeReduced()

        // Testing drift calculation...
        var vars = new CommonVars();
        const double tolerance = 1.0e-16;
        int factors = vars.TodaysForwards.Length;
        double[] evolutionTimes = vars.RateTimes[..^1];
        var evolution = new EvolutionDescription(vars.RateTimes, evolutionTimes);
        ReadOnlySpan<double> rateTaus = evolution.RateTaus.AsSpan();
        int[] numeraires = [.. EvolutionDescriptionFunctions.MoneyMarketPlusMeasure(evolution, CommonVars.MeasureOffset)];
        int[] alive = [.. evolution.FirstAliveRate];
        int numberOfSteps = evolutionTimes.Length;
        double[] drifts = new double[numberOfSteps], driftsReduced = new double[numberOfSteps];
        string[] marketModels = ["ExponentialCorrelationFlatVolatility", "ExponentialCorrelationAbcdVolatility"];
        var errors = new List<string>();
        foreach (string k in marketModels)
        {
            // loop over market models
            MarketModel marketModel = vars.MakeMarketModel(evolution, factors, k);
            double[] displacements = [.. marketModel.Displacements];
            for (int j = 0; j < numberOfSteps; ++j)
            {
                // loop over steps
                Matrix pseudo = marketModel.PseudoRoot(j);
                int inf = Math.Max(0, alive[j]);
                for (int h = inf; h < numeraires.Length; ++h)
                {
                    // loop over numeraires
                    var driftcalculator = new LMMDriftCalculator(pseudo, displacements, rateTaus, numeraires[h], alive[j]);
                    driftcalculator.ComputePlain(vars.TodaysForwards, drifts);
                    driftcalculator.ComputeReduced(vars.TodaysForwards, driftsReduced);
                    for (int i = 0; i < drifts.Length; ++i)
                    {
                        double error = Math.Abs(driftsReduced[i] - drifts[i]);
                        if (error > tolerance)
                        {
                            errors.Add(
                                $"MarketModel: {k}, {DataFormattersFunctions.Ordinal(j + 1)} step, , {DataFormattersFunctions.Ordinal(h + 1)} numeraire, , {DataFormattersFunctions.Ordinal(i + 1)} drift, \ndrift        ={drifts[i]}\ndriftReduced ={driftsReduced[i]}\n       error ={error}\n   tolerance ={tolerance}");
                        }
                    }
                }
            }
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void TestIsInSubset()
    {
        // Performance test for isInSubset function (temporary)
        // Testing isInSubset function...
        const int dim = 100;
        double[] set = new double[dim], subset = new double[dim];
        for (int i = 0; i < dim; i++)
        {
            set[i] = i * 1.0;
        }

        for (int i = 0; i < dim; i++)
        {
            subset[i] = dim + (i * 1.0);
        }

        // QuantLib's case prints the result when its report is asked for, and asserts nothing.
        _ = UtilitiesFunctions.IsInSubset(set, subset);
    }

    [Fact]
    public void TestAbcdDegenerateCases()
    {
        // Testing abcd degenerate cases...
        var f1 = new AbcdFunction(0.0, 0.0, 1.0E-15, 1.0);
        var f2 = new AbcdFunction(1.0, 0.0, 1.0E-50, 0.0);

        double cov1 = f1.Covariance(0.0, 1.0, 1.0, 1.0);
        Assert.False(
            Math.Abs(cov1 - 1.0) > 1E-14 || double.IsNaN(cov1) || double.IsInfinity(cov1),
            $"(a,b,c,d)=(0,0,0,1): true covariance should be 1.0, error is {Math.Abs(cov1 - 1.0)}");

        double cov2 = f2.Covariance(0.0, 1.0, 1.0, 1.0);
        Assert.False(
            Math.Abs(cov2 - 1.0) > 1E-14 || double.IsNaN(cov2) || double.IsInfinity(cov2),
            $"(a,b,c,d)=(1,0,0,0): true covariance should be 1.0, error is {Math.Abs(cov2 - 1.0)}");
    }

    [Fact]
    public void TestCovariance()
    {
        // Testing market models covariance...
        const int n = 10;

        var rateTimes = new List<double>();
        var evolTimes1 = new List<double>();
        var evolTimes2 = new List<double>();
        var evolTimes3 = new List<double>();
        var evolTimes4 = new List<double>();
        var evolTimes = new List<List<double>>();

        for (int i = 1; i <= n; i++)
        {
            rateTimes.Add(i);
        }

        evolTimes1.Add(n - 1);
        for (int i = 1; i <= n - 1; i++)
        {
            evolTimes2.Add(i);
        }

        for (int i = 1; i <= (2 * n) - 2; i++)
        {
            evolTimes3.Add(0.5 * i);
        }

        evolTimes4.Add(0.3);
        evolTimes4.Add(1.3);
        evolTimes4.Add(2.0);
        evolTimes4.Add(4.5);
        evolTimes4.Add(8.2);

        evolTimes.Add(evolTimes1);
        evolTimes.Add(evolTimes2);
        evolTimes.Add(evolTimes3);
        evolTimes.Add(evolTimes4);

        string[] evolNames =
        [
            "one evolution time",
            "evolution times on rate fixings",
            "evolution times on rate fixings and midpoints between fixings",
            "irregular evolution times",
        ];

        double[] ks = [.. Enumerable.Repeat(1.0, n - 1)];
        double[] displ = new double[n - 1];
        double[] rates = new double[n - 1];
        double[] vols = [.. Enumerable.Repeat(1.0, n - 1)];

        Matrix c = ExpCorrelationsFunctions.ExponentialCorrelations([.. rateTimes], 0.5, 0.2, 1.0, 0.0);
        var corr = new TimeHomogeneousForwardCorrelation(c, [.. rateTimes]);

        string[] modelNames = ["FlatVol", "AbcdVol"];

        for (int k = 0; k < modelNames.Length; k++)
        {
            for (int l = 0; l < evolNames.Length; l++)
            {
                var evolution = new EvolutionDescription([.. rateTimes], [.. evolTimes[l]]);
                MarketModel model = k switch
                {
                    0 => new FlatVol(vols, corr, evolution, n - 1, rates, displ),
                    1 => new AbcdVol(1.0, 0.0, 1.0E-50, 0.0, ks, corr, evolution, n - 1, rates, displ),
                    _ => throw new InvalidOperationException($"Unknown model {modelNames[k]}"),
                };
                for (int i = 0; i < evolTimes[l].Count; i++)
                {
                    Matrix cov = model.Covariance(i);
                    double dt = evolTimes[l][i] - (i > 0 ? evolTimes[l][i - 1] : 0.0);
                    for (int x = 0; x < n - 1; x++)
                    {
                        for (int y = 0; y < n - 1; y++)
                        {
                            Assert.False(
                                Math.Min(rateTimes[x], rateTimes[y]) >= evolTimes[l][i] && Math.Abs(cov[x, y] - (c[x, y] * dt)) > 1.0E-14,
                                $"Model {modelNames[k]} with {evolNames[l]}: covariance matrix in step {i}: true value at ({x},{y}) is {c[x, y] * dt} actual value is {cov[x, y]}");
                        }
                    }
                }
            }
        }
    }

    // Adapted: setup()'s evaluation date (marketmodel.cpp:161), the day QuantLib's suite runs, is 15 October 2026; its
    // schedule and SimpleDayCounter give the same semi-annual times on any day but the month's last few. Only the
    // members C3's cases read are kept, and makeMarketModel's volatility and correlation models of the legacy LIBOR
    // model (lines 303-307), which neither of its two models reads, are left out.
    private sealed class CommonVars
    {
        public const int MeasureOffset = 5;
        public const double LongTermCorrelation = 0.5;
        public const double Beta = 0.2;
        public const double Displacement = 0.0;

        private static readonly double[] MktVols =
        [
            0.15541283, 0.18719678, 0.20890740, 0.22318179, 0.23212717, 0.23731450, 0.23988649, 0.24066384, 0.24023111,
            0.23900189, 0.23726699, 0.23522952, 0.23303022, 0.23076564, 0.22850101, 0.22627951, 0.22412881, 0.22206569,
            0.22009939,
        ];

        public CommonVars()
        {
            // Times
            var calendar = new NullCalendar();
            var todaysDate = new Date(15, October, 2026);
            Date endDate = todaysDate + new Period(5, TimeUnit.Years);
            var context = new EvaluationContext { EvaluationDate = todaysDate };
            var dates = new Schedule(
                todaysDate, endDate, new Period(Frequency.Semiannual), calendar, BusinessDayConvention.Following,
                BusinessDayConvention.Following, DateGenerationRule.Backward, false, context);
            RateTimes = new double[dates.Size - 1];
            var dayCounter = new SimpleDayCounter();
            for (int i = 1; i < dates.Size; ++i)
            {
                RateTimes[i - 1] = dayCounter.YearFraction(todaysDate, dates[i]);
            }

            // Rates & displacement
            TodaysForwards = new double[RateTimes.Length - 1];
            for (int i = 0; i < TodaysForwards.Length; ++i)
            {
                TodaysForwards[i] = 0.03 + (0.0010 * i);
            }

            // Cap/Floor Volatilities
            Volatilities = new double[TodaysForwards.Length];
            BlackVols = new double[TodaysForwards.Length];
            for (int i = 0; i < Math.Min(MktVols.Length, TodaysForwards.Length); i++)
            {
                Volatilities[i] = TodaysForwards[i] * MktVols[i] / (TodaysForwards[i] + Displacement);
                BlackVols[i] = MktVols[i];
            }
        }

        public double[] RateTimes { get; }

        public double[] TodaysForwards { get; }

        public double[] Volatilities { get; }

        public double[] BlackVols { get; }

        // QuantLib's makeMarketModel, log-normal and unbumped, for its two exponential-correlation models.
        public MarketModel MakeMarketModel(EvolutionDescription evolution, int numberOfFactors, string marketModelType)
        {
            Matrix correlations = ExpCorrelationsFunctions.ExponentialCorrelations(evolution.RateTimes.AsSpan(), LongTermCorrelation, Beta);
            var corr = new TimeHomogeneousForwardCorrelation(correlations, evolution.RateTimes.AsSpan());
            double[] displacements = [.. Enumerable.Repeat(Displacement, TodaysForwards.Length)];
            return marketModelType switch
            {
                "ExponentialCorrelationFlatVolatility" => new FlatVol(Volatilities, corr, evolution, numberOfFactors, TodaysForwards, displacements),
                "ExponentialCorrelationAbcdVolatility" => new AbcdVol(0.0, 0.0, 1.0, 1.0, Volatilities, corr, evolution, numberOfFactors, TodaysForwards, displacements),
                _ => throw new ArgumentException($"unknown market model {marketModelType}", nameof(marketModelType)),
            };
        }
    }
}
