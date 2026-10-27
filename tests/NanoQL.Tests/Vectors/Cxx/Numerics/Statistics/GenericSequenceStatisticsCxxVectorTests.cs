// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's sequence statistics vectors, <c>tests/vectors/cxx/math/statistics/sequencestatistics.json</c>: the
/// lifted members the wheel doesn't bind, <see cref="SequenceStatistics"/>' Gaussian members and the empirical ones of
/// <see cref="GenericSequenceStatisticsExtensions"/>, and <see cref="SequenceStatisticsInc"/>' own downside members, at
/// percentiles and targets, with QuantLib's refusals on small sets (the expected shortfall left out where QuantLib
/// refuses it, nanoql reading the level, <c>Divergences/ExpectedShortfallNoLossTests.cs</c>).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GenericSequenceStatisticsCxxVectorTests
{
    [Fact]
    public void GenericSequenceStatisticsCxxVector()
    {
        var file = VectorFile.Load("cxx/math/statistics/sequencestatistics");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[][] samples = [.. c.Input("samples").EnumerateArray().Select(Reals)];
            double[] weights = c.Doubles("weights");
            double p = c.Double("percentile"), t = c.Double("target");
            if (c.String("statistics") == "Statistics")
            {
                var s = new SequenceStatistics(samples[0].Length);
                for (int i = 0; i < samples.Length; ++i)
                {
                    s.Add(samples[i], weights[i]);
                }

                check.Output(c, "gaussianPercentile", Lazy(() => s.GaussianPercentile(p)));
                check.Output(c, "gaussianPotentialUpside", Lazy(() => s.GaussianPotentialUpside(p)));
                check.Output(c, "gaussianValueAtRisk", Lazy(() => s.GaussianValueAtRisk(p)));
                check.Output(c, "gaussianExpectedShortfall", Lazy(() => s.GaussianExpectedShortfall(p)));
                check.Output(c, "gaussianShortfall", Lazy(() => s.GaussianShortfall(t)));
                check.Output(c, "gaussianAverageShortfall", Lazy(() => s.GaussianAverageShortfall(t)));
                check.Output(c, "percentile", Lazy(() => s.Percentile(p)));
                check.Output(c, "potentialUpside", Lazy(() => s.PotentialUpside(p)));
                check.Output(c, "valueAtRisk", Lazy(() => s.ValueAtRisk(p)));
                if (c.Expected.TryGetProperty("expectedShortfall", out _))
                {
                    check.Output(c, "expectedShortfall", Lazy(() => s.ExpectedShortfall(p)));
                }

                check.Output(c, "regret", Lazy(() => s.Regret(t)));
                check.Output(c, "shortfall", Lazy(() => s.Shortfall(t)));
                check.Output(c, "averageShortfall", Lazy(() => s.AverageShortfall(t)));
                check.Output(c, "semiVariance", Lazy(() => s.SemiVariance()));
                check.Output(c, "semiDeviation", Lazy(() => s.SemiDeviation()));
                check.Output(c, "downsideVariance", Lazy(() => s.DownsideVariance()));
                check.Output(c, "downsideDeviation", Lazy(() => s.DownsideDeviation()));
            }
            else
            {
                var s = new SequenceStatisticsInc(samples[0].Length);
                for (int i = 0; i < samples.Length; ++i)
                {
                    s.Add(samples[i], weights[i]);
                }

                check.Output(c, "downsideVariance", Lazy(() => s.DownsideVariance()));
                check.Output(c, "downsideDeviation", Lazy(() => s.DownsideDeviation()));
            }
        }

        check.Complete();
    }
}
