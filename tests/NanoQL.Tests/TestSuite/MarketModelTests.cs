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
// Partial: testGreeks, testPathwiseGreeks, testPathwiseVegas, testPathwiseMarketVegas (S-mmp)
// Not ported: testAbcdVolatilityCompare (it checks AbcdFunction against the legacy LIBOR model's LmExtLinearExponentialVolModel, which isn't ported)

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
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

    // QuantLib's EvolverType (marketmodel.cpp:419).
    private enum EvolverType
    {
        Ipc,
        Balland,
        Pc,
        NormalPc,
    }

    // QuantLib's MeasureType (marketmodel.cpp:362-363).
    private enum MeasureType
    {
        ProductSuggested,
        Terminal,
        MoneyMarket,
        MoneyMarketPlus,
    }

    [Fact]
    public void TestOneStepForwardsAndOptionlets()
    {
        // Testing exact repricing of one-step forwards and optionlets in a lognormal forward rate market model...

        var vars = new CommonVars();

        double[] forwardStrikes = new double[vars.TodaysForwards.Length];
        Payoff[] optionletPayoffs = new Payoff[vars.TodaysForwards.Length];
        StrikedTypePayoff[] displacedPayoffs = new StrikedTypePayoff[vars.TodaysForwards.Length];
        for (int i = 0; i < vars.TodaysForwards.Length; ++i)
        {
            forwardStrikes[i] = vars.TodaysForwards[i] + 0.01;
            optionletPayoffs[i] = new PlainVanillaPayoff(OptionType.Call, vars.TodaysForwards[i]);
            displacedPayoffs[i] = new PlainVanillaPayoff(OptionType.Call, vars.TodaysForwards[i] + CommonVars.Displacement);
        }

        var forwards = new OneStepForwards(vars.RateTimes, vars.Accruals, vars.PaymentTimes, forwardStrikes);
        var optionlets = new OneStepOptionlets(vars.RateTimes, vars.Accruals, vars.PaymentTimes, optionletPayoffs);

        var product = new MultiProductComposite();
        product.Add(forwards);
        product.Add(optionlets);
        product.FinalizeComposite();

        EvolutionDescription evolution = product.Evolution;

        string[] marketModels = ["ExponentialCorrelationFlatVolatility", "ExponentialCorrelationAbcdVolatility"];
        foreach (string j in marketModels)
        {
            // one step must be always full factors
            int[] testedFactors = [vars.TodaysForwards.Length];
            foreach (int factors in testedFactors)
            {
                // for one step product ProductSuggested is equal to Terminal
                // for one step product MoneyMarketPlus is equal to Terminal
                MeasureType[] measures = [MeasureType.MoneyMarket, MeasureType.Terminal];
                foreach (MeasureType measure in measures)
                {
                    int[] numeraires = vars.MakeMeasure(product, measure);

                    MarketModel marketModel = vars.MakeMarketModel(true, evolution, factors, j);

                    EvolverType[] evolvers = [EvolverType.Pc, EvolverType.Balland, EvolverType.Ipc];
                    int stop = EvolutionDescriptionFunctions.IsInTerminalMeasure(evolution, numeraires) ? 0 : 1;
                    for (int i = 0; i < evolvers.Length - stop; i++)
                    {
                        for (int n = 0; n < 1; n++)
                        {
                            var generatorFactory = new MTBrownianGeneratorFactory(CommonVars.Seed);
                            MarketModelEvolver evolver =
                                CommonVars.MakeMarketModelEvolver(marketModel, numeraires, generatorFactory, evolvers[i]);
                            string config = $"{j}, {factors} factors, {measure}, {evolvers[i]}, MT BGF";

                            SequenceStatisticsInc stats = vars.Simulate(evolver, product);
                            vars.CheckForwardsAndOptionlets(stats, forwardStrikes, displacedPayoffs, config);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void TestOneStepNormalForwardsAndOptionlets()
    {
        // Testing exact repricing of one-step forwards and optionlets in a normal forward rate market model...

        var vars = new CommonVars();

        double[] forwardStrikes = new double[vars.TodaysForwards.Length];
        Payoff[] optionletPayoffs = new Payoff[vars.TodaysForwards.Length];
        PlainVanillaPayoff[] displacedPayoffs = new PlainVanillaPayoff[vars.TodaysForwards.Length];
        for (int i = 0; i < vars.TodaysForwards.Length; ++i)
        {
            forwardStrikes[i] = vars.TodaysForwards[i] + 0.01;
            optionletPayoffs[i] = new PlainVanillaPayoff(OptionType.Call, vars.TodaysForwards[i]);
            displacedPayoffs[i] = new PlainVanillaPayoff(OptionType.Call, vars.TodaysForwards[i] + CommonVars.Displacement);
        }

        var forwards = new OneStepForwards(vars.RateTimes, vars.Accruals, vars.PaymentTimes, forwardStrikes);
        var optionlets = new OneStepOptionlets(vars.RateTimes, vars.Accruals, vars.PaymentTimes, optionletPayoffs);

        var product = new MultiProductComposite();
        product.Add(forwards);
        product.Add(optionlets);
        product.FinalizeComposite();

        EvolutionDescription evolution = product.Evolution;

        string[] marketModels = ["ExponentialCorrelationFlatVolatility", "ExponentialCorrelationAbcdVolatility"];
        foreach (string j in marketModels)
        {
            // one step must be always full factors
            int[] testedFactors = [vars.TodaysForwards.Length];
            foreach (int factors in testedFactors)
            {
                // for one step product ProductSuggested is equal to Terminal
                // for one step product MoneyMarketPlus is equal to Terminal
                MeasureType[] measures = [MeasureType.MoneyMarket, MeasureType.Terminal];
                foreach (MeasureType measure in measures)
                {
                    int[] numeraires = vars.MakeMeasure(product, measure);

                    MarketModel marketModel = vars.MakeMarketModel(false, evolution, factors, j);

                    EvolverType[] evolvers = [EvolverType.NormalPc];
                    int stop = EvolutionDescriptionFunctions.IsInTerminalMeasure(evolution, numeraires) ? 0 : 1;
                    for (int i = 0; i < evolvers.Length - stop; i++)
                    {
                        for (int n = 0; n < 1; n++)
                        {
                            var generatorFactory = new MTBrownianGeneratorFactory(CommonVars.Seed);
                            MarketModelEvolver evolver =
                                CommonVars.MakeMarketModelEvolver(marketModel, numeraires, generatorFactory, evolvers[i]);
                            string config = $"{j}, {factors} factors, {measure}, {evolvers[i]}, MT BGF";

                            SequenceStatisticsInc stats = vars.Simulate(evolver, product);
                            vars.CheckNormalForwardsAndOptionlets(stats, forwardStrikes, displacedPayoffs, config);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void TestInverseFloater()
    {
        // Testing exact repricing of inverse floater in forward rate market model...

        var vars = new CommonVars();

        // 1. Inverse Floater
        double[] fixedStrikes = [.. Enumerable.Repeat(0.1, vars.Accruals.Length)];
        double[] fixedMultipliers = [.. Enumerable.Repeat(2.0, vars.Accruals.Length)];
        double[] floatingSpreads = [.. Enumerable.Repeat(0.002, vars.Accruals.Length)];
        double[] fixedAccruals = vars.Accruals;
        double[] floatingAccruals = vars.Accruals;

        const bool payer = true;

        var product = new MultiStepInverseFloater(
            vars.RateTimes, fixedAccruals, floatingAccruals, fixedStrikes, fixedMultipliers, floatingSpreads,
            vars.PaymentTimes, payer);

        var productPathwise = new MarketModelPathwiseInverseFloater(
            vars.RateTimes, fixedAccruals, floatingAccruals, fixedStrikes, fixedMultipliers, floatingSpreads,
            vars.PaymentTimes, payer);

        var productWrapped = new MultiProductPathwiseWrapper(productPathwise);

        var productComposite = new MultiProductComposite();
        productComposite.Add(product);
        productComposite.Add(productWrapped);
        productComposite.FinalizeComposite();

        EvolutionDescription evolution = productComposite.Evolution;

        string[] marketModels = ["ExponentialCorrelationFlatVolatility", "ExponentialCorrelationAbcdVolatility"];
        foreach (string j in marketModels)
        {
            int[] testedFactors = [Math.Min(vars.TodaysForwards.Length, 3)];
            foreach (int factors in testedFactors)
            {
                MeasureType[] measures = [MeasureType.MoneyMarket];
                foreach (MeasureType measure in measures)
                {
                    int[] numeraires = vars.MakeMeasure(product, measure);

                    MarketModel marketModel = vars.MakeMarketModel(false, evolution, factors, j);

                    EvolverType[] evolvers = [EvolverType.Pc];

                    foreach (EvolverType i in evolvers)
                    {
                        var generatorFactory = new MTBrownianGeneratorFactory(CommonVars.Seed);

                        MarketModelEvolver evolver =
                            CommonVars.MakeMarketModelEvolver(marketModel, numeraires, generatorFactory, i);

                        SequenceStatisticsInc stats = vars.Simulate(evolver, productComposite);

                        double[] modelVolatilities = new double[vars.Accruals.Length];
                        for (int k = 0; k < vars.Accruals.Length; ++k)
                        {
                            modelVolatilities[k] = Math.Sqrt(marketModel.TotalCovariance(k)[k, k]);
                        }

                        double truePrice = 0.0;

                        for (int k = 0; k < vars.Accruals.Length; ++k)
                        {
                            double floatingCouponPV = floatingAccruals[k] * (vars.TodaysForwards[k] + floatingSpreads[k])
                                * vars.TodaysDiscounts[k + 1];
                            double inverseCouponPV = 2 * fixedAccruals[k] * vars.TodaysDiscounts[k + 1]
                                * BlackFormulaFunctions.BlackFormula(
                                    OptionType.Put, fixedStrikes[k] / 2.0, vars.TodaysForwards[k], modelVolatilities[k]);

                            truePrice += floatingCouponPV - inverseCouponPV;
                        }

                        double priceError = stats.Mean()[0] - truePrice;
                        double priceSD = stats.ErrorEstimate()[0];

                        double errorInSds = priceError / priceSD;
                        if (Math.Abs(errorInSds) > 4.0)
                        {
                            Assert.Fail(
                                $"Inverse floater product has price error equal to {errorInSds} sds . Price {truePrice} "
                                + $"MC price {stats.Mean()[0]} \n");
                        }

                        const double numericalTolerance = 1E-12;

                        if (Math.Abs(stats.Mean()[0] - stats.Mean()[1]) > numericalTolerance)
                        {
                            Assert.Fail(
                                "Inverse floater and wrapper pathwise inverse floater do not agree:"
                                + $"{stats.Mean()[0]}  {stats.Mean()[1]}");
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void TestAllMultiStepProducts()
    {
        const string testDescription = "all multi-step products ";

        var vars = new CommonVars();

        var product = new MultiProductComposite();
        var subProductExpectedValues = new List<SubProductExpectedValues>();
        vars.AddForwards(product, subProductExpectedValues);
        vars.AddOptionLets(product, subProductExpectedValues);
        vars.AddCoinitialSwaps(product, subProductExpectedValues);
        vars.AddCoterminalSwapsAndSwaptions(product, subProductExpectedValues);
        product.FinalizeComposite();
        vars.TestMultiProductComposite(product, subProductExpectedValues, testDescription);
    }

    [Fact]
    public void TestPeriodAdapter()
    {
        // Testing period-adaptation routines in LIBOR market model...

        var vars = new CommonVars();
        var cs = new LMMCurveState(vars.RateTimes);
        cs.SetOnForwardRates(vars.TodaysForwards);

        const int period = 2;
        const int offset = 0;

        LMMCurveState bigRateCS = ForwardForwardMappings.RestrictCurveState(cs, period, offset);

        double[] swaptionPaymentTimes = bigRateCS.RateTimes.AsSpan()[..^1].ToArray();

        double[] capletPaymentTimes = swaptionPaymentTimes;

        int numberBigRates = bigRateCS.NumberOfRates;

        StrikedTypePayoff[] optionletPayoffs = new StrikedTypePayoff[numberBigRates];
        StrikedTypePayoff[] swaptionPayoffs = new StrikedTypePayoff[numberBigRates];
        StrikedTypePayoff[] displacedOptionletPayoffs = new StrikedTypePayoff[numberBigRates];
        StrikedTypePayoff[] displacedSwaptionPayoffs = new StrikedTypePayoff[numberBigRates];

        for (int i = 0; i < numberBigRates; ++i)
        {
            optionletPayoffs[i] = new PlainVanillaPayoff(OptionType.Call, bigRateCS.ForwardRate(i));
            swaptionPayoffs[i] = new PlainVanillaPayoff(OptionType.Call, bigRateCS.CoterminalSwapRate(i));
            displacedOptionletPayoffs[i] =
                new PlainVanillaPayoff(OptionType.Call, bigRateCS.ForwardRate(i) + CommonVars.Displacement);
            displacedSwaptionPayoffs[i] =
                new PlainVanillaPayoff(OptionType.Call, bigRateCS.CoterminalSwapRate(i) + CommonVars.Displacement);
        }

        var theProduct = new MultiStepPeriodCapletSwaptions(
            vars.RateTimes, capletPaymentTimes, swaptionPaymentTimes, optionletPayoffs, swaptionPayoffs, period, offset);

        EvolutionDescription evolution = theProduct.Evolution;

        const int factors = 5;

        MarketModel originalModel = vars.MakeMarketModel(true, evolution, factors, "ExponentialCorrelationAbcdVolatility");

        double[] newDisplacements = [];

        MarketModel adaptedforwardModel = new FwdPeriodAdapter(originalModel, period, offset, newDisplacements);

        MarketModel adaptedSwapModel = new FwdToCotSwapAdapter(adaptedforwardModel);

        Matrix finalForwardCovariances = adaptedforwardModel.TotalCovariance(adaptedforwardModel.NumberOfSteps - 1);
        Matrix finalSwapCovariances = adaptedSwapModel.TotalCovariance(adaptedSwapModel.NumberOfSteps - 1);

        double[] adaptedForwardSds = new double[adaptedforwardModel.NumberOfRates];
        double[] adaptedSwapSds = new double[adaptedSwapModel.NumberOfRates];
        double[] approxCapletPrices = new double[adaptedforwardModel.NumberOfRates];
        double[] approxSwaptionPrices = new double[adaptedSwapModel.NumberOfRates];

        for (int j = 0; j < adaptedSwapModel.NumberOfRates; ++j)
        {
            adaptedForwardSds[j] = Math.Sqrt(finalForwardCovariances[j, j]);
            adaptedSwapSds[j] = Math.Sqrt(finalSwapCovariances[j, j]);

            double capletAnnuity = vars.TodaysDiscounts[0] * bigRateCS.DiscountRatio(j + 1, 0) * bigRateCS.RateTaus[j];

            approxCapletPrices[j] = new BlackCalculator(
                displacedOptionletPayoffs[j], bigRateCS.ForwardRate(j) + CommonVars.Displacement, adaptedForwardSds[j], capletAnnuity).Value;

            double swaptionAnnuity = vars.TodaysDiscounts[0] * bigRateCS.CoterminalSwapAnnuity(0, j);

            approxSwaptionPrices[j] = new BlackCalculator(
                displacedSwaptionPayoffs[j], bigRateCS.CoterminalSwapRate(j) + CommonVars.Displacement, adaptedSwapSds[j], swaptionAnnuity).Value;
        }

        var generatorFactory = new SobolBrownianGeneratorFactory(SobolBrownianGeneratorBaseOrdering.Diagonal, CommonVars.Seed);

        MarketModelEvolver evolver = CommonVars.MakeMarketModelEvolver(
            originalModel, [.. theProduct.SuggestedNumeraires()], generatorFactory, EvolverType.Pc);

        SequenceStatisticsInc stats = vars.Simulate(evolver, theProduct);

        double[] results = [.. stats.Mean()];
        double[] errors = [.. stats.ErrorEstimate()];

        double[] capletErrorsInSds = new double[numberBigRates];
        double[] swaptionErrorsInSds = new double[numberBigRates];

        Assert.True(2 * numberBigRates == results.Length, "mismatch between the size of the result and the number of results");

        for (int i = 0; i < numberBigRates; ++i)
        {
            capletErrorsInSds[i] = (results[i] - approxCapletPrices[i]) / errors[i];
            swaptionErrorsInSds[i] = (results[i + numberBigRates] - approxSwaptionPrices[i]) / errors[i + numberBigRates];
        }

        const double capletTolerance = 4;
        const double swaptionTolerance = 4;

        for (int i = 0; i < numberBigRates; ++i)
        {
            if (Math.Abs(capletErrorsInSds[i]) > capletTolerance)
            {
                Assert.Fail(
                    $"{DataFormattersFunctions.Ordinal(i + 1)}caplet , approx price {approxCapletPrices[i]}, \t simulation price "
                    + $"{results[i]}, \t error in sds {capletErrorsInSds[i]}");
            }
        }

        for (int i = 0; i < numberBigRates; ++i)
        {
            if (Math.Abs(swaptionErrorsInSds[i]) > swaptionTolerance)
            {
                Assert.Fail(
                    $"{DataFormattersFunctions.Ordinal(i + 1)}swaption, approx price {approxSwaptionPrices[i]}, \t simulation price "
                    + $"{results[i + numberBigRates]}, \t error in sds {swaptionErrorsInSds[i]}");
            }
        }
    }

    [Fact]
    public void TestCallableSwapNaif()
    {
        // Pricing callable swap with naif exercise strategy in a LIBOR market model...
        var vars = new CommonVars();

        double fixedRate = 0.04;

        // 0. a payer swap
        var payerSwap = new MultiStepSwap(vars.RateTimes, vars.Accruals, vars.Accruals, vars.PaymentTimes, fixedRate, true);

        // 1. the equivalent receiver swap
        var receiverSwap = new MultiStepSwap(vars.RateTimes, vars.Accruals, vars.Accruals, vars.PaymentTimes, fixedRate, false);

        // exercise schedule
        double[] exerciseTimes = vars.RateTimes[..^1];

        // naif exercise strategy
        double[] swapTriggers = [.. Enumerable.Repeat(fixedRate, exerciseTimes.Length)];
        var naifStrategy = new SwapRateTrigger(vars.RateTimes, swapTriggers, exerciseTimes);

        // Longstaff-Schwartz exercise strategy
        var nullRebate = new NothingExerciseValue(vars.RateTimes);

        var dummyProduct = new CallSpecifiedMultiProduct(receiverSwap, naifStrategy, new ExerciseAdapter(nullRebate));

        EvolutionDescription evolution = dummyProduct.Evolution;

        string[] marketModels = ["ExponentialCorrelationFlatVolatility", "ExponentialCorrelationAbcdVolatility"];
        foreach (string j in marketModels)
        {
            int[] testedFactors = [4, vars.TodaysForwards.Length];
            foreach (int factors in testedFactors)
            {
                MeasureType[] measures = [MeasureType.MoneyMarketPlus];
                foreach (MeasureType measure in measures)
                {
                    int[] numeraires = vars.MakeMeasure(dummyProduct, measure);

                    bool logNormal = true;
                    MarketModel marketModel = vars.MakeMarketModel(logNormal, evolution, factors, j);

                    EvolverType[] evolvers = [EvolverType.Pc, EvolverType.Balland, EvolverType.Ipc];
                    int stop = EvolutionDescriptionFunctions.IsInTerminalMeasure(evolution, numeraires) ? 0 : 1;
                    for (int i = 0; i < evolvers.Length - stop; i++)
                    {
                        for (int n = 0; n < 1; n++)
                        {
                            var generatorFactory = new SobolBrownianGeneratorFactory(
                                SobolBrownianGeneratorBaseOrdering.Diagonal, CommonVars.Seed);

                            MarketModelEvolver evolver =
                                CommonVars.MakeMarketModelEvolver(marketModel, numeraires, generatorFactory, evolvers[i]);
                            string config = $"{j}, {factors} factors, {measure}, {evolvers[i]}, MT BGF";

                            // use the naif strategy

                            // 2. bermudan swaption to enter into the payer swap
                            var bermudanProduct = new CallSpecifiedMultiProduct(new MultiStepNothing(evolution), naifStrategy, payerSwap);

                            // 3. callable receiver swap
                            var callableProduct = new CallSpecifiedMultiProduct(receiverSwap, naifStrategy, new ExerciseAdapter(nullRebate));

                            // lower bound: evolve all 4 products togheter
                            SequenceStatisticsInc stats = vars.Simulate(
                                evolver, CommonVars.AllProducts(payerSwap, receiverSwap, bermudanProduct, callableProduct));
                            CommonVars.CheckCallableSwap(stats, config);

                            // upper bound
                            vars.UpperBound(
                                marketModel, numeraires, evolvers[i], evolution, receiverSwap, nullRebate, naifStrategy);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void TestCallableSwapLS()
    {
        // Pricing callable swap with Longstaff-Schwartz exercise strategy in a LIBOR market model...
        var vars = new CommonVars();

        double fixedRate = 0.04;

        // 0. a payer swap
        var payerSwap = new MultiStepSwap(vars.RateTimes, vars.Accruals, vars.Accruals, vars.PaymentTimes, fixedRate, true);

        // 1. the equivalent receiver swap
        var receiverSwap = new MultiStepSwap(vars.RateTimes, vars.Accruals, vars.Accruals, vars.PaymentTimes, fixedRate, false);

        // exercise schedule
        double[] exerciseTimes = vars.RateTimes[..^1];

        // naif exercise strategy
        double[] swapTriggers = [.. Enumerable.Repeat(fixedRate, exerciseTimes.Length)];
        var naifStrategy = new SwapRateTrigger(vars.RateTimes, swapTriggers, exerciseTimes);

        // Longstaff-Schwartz exercise strategy
        var control = new NothingExerciseValue(vars.RateTimes);
        var basisSystem = new SwapBasisSystem(vars.RateTimes, exerciseTimes);
        var nullRebate = new NothingExerciseValue(vars.RateTimes);

        var dummyProduct = new CallSpecifiedMultiProduct(receiverSwap, naifStrategy, new ExerciseAdapter(nullRebate));

        EvolutionDescription evolution = dummyProduct.Evolution;

        string[] marketModels = ["ExponentialCorrelationFlatVolatility", "ExponentialCorrelationAbcdVolatility"];
        foreach (string j in marketModels)
        {
            int[] testedFactors = [4, vars.TodaysForwards.Length];
            foreach (int factors in testedFactors)
            {
                MeasureType[] measures = [MeasureType.MoneyMarket];
                foreach (MeasureType measure in measures)
                {
                    int[] numeraires = vars.MakeMeasure(dummyProduct, measure);

                    bool logNormal = true;
                    MarketModel marketModel = vars.MakeMarketModel(logNormal, evolution, factors, j);

                    EvolverType[] evolvers = [EvolverType.Pc, EvolverType.Balland, EvolverType.Ipc];
                    int stop = EvolutionDescriptionFunctions.IsInTerminalMeasure(evolution, numeraires) ? 0 : 1;
                    for (int i = 0; i < evolvers.Length - stop; i++)
                    {
                        for (int n = 0; n < 1; n++)
                        {
                            var generatorFactory = new SobolBrownianGeneratorFactory(
                                SobolBrownianGeneratorBaseOrdering.Diagonal, CommonVars.Seed);

                            MarketModelEvolver evolver =
                                CommonVars.MakeMarketModelEvolver(marketModel, numeraires, generatorFactory, evolvers[i]);
                            string config = $"{j}, {factors} factors, {measure}, {evolvers[i]}, MT BGF";

                            // calculate the exercise strategy's coefficients
                            NodeData[][] collectedData = CollectNodeDataFunctions.CollectNodeData(
                                evolver, receiverSwap, basisSystem, nullRebate, control, CommonVars.TrainingPaths);
                            GenericLSRegressionResults regression =
                                GenericLSRegressionFunctions.GenericLongstaffSchwartzRegression(collectedData);
                            var exerciseStrategy = new LongstaffSchwartzExerciseStrategy(
                                basisSystem, regression.BasisCoefficients, evolution, numeraires, nullRebate, control);

                            // 2. bermudan swaption to enter into the payer swap
                            var bermudanProduct = new CallSpecifiedMultiProduct(new MultiStepNothing(evolution), exerciseStrategy, payerSwap);

                            // 3. callable receiver swap
                            var callableProduct = new CallSpecifiedMultiProduct(receiverSwap, exerciseStrategy, new ExerciseAdapter(nullRebate));

                            // lower bound: evolve all 4 products togheter
                            SequenceStatisticsInc stats = vars.Simulate(
                                evolver, CommonVars.AllProducts(payerSwap, receiverSwap, bermudanProduct, callableProduct));
                            CommonVars.CheckCallableSwap(stats, config);

                            // upper bound
                            vars.UpperBound(
                                marketModel, numeraires, evolvers[i], evolution, receiverSwap, nullRebate, exerciseStrategy);
                        }
                    }
                }
            }
        }
    }

    // QuantLib's nested suite CallableSwapAnderson (marketmodel.cpp:1692-1874), its six slices a theory's rows; a
    // testedFactor of 0 is every rate's.
    [Trait("Oracle", "QuantLib")]
    public class CallableSwapAnderson
    {
        [Theory]
        [InlineData("ExponentialCorrelationFlatVolatility", 4)]
        [InlineData("ExponentialCorrelationFlatVolatility", 8)]
        [InlineData("ExponentialCorrelationFlatVolatility", 0)]
        [InlineData("ExponentialCorrelationAbcdVolatility", 4)]
        [InlineData("ExponentialCorrelationAbcdVolatility", 8)]
        [InlineData("ExponentialCorrelationAbcdVolatility", 0)]
        public void TestCallableSwapAnderson(string marketModelType, int sliceFactor)
        {
            var vars = new CommonVars();

            int testedFactor = sliceFactor != 0 ? sliceFactor : vars.TodaysForwards.Length;

            // Pricing callable swap with Anderson exercise strategy in a LIBOR market model for test factor
            // <testedFactor> and model type <marketModelType>...
            double fixedRate = 0.04;

            // 0. a payer swap
            var payerSwap = new MultiStepSwap(vars.RateTimes, vars.Accruals, vars.Accruals, vars.PaymentTimes, fixedRate, true);

            // 1. the equivalent receiver swap
            var receiverSwap = new MultiStepSwap(vars.RateTimes, vars.Accruals, vars.Accruals, vars.PaymentTimes, fixedRate, false);

            // exercise schedule
            double[] exerciseTimes = vars.RateTimes[..^1];

            // naif exercise strategy
            double[] swapTriggers = [.. Enumerable.Repeat(fixedRate, exerciseTimes.Length)];
            var naifStrategy = new SwapRateTrigger(vars.RateTimes, swapTriggers, exerciseTimes);

            // Anderson parametric exercise strategy
            var control = new NothingExerciseValue(vars.RateTimes);
            var nullRebate = new NothingExerciseValue(vars.RateTimes);
            var parametricForm = new TriggeredSwapExercise(
                vars.RateTimes, exerciseTimes, [.. Enumerable.Repeat(fixedRate, exerciseTimes.Length)]);

            var dummyProduct = new CallSpecifiedMultiProduct(receiverSwap, naifStrategy, new ExerciseAdapter(nullRebate));

            EvolutionDescription evolution = dummyProduct.Evolution;

            int factors = testedFactor;

            MeasureType[] measures = [MeasureType.Terminal];
            foreach (MeasureType measure in measures)
            {
                int[] numeraires = vars.MakeMeasure(dummyProduct, measure);
                bool logNormal = true;
                MarketModel marketModel = vars.MakeMarketModel(logNormal, evolution, factors, marketModelType);
                EvolverType[] evolvers = [EvolverType.Pc, EvolverType.Balland, EvolverType.Ipc];
                int stop = EvolutionDescriptionFunctions.IsInTerminalMeasure(evolution, numeraires) ? 0 : 1;
                for (int i = 0; i < evolvers.Length - stop; i++)
                {
                    for (int n = 0; n < 1; n++)
                    {
                        var generatorFactory = new SobolBrownianGeneratorFactory(
                            SobolBrownianGeneratorBaseOrdering.Diagonal, CommonVars.Seed);
                        MarketModelEvolver evolver =
                            CommonVars.MakeMarketModelEvolver(marketModel, numeraires, generatorFactory, evolvers[i]);
                        string config = $"{marketModelType}, {factors} factors, {measure}, {evolvers[i]}, MT BGF";

                        NodeData[][] collectedData = CollectNodeDataFunctions.CollectNodeData(
                            evolver, receiverSwap, parametricForm, nullRebate, control, CommonVars.TrainingPaths);
                        var om = new Simplex(0.01);
                        var ec = new EndCriteria(1000, 100, 1e-8, 1e-16, 1e-8);
                        int initialNumeraire = evolver.Numeraires[0];
                        double initialNumeraireValue = vars.TodaysDiscounts[initialNumeraire];
                        GenericEarlyExerciseOptimizationResults optimization =
                            ParametricExerciseFunctions.GenericEarlyExerciseOptimization(collectedData, parametricForm, ec, om);
                        _ = optimization.BiasedEstimate * initialNumeraireValue;
                        var exerciseStrategy = new ParametricExerciseAdapter(parametricForm, optimization.Parameters.AsSpan());

                        // 2. bermudan swaption to enter into the payer swap
                        var bermudanProduct = new CallSpecifiedMultiProduct(new MultiStepNothing(evolution), exerciseStrategy, payerSwap);

                        // 3. callable receiver swap
                        var callableProduct = new CallSpecifiedMultiProduct(receiverSwap, exerciseStrategy, new ExerciseAdapter(nullRebate));

                        // lower bound: evolve all 4 products togheter
                        SequenceStatisticsInc stats = vars.Simulate(
                            evolver, CommonVars.AllProducts(payerSwap, receiverSwap, bermudanProduct, callableProduct));
                        CommonVars.CheckCallableSwap(stats, config);

                        // upper bound
                        vars.UpperBound(
                            marketModel, numeraires, evolvers[i], evolution, receiverSwap, nullRebate, exerciseStrategy);
                    }
                }
            }
        }
    }

    [Fact]
    public void TestStochVolForwardsAndOptionlets()
    {
        // Testing exact repricing of forwards and optionlets in a stochastic vol displaced diffusion forward rate
        // market model...

        var vars = new CommonVars();

        double[] forwardStrikes = new double[vars.TodaysForwards.Length];
        Payoff[] optionletPayoffs = new Payoff[vars.TodaysForwards.Length];
        for (int i = 0; i < vars.TodaysForwards.Length; ++i)
        {
            forwardStrikes[i] = vars.TodaysForwards[i] + 0.01;
            optionletPayoffs[i] = new PlainVanillaPayoff(OptionType.Call, vars.TodaysForwards[i]);
        }

        var forwards = new MultiStepForwards(vars.RateTimes, vars.Accruals, vars.PaymentTimes, forwardStrikes);
        var optionlets = new MultiStepOptionlets(vars.RateTimes, vars.Accruals, vars.PaymentTimes, optionletPayoffs);

        var product = new MultiProductComposite();
        product.Add(forwards);
        product.Add(optionlets);
        product.FinalizeComposite();

        EvolutionDescription evolution = product.Evolution;

        string[] marketModels = ["ExponentialCorrelationFlatVolatility"];

        const int firstVolatilityFactor = 2;
        const int volatilityFactorStep = 2;

        const double meanLevel = 1.0;
        const double reversionSpeed = 1.0;

        const double volVar = 1;
        const double v0 = 1.0;
        const int numberSubSteps = 8;
        const double w1 = 0.5;
        const double w2 = 0.5;
        const double cutPoint = 1.5;

        var volProcess = new SquareRootAndersen(
            meanLevel, reversionSpeed, volVar, v0, evolution.EvolutionTimes.AsSpan(), numberSubSteps, w1, w2, cutPoint);

        foreach (string j in marketModels)
        {
            int[] testedFactors = [1, 2, vars.TodaysForwards.Length];
            foreach (int factors in testedFactors)
            {
                MeasureType[] measures = [MeasureType.MoneyMarket, MeasureType.Terminal];

                foreach (MeasureType measure in measures)
                {
                    int[] numeraires = vars.MakeMeasure(product, measure);

                    MarketModel marketModel = vars.MakeMarketModel(evolution, factors, j);

                    for (int n = 0; n < 1; n++)
                    {
                        var generatorFactory = new MTBrownianGeneratorFactory(CommonVars.Seed);

                        MarketModelEvolver evolver = new SVDDFwdRatePc(
                            marketModel, generatorFactory, volProcess, firstVolatilityFactor, volatilityFactorStep,
                            numeraires);

                        SequenceStatisticsInc stats = vars.Simulate(evolver, product);

                        double[] results = [.. stats.Mean()];
                        double[] errors = [.. stats.ErrorEstimate()];

                        // check forwards
                        for (int i = 0; i < vars.Accruals.Length; ++i)
                        {
                            double trueValue = vars.TodaysDiscounts[i]
                                - (vars.TodaysDiscounts[i + 1] * (1 + (forwardStrikes[i] * vars.Accruals[i])));
                            double error = results[i] - trueValue;
                            double errorSds = error / errors[i];

                            if (Math.Abs(errorSds) > 3.5)
                            {
                                Assert.Fail(
                                    $"error in sds: {errorSds} for forward {i} in SV LMM test. True value:{trueValue}, "
                                    + $"actual value: {results[i]} , standard error {errors[i]}");
                            }
                        }

                        for (int i = 0; i < vars.Accruals.Length; ++i)
                        {
                            double trueValue = vars.HestonCaplet(i, meanLevel, reversionSpeed, volVar, v0);
                            double error = results[i + vars.Accruals.Length] - trueValue;

                            // the forward's standard error, as QuantLib's (marketmodel.cpp:4450)
                            double errorSds = error / errors[i];

                            if (Math.Abs(errorSds) > 4)
                            {
                                Assert.Fail(
                                    $"error in sds: {errorSds} for caplet {i} in SV LMM test. True value:{trueValue}, "
                                    + $"actual value: {results[i + vars.Accruals.Length]} , standard error {errors[i]}");
                            }
                        }
                    }
                }
            }
        }
    }

    // QuantLib's SubProductExpectedValues (marketmodel.cpp:149-155).
    private sealed class SubProductExpectedValues(string description)
    {
        public string Description { get; } = description;

        public List<double> Values { get; } = [];

        public bool TestBias { get; init; }

        public double ErrorThreshold { get; init; }
    }

    // Adapted: setup()'s evaluation date (marketmodel.cpp:161), the day QuantLib's suite runs, is 15 October 2026; its
    // schedule and SimpleDayCounter give the same semi-annual times on any day but the month's last few. Only the
    // members the ported cases read are kept, and makeMarketModel's volatility and correlation models of the legacy
    // LIBOR model (lines 303-307), which neither of its two models reads, are left out. The paths are QuantLib's
    // release counts (lines 264-265), not its _DEBUG ones.
    private sealed class CommonVars
    {
        public const int MeasureOffset = 5;
        public const long Seed = 42;
        public const int Paths = 32767;
        public const int TrainingPaths = 8191;
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
            PaymentTimes = new double[RateTimes.Length - 1];
            Accruals = new double[RateTimes.Length - 1];
            var dayCounter = new SimpleDayCounter();
            for (int i = 1; i < dates.Size; ++i)
            {
                RateTimes[i - 1] = dayCounter.YearFraction(todaysDate, dates[i]);
            }

            Array.Copy(RateTimes, 1, PaymentTimes, 0, PaymentTimes.Length);
            for (int i = 1; i < RateTimes.Length; ++i)
            {
                Accruals[i - 1] = RateTimes[i] - RateTimes[i - 1];
            }

            TodaysDate = todaysDate;
            DayCounter = dayCounter;

            // Rates & displacement
            TodaysForwards = new double[RateTimes.Length - 1];
            for (int i = 0; i < TodaysForwards.Length; ++i)
            {
                TodaysForwards[i] = 0.03 + (0.0010 * i);
            }

            // Discount factors
            TodaysDiscounts = new double[RateTimes.Length];
            TodaysDiscounts[0] = 0.95;
            for (int i = 1; i < RateTimes.Length; ++i)
            {
                TodaysDiscounts[i] = TodaysDiscounts[i - 1] / (1.0 + (TodaysForwards[i - 1] * Accruals[i - 1]));
            }

            // Coterminal-related quantities
            int nRates = TodaysForwards.Length;
            TodaysCoterminalSwapRates = new double[nRates];
            CoterminalAnnuity = new double[nRates];
            double floatingLeg;
            for (int i = 1; i <= nRates; ++i)
            {
                if (i == 1)
                {
                    CoterminalAnnuity[nRates - 1] = Accruals[nRates - 1] * TodaysDiscounts[nRates];
                }
                else
                {
                    CoterminalAnnuity[nRates - i] =
                        CoterminalAnnuity[nRates - i + 1] + (Accruals[nRates - i] * TodaysDiscounts[nRates - i + 1]);
                }

                floatingLeg = TodaysDiscounts[nRates - i] - TodaysDiscounts[nRates];
                TodaysCoterminalSwapRates[nRates - i] = floatingLeg / CoterminalAnnuity[nRates - i];
            }

            // Cap/Floor Volatilities
            Volatilities = new double[TodaysForwards.Length];
            BlackVols = new double[TodaysForwards.Length];
            NormalVols = new double[TodaysForwards.Length];
            for (int i = 0; i < Math.Min(MktVols.Length, TodaysForwards.Length); i++)
            {
                Volatilities[i] = TodaysForwards[i] * MktVols[i] / (TodaysForwards[i] + Displacement);
                BlackVols[i] = MktVols[i];
                NormalVols[i] = MktVols[i] * TodaysForwards[i];
            }
        }

        public double[] RateTimes { get; }

        public double[] PaymentTimes { get; }

        public double[] Accruals { get; }

        public double[] TodaysForwards { get; }

        public double[] TodaysDiscounts { get; }

        public Date TodaysDate { get; }

        public DayCounter DayCounter { get; }

        public double[] Volatilities { get; }

        public double[] BlackVols { get; }

        public double[] NormalVols { get; }

        public double[] TodaysCoterminalSwapRates { get; }

        public double[] CoterminalAnnuity { get; }

        // QuantLib's makeMarketModelEvolver (marketmodel.cpp:436-461).
        public static MarketModelEvolver MakeMarketModelEvolver(
            MarketModel marketModel, int[] numeraires, BrownianGeneratorFactory generatorFactory, EvolverType evolverType,
            int initialStep = 0) => evolverType switch
            {
                EvolverType.Ipc => new LogNormalFwdRateIpc(marketModel, generatorFactory, numeraires, initialStep),
                EvolverType.Balland => new LogNormalFwdRateBalland(marketModel, generatorFactory, numeraires, initialStep),
                EvolverType.Pc => new LogNormalFwdRatePc(marketModel, generatorFactory, numeraires, initialStep),
                _ => new NormalFwdRatePc(marketModel, generatorFactory, numeraires, initialStep),
            };

        // QuantLib's checkForwardsAndOptionlets (marketmodel.cpp:521-584): the forwards' exact values and the caplets'
        // Black values, every discrepancy within 2.5 standard errors and of both signs.
        public void CheckForwardsAndOptionlets(
            SequenceStatisticsInc stats, double[] forwardStrikes, StrikedTypePayoff[] displacedPayoffs, string config) =>
            CheckForwardsAndCaplets(
                stats,
                forwardStrikes,
                i => new BlackCalculator(
                    displacedPayoffs[i],
                    TodaysForwards[i] + Displacement,
                    Volatilities[i] * Math.Sqrt(RateTimes[i]),
                    TodaysDiscounts[i + 1] * Accruals[i]).Value,
                config,
                checkSign: true);

        // QuantLib's checkNormalForwardsAndOptionlets (marketmodel.cpp:588-650): the caplets' Bachelier values.
        public void CheckNormalForwardsAndOptionlets(
            SequenceStatisticsInc stats, double[] forwardStrikes, PlainVanillaPayoff[] displacedPayoffs, string config) =>
            CheckForwardsAndCaplets(
                stats,
                forwardStrikes,
                i => BlackFormulaFunctions.BachelierBlackFormula(
                    displacedPayoffs[i],
                    TodaysForwards[i] + Displacement,
                    NormalVols[i] * Math.Sqrt(RateTimes[i]),
                    TodaysDiscounts[i + 1] * Accruals[i]),
                config,
                checkSign: true);

        // QuantLib's testMultiProductComposite (marketmodel.cpp:997-1074).
        public void TestMultiProductComposite(
            MarketModelMultiProduct product, List<SubProductExpectedValues> subProductExpectedValues, string testDescription)
        {
            // Testing exact repricing of <testDescription>in a lognormal forward rate market model...
            EvolutionDescription evolution = product.Evolution;

            string[] marketModels = ["ExponentialCorrelationFlatVolatility", "ExponentialCorrelationAbcdVolatility"];
            foreach (string j in marketModels)
            {
                int[] testedFactors = [4, 8, TodaysForwards.Length];
                foreach (int factors in testedFactors)
                {
                    MeasureType[] measures = [MeasureType.Terminal, MeasureType.MoneyMarketPlus, MeasureType.MoneyMarket];
                    foreach (MeasureType measure in measures)
                    {
                        int[] numeraires = MakeMeasure(product, measure);

                        MarketModel marketModel = MakeMarketModel(true, evolution, factors, j);

                        EvolverType[] evolvers = [EvolverType.Pc, EvolverType.Balland, EvolverType.Ipc];
                        int stop = EvolutionDescriptionFunctions.IsInTerminalMeasure(evolution, numeraires) ? 0 : 1;
                        for (int i = 0; i < evolvers.Length - stop; i++)
                        {
                            for (int n = 0; n < 1; n++)
                            {
                                var generatorFactory = new SobolBrownianGeneratorFactory(
                                    SobolBrownianGeneratorBaseOrdering.Diagonal, Seed);

                                MarketModelEvolver evolver =
                                    MakeMarketModelEvolver(marketModel, numeraires, generatorFactory, evolvers[i]);
                                string config = $"{testDescription}{j}, {factors} factors, {measure}, {evolvers[i]}, MT BGF";

                                SequenceStatisticsInc stats = Simulate(evolver, product);
                                CheckMultiProductCompositeResults(stats, subProductExpectedValues, config);
                            }
                        }
                    }
                }
            }
        }

        // QuantLib's addForwards (marketmodel.cpp:1076-1096).
        public void AddForwards(MultiProductComposite product, List<SubProductExpectedValues> subProductExpectedValues)
        {
            double[] forwardStrikes = [.. TodaysForwards.Select(f => f + 0.01)];

            var forwards = new MultiStepForwards(RateTimes, Accruals, PaymentTimes, forwardStrikes);
            product.Add(forwards);

            var v = new SubProductExpectedValues("Forward") { ErrorThreshold = 2.50 };
            for (int i = 0; i < TodaysForwards.Length; ++i)
            {
                v.Values.Add((TodaysForwards[i] - forwardStrikes[i]) * Accruals[i] * TodaysDiscounts[i + 1]);
            }

            subProductExpectedValues.Add(v);
        }

        // QuantLib's addOptionLets (marketmodel.cpp:1098-1128).
        public void AddOptionLets(MultiProductComposite product, List<SubProductExpectedValues> subProductExpectedValues)
        {
            Payoff[] optionletPayoffs = [.. TodaysForwards.Select(f => (Payoff)new PlainVanillaPayoff(OptionType.Call, f))];
            StrikedTypePayoff[] displacedPayoffs =
                [.. TodaysForwards.Select(f => (StrikedTypePayoff)new PlainVanillaPayoff(OptionType.Call, f + Displacement))];

            var optionlets = new MultiStepOptionlets(RateTimes, Accruals, PaymentTimes, optionletPayoffs);
            product.Add(optionlets);

            var v = new SubProductExpectedValues("Caplet") { ErrorThreshold = 2.50 };
            for (int i = 0; i < TodaysForwards.Length; ++i)
            {
                v.Values.Add(new BlackCalculator(
                    displacedPayoffs[i],
                    TodaysForwards[i] + Displacement,
                    Volatilities[i] * Math.Sqrt(RateTimes[i]),
                    TodaysDiscounts[i + 1] * Accruals[i]).Value);
            }

            subProductExpectedValues.Add(v);
        }

        // QuantLib's addCoinitialSwaps (marketmodel.cpp:1131-1149).
        public void AddCoinitialSwaps(MultiProductComposite product, List<SubProductExpectedValues> subProductExpectedValues)
        {
            const double fixedRate = 0.04;
            var multiStepCoinitialSwaps = new MultiStepCoinitialSwaps(RateTimes, Accruals, Accruals, PaymentTimes, fixedRate);
            product.Add(multiStepCoinitialSwaps);

            var v = new SubProductExpectedValues("coinitial swap") { TestBias = false, ErrorThreshold = 2.32 };
            double coinitialSwapValue = 0;
            for (int i = 0; i < TodaysForwards.Length; ++i)
            {
                coinitialSwapValue += (TodaysForwards[i] - fixedRate) * Accruals[i] * TodaysDiscounts[i + 1];
                v.Values.Add(coinitialSwapValue);
            }

            subProductExpectedValues.Add(v);
        }

        // QuantLib's addCoterminalSwapsAndSwaptions (marketmodel.cpp:1151-1210).
        public void AddCoterminalSwapsAndSwaptions(
            MultiProductComposite product, List<SubProductExpectedValues> subProductExpectedValues)
        {
            const double fixedRate = 0.04;
            var swaps = new MultiStepCoterminalSwaps(RateTimes, Accruals, Accruals, PaymentTimes, fixedRate);

            StrikedTypePayoff[] payoffs =
                [.. TodaysForwards.Select(f => (StrikedTypePayoff)new PlainVanillaPayoff(OptionType.Call, f))];

            var swaptions = new MultiStepCoterminalSwaptions(RateTimes, RateTimes, payoffs);
            product.Add(swaps);
            product.Add(swaptions);

            var swapValues = new SubProductExpectedValues("coterminal swap") { TestBias = false, ErrorThreshold = 2.32 };
            var curveState = new LMMCurveState(RateTimes); // not the best way to detect errors in LMMCurveState...
            curveState.SetOnForwardRates(TodaysForwards);
            double[] atmRates = curveState.CoterminalSwapRates().ToArray();
            for (int i = 0; i < TodaysForwards.Length; ++i)
            {
                double expectedNPV = curveState.CoterminalSwapAnnuity(0, i) * (atmRates[i] - fixedRate) * TodaysDiscounts[0];
                swapValues.Values.Add(expectedNPV);
            }

            subProductExpectedValues.Add(swapValues);

            var productClone = (MultiProductComposite)product.Clone();
            productClone.FinalizeComposite();
            var swaptionValues = new SubProductExpectedValues("coterminal swaption") { TestBias = false, ErrorThreshold = 2.32 };
            const double displacement = 0;
            Matrix jacobian = SwapForwardMappings.CoterminalSwapZedMatrix(curveState, displacement);

            EvolutionDescription evolution = productClone.Evolution;
            int factors = TodaysForwards.Length;
            MarketModel marketModel = MakeMarketModel(true, evolution, factors, "ExponentialCorrelationFlatVolatility");
            for (int i = 0; i < TodaysForwards.Length; ++i)
            {
                Matrix forwardsCovariance = marketModel.TotalCovariance(i);
                Matrix cotSwapsCovariance = jacobian * forwardsCovariance * MatrixFunctions.Transpose(jacobian);
                var payoff = new PlainVanillaPayoff(OptionType.Call, TodaysForwards[i] + displacement);

                double expectedSwaption = new BlackCalculator(
                    payoff,
                    TodaysCoterminalSwapRates[i] + displacement,
                    Math.Sqrt(cotSwapsCovariance[i, i]),
                    curveState.CoterminalSwapAnnuity(0, i) * TodaysDiscounts[0]).Value;
                swaptionValues.Values.Add(expectedSwaption);
            }

            subProductExpectedValues.Add(swaptionValues);
        }

        // QuantLib's checkMultiProductCompositeResults (marketmodel.cpp:463-518).
        private static void CheckMultiProductCompositeResults(
            SequenceStatisticsInc stats, List<SubProductExpectedValues> subProductExpectedValues, string config)
        {
            double[] results = [.. stats.Mean()];
            double[] errors = [.. stats.ErrorEstimate()];

            // size check
            int nbOfResults = subProductExpectedValues.Sum(s => s.Values.Count);
            Assert.True(nbOfResults == results.Length, "mismatch between the size of the result and the number of results");

            int currentResultIndex = 0;
            foreach (SubProductExpectedValues subProductExpectedValue in subProductExpectedValues)
            {
                double minError = double.MaxValue;
                double maxError = double.MinValue;
                double errorThreshold = subProductExpectedValue.ErrorThreshold;
                foreach (double value in subProductExpectedValue.Values)
                {
                    double stdDev = (results[currentResultIndex] - value) / errors[currentResultIndex];
                    maxError = Math.Max(maxError, stdDev);
                    minError = Math.Min(minError, stdDev);
                    ++currentResultIndex;
                }

                bool isBiased = minError > 0.0 || maxError < 0.0;
                if ((subProductExpectedValue.TestBias && isBiased) || Math.Max(-minError, maxError) > errorThreshold)
                {
                    Assert.Fail($"{config}: {subProductExpectedValue.Description}: test failed ({minError}, {maxError})");
                }
            }
        }

        private void CheckForwardsAndCaplets(
            SequenceStatisticsInc stats, double[] forwardStrikes, Func<int, double> caplet, string config, bool checkSign)
        {
            double[] results = [.. stats.Mean()];
            double[] errors = [.. stats.ErrorEstimate()];

            int n = TodaysForwards.Length;
            double minError = double.MaxValue;
            double maxError = double.MinValue;

            // forwards check
            for (int i = 0; i < n; ++i)
            {
                double expectedForward = (TodaysForwards[i] - forwardStrikes[i]) * Accruals[i] * TodaysDiscounts[i + 1];
                double forwardStdDev = (results[i] - expectedForward) / errors[i];
                if (forwardStdDev > maxError)
                {
                    maxError = forwardStdDev;
                }
                else if (forwardStdDev < minError)
                {
                    minError = forwardStdDev;
                }

                double expectedCaplet = caplet(i);
                double capletStdDev = (results[i + n] - expectedCaplet) / errors[i + n];
                if (capletStdDev > maxError)
                {
                    maxError = capletStdDev;
                }
                else if (capletStdDev < minError)
                {
                    minError = capletStdDev;
                }
            }

            const double errorThreshold = 2.50;
            if ((checkSign && (minError > 0.0 || maxError < 0.0)) || minError < -errorThreshold || maxError > errorThreshold)
            {
                Assert.Fail($"{config}: test failed ({minError}, {maxError})");
            }
        }

        // The test suite's flatRate(forward, dayCounter): a flat curve from the evaluation date.
        public YieldTermStructure FlatRate(double forward) => new FlatForward(TodaysDate, forward, DayCounter);

        // testStochVolForwardsAndOptionlets' true caplet value (marketmodel.cpp:4413-4440): Heston's price of the
        // at-the-money caplet on the forward, its variance process the volatility process scaled by the forward's
        // volatility, times its accrual and its payment's discount.
        public double HestonCaplet(int i, double meanLevel, double reversionSpeed, double volVar, double v0)
        {
            double volCoeff = Volatilities[i];
            double theta = volCoeff * volCoeff * meanLevel;
            double kappa = reversionSpeed;
            double sigma = volCoeff * volVar;
            double rho = 0.0;
            double v1 = v0 * volCoeff * volCoeff;

            double trueValue = new AnalyticHestonEngine(
                    new HestonModel(new HestonProcess(
                        FlatRate(0.0), FlatRate(0.0), TodaysForwards[i] + Displacement, v1, kappa, theta, sigma, rho)),
                    AnalyticHestonEngineComplexLogFormula.Gatheral,
                    FourierIntegration.GaussLaguerre(128))
                .PriceVanillaPayoff(
                    new PlainVanillaPayoff(OptionType.Call, TodaysForwards[i] + Displacement), RateTimes[i]);

            return trueValue * Accruals[i] * TodaysDiscounts[i + 1];
        }

        // The callable cases' four products in one composite, QuantLib's allProducts: the payer and receiver swaps, the
        // Bermudan to enter the payer swap and the callable receiver swap.
        public static MultiProductComposite AllProducts(
            MarketModelMultiProduct payerSwap,
            MarketModelMultiProduct receiverSwap,
            MarketModelMultiProduct bermudanProduct,
            MarketModelMultiProduct callableProduct)
        {
            var allProducts = new MultiProductComposite();
            allProducts.Add(payerSwap);
            allProducts.Add(receiverSwap);
            allProducts.Add(bermudanProduct);
            allProducts.Add(callableProduct);
            allProducts.FinalizeComposite();
            return allProducts;
        }

        // QuantLib's checkCallableSwap (marketmodel.cpp:654-697): the payer and receiver swaps opposite, the Bermudan
        // not negative, the callable receiver worth at least the receiver, and the receiver plus the Bermudan the
        // callable, within 1.1e-15.
        public static void CheckCallableSwap(SequenceStatisticsInc stats, string config)
        {
            ImmutableArray<double> mean = stats.Mean();
            double payerNPV = mean[0];
            double receiverNPV = mean[1];
            double bermudanNPV = mean[2];
            double callableNPV = mean[3];
            const double tolerance = 1.1e-15;
            double swapError = Math.Abs(receiverNPV + payerNPV);
            double callableError = Math.Abs(receiverNPV + bermudanNPV - callableNPV);

            Assert.True(
                swapError <= tolerance,
                $"{config}\nagreement between payer and receiver swap failed:\n    payer swap:    {payerNPV}\n"
                + $"    receiver swap: {receiverNPV}\n    error:         {swapError}\n    tolerance:     {tolerance}");
            Assert.True(bermudanNPV >= 0.0, $"{config}\nnegative bermudan option value:\n    bermudan:          {bermudanNPV}");
            Assert.True(
                callableNPV >= receiverNPV,
                $"{config}\ncallable receiver less valuable than plain receiver:\n    receiver swap:     {receiverNPV}\n"
                + $"    callable:          {callableNPV}");
            Assert.True(
                callableError <= tolerance,
                $"{config}\nagreement between receiver+bermudan and callable failed:\n    receiver swap:     {receiverNPV}\n"
                + $"    bermudan:          {bermudanNPV}\n    receiver+bermudan: {receiverNPV + bermudanNPV}\n"
                + $"    callable:          {callableNPV}\n    error:             {callableError}\n"
                + $"    tolerance:         {tolerance}");
        }

        // The callable cases' upper bound (marketmodel.cpp:1491-1525): Sobol outer paths on seed + 142, an inner
        // Mersenne-twister evolver on seed + s from each exercise step s, 255 outer paths of 256 inner ones. QuantLib
        // prints the bound and asserts nothing; the port computes it as QuantLib does (the S-mmp plan's SMD13), its
        // facts in Divergences/UpperBoundEngineTests.cs.
        public (double Delta, double DeltaError) UpperBound(
            MarketModel marketModel,
            int[] numeraires,
            EvolverType evolverType,
            EvolutionDescription evolution,
            MarketModelMultiProduct receiverSwap,
            MarketModelExerciseValue nullRebate,
            ExerciseStrategy<CurveState> strategy)
        {
            var uFactory = new SobolBrownianGeneratorFactory(SobolBrownianGeneratorBaseOrdering.Diagonal, Seed + 142);
            MarketModelEvolver evolver = MakeMarketModelEvolver(marketModel, numeraires, uFactory, evolverType);

            var innerEvolvers = new List<MarketModelEvolver>();

            ImmutableArray<bool> isExerciseTime =
                UtilitiesFunctions.IsInSubset(evolution.EvolutionTimes.AsSpan(), strategy.ExerciseTimes().AsSpan());
            for (int s = 0; s < isExerciseTime.Length; ++s)
            {
                if (isExerciseTime[s])
                {
                    var iFactory = new MTBrownianGeneratorFactory(Seed + s);
                    innerEvolvers.Add(MakeMarketModelEvolver(marketModel, numeraires, iFactory, evolverType, s));
                }
            }

            int initialNumeraire = evolver.Numeraires[0];
            double initialNumeraireValue = TodaysDiscounts[initialNumeraire];

            var uEngine = new UpperBoundEngine(
                evolver, [.. innerEvolvers], receiverSwap, nullRebate, receiverSwap, nullRebate, strategy, initialNumeraireValue);
            var uStats = new GeneralStatistics();
            uEngine.MultiplePathValues(uStats, 255, 256);
            return (uStats.Mean(), uStats.ErrorEstimate());
        }

        // QuantLib's simulate: the accounting engine on the evolver, its initial numeraire's discount the initial
        // value, over the paths.
        public SequenceStatisticsInc Simulate(MarketModelEvolver evolver, MarketModelMultiProduct product)
        {
            int initialNumeraire = evolver.Numeraires[0];
            double initialNumeraireValue = TodaysDiscounts[initialNumeraire];

            var engine = new AccountingEngine(evolver, product, initialNumeraireValue);
            var stats = new SequenceStatisticsInc(product.NumberOfProducts);
            engine.MultiplePathValues(stats, Paths);
            return stats;
        }

        // QuantLib's makeMeasure: each measure's numeraires, checked as QuantLib checks them.
        public int[] MakeMeasure(MarketModelMultiProduct product, MeasureType measureType)
        {
            int[] result;
            EvolutionDescription evolution = product.Evolution;
            switch (measureType)
            {
                case MeasureType.ProductSuggested:
                    result = [.. product.SuggestedNumeraires()];
                    break;
                case MeasureType.Terminal:
                    result = [.. EvolutionDescriptionFunctions.TerminalMeasure(evolution)];
                    Assert.True(
                        EvolutionDescriptionFunctions.IsInTerminalMeasure(evolution, result),
                        $"\nfailure in verifying Terminal measure:\n{string.Join(", ", result)}");
                    break;
                case MeasureType.MoneyMarket:
                    result = [.. EvolutionDescriptionFunctions.MoneyMarketMeasure(evolution)];
                    Assert.True(
                        EvolutionDescriptionFunctions.IsInMoneyMarketMeasure(evolution, result),
                        $"\nfailure in verifying MoneyMarket measure:\n{string.Join(", ", result)}");
                    break;
                default:
                    result = [.. EvolutionDescriptionFunctions.MoneyMarketPlusMeasure(evolution, MeasureOffset)];
                    Assert.True(
                        EvolutionDescriptionFunctions.IsInMoneyMarketPlusMeasure(evolution, result, MeasureOffset),
                        $"\nfailure in verifying MoneyMarketPlus({MeasureOffset}) measure:\n"
                        + $"{string.Join(", ", result)}");
                    break;
            }

            EvolutionDescriptionFunctions.CheckCompatibility(evolution, result);
            return result;
        }

        // QuantLib's makeMarketModel, log-normal and unbumped, for its two exponential-correlation models.
        public MarketModel MakeMarketModel(EvolutionDescription evolution, int numberOfFactors, string marketModelType) =>
            MakeMarketModel(true, evolution, numberOfFactors, marketModelType);

        // QuantLib's makeMarketModel (marketmodel.cpp:296-360) for its two exponential-correlation models: the
        // log-normal volatilities or the normal ones, the forwards and volatilities bumped.
        public MarketModel MakeMarketModel(
            bool logNormal,
            EvolutionDescription evolution,
            int numberOfFactors,
            string marketModelType,
            double forwardBump = 0.0,
            double volBump = 0.0)
        {
            double[] bumpedForwards = [.. TodaysForwards.Select(r => r + forwardBump)];
            double[] bumpedVols = logNormal
                ? [.. Volatilities.Select(v => v + volBump)]
                : [.. NormalVols.Select(v => v + volBump)];
            Matrix correlations = ExpCorrelationsFunctions.ExponentialCorrelations(evolution.RateTimes.AsSpan(), LongTermCorrelation, Beta);
            var corr = new TimeHomogeneousForwardCorrelation(correlations, evolution.RateTimes.AsSpan());
            double[] displacements = [.. Enumerable.Repeat(Displacement, bumpedForwards.Length)];
            return marketModelType switch
            {
                "ExponentialCorrelationFlatVolatility" => new FlatVol(bumpedVols, corr, evolution, numberOfFactors, bumpedForwards, displacements),
                "ExponentialCorrelationAbcdVolatility" => new AbcdVol(0.0, 0.0, 1.0, 1.0, bumpedVols, corr, evolution, numberOfFactors, bumpedForwards, displacements),
                _ => throw new ArgumentException($"unknown market model {marketModelType}", nameof(marketModelType)),
            };
        }
    }
}
