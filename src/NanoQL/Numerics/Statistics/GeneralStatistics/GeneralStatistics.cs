// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/statistics/generalstatistics.hpp ql/math/statistics/generalstatistics.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.Utilities.DataFormattersFunctions;

namespace NanoQL.Numerics;

/// <summary>
/// A statistics tool: it accumulates a set of data and returns their statistics (mean, variance, skewness, kurtosis,
/// error estimation, percentile, …) based on the empirical distribution, with no Gaussian assumption.
/// </summary>
/// <remarks>
/// <para>
/// It doesn't suffer the numerical instability problem of <see cref="IncrementalStatistics"/>. The downside is that it
/// stores all samples, thus increasing the memory requirements.
/// </para>
/// <para>
/// A working type (S2R2): <see cref="Add"/>, <see cref="Reset"/>, <see cref="Reserve"/> and the sorting members
/// change it, where QuantLib's sorts through <c>mutable</c> members of a <c>const</c> object; it isn't thread-safe.
/// QuantLib's <c>Statistics</c> and <c>RiskStatistics</c> typedefs name this class with its Gaussian and empirical risk
/// measures, which <see cref="GaussianStatisticsExtensions"/> and <see cref="RiskStatisticsExtensions"/> give it.
/// </para>
/// <para>
/// Where every sample a member averages weighs 0, the member refuses, with the text QuantLib's percentile gives a set
/// of zero weight, where QuantLib's divides 0 by 0 and returns NaN; and a NaN sample can't be sorted, which QuantLib's
/// percentiles do with a sort that NaN leaves undefined. Both are documented divergences.
/// </para>
/// </remarks>
[WorkingType]
public sealed class GeneralStatistics : IEmpiricalStatistics
{
    private (double Value, double Weight)[] samples = [];
    private int count;
    private bool sorted = true;

    /// <summary>Initializes a new instance of the <see cref="GeneralStatistics"/> class, with no samples.</summary>
    public GeneralStatistics()
    {
    }

    /// <inheritdoc/>
    public int Samples => count;

    /// <inheritdoc/>
    public ReadOnlySpan<(double Value, double Weight)> Data => new(samples, 0, count);

    /// <inheritdoc/>
    public double WeightSum()
    {
        double result = 0.0;
        for (int i = 0; i < count; i++)
        {
            result += samples[i].Weight;
        }

        return result;
    }

    /// <summary>The mean, ⟨x⟩ = Σ wᵢxᵢ / Σ wᵢ.</summary>
    /// <returns>The mean.</returns>
    /// <exception cref="NanoQLException">There's no sample, or the samples weigh 0 in all.</exception>
    public double Mean()
    {
        if (!(count != 0))
        {
            throw new NanoQLException("empty sample set");
        }

        return ExpectationValue(static x => x).Value!.Value;
    }

    /// <summary>The variance, σ² = N/(N − 1) ⟨(x − ⟨x⟩)²⟩.</summary>
    /// <returns>The variance.</returns>
    /// <exception cref="NanoQLException">There are fewer than two samples, or the samples weigh 0 in all.</exception>
    public double Variance()
    {
        int n = count;
        if (!(n > 1))
        {
            throw new NanoQLException("sample number <=1, unsufficient");
        }

        // Subtract the mean and square. Repeat on the whole range.
        // Hopefully, the whole thing will be inlined in a single loop.
        double m = Mean();
        double s2 = ExpectationValue(x =>
        {
            double d = x - m;
            return d * d;
        }).Value!.Value;
        return s2 * n / (n - 1.0);
    }

    /// <summary>The standard deviation σ, the square root of the variance.</summary>
    /// <returns>The standard deviation.</returns>
    /// <exception cref="NanoQLException">There are fewer than two samples, or the samples weigh 0 in all.</exception>
    public double StandardDeviation() => Math.Sqrt(Variance());

    /// <summary>The error estimate on the mean, ε = σ/√N.</summary>
    /// <returns>The error estimate.</returns>
    /// <exception cref="NanoQLException">There are fewer than two samples, or the samples weigh 0 in all.</exception>
    public double ErrorEstimate() => Math.Sqrt(Variance() / Samples);

