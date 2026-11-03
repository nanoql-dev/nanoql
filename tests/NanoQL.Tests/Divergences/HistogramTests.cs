// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The histogram's quantiles and given breaks: QuantLib's Freedman-Diaconis rule takes Hyndman and Fan's type 8
/// quantile, as its comment says, but its interpolation weight drops the method's p/3 term, so ten squares bin into 3
/// where type 8 gives 2; given breaks that repeat count their bins before QuantLib removes the repeats, so breaks 20,
/// 20 and 50 give 4 bins over 2 breaks (counts 4, 3, 0, 3); and at the upper boundary's probability QuantLib's quantile
/// reads a 0 past its sorted samples, where type 8 clamps to the largest. No public input reaches that last one:
/// QuantLib's two probabilities, ¼ and ¾, meet the boundary at no whole number of samples, so its fact reads the
/// private quantile. QuantLib's values are its own.
/// </summary>
public class HistogramTests
{
    private static readonly double[] Squares = [.. Enumerable.Range(1, 10).Select(i => (double)(i * i))];

    [Fact]
    public void TheFreedmanDiaconisQuantilesAreType8()
    {
        // type 8: Q(¼) = 11.25 and Q(¾) = 66.5 on the squares, h = 2·55.25·10^(−1/3) = 51.3, ⌈99/51.3⌉ = 2
        Assert.Equal(2, new Histogram(Squares, HistogramAlgorithm.FD).Bins); // QuantLib 3
    }

    [Fact]
    public void RepeatedBreaksCountTheirBinsOnce()
    {
        var h = new Histogram(Squares, [20.0, 20.0, 50.0]);
        Assert.Equal(3, h.Bins);                                          // QuantLib 4
        Assert.Equal([20.0, 50.0], h.Breaks.ToArray());                   // QuantLib's, deduplicated
        Assert.Equal([4, 3, 3], Enumerable.Range(0, h.Bins).Select(h.Counts)); // QuantLib 4, 3, 0, 3

        // QuantLib's counts on the deduplicated breaks
        var once = new Histogram(Squares, [20.0, 50.0]);
        Assert.Equal(Enumerable.Range(0, once.Bins).Select(once.Counts), Enumerable.Range(0, h.Bins).Select(h.Counts));
    }

    [Fact]
    public void TheUpperBoundarysQuantileIsTheLargestSample()
    {
        // at p = 1 − b, b = (2/3)/(n + 1/3), the index is n: type 8 clamps to the largest sample, as QuantLib's own
        // p > 1 − b branch returns it; QuantLib reads the 0 past its sorted copy
        // (8 samples: there the index's floating-point argument is 8.000000000000002, so it lands on n)
        var quantile = typeof(Histogram).GetMethod("Quantile", BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate<QuantileFunction>();
        double[] samples = [3.0, 1.0, 4.0, 1.0, 5.0, 9.0, 2.0, 6.0];
        const double a = 1.0 / 3;
        double b = 2 * a / (samples.Length + a);
        Assert.Equal(9.0, quantile(samples, 1 - b));
    }

    private delegate double QuantileFunction(ReadOnlySpan<double> samples, double prob);
}
