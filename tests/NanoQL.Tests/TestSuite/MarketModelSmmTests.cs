// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/marketmodel_smm.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Text;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Time;
using NanoQL.Utilities;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class MarketModelSmmTests
{
    private enum MeasureType
    {
        ProductSuggested,
        Terminal,
        MoneyMarket,
        MoneyMarketPlus,
    }

    private enum EvolverType
    {
        Ipc,
        Pc,
        NormalPc,
    }

    [Fact]
    public void TestMultiStepCoterminalSwapsAndSwaptions()
    {
        // Testing exact repricing of multi-step coterminal swaps and swaptions in a lognormal coterminal swap rate
        // market model...
        var vars = new CommonVars();

        double fixedRate = 0.04;

        // swaps
        double[] swapPaymentTimes = vars.RateTimes[1..];
        var swaps = new MultiStepCoterminalSwaps(vars.RateTimes, vars.Accruals, vars.Accruals, swapPaymentTimes, fixedRate);

        // swaptions
        double[] swaptionPaymentTimes = vars.RateTimes[..^1];
        var displacedPayoff = new StrikedTypePayoff[vars.TodaysForwards.Length];
        var undisplacedPayoff = new StrikedTypePayoff[vars.TodaysForwards.Length];
        for (int i = 0; i < undisplacedPayoff.Length; ++i)
        {
            displacedPayoff[i] = new PlainVanillaPayoff(OptionType.Call, fixedRate + CommonVars.Displacement);
            undisplacedPayoff[i] = new PlainVanillaPayoff(OptionType.Call, fixedRate);
        }

        var swaptions = new MultiStepCoterminalSwaptions(vars.RateTimes, swaptionPaymentTimes, undisplacedPayoff);
        var product = new MultiProductComposite();
        product.Add(swaps);
        product.Add(swaptions);
        product.FinalizeComposite();
        EvolutionDescription evolution = product.Evolution;
        string[] marketModels = ["ExponentialCorrelationFlatVolatility", "ExponentialCorrelationAbcdVolatility"];
        foreach (string j in marketModels)
        {
            int[] testedFactors = [vars.TodaysForwards.Length];
            foreach (int factors in testedFactors)
            {
                // Composite's ProductSuggested is the Terminal one
                MeasureType[] measures = [MeasureType.Terminal, MeasureType.MoneyMarket];
                foreach (MeasureType measure in measures)
                {
                    int[] numeraires = vars.MakeMeasure(product, measure);
                    MarketModel marketModel = vars.MakeMarketModel(evolution, factors, j);
                    EvolverType[] evolvers = [EvolverType.Pc];
                    int stop = EvolutionDescriptionFunctions.IsInTerminalMeasure(evolution, numeraires) ? 0 : 1;
                    for (int i = 0; i < evolvers.Length - stop; i++)
                    {
                        for (int n = 0; n < 1; n++)
                        {
                            var generatorFactory = new SobolBrownianGeneratorFactory(
                                SobolBrownianGeneratorBaseOrdering.Diagonal, CommonVars.Seed);
                            MarketModelEvolver evolver =
                                CommonVars.MakeMarketModelEvolver(marketModel, numeraires, generatorFactory, evolvers[i]);
                            string config = $"{j}, {factors} (full) factors, {measure}, {evolvers[i]}, MT BGF";
                            SequenceStatisticsInc stats = vars.Simulate(evolver, product);
                            vars.CheckCoterminalSwapsAndSwaptions(stats, fixedRate, displacedPayoff, config);
                        }
                    }
                }
            }
        }
    }

    // Adapted: setup()'s evaluation date (marketmodel_smm.cpp:81), the day QuantLib's suite runs, is 15 October 2026,
    // as in MarketModelTests. Only the members the ported case reads are kept, and makeMarketModel's volatility and
    // correlation models of the legacy LIBOR model (lines 202-208), which neither of its two models reads, are left
    // out. The paths are QuantLib's release count (line 157), not its _DEBUG one.
    private sealed class CommonVars
    {
        public const int MeasureOffset = 5;
        public const long Seed = 42;
        public const int Paths = 32767;
        public const double LongTermCorrelation = 0.5;
        public const double Beta = 0.2;
        public const double Displacement = 0.02;

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
            Date endDate = todaysDate + new Period(10, TimeUnit.Years);
            var context = new EvaluationContext { EvaluationDate = todaysDate };
            var dates = new Schedule(
                todaysDate, endDate, new Period(Frequency.Semiannual), calendar, BusinessDayConvention.Following,
                BusinessDayConvention.Following, DateGenerationRule.Backward, false, context);
            RateTimes = new double[dates.Size - 1];
            Accruals = new double[RateTimes.Length - 1];
            var dayCounter = new SimpleDayCounter();
            for (int i = 1; i < dates.Size; ++i)
            {
                RateTimes[i - 1] = dayCounter.YearFraction(todaysDate, dates[i]);
            }

            for (int i = 1; i < RateTimes.Length; ++i)
            {
                Accruals[i - 1] = RateTimes[i] - RateTimes[i - 1];
            }

            // Rates & displacement
            TodaysForwards = new double[Accruals.Length];
            for (int i = 0; i < TodaysForwards.Length; ++i)
            {
                TodaysForwards[i] = 0.03 + (0.0010 * i);
            }

            var curveStateLmm = new LMMCurveState(RateTimes);
            curveStateLmm.SetOnForwardRates(TodaysForwards);
            TodaysSwaps = curveStateLmm.CoterminalSwapRates().ToArray();

            // Discounts
            TodaysDiscounts = new double[RateTimes.Length];
            TodaysDiscounts[0] = 0.95;
            for (int i = 1; i < RateTimes.Length; ++i)
            {
                TodaysDiscounts[i] = TodaysDiscounts[i - 1] / (1.0 + (TodaysForwards[i - 1] * Accruals[i - 1]));
            }

            // Swaption Volatilities
            Volatilities = new double[TodaysSwaps.Length];
            for (int i = 0; i < TodaysSwaps.Length; i++)
            {
                Volatilities[i] = TodaysSwaps[i] * MktVols[i] / (TodaysSwaps[i] + Displacement);
            }
        }

        public double[] RateTimes { get; }

        public double[] Accruals { get; }

        public double[] TodaysForwards { get; }

        public double[] TodaysSwaps { get; }

        public double[] TodaysDiscounts { get; }

        public double[] Volatilities { get; }

        // QuantLib's makeMarketModelEvolver: the predictor-corrector coterminal swap-rate evolver, the only one it builds.
        public static MarketModelEvolver MakeMarketModelEvolver(
            MarketModel marketModel,
            ReadOnlySpan<int> numeraires,
            BrownianGeneratorFactory generatorFactory,
            EvolverType evolverType,
            int initialStep = 0) => evolverType switch
            {
                EvolverType.Pc => new LogNormalCotSwapRatePc(marketModel, generatorFactory, numeraires, initialStep),
                _ => throw new ArgumentException("unknown CoterminalSwapMarketModelEvolver type", nameof(evolverType)),
            };

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

        // QuantLib's makeMarketModel for its two exponential-correlation models, unbumped: the coterminal swap rates
        // the initial rates.
        public MarketModel MakeMarketModel(EvolutionDescription evolution, int numberOfFactors, string marketModelType)
        {
            var curveStateLmm = new LMMCurveState(RateTimes);
            curveStateLmm.SetOnForwardRates(TodaysForwards);
            double[] usedRates = curveStateLmm.CoterminalSwapRates().ToArray();
            Matrix correlations = ExpCorrelationsFunctions.ExponentialCorrelations(evolution.RateTimes.AsSpan(), LongTermCorrelation, Beta);
            var corr = new TimeHomogeneousForwardCorrelation(correlations, evolution.RateTimes.AsSpan());
            double[] displacements = [.. Enumerable.Repeat(Displacement, usedRates.Length)];
            return marketModelType switch
            {
                "ExponentialCorrelationFlatVolatility" => new FlatVol(Volatilities, corr, evolution, numberOfFactors, usedRates, displacements),
                "ExponentialCorrelationAbcdVolatility" => new AbcdVol(0.0, 0.0, 1.0, 1.0, Volatilities, corr, evolution, numberOfFactors, usedRates, displacements),
                _ => throw new ArgumentException($"unknown MarketModel type {marketModelType}", nameof(marketModelType)),
            };
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

        // QuantLib's checkCoterminalSwapsAndSwaptions: each swap against its annuity times the swap rate's excess over
        // the fixed rate, each swaption against Black's formula on the displaced rate.
        public void CheckCoterminalSwapsAndSwaptions(
            SequenceStatisticsInc stats, double fixedRate, StrikedTypePayoff[] displacedPayoff, string config)
        {
            double[] results = [.. stats.Mean()];
            double[] errors = [.. stats.ErrorEstimate()];
            double[] discrepancies = new double[TodaysForwards.Length];

            int n = TodaysForwards.Length;

            // check Swaps
            double maxError = double.MinValue;
            var curveStateLmm = new LMMCurveState(RateTimes);
            curveStateLmm.SetOnForwardRates(TodaysForwards);

            double[] expectedNPVs = new double[TodaysSwaps.Length];
            double errorThreshold = 0.5;
            for (int i = 0; i < n; ++i)
            {
                double expectedNPV = curveStateLmm.CoterminalSwapAnnuity(i, i) * (TodaysSwaps[i] - fixedRate) * TodaysDiscounts[i];
                expectedNPVs[i] = expectedNPV;
                discrepancies[i] = (results[i] - expectedNPVs[i]) / errors[i];
                maxError = Math.Max(Math.Abs(discrepancies[i]), maxError);
            }

            if (maxError > errorThreshold)
            {
                var message = new StringBuilder(config);
                for (int i = 0; i < n; ++i)
                {
                    message.Append($"\n{i + 1} coterminal swap NPV: {results[i]} +- {errors[i]}; expected: "
                        + $"{expectedNPVs[i]}; discrepancy/error = {discrepancies[n - 1 - i]} standard errors");
                }

                Assert.Fail(message.ToString());
            }

            // check Swaptions
            maxError = 0;
            double[] expectedSwaptions = new double[n];
            for (int i = 0; i < n; ++i)
            {
                double expectedSwaption = new BlackCalculator(
                    displacedPayoff[i],
                    TodaysSwaps[i] + Displacement,
                    Volatilities[i] * Math.Sqrt(RateTimes[i]),
                    curveStateLmm.CoterminalSwapAnnuity(i, i) * TodaysDiscounts[i]).Value;
                expectedSwaptions[i] = expectedSwaption;
                discrepancies[i] = (results[n + i] - expectedSwaptions[i]) / errors[n + i];
                maxError = Math.Max(Math.Abs(discrepancies[i]), maxError);
            }

            errorThreshold = 2.0;

            if (maxError > errorThreshold)
            {
                var message = new StringBuilder(config);
                for (int i = 1; i <= n; ++i)
                {
                    message.Append($"\n{i} Swaption: {results[(2 * n) - i]} +- {errors[(2 * n) - i]}; expected: "
                        + $"{expectedSwaptions[n - i]}; discrepancy/error = {discrepancies[n - i]} standard errors");
                }

                Assert.Fail(message.ToString());
            }
        }
    }
}
