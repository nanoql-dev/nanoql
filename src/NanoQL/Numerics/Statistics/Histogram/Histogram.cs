// SPDX-FileCopyrightText: Copyright (C) 2007 Gang Liang
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/statistics/histogram.hpp ql/math/statistics/histogram.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// The histogram of a data set. The caller can specify the number of bins, the breaks, or the algorithm for determining
/// these quantities in computing the histogram.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: its breaks, counts and frequencies are computed once, at construction. The default construction is
/// QuantLib's empty histogram, of 0 bins and the algorithm <c>Algorithm(-1)</c>.
/// </para>
/// <para>
/// Three fixes of QuantLib's, each documented (<c>Divergences/HistogramTests.cs</c>): the Freedman-Diaconis rule's
/// quantiles are Hyndman and Fan's type 8, as QuantLib's comment says, where QuantLib's interpolation weight drops the
/// method's p/3 term; at the upper boundary's probability the quantile reads the largest sample, type 8's clamp, where
/// QuantLib reads a 0 past its sorted samples; and given breaks that repeat count their bins once, where QuantLib
/// counts the bins before it removes the repeats. Data giving a bin width of 0 under the Freedman-Diaconis or Scott
/// rule (no spread, or an interquartile range of 0) are refused, where QuantLib's bin count is undefined and it throws
/// <c>std::length_error</c>.
/// </para>
/// </remarks>
public sealed class Histogram
{
    private readonly ImmutableArray<int> counts;
    private readonly ImmutableArray<double> frequency;

    /// <summary>Initializes a new instance of the <see cref="Histogram"/> class: QuantLib's empty histogram.</summary>
    public Histogram()
    {
        Bins = 0;
        Algorithm = (HistogramAlgorithm)(-1);
        Breaks = [];
        counts = [];
        frequency = [];
    }

    /// <summary>Initializes a new instance of the <see cref="Histogram"/> class with a number of breaks.</summary>
    /// <param name="data">The data, at least one point.</param>
    /// <param name="breaks">The number of breaks, spread evenly over the data's range: the bins are one more.</param>
    /// <exception cref="ArgumentException"><paramref name="data"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="breaks"/> is negative.</exception>
    public Histogram(ReadOnlySpan<double> data, int breaks)
        : this(data, CheckBreaks(breaks) + 1, HistogramAlgorithm.None, [])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Histogram"/> class with an algorithm for its
    /// bins.</summary>
    /// <param name="data">The data, at least one point.</param>
    /// <param name="algorithm">The bin-partition algorithm.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="data"/> is empty, or gives a bin width of 0 under the Freedman-Diaconis or Scott rule, or
    /// <paramref name="algorithm"/> is <see cref="HistogramAlgorithm.None"/> or unknown.
    /// </exception>
    public Histogram(ReadOnlySpan<double> data, HistogramAlgorithm algorithm)
        : this(data, null, algorithm, [])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Histogram"/> class with its breaks.</summary>
    /// <param name="data">The data, at least one point.</param>
    /// <param name="breaks">The breaks, sorted here, and those close enough to their predecessor removed.</param>
    /// <exception cref="ArgumentException"><paramref name="data"/> is empty.</exception>
    public Histogram(ReadOnlySpan<double> data, ReadOnlySpan<double> breaks)
        : this(data, null, HistogramAlgorithm.None, breaks)
    {
    }

