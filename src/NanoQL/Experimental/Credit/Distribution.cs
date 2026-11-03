// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/distribution.hpp ql/experimental/credit/distribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A discretized probability density and cumulative probability: buckets of a range, each holding a density, an
/// average and the cumulated and excess probabilities.
/// </summary>
/// <remarks>
/// <para>
/// A mutable working type (§2.7), QuantLib's API: values or densities are added, then <see cref="Normalize"/>, which
/// the reads call, computes the cumulative and excess probabilities. The density is uniform within a bucket, as
/// <see cref="CumulativeDensity"/>'s interpolation reads it. QuantLib's default constructor leaves the size
/// uninitialized; a distribution here is built of its buckets.
/// </para>
/// <para>
/// Three members depart from QuantLib's, each fixed on that density (<c>Divergences/DistributionMembersTests.cs</c>):
/// <see cref="CumulativeDensity"/> in the first bucket interpolates from 0, where QuantLib's reads the cumulative at
/// index −1 (<c>distribution.cpp:220-230</c>); <see cref="ExpectedShortfall"/> integrates the density above the
/// quantile, where QuantLib's sums the left edges from the bucket after the quantile's over a denominator leaving its
/// first term out (5.625 and 9 for a uniform's 5.25 and 9.5, <c>:328-341</c>); and <see cref="Tranche"/> keeps the mass
/// above the detachment in the last bucket, where QuantLib's sets the last cumulative to 1 before computing the last
/// density from it, 0 (a total probability of 0.7, <c>:236-284</c>).
/// </para>
/// <para>
/// A tranched distribution's buckets end at the tranche's width D − A: the bucket spanning it is uniform up to it, and
/// the buckets <see cref="Tranche"/> truncates to it, the mass above the detachment among them, are an atom there, as
/// <see cref="CumulativeDensity"/> and <see cref="ExpectedShortfall"/> read them. QuantLib's keeps their widths, which
/// its left-edge shortfall never reads; the uniform shortfall read past the tranche (3.5 for 3 at 0.5 on a uniform's
/// [2, 5] tranche).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
[WorkingType]
public sealed class Distribution
{
    private readonly double xmin;
    private readonly double xmax;
    private readonly List<int> count;

    // x: coordinate of left hand cell bundary
    // dx: cell width
    private readonly List<double> x;
    private readonly List<double> dx;

    // density: probability density, densitx*dx = prob. of loss in cell i
    // cumulatedDensity: cumulated (integrated) from x = 0
    // excessProbability: cumulated from x_i to infinity
    // cumulativeExcessProbability: integrated excessProbability from x = 0
    private readonly List<double> density;
    private readonly List<double> cumulativeDensity;
    private readonly List<double> excessProbability;
    private readonly List<double> cumulativeExcessProbability;

    // average loss in cell i
    private readonly List<double> average;
    private int size;

    // the tranche's width D - A once tranched: the last buckets' upper edges are capped there
    private double? trancheWidth;
    private int overFlow;
    private int underFlow;
    private bool isNormalized;

    /// <summary>Initializes a new instance of the <see cref="Distribution"/> class: equal buckets over a range, the
    /// last one ending at its upper end exactly.</summary>
    /// <param name="nBuckets">The number of buckets, at least 1.</param>
    /// <param name="xmin">The range's lower end.</param>
    /// <param name="xmax">The range's upper end.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nBuckets"/> is below 1, where QuantLib's reads
    /// an empty vector's last element.</exception>
    public Distribution(int nBuckets, double xmin, double xmax)
    {
        if (nBuckets < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(nBuckets), nBuckets, "a distribution needs at least one bucket");
        }

        size = nBuckets;
        this.xmin = xmin;
        this.xmax = xmax;
        count = [.. new int[nBuckets]];
        x = [.. new double[nBuckets]];
        dx = [.. new double[nBuckets]];
        density = [.. new double[nBuckets]];
        cumulativeDensity = [.. new double[nBuckets]];
        excessProbability = [.. new double[nBuckets]];
        cumulativeExcessProbability = [.. new double[nBuckets]];
        average = [.. new double[nBuckets]];
        for (int i = 0; i < nBuckets; i++)
        {
            dx[i] = (xmax - xmin) / nBuckets;
            x[i] = i == 0 ? xmin : x[i - 1] + dx[i - 1];
        }