    /// <summary>
    /// The skewness, N²/((N − 1)(N − 2)) ⟨(x − ⟨x⟩)³⟩/σ³, which evaluates to 0 for a Gaussian distribution.
    /// </summary>
    /// <returns>The skewness.</returns>
    /// <exception cref="NanoQLException">There are fewer than three samples, or the samples weigh 0 in all.</exception>
    public double Skewness()
    {
        int n = count;
        if (!(n > 2))
        {
            throw new NanoQLException("sample number <=2, unsufficient");
        }

        double m = Mean();
        double x3 = ExpectationValue(x =>
        {
            double d = x - m;
            return d * d * d;
        }).Value!.Value;
        double sigma = StandardDeviation();

        return x3 / (sigma * sigma * sigma) * (n / (n - 1.0)) * (n / (n - 2.0));
    }

    /// <summary>
    /// The excess kurtosis, N²(N + 1)/((N − 1)(N − 2)(N − 3)) ⟨(x − ⟨x⟩)⁴⟩/σ⁴ − 3(N − 1)²/((N − 2)(N − 3)), which
    /// evaluates to 0 for a Gaussian distribution.
    /// </summary>
    /// <returns>The excess kurtosis.</returns>
    /// <exception cref="NanoQLException">There are fewer than four samples, or the samples weigh 0 in all.</exception>
    public double Kurtosis()
    {
        int n = count;
        if (!(n > 3))
        {
            throw new NanoQLException("sample number <=3, unsufficient");
        }

        double m = Mean();
        double x4 = ExpectationValue(x =>
        {
            double d = x - m;
            double d2 = d * d;
            return d2 * d2;
        }).Value!.Value;
        double sigma2 = Variance();

        double c1 = n / (n - 1.0) * (n / (n - 2.0)) * ((n + 1.0) / (n - 3.0));
        double c2 = 3.0 * ((n - 1.0) / (n - 2.0)) * ((n - 1.0) / (n - 3.0));

        return (c1 * (x4 / (sigma2 * sigma2))) - c2;
    }

    /// <summary>The smallest sample: the first of the samples least by value, then by weight.</summary>
    /// <returns>The minimum.</returns>
    /// <exception cref="NanoQLException">There's no sample.</exception>
    public double Min()
    {
        if (!(Samples > 0))
        {
            throw new NanoQLException("empty sample set");
        }

        // std::min_element over the (value, weight) pairs: the first least by the pair's operator<.
        int best = 0;
        for (int i = 1; i < count; i++)
        {
            if (Less(samples[i], samples[best]))
            {
                best = i;
            }
        }

        return samples[best].Value;
    }

