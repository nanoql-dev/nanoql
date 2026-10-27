// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 François du Vignaud
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/swapforwardmappings.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class SwapForwardMappingsTests
{
    [Fact]
    public void TestForwardSwapJacobians()
    {
        // Testing forward-rate coinitial-swap Jacobian...
        {
            var marketData = new MarketModelData();
            double[] rateTimes = marketData.RateTimes;
            double[] forwards = marketData.Forwards;
            int nbRates = marketData.NbRates;
            var lmmCurveState = new LMMCurveState(rateTimes);
            lmmCurveState.SetOnForwardRates(forwards);

            const double bumpSize = 1e-8;

            double[] bumpedForwards;

            var coinitialJacobian = new Matrix(nbRates, nbRates);

            for (int i = 0; i < nbRates; ++i)
            {
                for (int j = 0; j < nbRates; ++j)
                {
                    bumpedForwards = (double[])forwards.Clone();
                    bumpedForwards[j] += bumpSize;
                    lmmCurveState.SetOnForwardRates(bumpedForwards);
                    double upRate = lmmCurveState.CmSwapRate(0, i + 1);
                    bumpedForwards[j] -= 2.0 * bumpSize;
                    lmmCurveState.SetOnForwardRates(bumpedForwards);
                    double downRate = lmmCurveState.CmSwapRate(0, i + 1);
                    double deriv = (upRate - downRate) / (2.0 * bumpSize);
                    coinitialJacobian[i, j] = deriv;
                }
            }

            Matrix modelJacobian = SwapForwardMappings.CoinitialSwapForwardJacobian(lmmCurveState);

            const double errorTolerance = 1e-5;

            for (int i = 0; i < nbRates; ++i)
            {
                for (int j = 0; j < nbRates; ++j)
                {
                    Assert.True(
                        Math.Abs(modelJacobian[i, j] - coinitialJacobian[i, j]) <= errorTolerance,
                        $"rate {i}, sensitivity {j}, formula value {modelJacobian[i, j]} bumping value {coinitialJacobian[i, j]}");
                }
            }
        }

        // Testing forward-rate constant-maturity swap Jacobian...
        {
            var marketData = new MarketModelData();
            double[] rateTimes = marketData.RateTimes;
            double[] forwards = marketData.Forwards;
            int nbRates = marketData.NbRates;
            var lmmCurveState = new LMMCurveState(rateTimes);
            lmmCurveState.SetOnForwardRates(forwards);

            const double bumpSize = 1e-8;

            for (int spanningForwards = 1; spanningForwards < nbRates; ++spanningForwards)
            {
                double[] bumpedForwards;

                var cmsJacobian = new Matrix(nbRates, nbRates);

                for (int i = 0; i < nbRates; ++i)
                {
                    for (int j = 0; j < nbRates; ++j)
                    {
                        bumpedForwards = (double[])forwards.Clone();
                        bumpedForwards[j] += bumpSize;
                        lmmCurveState.SetOnForwardRates(bumpedForwards);
                        double upRate = lmmCurveState.CmSwapRate(i, spanningForwards);
                        bumpedForwards[j] -= 2.0 * bumpSize;
                        lmmCurveState.SetOnForwardRates(bumpedForwards);
                        double downRate = lmmCurveState.CmSwapRate(i, spanningForwards);
                        double deriv = (upRate - downRate) / (2.0 * bumpSize);
                        cmsJacobian[i, j] = deriv;
                    }
                }

                Matrix modelJacobian = SwapForwardMappings.CmSwapForwardJacobian(lmmCurveState, spanningForwards);

                const double errorTolerance = 1e-5;

                for (int i = 0; i < nbRates; ++i)
                {
                    for (int j = 0; j < nbRates; ++j)
                    {
                        Assert.True(
                            Math.Abs(modelJacobian[i, j] - cmsJacobian[i, j]) <= errorTolerance,
                            $"rate {i}, sensitivity {j}, formula value {modelJacobian[i, j]} bumping value {cmsJacobian[i, j]}");
                    }
                }
            }
        }
    }

    [Fact]
    public void TestSwaptionImpliedVolatility()
    {
        // Testing implied swaption vol in LMM using HW approximation...
        var marketData = new MarketModelData();
        double[] rateTimes = marketData.RateTimes;
        double[] forwards = marketData.Forwards;
        int nbRates = marketData.NbRates;
        var lmmCurveState = new LMMCurveState(rateTimes);
        lmmCurveState.SetOnForwardRates(forwards);

        const double longTermCorr = 0.5;
        const double beta = .2;
        double strike = .03;

        for (int startIndex = 1; startIndex + 2 < nbRates; startIndex = startIndex + 5)
        {
            int endIndex = nbRates - 2;

            StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Call, strike);
            var product = new MultiStepSwaption(rateTimes, startIndex, endIndex, payoff);

            EvolutionDescription evolution = product.Evolution;
            int numberOfFactors = nbRates;
            double displacement = marketData.Displacements[0];

            // QuantLib computes the Jacobian and doesn't read it.
            _ = SwapForwardMappings.CoterminalSwapZedMatrix(lmmCurveState, displacement);

            Matrix correlations = ExpCorrelationsFunctions.ExponentialCorrelations(evolution.RateTimes.AsSpan(), longTermCorr, beta);
            var corr = new TimeHomogeneousForwardCorrelation(correlations, rateTimes);
            var lmmMarketModel = new FlatVol(
                marketData.Volatilities, corr, evolution, numberOfFactors, lmmCurveState.ForwardRates(), marketData.Displacements);

            // Adapted: QuantLib's default seed 0 draws one from the clock, which nanoql refuses. Sobol reads its seed
            // past the Jaeckel direction integers' 32 tabulated dimensions, which these 17 x 17 reach, so QuantLib's
            // own test draws a different sequence on each run; seed 42 fixes one draw.
            var generatorFactory = new SobolBrownianGeneratorFactory(SobolBrownianGeneratorBaseOrdering.Diagonal, 42);
            int[] numeraires = [.. Enumerable.Repeat(nbRates, nbRates)];
            var evolver = new LogNormalFwdRatePc(lmmMarketModel, generatorFactory, numeraires);

            SequenceStatisticsInc stats = Simulate(marketData.DiscountFactors, evolver, product);
            double[] results = [.. stats.Mean()];
            double[] errors = [.. stats.ErrorEstimate()];

            double estimatedImpliedVol = SwapForwardMappings.SwaptionImpliedVolatility(lmmMarketModel, startIndex, endIndex);

            double swapRate = lmmCurveState.CmSwapRate(startIndex, endIndex - startIndex);
            double swapAnnuity = lmmCurveState.CmSwapAnnuity(startIndex, startIndex, endIndex - startIndex)
                * marketData.DiscountFactors[startIndex];

            var payoffDis = new PlainVanillaPayoff(OptionType.Call, strike + displacement);

            double expectedSwaption = new BlackCalculator(
                payoffDis, swapRate + displacement, estimatedImpliedVol * Math.Sqrt(rateTimes[startIndex]), swapAnnuity).Value;

            double error = expectedSwaption - results[0];
            double errorInSds = error / errors[0];
            Assert.True(
                Math.Abs(errorInSds) <= 3.5,
                $"expected\t{expectedSwaption}\tLMM\t{results[0]}\tstdev:\t{errors[0]}\t{errorInSds}");
        }
    }

    // QuantLib's simulate: the accounting engine on the evolver, its initial numeraire's discount the initial value,
    // over QuantLib's release paths (swapforwardmappings.cpp:152), not its _DEBUG ones.
    private static SequenceStatisticsInc Simulate(
        double[] todaysDiscounts, MarketModelEvolver evolver, MarketModelMultiProduct product)
    {
        const int paths = 32767;

        int initialNumeraire = evolver.Numeraires[0];
        double initialNumeraireValue = todaysDiscounts[initialNumeraire];

        var engine = new AccountingEngine(evolver, product, initialNumeraireValue);
        var stats = new SequenceStatisticsInc(product.NumberOfProducts);
        engine.MultiplePathValues(stats, paths);
        return stats;
    }

    private sealed class MarketModelData
    {
        private static readonly double[] MktVols =
        [
            0.15541283, 0.18719678, 0.20890740, 0.22318179, 0.23212717, 0.23731450, 0.23988649, 0.24066384, 0.24023111,
            0.23900189, 0.23726699, 0.23522952, 0.23303022, 0.23076564, 0.22850101, 0.22627951, 0.22412881, 0.22206569,
            0.22009939,
        ];

        public MarketModelData()
        {
            // Times
            Calendar calendar = new NullCalendar();
            var context = new EvaluationContext { EvaluationDate = new Date(16, Month.September, 2015) };
            Date todaysDate = context.EvaluationDate;
            Date endDate = todaysDate + new Period(9, TimeUnit.Years); // change back
            var dates = new Schedule(
                todaysDate,
                endDate,
                new Period(Frequency.Semiannual),
                calendar,
                BusinessDayConvention.Following,
                BusinessDayConvention.Following,
                DateGenerationRule.Backward,
                false,
                context);
            NbRates = dates.Size - 2;
            RateTimes = new double[NbRates + 1];
            double[] accruals = new double[NbRates];
            DayCounter dayCounter = new SimpleDayCounter();
            for (int i = 1; i < NbRates + 2; ++i)
            {
                RateTimes[i - 1] = dayCounter.YearFraction(todaysDate, dates[i]);
            }

            Displacements = new double[NbRates];

            Forwards = new double[NbRates];
            DiscountFactors = new double[NbRates + 1];
            DiscountFactors[0] = 1.0; // .95; fdv1-> WHY ???????
            for (int i = 0; i < NbRates; ++i)
            {
                Forwards[i] = 0.03 + (0.0010 * i);
                accruals[i] = RateTimes[i + 1] - RateTimes[i];
                DiscountFactors[i + 1] = DiscountFactors[i] / (1 + (Forwards[i] * accruals[i]));
            }

            Volatilities = MktVols[..NbRates];
        }

        public double[] RateTimes { get; }

        public double[] Forwards { get; }

        public int NbRates { get; }

        public double[] Volatilities { get; }

        public double[] Displacements { get; }

        public double[] DiscountFactors { get; }
    }
}