        // ensure we match exactly the domain, otherwise we might fail the
        //   locate test because of precission mismatches
        dx[^1] = xmax - x[^1];
    }

    /// <summary>Gets the number of buckets.</summary>
    public int Size => size;

    /// <summary>A bucket's left edge.</summary>
    /// <param name="k">The bucket.</param>
    /// <returns>The edge.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="k"/> isn't a bucket.</exception>
    public double X(int k) => x[k];

    /// <summary>The buckets' left edges.</summary>
    /// <returns>A view of the edges.</returns>
    public IReadOnlyList<double> X() => x.AsReadOnly();

    /// <summary>A bucket's width.</summary>
    /// <param name="k">The bucket.</param>
    /// <returns>The width.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="k"/> isn't a bucket.</exception>
    public double Dx(int k) => dx[k];

    /// <summary>The buckets' widths.</summary>
    /// <returns>A view of the widths.</returns>
    public IReadOnlyList<double> Dx() => dx.AsReadOnly();

    /// <summary>The width of the bucket holding a coordinate.</summary>
    /// <param name="value">The coordinate.</param>
    /// <returns>The width.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> lies outside the range.</exception>
    public double Dx(double value) => dx[Locate(value)];

    /// <summary>A bucket's density, normalizing first.</summary>
    /// <param name="k">The bucket.</param>
    /// <returns>The density.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="k"/> isn't a bucket.</exception>
    public double Density(int k)
    {
        Normalize();
        return density[k];
    }

    /// <summary>The cumulative probability to a bucket's right edge, normalizing first.</summary>
    /// <param name="k">The bucket.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="k"/> isn't a bucket.</exception>
    public double Cumulative(int k)
    {
        Normalize();
        return cumulativeDensity[k];
    }

    /// <summary>The probability from a bucket's left edge on, normalizing first.</summary>
    /// <param name="k">The bucket.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="k"/> isn't a bucket.</exception>
    public double Excess(int k)
    {
        Normalize();
        return excessProbability[k];
    }

    /// <summary>The excess probability integrated to a bucket's left edge, normalizing first.</summary>
    /// <param name="k">The bucket.</param>
    /// <returns>The integral.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="k"/> isn't a bucket, or the distribution is a
    /// tranche's, which keeps no integral (QuantLib's empty vector).</exception>
    public double CumulativeExcess(int k)
    {
        Normalize();
        return cumulativeExcessProbability[k];
    }

    /// <summary>A bucket's average value.</summary>
    /// <param name="k">The bucket.</param>
    /// <returns>The average.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="k"/> isn't a bucket.</exception>
    public double Average(int k) => average[k];

    /// <summary>Adds a value to the bucket holding it, or to the underflow or overflow.</summary>
    /// <param name="value">The value.</param>
    public void Add(double value)
    {
        isNormalized = false;
        if (value < x[0])
        {
            underFlow++;
        }
        else
        {
            for (int i = 0; i < count.Count; i++)
            {
                if (x[i] + dx[i] > value)
                {
                    count[i]++;
                    average[i] += value;
                    return;
                }
            }

            overFlow++;
        }
    }

    /// <summary>Adds to a bucket's density.</summary>
    /// <param name="bucket">The bucket.</param>
    /// <param name="value">The density added.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bucket"/> isn't a bucket ("bucket out of
    /// range").</exception>
    public void AddDensity(int bucket, double value)
    {
        CheckBucket(bucket);
        isNormalized = false;
        density[bucket] += value;
    }

    /// <summary>Adds to a bucket's average.</summary>
    /// <param name="bucket">The bucket.</param>
    /// <param name="value">The value added.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bucket"/> isn't a bucket ("bucket out of
    /// range").</exception>
    public void AddAverage(int bucket, double value)
    {
        CheckBucket(bucket);
        isNormalized = false;
        average[bucket] += value;
    }

    /// <summary>
    /// Computes the densities of the added values, if any, and the cumulative and excess probabilities, unless done
    /// since the last addition.
    /// </summary>
    public void Normalize()
    {
        if (isNormalized)
        {
            return;
        }

        int total = underFlow + overFlow;
        for (int i = 0; i < size; i++)
        {
            total += count[i];
        }

        excessProbability[0] = 1.0;
        cumulativeExcessProbability[0] = 0.0;
        for (int i = 0; i < size; i++)
        {
            if (total > 0)
            {
                density[i] = 1.0 / dx[i] * count[i] / total;
                if (count[i] > 0)
                {
                    average[i] /= count[i];
                }
            }

            if (density[i] == 0.0)
            {
                average[i] = x[i] + (dx[i] / 2);
            }

            cumulativeDensity[i] = density[i] * dx[i];
            if (i > 0)
            {
                cumulativeDensity[i] += cumulativeDensity[i - 1];
                excessProbability[i] = 1.0 - cumulativeDensity[i - 1];
                cumulativeExcessProbability[i] = (excessProbability[i - 1] * dx[i - 1]) + cumulativeExcessProbability[i - 1];
            }
        }

        isNormalized = true;
    }

    /// <summary>The right edge of the first bucket whose cumulative probability exceeds a level, or the range's upper
    /// end.</summary>
    /// <param name="quantil">The level.</param>
    /// <returns>The edge.</returns>
    public double ConfidenceLevel(double quantil)
    {
        Normalize();
        for (int i = 0; i < size; i++)
        {
            if (cumulativeDensity[i] > quantil)
            {
                return x[i] + dx[i];
            }
        }

        return x[^1] + dx[^1];
    }

    /// <summary>The expected value, each bucket at its midpoint.</summary>
    /// <returns>The value.</returns>
    public double ExpectedValue()
    {
        Normalize();
        double expected = 0;
        for (int i = 0; i < size; i++)
        {
            double xi = x[i] + (dx[i] / 2);
            expected += xi * dx[i] * density[i];
        }

        return expected;
    }

    /// <summary>The expected value of a function, each bucket at its midpoint.</summary>
    /// <param name="f">The function.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is <see langword="null"/>.</exception>
    public double ExpectedValue(Func<double, double> f)
    {
        ArgumentNullException.ThrowIfNull(f);
        Normalize();
        double expected = 0;
        for (int i = 0; i < size; i++)
        {
            double xi = x[i] + (dx[i] / 2);
            expected += f(xi) * dx[i] * density[i];
        }

        return expected;
    }

    /// <summary>The expected tranche value, E[min(max(X − a, 0), d − a)], the buckets at their midpoints below
    /// <paramref name="d"/>.</summary>
    /// <param name="a">The attachment.</param>
    /// <param name="d">The detachment, positive and within the range.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="d"/> isn't positive or lies beyond the
    /// range.</exception>
    public double TrancheExpectedValue(double a, double d)
    {
        Normalize();
        double expected = 0;
        for (int i = 0; i < size; i++)
        {
            double xi = x[i] + (dx[i] / 2);
            if (xi < a)
            {
                continue;
            }

            if (xi > d)
            {
                break;
            }

            expected += (xi - a) * dx[i] * density[i];
        }

        expected += (d - a) * (1.0 - CumulativeDensity(d));

        return expected;
    }

    /// <summary>The excess probability integrated between the left edges of the buckets holding two
    /// coordinates.</summary>
    /// <param name="a">The interval's start, within the range.</param>
    /// <param name="b">The interval's end, within the range.</param>
    /// <returns>The integral.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="a"/> or <paramref name="b"/> lies outside the
    /// range.</exception>
    public double CumulativeExcessProbability(double a, double b)
    {
        Normalize();
        if (!(b <= xmax))
        {
            throw new ArgumentOutOfRangeException(nameof(b), b, string.Create(
                CultureInfo.InvariantCulture, $"end of interval {b} out of range [{xmin}, {xmax}]"));
        }

        if (!(a >= xmin))
        {
            throw new ArgumentOutOfRangeException(nameof(a), a, string.Create(
                CultureInfo.InvariantCulture, $"start of interval {a} out of range [{xmin}, {xmax}]"));
        }

        int i = Locate(a);
        int j = Locate(b);
        return cumulativeExcessProbability[j] - cumulativeExcessProbability[i];
    }

    /// <summary>The cumulative probability at a coordinate, linear within its bucket and from 0 in the first.</summary>
    /// <param name="value">The coordinate, positive and within the range.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> isn't positive ("x must be
    /// positive").</exception>
    /// <exception cref="NanoQLException"><paramref name="value"/> lies beyond the range ("beyond distribution
    /// cutoff").</exception>
    public double CumulativeDensity(double value)
    {
        double tiny = dx[^1] * 1e-3;
        if (!(value > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "x must be positive");
        }

        Normalize();
        if (trancheWidth is double w && value > w && value <= x[^1] + dx[^1] + tiny)
        {
            // past a tranche's width, every loss is below
            return cumulativeDensity[size - 1];
        }

        for (int i = 0; i < size; i++)
        {
            if (x[i] + dx[i] + tiny >= value)
            {
                // the cumulative at the first bucket's left edge is 0, where QuantLib's reads index -1
                double before = i > 0 ? cumulativeDensity[i - 1] : 0.0;
                double width = Width(i);
                if (value >= x[i] + width)
                {
                    return cumulativeDensity[i];
                }

                return (((value - x[i]) * cumulativeDensity[i]) + ((x[i] + width - value) * before)) / width;
            }
        }

        throw new NanoQLException(string.Create(
            CultureInfo.InvariantCulture, $"x = {value} beyond distribution cutoff {x[^1] + dx[^1]}"));
    }

    /// <summary>
    /// Transforms the distribution into its tranche's: for L_T = min(L, D) − min(L, A), shifted to the left by A, cut
    /// off at D − A with the mass above it in the last bucket, and a density spike at L_T = 0.
    /// </summary>
    /// <param name="attachmentPoint">The attachment A.</param>
    /// <param name="detachmentPoint">The detachment D, above A.</param>
    /// <exception cref="ArgumentException">A isn't below D ("attachment >= detachment point"), or either lies beyond
    /// the range ("attachment or detachment too large").</exception>
    public void Tranche(double attachmentPoint, double detachmentPoint)
    {
        if (!(attachmentPoint < detachmentPoint))
        {
            throw new ArgumentException("attachment >= detachment point", nameof(attachmentPoint));
        }

        if (!(x[^1] > attachmentPoint && x[^1] + dx[^1] >= detachmentPoint))
        {
            throw new ArgumentException("attachment or detachment too large", nameof(detachmentPoint));
        }

        Normalize();

        // shift
        while (x[0] < attachmentPoint)
        {
            x.RemoveAt(0);
            dx.RemoveAt(0);
            count.RemoveAt(0);
            density.RemoveAt(0);
            cumulativeDensity.RemoveAt(0);
            excessProbability.RemoveAt(0);
        }

        // remove losses over detachment point:
        int detachPosit = x.FindIndex(v => v > detachmentPoint);
        if (detachPosit >= 0)
        {
            x.RemoveRange(detachPosit + 1, x.Count - detachPosit - 1);
        }

        size = x.Count;
        cumulativeDensity.RemoveRange(size, cumulativeDensity.Count - size);

        // the last bucket keeps the mass above the detachment: its probability is 1 less the cumulative before it,
        // where QuantLib's computes it after setting the last cumulative to 1
        double lastMass = 1.0 - (size > 1 ? cumulativeDensity[size - 2] : 0.0);
        cumulativeDensity[^1] = 1.0;
        count.RemoveRange(size, count.Count - size);
        dx.RemoveRange(size, dx.Count - size);

        // truncate
        for (int i = 0; i < x.Count; i++)
        {
            x[i] = Math.Min(Math.Max(x[i] - attachmentPoint, 0.0), detachmentPoint - attachmentPoint);
        }

        trancheWidth = detachmentPoint - attachmentPoint;
        density.Clear();
        excessProbability.Clear();
        cumulativeExcessProbability.Clear();
        if (size == 1)
        {
            density.Add(lastMass / dx[0]);
            excessProbability.Add(1.0);
            return;
        }

        density.Add((cumulativeDensity[0] - 0.0) / dx[0]);
        excessProbability.Add(1.0);
        for (int i = 1; i < size - 1; i++)
        {
            excessProbability.Add(1.0 - cumulativeDensity[i - 1]);
            density.Add((cumulativeDensity[i] - cumulativeDensity[i - 1]) / dx[i]);
        }

        excessProbability.Add(lastMass);
        density.Add(lastMass / dx[^1]);
    }

    /// <summary>The index of the bucket holding a coordinate: of the last left edge at or below it.</summary>
    /// <param name="value">The coordinate, within the range or close to its ends.</param>
    /// <returns>The bucket.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> lies outside the range ("coordinate …
    /// out of range").</exception>
    public int Locate(double value)
    {
        double end = x[^1] + dx[^1];
        if (!((value >= x[0] || ComparisonFunctions.Close(value, x[0])) && (value <= end || ComparisonFunctions.Close(value, end))))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, string.Create(
                CultureInfo.InvariantCulture, $"coordinate {value} out of range [{x[0]}; {end}]"));
        }

        for (int i = 0; i < x.Count; i++)
        {
            if (x[i] > value)
            {
                return i - 1;
            }
        }

        return x.Count - 1;
    }

    /// <summary>
    /// The expected value above a percentile: the density above its quantile integrated, the quantile interpolated in
    /// its bucket, divided by 1 less the percentile; at 1, the right edge of the last bucket holding mass.
    /// </summary>
    /// <param name="percValue">The percentile, in [0, 1].</param>
    /// <returns>The expected shortfall.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="percValue"/> lies outside [0, 1] ("Incorrect
    /// percentile").</exception>
    public double ExpectedShortfall(double percValue)
    {
        if (!(percValue >= 0.0 && percValue <= 1.0))
        {
            throw new ArgumentOutOfRangeException(nameof(percValue), percValue, "Incorrect percentile");
        }

        Normalize();
        int k = 0;
        while (k < size && !(cumulativeDensity[k] > percValue))
        {
            k++;
        }

        if (k == size || percValue == 1.0)
        {
            int last = size - 1;
            while (last > 0 && !(Mass(last) > 0.0))
            {
                last--;
            }

            return x[last] + Width(last);
        }

        double before = k > 0 ? cumulativeDensity[k - 1] : 0.0;
        double quantile = x[k] + (Width(k) * (percValue - before) / (cumulativeDensity[k] - before));
        double expected = (cumulativeDensity[k] - percValue) * (quantile + x[k] + Width(k)) / 2;
        for (int i = k + 1; i < size; i++)
        {
            expected += Mass(i) * (x[i] + (Width(i) / 2));
        }

        return expected / (1.0 - percValue);
    }

    // A bucket's width, its upper edge at most a tranche's width.
    private double Width(int i) => trancheWidth is double w ? Math.Min(x[i] + dx[i], w) - x[i] : dx[i];

    private double Mass(int i) => cumulativeDensity[i] - (i > 0 ? cumulativeDensity[i - 1] : 0.0);

    private void CheckBucket(int bucket)
    {
        if (!(bucket >= 0 && bucket < size))
        {
            throw new ArgumentOutOfRangeException(nameof(bucket), bucket, "bucket out of range");
        }
    }

    /// <summary>The convolution of two distributions of equal buckets from 0: <see cref="ManipulateDistribution"/>'s
    /// access to the buckets.</summary>
    internal (List<double> Dx, List<double> Density, double Xmin, double Xmax) Buckets() => (dx, density, xmin, xmax);

    /// <summary>Sets a convolution's densities and its cumulative and excess probabilities.</summary>
    /// <param name="densities">The densities.</param>
    internal void SetConvolution(double[] densities)
    {
        for (int i = 0; i < size; i++)
        {
            density[i] = densities[i];
        }

        // update cumulated and excess
        excessProbability[0] = 1.0;
        for (int i = 0; i < size; i++)
        {
            cumulativeDensity[i] = density[i] * dx[i];
            if (i > 0)
            {
                cumulativeDensity[i] += cumulativeDensity[i - 1];
                excessProbability[i] = excessProbability[i - 1] - (density[i - 1] * dx[i - 1]);
            }
        }
    }
}
