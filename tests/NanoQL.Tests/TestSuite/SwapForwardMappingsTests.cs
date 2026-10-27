// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 François du Vignaud
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/swapforwardmappings.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testSwaptionImpliedVolatility (S-mmp)

using NanoQL.Models;
using NanoQL.Numerics;
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

    private sealed class MarketModelData
    {
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

            Forwards = new double[NbRates];
            double[] discountFactors = new double[NbRates + 1];
            discountFactors[0] = 1.0; // .95; fdv1-> WHY ???????
            for (int i = 0; i < NbRates; ++i)
            {
                Forwards[i] = 0.03 + (0.0010 * i);
                accruals[i] = RateTimes[i + 1] - RateTimes[i];
                discountFactors[i + 1] = discountFactors[i] / (1 + (Forwards[i] * accruals[i]));
            }
        }

        public double[] RateTimes { get; }

        public double[] Forwards { get; }

        public int NbRates { get; }
    }
}