    /// <summary>The largest sample: the first of the samples greatest by value, then by weight.</summary>
    /// <returns>The maximum.</returns>
    /// <exception cref="NanoQLException">There's no sample.</exception>
    public double Max()
    {
        if (!(Samples > 0))
        {
            throw new NanoQLException("empty sample set");
        }

        // std::max_element over the (value, weight) pairs: the first greatest by the pair's operator<.
        int best = 0;
        for (int i = 1; i < count; i++)
        {
            if (Less(samples[best], samples[i]))
            {
                best = i;
            }
        }

        return samples[best].Value;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The samples in the range weigh 0 in all.</exception>
    public (double? Value, int Samples) ExpectationValue(Func<double, double> f, Func<double, bool> inRange)
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentNullException.ThrowIfNull(inRange);
        double num = 0.0, den = 0.0;
        int n = 0;
        for (int i = 0; i < count; i++)
        {
            double x = samples[i].Value, w = samples[i].Weight;
            if (inRange(x))
            {
                num += f(x) * w;
                den += w;
                n += 1;
            }
        }

        if (n == 0)
        {
            return (null, 0);
        }

        // QuantLib returns num/den, 0/0 where the samples in range weigh 0 in all.
        if (!(den > 0.0))
        {
            throw new NanoQLException("empty sample set");
        }

        return (num / den, n);
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The samples weigh 0 in all.</exception>
    public (double? Value, int Samples) ExpectationValue(Func<double, double> f) =>
        ExpectationValue(f, static x => true);

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="y"/> is outside (0, 1].</exception>
    /// <exception cref="NanoQLException">The samples weigh 0 in all, or one is NaN.</exception>
    public double Percentile(double y)
    {
        if (!(y > 0.0 && y <= 1.0))
        {
            throw new ArgumentOutOfRangeException(nameof(y), $"percentile ({General(y)}) must be in (0.0, 1.0]");
        }

        double sampleWeight = WeightSum();
        if (!(sampleWeight > 0.0))
        {
            throw new NanoQLException("empty sample set");
        }

        Sort();

        int k = 0, l = count - 1;
        /* the sum of weight is non null, therefore there's
           at least one sample */
        double integral = samples[k].Weight, target = y * sampleWeight;
        while (integral < target && k != l)
        {
            ++k;
            integral += samples[k].Weight;
        }

        return samples[k].Value;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="y"/> is outside (0, 1].</exception>
    /// <exception cref="NanoQLException">The samples weigh 0 in all, or one is NaN.</exception>
    public double TopPercentile(double y)
    {
        if (!(y > 0.0 && y <= 1.0))
        {
            throw new ArgumentOutOfRangeException(nameof(y), $"percentile ({General(y)}) must be in (0.0, 1.0]");
        }

        double sampleWeight = WeightSum();
        if (!(sampleWeight > 0.0))
        {
            throw new NanoQLException("empty sample set");
        }

        Sort();

        int k = count - 1, l = 0;
        /* the sum of weight is non null, therefore there's
           at least one sample */
        double integral = samples[k].Weight, target = y * sampleWeight;
        while (integral < target && k != l)
        {
            --k;
            integral += samples[k].Weight;
        }

        return samples[k].Value;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="weight"/> is negative or NaN.</exception>
    public void Add(double value, double weight = 1.0)
    {
        if (!(weight >= 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(weight), "negative weight not allowed");
        }

        if (count == samples.Length)
        {
            // std::vector's growth by a half, as MSVC's emplace_back grows it.
            Array.Resize(ref samples, Math.Max(count + 1, count + (count / 2)));
        }

        samples[count++] = (value, weight);
        sorted = false;
    }

    /// <summary>Adds a sequence of samples, each with weight 1.</summary>
    /// <param name="data">The samples.</param>
    public void AddSequence(ReadOnlySpan<double> data)
    {
        foreach (double x in data)
        {
            Add(x);
        }
    }

    /// <summary>Adds a sequence of samples, each with its weight.</summary>
    /// <param name="data">The samples.</param>
    /// <param name="weights">Their weights, positive or 0, at least as many as the samples.</param>
    /// <exception cref="ArgumentException">
    /// There are fewer weights than samples, where QuantLib reads past the weights' end (a divergence: nanoql refuses it).
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">A weight is negative or NaN.</exception>
    public void AddSequence(ReadOnlySpan<double> data, ReadOnlySpan<double> weights)
    {
        if (!(weights.Length >= data.Length))
        {
            throw new ArgumentException(
                $"{weights.Length} weights given for {data.Length} samples", nameof(weights));
        }

        for (int i = 0; i < data.Length; i++)
        {
            Add(data[i], weights[i]);
        }
    }

    /// <inheritdoc/>
    public void Reset()
    {
        samples = [];
        count = 0;
        sorted = true;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    public void Reserve(int n)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        if (n > samples.Length)
        {
            Array.Resize(ref samples, n);
        }
    }

    /// <summary>Sorts the data set in increasing order of value, then of weight.</summary>
    /// <exception cref="NanoQLException">
    /// A sample is NaN, which QuantLib's sort leaves undefined (a divergence: nanoql refuses it).
    /// </exception>
    public void Sort()
    {
        if (!sorted)
        {
            // std::sort needs a strict weak ordering, which a NaN breaks: QuantLib's sort is undefined then.
            for (int i = 0; i < count; i++)
            {
                if (double.IsNaN(samples[i].Value))
                {
                    throw new NanoQLException("NaN sample not allowed in a sort");
                }
            }

            // A stable sort: pairs equal by the operator< (-0.0 and 0.0 of one weight) keep their order, as MSVC's
            // std::sort keeps it up to 32 elements.
            var keyed = new (double Value, double Weight, int Index)[count];
            for (int i = 0; i < count; i++)
            {
                keyed[i] = (samples[i].Value, samples[i].Weight, i);
            }

            Array.Sort(keyed, static (a, b) =>
                a.Value < b.Value ? -1
                : b.Value < a.Value ? 1
                : a.Weight < b.Weight ? -1
                : b.Weight < a.Weight ? 1
                : a.Index.CompareTo(b.Index));
            for (int i = 0; i < count; i++)
            {
                samples[i] = (keyed[i].Value, keyed[i].Weight);
            }

            sorted = true;
        }
    }

    // std::pair's operator<: by value, then by weight.
    private static bool Less((double Value, double Weight) a, (double Value, double Weight) b) =>
        a.Value < b.Value || (!(b.Value < a.Value) && a.Weight < b.Weight);
}