    private Histogram(ReadOnlySpan<double> data, int? bins, HistogramAlgorithm algorithm, ReadOnlySpan<double> breaks)
    {
        if (data.IsEmpty)
        {
            throw new ArgumentException("no data given", nameof(data));
        }

        Algorithm = algorithm;
        double min = data[0], max = data[0];
        foreach (double p in data)
        {
            // std::min_element's and std::max_element's first extreme, by operator<
            if (p < min)
            {
                min = p;
            }

            if (max < p)
            {
                max = p;
            }
        }

        // calculate number of bins if necessary
        if (bins is null && breaks.IsEmpty)
        {
            int n;
            switch (algorithm)
            {
                case HistogramAlgorithm.Sturges:
                    n = (int)Math.Ceiling((Math.Log(data.Length) / Math.Log(2.0)) + 1);
                    break;
                case HistogramAlgorithm.FD:
                    {
                        double r1 = Quantile(data, 0.25);
                        double r2 = Quantile(data, 0.75);
                        double h = 2.0 * (r2 - r1) * Math.Pow(data.Length, -1.0 / 3.0);
                        n = BinCount(max, min, h);
                        break;
                    }

                case HistogramAlgorithm.Scott:
                    {
                        var summary = new IncrementalStatistics();
                        summary.AddSequence(data);
                        double variance = summary.Variance();
                        double h = 3.5 * Math.Sqrt(variance) * Math.Pow(data.Length, -1.0 / 3.0);
                        n = BinCount(max, min, h);
                        break;
                    }

                case HistogramAlgorithm.None:
                    throw new ArgumentException("a bin-partition algorithm is required", nameof(algorithm));
                default:
                    throw new ArgumentException("unknown bin-partition algorithm", nameof(algorithm));
            }

            bins = Math.Max(n, 1);
        }

        double[] b;
        if (breaks.IsEmpty)
        {
            // set breaks if not provided
            b = new double[bins!.Value - 1];

            // ensure breaks_ evenly span over the range of data_
            // TODO: borrow the idea of pretty in R.
            double h = (max - min) / bins.Value;
            for (int i = 0; i < b.Length; ++i)
            {
                b[i] = min + ((i + 1) * h);
            }
        }
        else
        {
            // or ensure they're sorted if given, those close enough to their predecessor removed
            double[] sorted = breaks.ToArray();
            Array.Sort(sorted);
            var unique = new List<double>(sorted.Length);
            foreach (double x in sorted)
            {
                if (unique.Count == 0 || !ComparisonFunctions.CloseEnough(unique[^1], x))
                {
                    unique.Add(x);
                }
            }

            b = [.. unique];
            bins = b.Length + 1;
        }

        Bins = bins.Value;
        Breaks = ImmutableArray.Create(b);

        // finally, calculate counts and frequencies
        var c = new int[Bins];
        foreach (double p in data)
        {
            bool processed = false;
            for (int i = 0; i < b.Length; ++i)
            {
                if (p < b[i])
                {
                    ++c[i];
                    processed = true;
                    break;
                }
            }

            if (!processed)
            {
                ++c[Bins - 1];
            }
        }

        var f = new double[Bins];
        int totalCounts = data.Length;
        for (int i = 0; i < Bins; ++i)
        {
            f[i] = (double)c[i] / totalCounts;
        }

        counts = ImmutableArray.Create(c);
        frequency = ImmutableArray.Create(f);
    }

    /// <summary>Gets the number of bins.</summary>
    public int Bins { get; }

    /// <summary>Gets the breaks between the bins, sorted.</summary>
    public ImmutableArray<double> Breaks { get; }

    /// <summary>Gets the algorithm the bins were taken by: <see cref="HistogramAlgorithm.None"/> where they were
    /// given.</summary>
    public HistogramAlgorithm Algorithm { get; }

    /// <summary>Gets a value indicating whether the histogram has no bins, QuantLib's <c>empty()</c>.</summary>
    public bool IsEmpty => Bins == 0;

    /// <summary>The number of data points in a bin.</summary>
    /// <param name="i">The bin, from 0.</param>
    /// <returns>The count.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a bin.</exception>
    public int Counts(int i) => counts[CheckBin(i)];

    /// <summary>The share of the data points in a bin.</summary>
    /// <param name="i">The bin, from 0.</param>
    /// <returns>The frequency.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a bin.</exception>
    public double Frequency(int i) => frequency[CheckBin(i)];

    private static int CheckBreaks(int breaks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(breaks);
        return breaks;
    }

    // QuantLib's static_cast<Size>(std::ceil((max-min)/h)), where a bin width of 0 is refused
    private static int BinCount(double max, double min, double h)
    {
        double bins = Math.Ceiling((max - min) / h);
        if (!double.IsFinite(bins))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"the rule's bin width is {h}, which bins nothing"),
                "data");
        }

        return (int)bins;
    }

    // The discontinuous quantiles use the method (type 8) as recommended by Hyndman and Fan (1996). The resulting
    // quantile estimates are approximately median-unbiased regardless of the distribution of 'samples'.
    private static double Quantile(ReadOnlySpan<double> samples, double prob)
    {
        int nsample = samples.Length;
        if (nsample == 1)
        {
            return samples[0];
        }

        // two special cases: close to boundaries
        const double a = 1.0 / 3;
        double b = 2 * a / (nsample + a);
        double[] sorted = samples.ToArray();
        Array.Sort(sorted);
        if (prob < b)
        {
            return sorted[0];
        }
        else if (prob > 1 - b)
        {
            return sorted[nsample - 1];
        }

        // general situation: middle region and nsample >= 2; type 8's j = floor(n·p + m) and g = n·p + m − j,
        // m = (p + 1)/3
        double position = ((nsample + a) * prob) + a;
        int index = (int)Math.Floor(position);

        // use "index & index+1"th elements to interpolate the quantile, the largest sample past the last
        double weight = position - index;
        double upper = index < nsample ? sorted[index] : sorted[nsample - 1];
        return ((1 - weight) * sorted[index - 1]) + (weight * upper);
    }

    private int CheckBin(int i)
    {
        if (i < 0 || i >= Bins)
        {
            throw new ArgumentOutOfRangeException(nameof(i), i, $"the bin ({i}) must be in [0, {Bins})");
        }

        return i;
    }
}
