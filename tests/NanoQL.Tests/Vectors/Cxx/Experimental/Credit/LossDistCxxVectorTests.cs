// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's loss distribution vectors, <c>tests/vectors/cxx/experimental/credit/lossdistribution.json</c>: the
/// probabilities of n events, binomial and exact, static and as functors; the homogeneous, bucketing and Monte Carlo
/// distributions.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LossDistCxxVectorTests
{
    [Fact]
    public void LossDistCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/credit/lossdistribution");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[][] pools = [.. c.Input("pools").EnumerateArray().Select(Reals)];
            check.Output(c, "probabilityOfNEvents", Each(pools, p => LossDist.ProbabilityOfNEvents(p).ToArray()));
            check.Output(c, "binomialProbabilityOfNEvents", Each(Counts(pools, 0, 0), x => LossDist.BinomialProbabilityOfNEvents(x.N, x.P)));
            check.Output(c, "binomialProbabilityOfAtLeastNEvents", Each(Counts(pools, 1, 1), x => LossDist.BinomialProbabilityOfAtLeastNEvents(x.N, x.P)));
            check.Output(c, "probabilityOfNEventsAt", Each(Counts(pools, 0, 0), x => LossDist.ProbabilityOfNEvents(x.N, x.P)));
            check.Output(c, "probabilityOfAtLeastNEvents", Each(Counts(pools, 0, 1), x => LossDist.ProbabilityOfAtLeastNEvents(x.N, x.P)));
            check.Output(c, "functors", Each(Counts(pools, 1, 0), x => new object[]
            {
                new ProbabilityOfNEvents(x.N).Evaluate(x.P),
                new ProbabilityOfAtLeastNEvents(x.N).Evaluate(x.P),
                new BinomialProbabilityOfAtLeastNEvents(x.N).Evaluate(x.P),
            }));
            JsonElement[] homogeneous = [.. c.Input("homogeneous").EnumerateArray()];
            check.Output(c, "homogeneous", Each(homogeneous, h =>
            {
                var l = new LossDistHomogeneous(h[0].GetInt32(), VectorCase.Real(h[1]));
                LossDistResult r = l.EvaluateWithDetails(VectorCase.Real(h[2]), Reals(h[3]));
                return new object[]
                {
                    DistributionCxxVectorTests.Summary(r.Distribution), r.Size, r.Volume, r.Probability.ToArray(),
                    r.ExcessProbability.ToArray(), l.Buckets, l.Maximum,
                };
            }));
            check.Output(c, "homogeneousVectors", Each(homogeneous, h =>
            {
                double[] p = Reals(h[3]);
                var l = new LossDistHomogeneous(h[0].GetInt32(), VectorCase.Real(h[1]));
                return DistributionCxxVectorTests.Summary(l.Evaluate([.. Enumerable.Repeat(VectorCase.Real(h[2]), p.Length)], p));
            }));
            check.Output(c, "bucketing", Each(c.Input("bucketing").EnumerateArray(), b =>
            {
                var l = new LossDistBucketing(b[0].GetInt32(), VectorCase.Real(b[1]), VectorCase.Real(b[2]));
                return new object[] { DistributionCxxVectorTests.Summary(l.Evaluate(Reals(b[3]), Reals(b[4]))), l.Buckets, l.Maximum };
            }));
            check.Output(c, "binomialLimits", Each(c.Input("binomial").EnumerateArray(), b =>
            {
                var l = new LossDistBinomial(b[0].GetInt32(), VectorCase.Real(b[1]));
                return new object[] { l.Buckets, l.Maximum };
            }));
            check.Output(c, "monteCarlo", Each(c.Input("monteCarlo").EnumerateArray(), m =>
            {
                var l = new LossDistMonteCarlo(m[0].GetInt32(), VectorCase.Real(m[1]), m[2].GetInt32(), m[3].GetInt64(), VectorCase.Real(m[4]));
                return new object[] { DistributionCxxVectorTests.Summary(l.Evaluate(Reals(m[5]), Reals(m[6]))), l.Buckets, l.Maximum };
            }));
        }

        check.Complete();
    }

    // Each pool's counts from a first value to its size plus an offset.
    private static IEnumerable<(int N, double[] P)> Counts(double[][] pools, int first, int offset) =>
        pools.SelectMany(p => Enumerable.Range(first, p.Length + offset - first + 1).Select(n => (n, p)));

    private static double[] Reals(JsonElement e) => [.. e.EnumerateArray().Select(VectorCase.Real)];

    // Each value lazily, so that a throw is checked element by element.
    private static Func<object?>[] Each<T>(IEnumerable<T> xs, Func<T, object?> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];
}
