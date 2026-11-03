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
    public void TestGreeks()
    {
        // Testing caplet greeks in a lognormal forward rate market model using partial proxy simulation...
        var vars = new CommonVars();

        Payoff[] payoffs = new Payoff[vars.TodaysForwards.Length];
        for (int i = 0; i < vars.TodaysForwards.Length; ++i)
        {
            payoffs[i] = new CashOrNothingPayoff(OptionType.Call, vars.TodaysForwards[i], 0.01);
        }

        var product = new MultiStepOptionlets(vars.RateTimes, vars.Accruals, vars.PaymentTimes, payoffs);

        EvolutionDescription evolution = product.Evolution;

        string[] marketModels = ["ExponentialCorrelationAbcdVolatility"];
        foreach (string j in marketModels)
        {
            int[] testedFactors = [4, 8, vars.TodaysForwards.Length];
            foreach (int factors in testedFactors)
            {
                MeasureType[] measures = [MeasureType.MoneyMarket];
                foreach (MeasureType measure in measures)
                {
                    int[] numeraires = vars.MakeMeasure(product, measure);

                    for (int n = 0; n < 1; n++)
                    {
                        var generatorFactory = new SobolBrownianGeneratorFactory(
                            SobolBrownianGeneratorBaseOrdering.Diagonal, CommonVars.Seed);

                        bool logNormal = true;
                        MarketModel marketModel = vars.MakeMarketModel(logNormal, evolution, factors, j);

                        // Adapted: the constrained evolvers take the unbumped model as their reference
                        MarketModel originalModel = marketModel;

                        var evolver = new LogNormalFwdRateEuler(marketModel, generatorFactory, numeraires);
                        var stats = new SequenceStatisticsInc(product.NumberOfProducts);

                        int[] startIndexOfConstraint = new int[evolution.EvolutionTimes.Length];
                        int[] endIndexOfConstraint = new int[evolution.EvolutionTimes.Length];
                        for (int i = 0; i < evolution.EvolutionTimes.Length; ++i)
                        {
                            startIndexOfConstraint[i] = i;
                            endIndexOfConstraint[i] = i + 1;
                        }

                        var constrainedEvolvers = new List<ConstrainedEvolver[]>();
                        var diffWeights = new List<double[][]>();
                        var greekStats = new List<SequenceStatisticsInc[]>();

                        double[][] deltaGammaWeights = [new double[3], new double[3]];
                        SequenceStatisticsInc[] deltaGammaStats =
                            [new(product.NumberOfProducts), new(product.NumberOfProducts)];

                        const double forwardBump = 1.0e-6;
                        marketModel = vars.MakeMarketModel(logNormal, evolution, factors, j, -forwardBump);
                        var down = new LogNormalFwdRateEulerConstrained(marketModel, originalModel, generatorFactory, numeraires);
                        down.SetConstraintType(startIndexOfConstraint, endIndexOfConstraint);
                        marketModel = vars.MakeMarketModel(logNormal, evolution, factors, j, forwardBump);
                        var up = new LogNormalFwdRateEulerConstrained(marketModel, originalModel, generatorFactory, numeraires);
                        up.SetConstraintType(startIndexOfConstraint, endIndexOfConstraint);
                        ConstrainedEvolver[] deltaGammaEvolvers = [down, up];

                        deltaGammaWeights[0][0] = 0.0;
                        deltaGammaWeights[0][1] = -1.0 / (2.0 * forwardBump);
                        deltaGammaWeights[0][2] = 1.0 / (2.0 * forwardBump);

                        deltaGammaWeights[1][0] = -2.0 / (forwardBump * forwardBump);
                        deltaGammaWeights[1][1] = 1.0 / (forwardBump * forwardBump);
                        deltaGammaWeights[1][2] = 1.0 / (forwardBump * forwardBump);

                        double[][] vegaWeights = [new double[3]];
                        SequenceStatisticsInc[] vegaStats = [new(product.NumberOfProducts)];

                        const double volBump = 1.0e-4;
                        marketModel = vars.MakeMarketModel(logNormal, evolution, factors, j, 0.0, -volBump);
                        var volDown = new LogNormalFwdRateEulerConstrained(marketModel, originalModel, generatorFactory, numeraires);
                        volDown.SetConstraintType(startIndexOfConstraint, endIndexOfConstraint);
                        marketModel = vars.MakeMarketModel(logNormal, evolution, factors, j, 0.0, volBump);
                        var volUp = new LogNormalFwdRateEulerConstrained(marketModel, originalModel, generatorFactory, numeraires);
                        volUp.SetConstraintType(startIndexOfConstraint, endIndexOfConstraint);
                        ConstrainedEvolver[] vegaEvolvers = [volDown, volUp];

                        vegaWeights[0][0] = 0.0;
                        vegaWeights[0][1] = -1.0 / (2.0 * volBump);
                        vegaWeights[0][2] = 1.0 / (2.0 * volBump);

                        constrainedEvolvers.Add(deltaGammaEvolvers);
                        diffWeights.Add(deltaGammaWeights);
                        greekStats.Add(deltaGammaStats);

                        constrainedEvolvers.Add(vegaEvolvers);
                        diffWeights.Add(vegaWeights);
                        greekStats.Add(vegaStats);

                        int initialNumeraire = evolver.Numeraires[0];
                        double initialNumeraireValue = vars.TodaysDiscounts[initialNumeraire];

                        var engine = new ProxyGreekEngine(
                            evolver, constrainedEvolvers, diffWeights, startIndexOfConstraint, endIndexOfConstraint, product,
                            initialNumeraireValue);

                        engine.MultiplePathValues(stats, greekStats, CommonVars.Paths);

                        // QuantLib's test prints the values and Greeks beside Black's and asserts nothing: nanoql's facts
                        // assert them (Divergences/ProxyGreekVegaTests.cs)
                    }
                }
            }
        }
    }

    [Fact]
    public void TestPathwiseGreeks()
    {
        // Testing caplet deltas in a lognormal forward rate market model using pathwise method...
        var vars = new CommonVars();

        Payoff[] payoffs = new Payoff[vars.TodaysForwards.Length];
        StrikedTypePayoff[] displacedPayoffs = new StrikedTypePayoff[vars.TodaysForwards.Length];
        for (int i = 0; i < vars.TodaysForwards.Length; ++i)
        {
            payoffs[i] = new PlainVanillaPayoff(OptionType.Call, vars.TodaysForwards[i]);
            displacedPayoffs[i] = new PlainVanillaPayoff(OptionType.Call, vars.TodaysForwards[i] + CommonVars.Displacement);
        }

        for (int whichProduct = 0; whichProduct < 2; ++whichProduct)
        {
            var product1 = new MarketModelPathwiseMultiDeflatedCaplet(vars.RateTimes, vars.Accruals, vars.PaymentTimes, vars.TodaysForwards);

            var product2 = new MarketModelPathwiseMultiCaplet(vars.RateTimes, vars.Accruals, vars.PaymentTimes, vars.TodaysForwards);

            MarketModelPathwiseMultiProduct product = whichProduct == 0 ? product2 : product1;

            var productDummy = new MultiStepOptionlets(vars.RateTimes, vars.Accruals, vars.PaymentTimes, payoffs);

            EvolutionDescription evolution = product.Evolution;

            string[] marketModels = ["ExponentialCorrelationAbcdVolatility"];
            foreach (string j in marketModels)
            {
                int[] testedFactors = [2];
                foreach (int factors in testedFactors)
                {
                    MeasureType[] measures = [MeasureType.MoneyMarket];
                    foreach (MeasureType measure in measures)
                    {
                        int[] numeraires = vars.MakeMeasure(productDummy, measure);

                        for (int n = 0; n < 1; n++)
                        {
                            var generatorFactory = new MTBrownianGeneratorFactory(CommonVars.Seed);

                            bool logNormal = true;
                            MarketModel marketModel = vars.MakeMarketModel(logNormal, evolution, factors, j);

                            var evolver = new LogNormalFwdRateEuler(marketModel, generatorFactory, numeraires);
                            var stats = new SequenceStatisticsInc(product.NumberOfProducts * (vars.TodaysForwards.Length + 1));

                            const double forwardBump = 1.0e-6;

                            int initialNumeraire = evolver.Numeraires[0];
                            double initialNumeraireValue = vars.TodaysDiscounts[initialNumeraire];

                            {
                                var accountingengine = new PathwiseAccountingEngine(evolver, product, marketModel, initialNumeraireValue);

                                accountingengine.MultiplePathValues(stats, CommonVars.Paths);
                            }

                            ImmutableArray<double> valuesAndDeltas = stats.Mean();
                            ImmutableArray<double> errors = stats.ErrorEstimate();

                            double[] prices = new double[product.NumberOfProducts];
                            double[] priceErrors = new double[product.NumberOfProducts];

                            var deltas = new Matrix(product.NumberOfProducts, vars.TodaysForwards.Length);
                            var deltasErrors = new Matrix(product.NumberOfProducts, vars.TodaysForwards.Length);

                            double[] modelPrices = new double[product.NumberOfProducts];

                            for (int i = 0; i < product.NumberOfProducts; ++i)
                            {
                                prices[i] = valuesAndDeltas[i];
                                priceErrors[i] = errors[i];
                                modelPrices[i] = new BlackCalculator(
                                    displacedPayoffs[i],
                                    vars.TodaysForwards[i],
                                    vars.Volatilities[i] * Math.Sqrt(vars.RateTimes[i]),
                                    vars.TodaysDiscounts[i + 1] * (vars.RateTimes[i + 1] - vars.RateTimes[i])).Value;

                                for (int k = 0; k < vars.TodaysForwards.Length; ++k)
                                {
                                    deltas[i, k] = valuesAndDeltas[((i + 1) * product.NumberOfProducts) + k];
                                    deltasErrors[i, k] = errors[((i + 1) * product.NumberOfProducts) + k];
                                }
                            }

                            var modelDeltas = new Matrix(product.NumberOfProducts, vars.TodaysForwards.Length);

                            double[] discPlus = [.. Enumerable.Repeat(vars.TodaysDiscounts[0], vars.TodaysForwards.Length + 1)];
                            double[] discMinus = [.. Enumerable.Repeat(vars.TodaysDiscounts[0], vars.TodaysForwards.Length + 1)];
                            double[] fwdPlus = new double[vars.TodaysForwards.Length];
                            double[] fwdMinus = new double[vars.TodaysForwards.Length];

                            for (int i = 0; i < vars.TodaysForwards.Length; ++i)
                            {
                                for (int k = 0; k < vars.TodaysForwards.Length; ++k)
                                {
                                    if (i != k)
                                    {
                                        fwdPlus[k] = vars.TodaysForwards[k];
                                        fwdMinus[k] = vars.TodaysForwards[k];
                                    }
                                    else
                                    {
                                        fwdPlus[k] = vars.TodaysForwards[k] + forwardBump;
                                        fwdMinus[k] = vars.TodaysForwards[k] - forwardBump;
                                    }

                                    double tau = vars.RateTimes[k + 1] - vars.RateTimes[k];
                                    discPlus[k + 1] = discPlus[k] / (1.0 + (fwdPlus[k] * tau));
                                    discMinus[k + 1] = discMinus[k] / (1.0 + (fwdMinus[k] * tau));
                                }

                                for (int k = 0; k < product.NumberOfProducts; ++k)
                                {
                                    double tau = vars.RateTimes[k + 1] - vars.RateTimes[k];
                                    double priceUp = new BlackCalculator(
                                        displacedPayoffs[k], fwdPlus[k], vars.Volatilities[k] * Math.Sqrt(vars.RateTimes[k]), discPlus[k + 1] * tau).Value;
                                    double priceDown = new BlackCalculator(
                                        displacedPayoffs[k], fwdMinus[k], vars.Volatilities[k] * Math.Sqrt(vars.RateTimes[k]), discMinus[k + 1] * tau).Value;
                                    modelDeltas[k, i] = (priceUp - priceDown) / (2 * forwardBump);
                                }
                            }

                            int numberErrors = 0;
                            for (int i = 0; i < product.NumberOfProducts; ++i)
                            {
                                double thisPrice = prices[i];
                                double thisModelPrice = modelPrices[i];
                                double priceErrorInSds = (thisPrice - thisModelPrice) / priceErrors[i];
                                const double errorTheshold = 3.5;
                                if (Math.Abs(priceErrorInSds) > errorTheshold)
                                {
                                    ++numberErrors;
                                }

                                const double threshold = 1e-10;
                                for (int k = 0; k < vars.TodaysForwards.Length; ++k)
                                {
                                    double delta = deltas[i, k];
                                    double modelDelta = modelDeltas[i, k];

                                    double deltaErrorInSds = 100;
                                    if (deltasErrors[i, k] > 0.0)
                                    {
                                        deltaErrorInSds = (delta - modelDelta) / deltasErrors[i, k];
                                    }
                                    else if (Math.Abs(modelDelta - delta) < threshold)
                                    {
                                        // to cope with zero over zero
                                        deltaErrorInSds = 0.0;
                                    }

                                    if (Math.Abs(deltaErrorInSds) > errorTheshold)
                                    {
                                        ++numberErrors;
                                    }
                                }
                            }

                            if (numberErrors > 0)
                            {
                                Assert.Fail($"Pathwise greeks test has {numberErrors}\n");
                            }
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void TestPathwiseVegas()
    {
        // Testing pathwise vegas in a lognormal forward rate market model...
        var vars = new CommonVars();

        Payoff[] payoffs = new Payoff[vars.TodaysForwards.Length];
        StrikedTypePayoff[] displacedPayoffs = new StrikedTypePayoff[vars.TodaysForwards.Length];
        for (int i = 0; i < vars.TodaysForwards.Length; ++i)
        {
            payoffs[i] = new PlainVanillaPayoff(OptionType.Call, vars.TodaysForwards[i]);
            displacedPayoffs[i] = new PlainVanillaPayoff(OptionType.Call, vars.TodaysForwards[i] + CommonVars.Displacement);
        }

        var product = new MultiStepOptionlets(vars.RateTimes, vars.Accruals, vars.PaymentTimes, payoffs);

        var caplets = new MarketModelPathwiseMultiCaplet(vars.RateTimes, vars.Accruals, vars.PaymentTimes, vars.TodaysForwards);

        var capletsDeflated = new MarketModelPathwiseMultiDeflatedCaplet(vars.RateTimes, vars.Accruals, vars.PaymentTimes, vars.TodaysForwards);

        EvolutionDescription evolution = product.Evolution;

        int steps = evolution.NumberOfSteps;
        int numberRates = evolution.NumberOfRates;
        const double bumpSizeNumericalDifferentiation = 1E-6;
        const double vegaBumpSize = 1e-2;

        // for the numerical differentiation test we are requiring equality on each path so this is actually quite strict
        const int pathsToDo = 10;

        const int pathsToDoSimulation = CommonVars.Paths;
        int bumpIncrement = 1 + (evolution.NumberOfSteps / 3);

        const double numericalBumpSizeForSwaptionPseudo = 1E-7;

        // how many times the bump size squared, the numerical differentation is allowed to differ by
        const double multiplier = 50;
        double maxError = 0.0;

        int numberSwaptionPseudoFailures = 0;
        int numberCapPseudoFailures = 0;
        int numberCapImpVolFailures = 0;
        int numberCapVolPseudoFailures = 0;
        const double swaptionPseudoTolerance = 1e-8;
        const double impVolTolerance = 1e-5;
        double capStrike = vars.MeanForward;
        const double initialNumeraireValue = 0.95;

        string[] marketModels = ["ExponentialCorrelationAbcdVolatility"];

        // test swaption pseudo-root derivatives
        foreach (string j in marketModels)
        {
            int[] testedFactors = [Math.Min(3, vars.TodaysForwards.Length)];
            foreach (int factors in testedFactors)
            {
                bool logNormal = true;
                MarketModel marketModel = vars.MakeMarketModel(logNormal, evolution, factors, j);

                int startIndex = Math.Min(1, evolution.NumberOfRates - 2);
                int endIndex = evolution.NumberOfRates - 1;

                var derivative = new SwaptionPseudoDerivative(marketModel, startIndex, endIndex);

                Matrix[] pseudoRoots = [.. Enumerable.Range(0, marketModel.NumberOfSteps).Select(marketModel.PseudoRoot)];

                for (int step = 0; step < evolution.NumberOfSteps; ++step)
                {
                    for (int l = 0; l < evolution.NumberOfRates; ++l)
                    {
                        for (int f = 0; f < factors; ++f)
                        {
                            pseudoRoots[step][l, f] += numericalBumpSizeForSwaptionPseudo;
                            var bumpedUp = new PseudoRootFacade(pseudoRoots, vars.RateTimes, marketModel.InitialRates.AsSpan(), marketModel.Displacements.AsSpan());
                            double upImpVol = SwapForwardMappings.SwaptionImpliedVolatility(bumpedUp, startIndex, endIndex);

                            pseudoRoots[step][l, f] -= numericalBumpSizeForSwaptionPseudo;
                            pseudoRoots[step][l, f] -= numericalBumpSizeForSwaptionPseudo;
                            var bumpedDown = new PseudoRootFacade(pseudoRoots, vars.RateTimes, marketModel.InitialRates.AsSpan(), marketModel.Displacements.AsSpan());
                            double downImpVol = SwapForwardMappings.SwaptionImpliedVolatility(bumpedDown, startIndex, endIndex);
                            pseudoRoots[step][l, f] += numericalBumpSizeForSwaptionPseudo;

                            double volDeriv = (upImpVol - downImpVol) / (2.0 * numericalBumpSizeForSwaptionPseudo);
                            double modelVal = derivative.VolatilityDerivative(step)[l, f];

                            double error = volDeriv - modelVal;
                            if (Math.Abs(error) > swaptionPseudoTolerance)
                            {
                                ++numberSwaptionPseudoFailures;
                            }
                        }
                    }
                }

                if (numberSwaptionPseudoFailures > 0)
                {
                    Assert.Fail($"swaption pseudo test failed {numberSwaptionPseudoFailures} times");
                }
            }
        }

        // test cap pseudo-root derivatives
        foreach (string j in marketModels)
        {
            int[] testedFactors = [Math.Min(3, vars.TodaysForwards.Length)];
            foreach (int factors in testedFactors)
            {
                bool logNormal = true;
                MarketModel marketModel = vars.MakeMarketModel(logNormal, evolution, factors, j);

                for (int startIndex = 1; startIndex < evolution.NumberOfRates - 1; ++startIndex)
                {
                    for (int endIndex = startIndex + 1; endIndex < evolution.NumberOfRates; ++endIndex)
                    {
                        var derivative = new CapPseudoDerivative(marketModel, capStrike, startIndex, endIndex, initialNumeraireValue);

                        Matrix[] pseudoRoots = [.. Enumerable.Range(0, marketModel.NumberOfSteps).Select(marketModel.PseudoRoot)];

                        for (int step = 0; step < evolution.NumberOfSteps; ++step)
                        {
                            for (int l = 0; l < evolution.NumberOfRates; ++l)
                            {
                                for (int f = 0; f < factors; ++f)
                                {
                                    pseudoRoots[step][l, f] += numericalBumpSizeForSwaptionPseudo;
                                    var bumpedUp = new PseudoRootFacade(pseudoRoots, vars.RateTimes, marketModel.InitialRates.AsSpan(), marketModel.Displacements.AsSpan());
                                    Matrix totalCovUp = bumpedUp.TotalCovariance(marketModel.NumberOfSteps - 1);

                                    pseudoRoots[step][l, f] -= numericalBumpSizeForSwaptionPseudo;
                                    pseudoRoots[step][l, f] -= numericalBumpSizeForSwaptionPseudo;
                                    var bumpedDown = new PseudoRootFacade(pseudoRoots, vars.RateTimes, marketModel.InitialRates.AsSpan(), marketModel.Displacements.AsSpan());
                                    Matrix totalCovDown = bumpedDown.TotalCovariance(marketModel.NumberOfSteps - 1);

                                    pseudoRoots[step][l, f] += numericalBumpSizeForSwaptionPseudo;

                                    double priceDeriv = 0.0;
                                    for (int k = startIndex; k < endIndex; ++k)
                                    {
                                        double upSd = Math.Sqrt(totalCovUp[k, k]);
                                        double downSd = Math.Sqrt(totalCovDown[k, k]);
                                        double annuity = vars.TodaysDiscounts[k + 1] * marketModel.Evolution.RateTaus[k];
                                        double forward = vars.TodaysForwards[k];

                                        double upPrice = BlackFormulaFunctions.BlackFormula(
                                            OptionType.Call, capStrike, forward, upSd, annuity, marketModel.Displacements[k]);

                                        double downPrice = BlackFormulaFunctions.BlackFormula(
                                            OptionType.Call, capStrike, forward, downSd, annuity, marketModel.Displacements[k]);

                                        priceDeriv += (upPrice - downPrice) / (2.0 * numericalBumpSizeForSwaptionPseudo);
                                    }

                                    double modelVal = derivative.PriceDerivative(step)[l, f];

                                    double error = priceDeriv - modelVal;
                                    if (Math.Abs(error) > swaptionPseudoTolerance)
                                    {
                                        ++numberCapPseudoFailures;
                                    }
                                }
                            }
                        }

                        double impVol = derivative.ImpliedVolatility;
                        Matrix totalCov = marketModel.TotalCovariance(evolution.NumberOfSteps - 1);

                        double priceConstVol = 0.0;
                        double priceVarVol = 0.0;
                        for (int m = startIndex; m < endIndex; ++m)
                        {
                            double annuity = vars.TodaysDiscounts[m + 1] * marketModel.Evolution.RateTaus[m];
                            double expiry = vars.RateTimes[m];
                            double forward = vars.TodaysForwards[m];

                            priceConstVol += BlackFormulaFunctions.BlackFormula(
                                OptionType.Call, capStrike, forward, impVol * Math.Sqrt(expiry), annuity, marketModel.Displacements[m]);

                            priceVarVol += BlackFormulaFunctions.BlackFormula(
                                OptionType.Call, capStrike, forward, Math.Sqrt(totalCov[m, m]), annuity, marketModel.Displacements[m]);
                        }

                        if (Math.Abs(priceVarVol - priceConstVol) > impVolTolerance)
                        {
                            ++numberCapImpVolFailures;
                        }
                    }
                }

                if (numberCapPseudoFailures > 0)
                {
                    Assert.Fail($"cap pseudo test failed for prices {numberCapPseudoFailures} times");
                }

                if (numberCapImpVolFailures > 0)
                {
                    Assert.Fail($"cap pseudo test failed for implied vols {numberCapImpVolFailures} times");
                }
            }

            foreach (int factors in testedFactors)
            {
                bool logNormal = true;
                MarketModel marketModel = vars.MakeMarketModel(logNormal, evolution, factors, j);

                for (int startIndex = 1; startIndex < evolution.NumberOfRates - 1; ++startIndex)
                {
                    for (int endIndex = startIndex + 1; endIndex < evolution.NumberOfRates; ++endIndex)
                    {
                        var derivative = new CapPseudoDerivative(marketModel, capStrike, startIndex, endIndex, initialNumeraireValue);

                        Matrix[] pseudoRoots = [.. Enumerable.Range(0, marketModel.NumberOfSteps).Select(marketModel.PseudoRoot)];

                        for (int step = 0; step < evolution.NumberOfSteps; ++step)
                        {
                            for (int l = 0; l < evolution.NumberOfRates; ++l)
                            {
                                for (int f = 0; f < factors; ++f)
                                {
                                    pseudoRoots[step][l, f] += numericalBumpSizeForSwaptionPseudo;
                                    var bumpedUp = new PseudoRootFacade(pseudoRoots, vars.RateTimes, marketModel.InitialRates.AsSpan(), marketModel.Displacements.AsSpan());
                                    var upDerivative = new CapPseudoDerivative(bumpedUp, capStrike, startIndex, endIndex, initialNumeraireValue);
                                    double volUp = upDerivative.ImpliedVolatility;

                                    pseudoRoots[step][l, f] -= numericalBumpSizeForSwaptionPseudo;
                                    pseudoRoots[step][l, f] -= numericalBumpSizeForSwaptionPseudo;
                                    var bumpedDown = new PseudoRootFacade(pseudoRoots, vars.RateTimes, marketModel.InitialRates.AsSpan(), marketModel.Displacements.AsSpan());
                                    var downDerivative = new CapPseudoDerivative(bumpedDown, capStrike, startIndex, endIndex, initialNumeraireValue);
                                    double volDown = downDerivative.ImpliedVolatility;

                                    pseudoRoots[step][l, f] += numericalBumpSizeForSwaptionPseudo;

                                    double volDeriv = (volUp - volDown) / (2.0 * numericalBumpSizeForSwaptionPseudo);

                                    double modelVal = derivative.VolatilityDerivative(step)[l, f];

                                    double error = volDeriv - modelVal;
                                    if (Math.Abs(error) > impVolTolerance * 10)
                                    {
                                        ++numberCapVolPseudoFailures;
                                    }
                                }
                            }
                        }
                    }
                }

                if (numberCapVolPseudoFailures > 0)
                {
                    Assert.Fail($"cap pseudo test failed for implied vols {numberCapVolPseudoFailures} times");
                }
            }
        }

        for (int j = 0; j < marketModels.Length; j++)
        {
            int[] testedFactors = [Math.Min(1, vars.TodaysForwards.Length)];
            foreach (int factors in testedFactors)
            {
                // doing all possible vegas is combinatorially explosive
                int factorsToTest = Math.Min(2, factors);

                MeasureType[] measures = [MeasureType.MoneyMarket];

                // we test the pseudo-root jacobians by bumping each element and comparing analytic with numerical
                var pseudoBumps = new List<Matrix>();
                var pseudoBumpsDown = new List<Matrix>();
                for (int k = 0; k < evolution.NumberOfRates; ++k)
                {
                    for (int f = 0; f < factors; ++f)
                    {
                        var bump = new Matrix(evolution.NumberOfRates, factors, 0.0);
                        bump[k, f] = bumpSizeNumericalDifferentiation;
                        pseudoBumps.Add(bump.Clone());
                        bump[k, f] = -bumpSizeNumericalDifferentiation;
                        pseudoBumpsDown.Add(bump);
                    }
                }

                // we do one bump for each rate and each step
                var vegaBumps = new List<List<Matrix>>();
                var modelBump = new Matrix(evolution.NumberOfRates, factors, 0.0);
                for (int l = 0; l < evolution.NumberOfSteps; ++l)
                {
                    vegaBumps.Add([]);
                    for (int k = 0; k < evolution.NumberOfRates; k += bumpIncrement)
                    {
                        for (int f = 0; f < factorsToTest; ++f)
                        {
                            for (int m = 0; m < evolution.NumberOfSteps; ++m)
                            {
                                if (l == m && k >= l)
                                {
                                    modelBump[k, f] = vegaBumpSize;
                                }

                                vegaBumps[l].Add(modelBump.Clone());
                                modelBump[k, f] = 0.0;
                            }
                        }
                    }
                }

                foreach (MeasureType measure in measures)
                {
                    int[] numeraires = vars.MakeMeasure(product, measure);

                    var testees = new List<RatePseudoRootJacobian>();
                    var testees2 = new List<RatePseudoRootJacobianAllElements>();
                    var testers = new List<RatePseudoRootJacobianNumerical>();
                    var testersDown = new List<RatePseudoRootJacobianNumerical>();

                    var generatorFactory = new MTBrownianGeneratorFactory(CommonVars.Seed);

                    bool logNormal = true;
                    MarketModel marketModel = vars.MakeMarketModel(logNormal, evolution, factors, marketModels[j]);

                    for (int l = 0; l < evolution.NumberOfSteps; ++l)
                    {
                        Matrix pseudoRoot = marketModel.PseudoRoot(l);

                        testees.Add(new RatePseudoRootJacobian(
                            pseudoRoot, evolution.FirstAliveRate[l], numeraires[l], evolution.RateTaus.AsSpan(), pseudoBumps,
                            marketModel.Displacements.AsSpan()));

                        testees2.Add(new RatePseudoRootJacobianAllElements(
                            pseudoRoot, evolution.FirstAliveRate[l], numeraires[l], evolution.RateTaus.AsSpan(), marketModel.Displacements.AsSpan()));

                        testers.Add(new RatePseudoRootJacobianNumerical(
                            pseudoRoot, evolution.FirstAliveRate[l], numeraires[l], evolution.RateTaus.AsSpan(), pseudoBumps,
                            marketModel.Displacements.AsSpan()));

                        testersDown.Add(new RatePseudoRootJacobianNumerical(
                            pseudoRoot, evolution.FirstAliveRate[l], numeraires[l], evolution.RateTaus.AsSpan(), pseudoBumpsDown,
                            marketModel.Displacements.AsSpan()));
                    }

                    BrownianGenerator generator = generatorFactory.Create(factors, steps);
                    var evolver = new LogNormalFwdRateEuler(marketModel, generatorFactory, numeraires);

                    double[] oldRates = new double[evolution.NumberOfRates];
                    double[] newRates = new double[evolution.NumberOfRates];
                    double[] gaussians = new double[factors];

                    int[] numberCashFlowsThisStep = new int[product.NumberOfProducts];
                    var cashFlowsGenerated = new MarketModelMultiProductCashFlow[product.NumberOfProducts][];
                    for (int i = 0; i < product.NumberOfProducts; ++i)
                    {
                        cashFlowsGenerated[i] = new MarketModelMultiProductCashFlow[product.MaxNumberOfCashFlowsPerProductPerStep];
                    }

                    var b = new Matrix(pseudoBumps.Count, evolution.NumberOfRates);
                    var b2 = new Matrix(pseudoBumps.Count, evolution.NumberOfRates);
                    var b3 = new Matrix(pseudoBumps.Count, evolution.NumberOfRates);
                    var b4 = new Matrix(pseudoBumps.Count, evolution.NumberOfRates);

                    Matrix[] globalB = [.. Enumerable.Range(0, steps).Select(_ => new Matrix(evolution.NumberOfRates, factors))];

                    double[] oneStepDFs = new double[evolution.NumberOfRates + 1];
                    oneStepDFs[0] = 1.0;

                    int numberFailures = 0;
                    int numberFailures2 = 0;

                    for (int l = 0; l < pathsToDo; ++l)
                    {
                        evolver.StartNewPath();
                        product.Reset();
                        generator.NextPath();
                        bool done;
                        marketModel.InitialRates.AsSpan().CopyTo(newRates);
                        int currentStep = 0;
                        do
                        {
                            newRates.CopyTo(oldRates, 0);
                            evolver.AdvanceStep();
                            done = product.NextTimeStep(evolver.CurrentState, numberCashFlowsThisStep, cashFlowsGenerated);
                            evolver.CurrentState.ForwardRates().CopyTo(newRates);

                            for (int i = 1; i <= evolution.NumberOfRates; ++i)
                            {
                                oneStepDFs[i] = 1.0 / (1 + (oldRates[i - 1] * evolution.RateTaus[i - 1]));
                            }

                            generator.NextStep(gaussians);

                            testees[currentStep].GetBumps(oldRates, oneStepDFs, newRates, gaussians, b);

                            testees2[currentStep].GetBumps(oldRates, oneStepDFs, newRates, gaussians, globalB);

                            testers[currentStep].GetBumps(oldRates, oneStepDFs, newRates, gaussians, b2);

                            testersDown[currentStep].GetBumps(oldRates, oneStepDFs, newRates, gaussians, b3);

                            for (int i1 = 0; i1 < pseudoBumps.Count; ++i1)
                            {
                                int j1 = 0;
                                for (; j1 < evolution.FirstAliveRate[i1]; ++j1)
                                {
                                    b4[i1, j1] = 0.0;
                                }

                                for (; j1 < numberRates; ++j1)
                                {
                                    double sum = 0.0;
                                    for (int k1 = evolution.FirstAliveRate[i1]; k1 < numberRates; ++k1)
                                    {
                                        for (int f1 = 0; f1 < factors; ++f1)
                                        {
                                            sum += pseudoBumps[i1][k1, f1] * globalB[j1][k1, f1];
                                        }
                                    }

                                    b4[i1, j1] = sum;
                                }
                            }

                            for (int jj = 0; jj < b.Rows; ++jj)
                            {
                                for (int k = 0; k < b.Columns; ++k)
                                {
                                    double analytic = b[jj, k] / bumpSizeNumericalDifferentiation;
                                    double analytic2 = b4[jj, k] / bumpSizeNumericalDifferentiation;
                                    double numerical = (b2[jj, k] - b3[jj, k]) / (2 * bumpSizeNumericalDifferentiation);
                                    double errorSize = (analytic - numerical) / (bumpSizeNumericalDifferentiation * bumpSizeNumericalDifferentiation);
                                    double errorSize2 = (analytic2 - numerical) / (bumpSizeNumericalDifferentiation * bumpSizeNumericalDifferentiation);
                                    maxError = Math.Max(maxError, Math.Abs(errorSize));

                                    if (Math.Abs(errorSize) > multiplier)
                                    {
                                        ++numberFailures;
                                    }

                                    if (Math.Abs(errorSize2) > multiplier)
                                    {
                                        ++numberFailures2;
                                    }
                                }
                            }

                            ++currentStep;
                        }
                        while (!done);
                    }

                    if (numberFailures > 0)
                    {
                        Assert.Fail($"Pathwise rate pseudoroot jacobian test fails : {numberFailures}\n");
                    }

                    if (numberFailures2 > 0)
                    {
                        Assert.Fail($"Pathwise rate pseudoroot jacobian all elements test fails : {numberFailures2}\n");
                    }
                }

                // end of k loop over measures

                // we now test the pathwise vega engine against the analytic vegas
                int numberDeflatedErrors = 0;
                int numberUndeflatedErrors = 0;
                double biggestError = 0.0;

                for (int deflate = 0; deflate < 2; ++deflate)
                {
                    MarketModelPathwiseMultiProduct productToUse = deflate == 0 ? caplets : capletsDeflated;

                    foreach (MeasureType measure in measures)
                    {
                        int[] numeraires = vars.MakeMeasure(product, measure);

                        var generatorFactory = new MTBrownianGeneratorFactory(CommonVars.Seed);

                        bool logNormal = true;
                        MarketModel marketModel = vars.MakeMarketModel(logNormal, evolution, factors, marketModels[j]);

                        var evolver = new LogNormalFwdRateEuler(marketModel, generatorFactory, numeraires);

                        int initialNumeraire = evolver.Numeraires[0];
                        double initialNumeraireValueThis = vars.TodaysDiscounts[initialNumeraire];

                        double[] values;
                        double[] errors;

                        {
                            // Adapted: QuantLib's engine resizes the caller's vectors; nanoql's writes spans of its size
                            var accountingengine = new PathwiseVegasAccountingEngine(
                                new LogNormalFwdRateEuler(marketModel, generatorFactory, numeraires), productToUse, marketModel, vegaBumps,
                                initialNumeraireValueThis);
                            values = new double[accountingengine.NumberOfResults];
                            errors = new double[accountingengine.NumberOfResults];
                            accountingengine.MultiplePathValues(values, errors, pathsToDoSimulation);
                        }

                        var vegasMatrix = new Matrix(caplets.NumberOfProducts, vegaBumps[0].Count);
                        var standardErrors = new Matrix(caplets.NumberOfProducts, vegaBumps[0].Count);
                        var deltasMatrix = new Matrix(caplets.NumberOfProducts, numberRates);
                        var deltasErrors = new Matrix(caplets.NumberOfProducts, numberRates);
                        double[] prices = new double[caplets.NumberOfProducts];
                        double[] priceErrors = new double[caplets.NumberOfProducts];

                        int entriesPerProduct = 1 + numberRates + vegaBumps[0].Count;

                        for (int i = 0; i < caplets.NumberOfProducts; ++i)
                        {
                            prices[i] = values[i * entriesPerProduct];
                            priceErrors[i] = errors[i * entriesPerProduct];

                            for (int k = 0; k < vegaBumps[0].Count; ++k)
                            {
                                vegasMatrix[i, k] = values[(i * entriesPerProduct) + numberRates + 1 + k];
                                standardErrors[i, k] = errors[(i * entriesPerProduct) + numberRates + 1 + k];
                            }

                            for (int k = 0; k < numberRates; ++k)
                            {
                                deltasMatrix[i, k] = values[(i * entriesPerProduct) + 1 + k];
                                deltasErrors[i, k] = errors[(i * entriesPerProduct) + 1 + k];
                            }
                        }

                        Matrix totalCovariance = marketModel.TotalCovariance(marketModel.NumberOfSteps - 1);

                        double[] truePrices = new double[caplets.NumberOfProducts];
                        for (int r = 0; r < truePrices.Length; ++r)
                        {
                            truePrices[r] = new BlackCalculator(
                                displacedPayoffs[r], vars.TodaysForwards[r], Math.Sqrt(totalCovariance[r, r]),
                                vars.TodaysDiscounts[r + 1] * (vars.RateTimes[r + 1] - vars.RateTimes[r])).Value;
                        }

                        for (int bIndex = 0; bIndex < vegaBumps[0].Count; ++bIndex)
                        {
                            double[] bumpedPrices = new double[truePrices.Length];
                            double[] variances = new double[truePrices.Length];
                            double[] vegas = new double[truePrices.Length];

                            for (int step = 0; step < marketModel.NumberOfSteps; ++step)
                            {
                                Matrix pseudoRoot = marketModel.PseudoRoot(step);
                                pseudoRoot += vegaBumps[step][bIndex];

                                for (int rate = step; rate < marketModel.NumberOfRates; ++rate)
                                {
                                    double variance = 0.0;
                                    for (int f = 0; f < marketModel.NumberOfFactors; ++f)
                                    {
                                        variance += pseudoRoot[rate, f] * pseudoRoot[rate, f];
                                    }

                                    variances[rate] += variance;
                                }
                            }

                            for (int r = 0; r < truePrices.Length; ++r)
                            {
                                bumpedPrices[r] = new BlackCalculator(
                                    displacedPayoffs[r], vars.TodaysForwards[r], Math.Sqrt(variances[r]),
                                    vars.TodaysDiscounts[r + 1] * (vars.RateTimes[r + 1] - vars.RateTimes[r])).Value;
                                vegas[r] = bumpedPrices[r] - truePrices[r];
                            }

                            for (int s = 0; s < truePrices.Length; ++s)
                            {
                                double mcVega = vegasMatrix[s, bIndex];
                                double analyticVega = vegas[s];
                                double thisError = mcVega - analyticVega;
                                double thisSE = standardErrors[s, bIndex];
                                if (Math.Abs(thisError) > 0.0)
                                {
                                    double errorInSEs = thisError / thisSE;
                                    biggestError = Math.Max(Math.Abs(errorInSEs), biggestError);
                                    if (Math.Abs(errorInSEs) > 4.5)
                                    {
                                        if (deflate == 0)
                                        {
                                            ++numberUndeflatedErrors;
                                        }
                                        else
                                        {
                                            ++numberDeflatedErrors;
                                        }
                                    }
                                }
                            }
                        }

                        // check the pathwise vega engine's prices and deltas against the pathwise delta engine's
                        MarketModelPathwiseMultiProduct productToUse2 = deflate == 0 ? caplets : capletsDeflated;

                        var stats = new SequenceStatisticsInc(productToUse2.NumberOfProducts * (vars.TodaysForwards.Length + 1));
                        {
                            var accountingengine = new PathwiseAccountingEngine(evolver, productToUse2, marketModel, initialNumeraireValueThis);

                            accountingengine.MultiplePathValues(stats, pathsToDoSimulation);
                        }

                        ImmutableArray<double> valuesAndDeltas2 = stats.Mean();
                        ImmutableArray<double> errors2 = stats.ErrorEstimate();

                        double[] prices2 = new double[productToUse2.NumberOfProducts];
                        double[] priceErrors2 = new double[productToUse2.NumberOfProducts];
                        var deltas2 = new Matrix(productToUse2.NumberOfProducts, vars.TodaysForwards.Length);
                        var deltasErrors2 = new Matrix(productToUse2.NumberOfProducts, vars.TodaysForwards.Length);

                        for (int i = 0; i < productToUse2.NumberOfProducts; ++i)
                        {
                            prices2[i] = valuesAndDeltas2[i];
                            priceErrors2[i] = errors2[i];
                            for (int k = 0; k < vars.TodaysForwards.Length; ++k)
                            {
                                deltas2[i, k] = valuesAndDeltas2[((i + 1) * productToUse2.NumberOfProducts) + k];
                                deltasErrors2[i, k] = errors2[((i + 1) * productToUse2.NumberOfProducts) + k];
                            }
                        }

                        for (int i = 0; i < productToUse2.NumberOfProducts; ++i)
                        {
                            double priceDiff = prices2[i] - prices[i];

                            // two sets of standard error
                            if (Math.Abs(priceDiff) > 5 * priceErrors2[i])
                            {
                                Assert.Fail(
                                    "pathwise accounting engine and pathwise vegas accounting engine not in perfect agreement for price.\n"
                                    + $" product {i},  vega computed price: {prices[i]} previous price {prices2[i]}, deflate {deflate}\n");
                            }

                            for (int k = 0; k < vars.TodaysForwards.Length; ++k)
                            {
                                double error = deltas2[i, k] - deltasMatrix[i, k];

                                // two sets of standard error
                                if (Math.Abs(error) > 5 * deltasErrors2[i, k])
                                {
                                    Assert.Fail(
                                        "pathwise accounting engine and pathwise vegas accounting engine not in perfect agreement for dealts.\n"
                                        + $" product {i}, rate {k} vega computed delta: {deltasMatrix[i, k]} previous delta {deltas2[i, k]}\n");
                                }
                            }
                        }
                    }

                    // end of k loop over measures
                }

                // end of loop over deflation
                if (numberDeflatedErrors + numberUndeflatedErrors > 0)
                {
                    Assert.Fail(
                        $"Model pathwise vega test for caplets fails : {numberDeflatedErrors} deflated errors and {numberUndeflatedErrors}"
                        + $"  undeflated errors , biggest error in SEs is {biggestError}\n");
                }

                // ok now try with caps
                {
                    var caps = new List<VolatilityBumpInstrumentJacobianCap>();

                    double capStrikeCaps = vars.TodaysForwards[0];

                    for (int i = 0; i + 2 < numberRates; i += 3)
                    {
                        caps.Add(new VolatilityBumpInstrumentJacobianCap(i + 2, i + 3, capStrikeCaps));
                    }

                    (int, int)[] startsAndEnds = [.. caps.Select(c => (c.StartIndex, c.EndIndex))];

                    var capsDeflated = new MarketModelPathwiseMultiDeflatedCap(
                        vars.RateTimes, vars.Accruals, vars.PaymentTimes, capStrikeCaps, startsAndEnds);

                    foreach (MeasureType measure in measures)
                    {
                        int[] numeraires = vars.MakeMeasure(product, measure);

                        var generatorFactory = new MTBrownianGeneratorFactory(CommonVars.Seed);
                        var generatorFactory2 = new MTBrownianGeneratorFactory(CommonVars.Seed);

                        bool logNormal = true;
                        MarketModel marketModel = vars.MakeMarketModel(logNormal, evolution, factors, marketModels[j]);

                        var evolver = new LogNormalFwdRateEuler(marketModel, generatorFactory, numeraires);

                        var evolver2 = new LogNormalFwdRateEuler(marketModel, generatorFactory2, numeraires);

                        int initialNumeraire = evolver.Numeraires[0];
                        double initialNumeraireValueThis = vars.TodaysDiscounts[initialNumeraire];

                        double[] values;
                        double[] errors;
                        double[] values2;
                        double[] errors2;

                        {
                            var accountingengine = new PathwiseVegasOuterAccountingEngine(
                                evolver2, capsDeflated, marketModel, vegaBumps, initialNumeraireValueThis);
                            values2 = new double[accountingengine.NumberOfResults];
                            errors2 = new double[accountingengine.NumberOfResults];
                            accountingengine.MultiplePathValues(values2, errors2, pathsToDoSimulation);
                        }

                        {
                            var accountingengine = new PathwiseVegasAccountingEngine(
                                evolver, capsDeflated, marketModel, vegaBumps, initialNumeraireValueThis);
                            values = new double[accountingengine.NumberOfResults];
                            errors = new double[accountingengine.NumberOfResults];
                            accountingengine.MultiplePathValues(values, errors, pathsToDoSimulation);
                        }

                        {
                            const double tol = 1E-8;
                            int numberMeanFailures = 0;
                            for (int i = 0; i < values.Length; ++i)
                            {
                                if (Math.Abs(values[i] - values2[i]) > tol)
                                {
                                    ++numberMeanFailures;
                                }
                            }

                            if (numberMeanFailures > 0)
                            {
                                Assert.Fail(
                                    "Comparison of Pathwise vegas accounting engine and PathwiseVegasOuterAccountingEngine yields discrepancies:"
                                    + $"{numberMeanFailures}  out of {values.Length}");
                            }
                        }

                        var vegasMatrix = new Matrix(capsDeflated.NumberOfProducts, vegaBumps[0].Count);
                        var standardErrors = new Matrix(capsDeflated.NumberOfProducts, vegaBumps[0].Count);

                        int entriesPerProduct = 1 + numberRates + vegaBumps[0].Count;

                        for (int i = 0; i < capsDeflated.NumberOfProducts; ++i)
                        {
                            for (int k = 0; k < vegaBumps[0].Count; ++k)
                            {
                                vegasMatrix[i, k] = values[(i * entriesPerProduct) + numberRates + 1 + k];
                                standardErrors[i, k] = errors[(i * entriesPerProduct) + numberRates + 1 + k];
                            }
                        }

                        Matrix totalCovariance = marketModel.TotalCovariance(marketModel.NumberOfSteps - 1);

                        double[] trueCapletPrices = new double[numberRates];

                        StrikedTypePayoff dispayoff = new PlainVanillaPayoff(OptionType.Call, capStrikeCaps + CommonVars.Displacement);

                        for (int r = 0; r < trueCapletPrices.Length; ++r)
                        {
                            trueCapletPrices[r] = new BlackCalculator(
                                dispayoff, vars.TodaysForwards[r], Math.Sqrt(totalCovariance[r, r]),
                                vars.TodaysDiscounts[r + 1] * (vars.RateTimes[r + 1] - vars.RateTimes[r])).Value;
                        }

                        double[] trueCapPrices = new double[capsDeflated.NumberOfProducts];
                        double[] vegaCaps = new double[capsDeflated.NumberOfProducts];
                        for (int s = 0; s < capsDeflated.NumberOfProducts; ++s)
                        {
                            trueCapPrices[s] = 0.0;
                            for (int t = caps[s].StartIndex; t < caps[s].EndIndex; ++t)
                            {
                                trueCapPrices[s] += trueCapletPrices[t];
                            }
                        }

                        int numberErrors = 0;

                        for (int bIndex = 0; bIndex < vegaBumps[0].Count; ++bIndex)
                        {
                            double[] bumpedCapletPrices = new double[trueCapletPrices.Length];
                            double[] variances = new double[trueCapletPrices.Length];
                            double[] vegasCaplets = new double[trueCapletPrices.Length];

                            for (int step = 0; step < marketModel.NumberOfSteps; ++step)
                            {
                                Matrix pseudoRoot = marketModel.PseudoRoot(step);
                                pseudoRoot += vegaBumps[step][bIndex];

                                for (int rate = step; rate < marketModel.NumberOfRates; ++rate)
                                {
                                    double variance = 0.0;
                                    for (int f = 0; f < marketModel.NumberOfFactors; ++f)
                                    {
                                        variance += pseudoRoot[rate, f] * pseudoRoot[rate, f];
                                    }

                                    variances[rate] += variance;
                                }
                            }

                            for (int r = 0; r < trueCapletPrices.Length; ++r)
                            {
                                bumpedCapletPrices[r] = new BlackCalculator(
                                    dispayoff, vars.TodaysForwards[r], Math.Sqrt(variances[r]),
                                    vars.TodaysDiscounts[r + 1] * (vars.RateTimes[r + 1] - vars.RateTimes[r])).Value;
                                vegasCaplets[r] = bumpedCapletPrices[r] - trueCapletPrices[r];
                            }

                            for (int s = 0; s < capsDeflated.NumberOfProducts; ++s)
                            {
                                vegaCaps[s] = 0.0;
                                for (int t = caps[s].StartIndex; t < caps[s].EndIndex; ++t)
                                {
                                    vegaCaps[s] += vegasCaplets[t];
                                }
                            }

                            for (int s = 0; s < capsDeflated.NumberOfProducts; ++s)
                            {
                                double mcVega = vegasMatrix[s, bIndex];
                                double analyticVega = vegaCaps[s];
                                double thisError = mcVega - analyticVega;
                                double thisSE = standardErrors[s, bIndex];
                                if (Math.Abs(thisError) > 0.0)
                                {
                                    double errorInSEs = Math.Abs(thisError / thisSE);
                                    if (errorInSEs > 4.0)
                                    {
                                        ++numberErrors;
                                    }
                                }
                            }
                        }

                        if (numberErrors > 0)
                        {
                            Assert.Fail($"caps Pathwise vega test fails : {numberErrors}\n");
                        }
                    }

                    // end of k loop over measures
                }
            }
        }
    }

    [Fact]
    public void TestPathwiseMarketVegas()
    {
        // Testing pathwise market vegas in a lognormal forward rate market model...
        var vars = new CommonVars();

        var cs = new LMMCurveState(vars.RateTimes);
        cs.SetOnForwardRates(vars.TodaysForwards);

        Payoff[] payoffs = new Payoff[vars.TodaysForwards.Length];
        for (int i = 0; i < vars.TodaysForwards.Length; ++i)
        {
            payoffs[i] = new PlainVanillaPayoff(OptionType.Call, cs.CoterminalSwapRate(i));
        }

        var dummyProduct = new MultiStepOptionlets(vars.RateTimes, vars.Accruals, vars.PaymentTimes, payoffs);

        const double bumpSizeNumericalDifferentiation = 1E-6;

        var swaptionsDeflated = new MarketModelPathwiseCoterminalSwaptionsDeflated(vars.RateTimes, cs.CoterminalSwapRates());

        var swaptionsDeflated2 = new MarketModelPathwiseCoterminalSwaptionsNumericalDeflated(
            vars.RateTimes, cs.CoterminalSwapRates(), bumpSizeNumericalDifferentiation);

        EvolutionDescription evolution = dummyProduct.Evolution;

        int steps = evolution.NumberOfSteps;
        int numberRates = evolution.NumberOfRates;

        // for the numerical differentiation test we are requiring equality on each path so this is actually quite strict
        const int pathsToDo = 10;

        const int pathsToDoSimulation = CommonVars.Paths;

        // how many times the bump size squared, the numerical differentation is allowed to differ by
        const double multiplier = 50;

        const double tolerance = 1E-6;
        const double initialNumeraireValue = 0.95;

        bool allowFactorwiseBumping = true;

        var caps = new List<VolatilityBumpInstrumentJacobianCap>();

        double capStrike = vars.TodaysForwards[0];

        for (int i = 0; i + 2 < numberRates; i += 3)
        {
            caps.Add(new VolatilityBumpInstrumentJacobianCap(i, i + 3, capStrike));
        }

        (int, int)[] startsAndEnds = [.. caps.Select(c => (c.StartIndex, c.EndIndex))];

        var capsDeflated = new MarketModelPathwiseMultiDeflatedCap(vars.RateTimes, vars.Accruals, vars.PaymentTimes, capStrike, startsAndEnds);

        VolatilityBumpInstrumentJacobianSwaption[] swaptions =
            [.. Enumerable.Range(0, numberRates).Select(i => new VolatilityBumpInstrumentJacobianSwaption(i, numberRates))];

        string[] marketModels = ["ExponentialCorrelationAbcdVolatility"];

        // first we check the bumps' implied volatility derivatives
        foreach (string j in marketModels)
        {
            int[] testedFactors = [Math.Min(1, vars.TodaysForwards.Length)];
            foreach (int factors in testedFactors)
            {
                bool logNormal = true;
                MarketModel marketModel = vars.MakeMarketModel(logNormal, evolution, factors, j);

                var possibleBumps = new VegaBumpCollection(marketModel, allowFactorwiseBumping);

                var bumpFinder = new OrthogonalizedBumpFinder(possibleBumps, swaptions, caps, multiplier, tolerance);

                Matrix[][] theBumps = bumpFinder.GetVegaBumps();

                {
                    var swaptionVegasMatrix = new Matrix(swaptionsDeflated.NumberOfProducts, theBumps[0].Length);

                    for (int i = 0; i < swaptionsDeflated.NumberOfProducts; ++i)
                    {
                        var thisPseudoDerivative = new SwaptionPseudoDerivative(marketModel, swaptions[i].StartIndex, swaptions[i].EndIndex);

                        for (int jj = 0; jj < theBumps[0].Length; ++jj)
                        {
                            swaptionVegasMatrix[i, jj] = 0;
                            for (int k = 0; k < steps; ++k)
                            {
                                Matrix volatilityDerivative = thisPseudoDerivative.VolatilityDerivative(k);
                                for (int l = 0; l < numberRates; ++l)
                                {
                                    for (int m = 0; m < factors; ++m)
                                    {
                                        swaptionVegasMatrix[i, jj] += theBumps[k][jj][l, m] * volatilityDerivative[l, m];
                                    }
                                }
                            }
                        }
                    }

                    int numberDiagonalFailures = 0;
                    int offDiagonalFailures = 0;

                    for (int i = 0; i < swaptions.Length; ++i)
                    {
                        for (int jj = 0; jj < theBumps[0].Length; ++jj)
                        {
                            if (i == jj)
                            {
                                double thisError = swaptionVegasMatrix[i, i] - 0.01;
                                if (Math.Abs(thisError) > 1e-8)
                                {
                                    ++numberDiagonalFailures;
                                }
                            }
                            else
                            {
                                double thisError = swaptionVegasMatrix[i, jj];
                                if (Math.Abs(thisError) > 1e-8)
                                {
                                    ++offDiagonalFailures;
                                }
                            }
                        }
                    }

                    if (numberDiagonalFailures + offDiagonalFailures > 0)
                    {
                        Assert.Fail(
                            $"Pathwise market vega analytic test fails for  swaptions : {offDiagonalFailures} off diagonal failures \n "
                            + $" and {numberDiagonalFailures} on the diagonal.");
                    }
                }

                var capsVegasMatrix = new Matrix(caps.Count, theBumps[0].Length);

                for (int i = 0; i < caps.Count; ++i)
                {
                    var thisPseudoDerivative = new CapPseudoDerivative(
                        marketModel, caps[i].Strike, caps[i].StartIndex, caps[i].EndIndex, initialNumeraireValue);

                    for (int jj = 0; jj < theBumps[0].Length; ++jj)
                    {
                        capsVegasMatrix[i, jj] = 0;
                        for (int k = 0; k < steps; ++k)
                        {
                            Matrix volatilityDerivative = thisPseudoDerivative.VolatilityDerivative(k);
                            for (int l = 0; l < numberRates; ++l)
                            {
                                for (int m = 0; m < factors; ++m)
                                {
                                    capsVegasMatrix[i, jj] += theBumps[k][jj][l, m] * volatilityDerivative[l, m];
                                }
                            }
                        }
                    }
                }

                {
                    int numberDiagonalFailures = 0;
                    int offDiagonalFailures = 0;

                    for (int i = 0; i < caps.Count; ++i)
                    {
                        for (int jj = 0; jj < theBumps[0].Length; ++jj)
                        {
                            if (i + swaptions.Length == jj)
                            {
                                double thisError = capsVegasMatrix[i, jj] - 0.01;
                                if (Math.Abs(thisError) > 1e-8)
                                {
                                    ++numberDiagonalFailures;
                                }
                            }
                            else
                            {
                                double thisError = capsVegasMatrix[i, jj];
                                if (Math.Abs(thisError) > 1e-8)
                                {
                                    ++offDiagonalFailures;
                                }
                            }
                        }
                    }

                    if (numberDiagonalFailures + offDiagonalFailures > 0)
                    {
                        Assert.Fail(
                            $"Pathwise market vega analytic test fails for caps : {offDiagonalFailures} off diagonal failures \n "
                            + $" and {numberDiagonalFailures} on the diagonal.");
                    }
                }
            }
        }

        // check the swaptions' pathwise derivatives against their numerical twin's
        int[] numberCashFlowsThisStep1 = new int[swaptionsDeflated.NumberOfProducts];
        var cashFlowsGenerated1 = new MarketModelPathwiseMultiProductCashFlow[swaptionsDeflated.NumberOfProducts][];
        int[] numberCashFlowsThisStep2 = new int[swaptionsDeflated.NumberOfProducts];
        var cashFlowsGenerated2 = new MarketModelPathwiseMultiProductCashFlow[swaptionsDeflated.NumberOfProducts][];
        for (int i = 0; i < swaptionsDeflated.NumberOfProducts; ++i)
        {
            cashFlowsGenerated1[i] = new MarketModelPathwiseMultiProductCashFlow[swaptionsDeflated.MaxNumberOfCashFlowsPerProductPerStep];
            cashFlowsGenerated2[i] = new MarketModelPathwiseMultiProductCashFlow[swaptionsDeflated.MaxNumberOfCashFlowsPerProductPerStep];
            for (int k = 0; k < swaptionsDeflated.MaxNumberOfCashFlowsPerProductPerStep; ++k)
            {
                cashFlowsGenerated1[i][k] = new MarketModelPathwiseMultiProductCashFlow(numberRates + 1);
                cashFlowsGenerated2[i][k] = new MarketModelPathwiseMultiProductCashFlow(numberRates + 1);
            }
        }

        foreach (string j in marketModels)
        {
            int[] testedFactors = [Math.Min(1, vars.TodaysForwards.Length)];
            foreach (int factors in testedFactors)
            {
                var generatorFactory = new MTBrownianGeneratorFactory(CommonVars.Seed);

                bool logNormal = true;
                MarketModel marketModel = vars.MakeMarketModel(logNormal, evolution, factors, j);

                var evolver1 = new LogNormalFwdRateEuler(marketModel, generatorFactory, swaptionsDeflated.SuggestedNumeraires().AsSpan());
                var evolver2 = new LogNormalFwdRateEuler(marketModel, generatorFactory, swaptionsDeflated.SuggestedNumeraires().AsSpan());

                for (int p = 0; p < pathsToDo; ++p)
                {
                    evolver1.StartNewPath();
                    swaptionsDeflated.Reset();

                    evolver2.StartNewPath();
                    swaptionsDeflated2.Reset();

                    int step = 0;
                    bool done;
                    do
                    {
                        evolver1.AdvanceStep();
                        done = swaptionsDeflated.NextTimeStep(evolver1.CurrentState, numberCashFlowsThisStep1, cashFlowsGenerated1);

                        evolver2.AdvanceStep();
                        bool done2 = swaptionsDeflated2.NextTimeStep(evolver2.CurrentState, numberCashFlowsThisStep2, cashFlowsGenerated2);

                        if (done != done2)
                        {
                            Assert.Fail("numerical swaptions derivative and swaptions disagree on termination");
                        }

                        for (int prod = 0; prod < swaptionsDeflated.NumberOfProducts; ++prod)
                        {
                            if (numberCashFlowsThisStep1[prod] != numberCashFlowsThisStep2[prod])
                            {
                                Assert.Fail("numerical swaptions derivative and swaptions disagree on number of cash flows");
                            }

                            for (int cf = 0; cf < numberCashFlowsThisStep1[prod]; ++cf)
                            {
                                for (int rate = 0; rate <= numberRates; ++rate)
                                {
                                    if (Math.Abs(cashFlowsGenerated1[prod][cf].Amount[rate] - cashFlowsGenerated2[prod][cf].Amount[rate]) > tolerance)
                                    {
                                        Assert.Fail(
                                            $"numerical swaptions derivative and swaptions disagree on cash flow size. cf = {cf}step {step}, rate {rate},"
                                            + $" amount1 {cashFlowsGenerated1[prod][cf].Amount[rate]} ,amount2 {cashFlowsGenerated2[prod][cf].Amount[rate]}\n");
                                    }
                                }
                            }
                        }

                        ++step;
                    }
                    while (!done);
                }
            }
        }

        // the swaptions' market vegas
        foreach (string j in marketModels)
        {
            int[] testedFactors = [Math.Min(1, vars.TodaysForwards.Length)];
            foreach (int factors in testedFactors)
            {
                var generatorFactory = new MTBrownianGeneratorFactory(CommonVars.Seed);

                bool logNormal = true;
                MarketModel marketModel = vars.MakeMarketModel(logNormal, evolution, factors, j);

                var evolver = new LogNormalFwdRateEuler(marketModel, generatorFactory, swaptionsDeflated.SuggestedNumeraires().AsSpan());

                int initialNumeraire = evolver.Numeraires[0];
                double initialNumeraireValueThis = vars.TodaysDiscounts[initialNumeraire];

                var possibleBumps = new VegaBumpCollection(marketModel, allowFactorwiseBumping);

                var bumpFinder = new OrthogonalizedBumpFinder(possibleBumps, swaptions, caps, multiplier, tolerance);

                Matrix[][] theBumps = bumpFinder.GetVegaBumps();

                double[] values;
                double[] errors;

                {
                    // Adapted: QuantLib's engine resizes the caller's vectors; nanoql's writes spans of its size
                    var accountingEngine = new PathwiseVegasAccountingEngine(
                        evolver, swaptionsDeflated, marketModel, theBumps, initialNumeraireValueThis);
                    values = new double[accountingEngine.NumberOfResults];
                    errors = new double[accountingEngine.NumberOfResults];
                    accountingEngine.MultiplePathValues(values, errors, pathsToDoSimulation);
                }

                var vegasMatrix = new Matrix(swaptionsDeflated.NumberOfProducts, theBumps[0].Length);
                var standardErrors = new Matrix(swaptionsDeflated.NumberOfProducts, theBumps[0].Length);

                int entriesPerProduct = 1 + numberRates + theBumps[0].Length;

                for (int i = 0; i < swaptionsDeflated.NumberOfProducts; ++i)
                {
                    for (int jj = 0; jj < theBumps[0].Length; ++jj)
                    {
                        vegasMatrix[i, jj] = values[(i * entriesPerProduct) + numberRates + 1 + jj];
                        standardErrors[i, jj] = errors[(i * entriesPerProduct) + numberRates + 1 + jj];
                    }
                }

                double[] impliedVols = new double[swaptions.Length];
                for (int i = 0; i < swaptions.Length; ++i)
                {
                    impliedVols[i] = SwapForwardMappings.SwaptionImpliedVolatility(marketModel, swaptions[i].StartIndex, swaptions[i].EndIndex);
                }

                double[] analyticVegas = new double[swaptions.Length];

                for (int i = 0; i < swaptions.Length; ++i)
                {
                    double swapRate = cs.CoterminalSwapRates()[i];
                    double annuity = cs.CoterminalSwapAnnuity(0, i) * initialNumeraireValueThis;
                    double expiry = vars.RateTimes[i];
                    double sd = impliedVols[i] * Math.Sqrt(expiry);
                    const double swapDisplacement = 0.0;
                    double vega = BlackFormulaFunctions.BlackFormulaVolDerivative(swapRate, swapRate, sd, expiry, annuity, swapDisplacement);

                    // one percent move
                    analyticVegas[i] = vega * 0.01;
                }

                int numberDiagonalFailures = 0;
                int offDiagonalFailures = 0;

                for (int i = 0; i < swaptions.Length; ++i)
                {
                    double thisError = vegasMatrix[i, i] - analyticVegas[i];

                    // silly to penalize for tiny standard error
                    double thisErrorInSds = thisError / (standardErrors[i, i] + 1E-6);
                    if (Math.Abs(thisErrorInSds) > 4)
                    {
                        ++numberDiagonalFailures;
                    }
                }

                for (int i = 0; i < swaptions.Length; ++i)
                {
                    for (int jj = 0; jj < theBumps[0].Length; ++jj)
                    {
                        if (i != jj)
                        {
                            // true value is zero
                            double thisError = vegasMatrix[i, jj];
                            double thisErrorInSds = thisError / (standardErrors[i, jj] + 1E-6);
                            if (Math.Abs(thisErrorInSds) > 3.5)
                            {
                                ++offDiagonalFailures;
                            }
                        }
                    }
                }

                if (offDiagonalFailures + numberDiagonalFailures > 0)
                {
                    Assert.Fail(
                        $"Pathwise market vega test fails for coterminal swaptions : {offDiagonalFailures} off diagonal failures \n "
                        + $" and {numberDiagonalFailures} on the diagonal.");
                }
            }
        }

        // the caps' market vegas
        foreach (string j in marketModels)
        {
            int[] testedFactors = [Math.Min(2, vars.TodaysForwards.Length)];
            foreach (int factors in testedFactors)
            {
                var generatorFactory = new MTBrownianGeneratorFactory(CommonVars.Seed);

                bool logNormal = true;
                MarketModel marketModel = vars.MakeMarketModel(logNormal, evolution, factors, j);

                var evolver = new LogNormalFwdRateEuler(marketModel, generatorFactory, capsDeflated.SuggestedNumeraires().AsSpan());

                int initialNumeraire = evolver.Numeraires[0];
                double initialNumeraireValueThis = vars.TodaysDiscounts[initialNumeraire];

                var possibleBumps = new VegaBumpCollection(marketModel, allowFactorwiseBumping);

                var bumpFinder = new OrthogonalizedBumpFinder(possibleBumps, swaptions, caps, multiplier, tolerance);

                Matrix[][] theBumps = bumpFinder.GetVegaBumps();

                double[] values;
                double[] errors;

                {
                    var accountingEngine = new PathwiseVegasAccountingEngine(evolver, capsDeflated, marketModel, theBumps, initialNumeraireValueThis);
                    values = new double[accountingEngine.NumberOfResults];
                    errors = new double[accountingEngine.NumberOfResults];
                    accountingEngine.MultiplePathValues(values, errors, pathsToDoSimulation);
                }

                var vegasMatrix = new Matrix(capsDeflated.NumberOfProducts, theBumps[0].Length);
                var standardErrors = new Matrix(capsDeflated.NumberOfProducts, theBumps[0].Length);

                int entriesPerProduct = 1 + numberRates + theBumps[0].Length;

                for (int i = 0; i < capsDeflated.NumberOfProducts; ++i)
                {
                    for (int jj = 0; jj < theBumps[0].Length; ++jj)
                    {
                        vegasMatrix[i, jj] = values[(i * entriesPerProduct) + numberRates + jj + 1];
                        standardErrors[i, jj] = errors[(i * entriesPerProduct) + numberRates + jj + 1];
                    }
                }

                double[] impliedVols = new double[caps.Count];
                double[] analyticVegas = new double[caps.Count];

                for (int i = 0; i < caps.Count; ++i)
                {
                    var capPseudo = new CapPseudoDerivative(marketModel, caps[i].Strike, caps[i].StartIndex, caps[i].EndIndex, initialNumeraireValueThis);

                    impliedVols[i] = capPseudo.ImpliedVolatility;

                    double vega = 0.0;
                    for (int k = caps[i].StartIndex; k < caps[i].EndIndex; ++k)
                    {
                        double forward = cs.ForwardRates()[k];
                        double annuity = cs.DiscountRatio(k + 1, 0) * initialNumeraireValueThis * vars.Accruals[k];
                        double expiry = vars.RateTimes[k];
                        double sd = impliedVols[i] * Math.Sqrt(expiry);
                        const double displacement = 0.0;

                        double capletVega = BlackFormulaFunctions.BlackFormulaVolDerivative(caps[i].Strike, forward, sd, expiry, annuity, displacement);

                        vega += capletVega;
                    }

                    // one percent move
                    analyticVegas[i] = vega * 0.01;
                }

                int numberDiagonalFailures = 0;
                int offDiagonalFailures = 0;

                for (int i = 0; i < caps.Count; ++i)
                {
                    double thisError = vegasMatrix[i, i + swaptions.Length] - analyticVegas[i];

                    // silly to penalize for tiny standard error
                    double thisErrorInSds = thisError / (standardErrors[i, i + swaptions.Length] + 1E-6);
                    if (Math.Abs(thisErrorInSds) > 4)
                    {
                        ++numberDiagonalFailures;
                    }
                }

                for (int i = 0; i < caps.Count; ++i)
                {
                    for (int jj = 0; jj < theBumps[0].Length; ++jj)
                    {
                        if (i + swaptions.Length != jj)
                        {
                            // true value is zero
                            double thisError = vegasMatrix[i, jj];
                            double thisErrorInSds = thisError / (standardErrors[i, jj] + 1E-6);
                            if (Math.Abs(thisErrorInSds) > 3.5)
                            {
                                ++offDiagonalFailures;
                            }
                        }
                    }
                }

                if (offDiagonalFailures + numberDiagonalFailures > 0)
                {
                    Assert.Fail(
                        $"Pathwise market vega test fails for caps: {offDiagonalFailures} off diagonal failures \n "
                        + $" and {numberDiagonalFailures} on the diagonal.");
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
            MeanForward = 0.0;
            for (int i = 0; i < TodaysForwards.Length; ++i)
            {
                TodaysForwards[i] = 0.03 + (0.0010 * i);
                MeanForward += TodaysForwards[i];
            }

            MeanForward /= TodaysForwards.Length;

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

        public double MeanForward { get; }

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
        // prints the bound and asserts nothing; the port computes it as QuantLib does, its facts in
        // Divergences/UpperBoundEngineTests.cs.
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
