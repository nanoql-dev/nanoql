// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The RiskStatistics vectors, <c>tests/vectors/math/statistics/riskstatistics.json</c>: the empirical risk measures of
/// <see cref="RiskStatisticsExtensions"/> on <see cref="GeneralStatistics"/>, over losses and gains, gains only, tails
/// that are atoms, one sample and none, at targets and percentiles (the refusals outside [0.9, 1) among them);
/// <c>ExpectedShortfall</c> at the percentiles where QuantLib gives it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RiskStatisticsVectorTests
{
    [Fact]
    public void RiskStatisticsVector()
    {
        var file = VectorFile.Load("math/statistics/riskstatistics");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] data = Numbers(c.Input("data")), weights = Numbers(c.Input("weights"));
            double[] targets = Numbers(c.Input("targets")), percentiles = Numbers(c.Input("percentiles"));
            double[] esPercentiles = Numbers(c.Input("expectedShortfallPercentiles"));
            var s = new GeneralStatistics();
            s.AddSequence(data, weights);

            check.Output(c, "semiVariance", () => s.SemiVariance());
            check.Output(c, "semiDeviation", () => s.SemiDeviation());
            check.Output(c, "downsideVariance", () => s.DownsideVariance());
            check.Output(c, "downsideDeviation", () => s.DownsideDeviation());
            check.Output(c, "regret", targets.Select(t => (object?)(Func<object?>)(() => s.Regret(t))).ToArray());
            check.Output(c, "shortfall", targets.Select(t => (object?)(Func<object?>)(() => s.Shortfall(t))).ToArray());
            check.Output(
                c, "averageShortfall", targets.Select(t => (object?)(Func<object?>)(() => s.AverageShortfall(t))).ToArray());
            check.Output(
                c, "potentialUpside", percentiles.Select(p => (object?)(Func<object?>)(() => s.PotentialUpside(p))).ToArray());
            check.Output(
                c, "valueAtRisk", percentiles.Select(p => (object?)(Func<object?>)(() => s.ValueAtRisk(p))).ToArray());
            check.Output(
                c,
                "expectedShortfall",
                esPercentiles.Select(p => (object?)(Func<object?>)(() => s.ExpectedShortfall(p))).ToArray());
        }

        check.Complete();
    }
}
