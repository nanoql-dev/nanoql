// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticContinuousGeometricAveragePriceAsianHestonEngine vectors,
/// <c>tests/vectors/experimental/asian/analytic_cont_geom_av_price_heston.json</c>: continuous geometric average-price
/// calls and puts on three Heston processes over three expiries, at two summation cutoffs; an expired option and a
/// refused arithmetic average.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticContinuousGeometricAveragePriceAsianHestonEngineVectorTests
{
    [Fact]
    public void AnalyticContinuousGeometricAveragePriceAsianHestonEngineVector()
    {
        var file = VectorFile.Load("experimental/asian/analytic_cont_geom_av_price_heston");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            HestonProcess process = AnalyticHestonForwardEuropeanEngineVectorTests.HestonProcessOf(c.Input("process"));
            int cutoff = c.Int("summationCutoff");
            double limit = c.Double("xiRightLimit");
            var exercise = new EuropeanExercise(new Date(c.Int("exerciseDate")));
            var average = (AverageType)c.Int("averageType");
            check.Output(
                c,
                "NPV",
                c.Input("payoffs").EnumerateArray()
                    .Select(p => Lazy(() => AnalyticContinuousGeometricAveragePriceAsianHestonEngine.Calculate(
                        new ContinuousAveragingAsianOption(average, PayoffOf(p), exercise), context, process, cutoff,
                        limit).NPV))
                    .ToArray());
        }

        check.Complete();
    }
}
