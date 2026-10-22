// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's GaussianStatistics vectors, <c>tests/vectors/cxx/math/statistics/gaussianstatistics.json</c>: the
/// Gaussian risk measures of <see cref="GaussianStatisticsExtensions"/>, which the wheel doesn't expose, over
/// <see cref="GeneralStatistics"/>, <see cref="IncrementalStatistics"/> and <see cref="StatsHolder"/>, at targets and
/// percentiles, the refusals among them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GaussianStatisticsCxxVectorTests
{
    [Fact]
    public void GaussianStatisticsCxxVector()
    {
        var file = VectorFile.Load("cxx/math/statistics/gaussianstatistics");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] data = Numbers(c.Input("data")), weights = Numbers(c.Input("weights"));
            double[] targets = Numbers(c.Input("targets")), percentiles = Numbers(c.Input("percentiles"));
            switch (c.String("kind"))
            {
                case "general":
                    var g = new GeneralStatistics();
                    g.AddSequence(data, weights);
                    Measures(check, c, g, targets, percentiles);
                    break;
                case "incremental":
                    var i = new IncrementalStatistics();
                    i.AddSequence(data, weights);
                    Measures(check, c, i, targets, percentiles);
                    break;
                default:
                    Measures(check, c, new StatsHolder(data[0], data[1]), targets, percentiles);
                    break;
            }
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"cxx/math/statistics/gaussianstatistics: bit-identical {check.BitIdentical} of {check.Reals} real outputs");
        check.Complete();
    }

    private static void Measures<TStat>(VectorAssert check, VectorCase c, TStat s, double[] targets, double[] percentiles)
        where TStat : ISummaryStatistics
    {
        check.Output(c, "gaussianDownsideVariance", () => s.GaussianDownsideVariance());
        check.Output(c, "gaussianDownsideDeviation", () => s.GaussianDownsideDeviation());
        check.Output(c, "gaussianRegret", At(targets, x => s.GaussianRegret(x)));
        check.Output(c, "gaussianShortfall", At(targets, x => s.GaussianShortfall(x)));
        check.Output(c, "gaussianAverageShortfall", At(targets, x => s.GaussianAverageShortfall(x)));
        check.Output(c, "gaussianPercentile", At(percentiles, p => s.GaussianPercentile(p)));
        check.Output(c, "gaussianTopPercentile", At(percentiles, p => s.GaussianTopPercentile(p)));
        check.Output(c, "gaussianPotentialUpside", At(percentiles, p => s.GaussianPotentialUpside(p)));
        check.Output(c, "gaussianValueAtRisk", At(percentiles, p => s.GaussianValueAtRisk(p)));
        check.Output(c, "gaussianExpectedShortfall", At(percentiles, p => s.GaussianExpectedShortfall(p)));
    }

    private static object?[] At(double[] xs, Func<double, double> f) =>
        [.. xs.Select(x => (object?)(Func<object?>)(() => f(x)))];
}
