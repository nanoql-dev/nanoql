// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/statistics/incrementalstatistics.hpp ql/math/statistics/incrementalstatistics.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.Utilities.DataFormattersFunctions;

namespace NanoQL.Numerics;

/// <summary>
/// A statistics tool based on incremental accumulation: it accumulates a set of data and returns their statistics
/// (mean, variance, skewness, kurtosis, error estimation, …) from running sums, keeping no sample.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's class wraps Boost.Accumulators' weighted accumulator set (count, minimum, maximum, weighted mean,
/// variance, skewness and kurtosis, and the sum of weights) and a second set over the negative samples (count,
/// weighted second moment, sum of weights). This port keeps each feature's running value and updates them in Boost's
/// order, through the ported Boost arithmetic: the weighted variance's iterative recurrence on the lazy weighted mean,
/// the moments' powers by squaring, the minimum and maximum starting from ±<see cref="double.MaxValue"/>. That order
/// gives QuantLib's results bit for bit.
/// </para>
/// <para>
/// A working type: <see cref="Add"/> and <see cref="Reset"/> change it; it isn't thread-safe. <see cref="Skewness"/>
/// and <see cref="Kurtosis"/> refuse a set whose samples weigh 0 in all, with the text <see cref="Mean"/> refuses it
/// with, where QuantLib's divide 0 by 0 and return NaN: a documented divergence. The weighted variance's recurrence
/// resets the variance to 0 at a sample whose predecessors' weights vanish in the sum (they weigh 0, or less than its
/// rounding), where Boost's divides 0 by 0, or x by 0, and leaves <see cref="Variance"/> NaN or infinite for good: a
/// documented divergence, the variance then that of the samples that carry the weight, as
/// <see cref="GeneralStatistics"/>' is.
/// </para>
/// </remarks>
[WorkingType]
public sealed class IncrementalStatistics : IStatistics
{
    // acc_: count, min, max, the sum of weights, the weighted sum, the weighted variance and the weighted moments' sums
    private int count;
    private double min = double.MaxValue;
    private double max = -double.MaxValue;
    private double sumOfWeights;
    private double weightedSum;
    private double weightedVariance;
    private double moment2Sum;
    private double moment3Sum;
    private double moment4Sum;

    // downsideAcc_: count, the weighted second moment's sum and the sum of weights of the negative samples
    private int downsideCount;
    private double downsideMoment2Sum;
    private double downsideSumOfWeights;

    /// <summary>Initializes a new instance of the <see cref="IncrementalStatistics"/> class, with no samples.</summary>
    public IncrementalStatistics()
    {
    }

    /// <inheritdoc/>
    public int Samples => count;

    /// <summary>Gets the number of negative samples collected.</summary>
    public int DownsideSamples => downsideCount;

    /// <inheritdoc/>
    public double WeightSum() => sumOfWeights;

    /// <summary>The mean, ⟨x⟩ = Σ wᵢxᵢ / Σ wᵢ.</summary>
    /// <returns>The mean.</returns>
    /// <exception cref="NanoQLException">The samples weigh 0 in all, or there's none.</exception>
    public double Mean()
    {
        if (!(WeightSum() > 0.0))
        {
            throw new NanoQLException("sampleWeight_= 0, unsufficient");
        }

        return WeightedMeanFunctions.WeightedMean(weightedSum, sumOfWeights);
    }

    /// <summary>The variance, N/(N − 1) ⟨(x − ⟨x⟩)²⟩.</summary>
    /// <returns>The variance.</returns>
    /// <exception cref="NanoQLException">The samples weigh 0 in all, or there are fewer than two.</exception>
    public double Variance()
    {
        if (!(WeightSum() > 0.0))
        {
            throw new NanoQLException("sampleWeight_= 0, unsufficient");
        }

        if (!(Samples > 1))
        {
            throw new NanoQLException("sample number <= 1, unsufficient");
        }

        double n = Samples;
        return n / (n - 1.0) * weightedVariance;
    }

    /// <summary>The standard deviation σ, the square root of the variance.</summary>
    /// <returns>The standard deviation.</returns>
    /// <exception cref="NanoQLException">The samples weigh 0 in all, or there are fewer than two.</exception>
    public double StandardDeviation() => Math.Sqrt(Variance());

    /// <summary>The error estimate ε, the square root of the ratio of the variance to the number of samples.</summary>
    /// <returns>The error estimate.</returns>
    /// <exception cref="NanoQLException">The samples weigh 0 in all, or there are fewer than two.</exception>
    public double ErrorEstimate() => Math.Sqrt(Variance() / Samples);

    /// <summary>
    /// The skewness, N²/((N − 1)(N − 2)) ⟨(x − ⟨x⟩)³⟩/σ³, which evaluates to 0 for a Gaussian distribution.
    /// </summary>
    /// <returns>The skewness.</returns>
    /// <exception cref="NanoQLException">
    /// There are fewer than three samples, or they weigh 0 in all (a divergence: QuantLib returns NaN).
    /// </exception>
    public double Skewness()
    {
        if (!(Samples > 2))
        {
            throw new NanoQLException("sample number <= 2, unsufficient");
        }

        // QuantLib's Boost moments divide by the sum of weights, 0/0 where the samples weigh 0 in all.
        if (!(WeightSum() > 0.0))
        {
            throw new NanoQLException("sampleWeight_= 0, unsufficient");
        }

        double n = Samples;
        double r1 = n / (n - 2.0);
        double r2 = (n - 1.0) / (n - 2.0);
        return Math.Sqrt(r1 * r2) * WeightedSkewness();
    }

    /// <summary>
    /// The excess kurtosis, N²(N + 1)/((N − 1)(N − 2)(N − 3)) ⟨(x − ⟨x⟩)⁴⟩/σ⁴ − 3(N − 1)²/((N − 2)(N − 3)), which
    /// evaluates to 0 for a Gaussian distribution.
    /// </summary>
    /// <returns>The excess kurtosis.</returns>
    /// <exception cref="NanoQLException">
    /// There are fewer than four samples, or they weigh 0 in all (a divergence: QuantLib returns NaN).
    /// </exception>
    public double Kurtosis()
    {
        if (!(Samples > 3))
        {
            throw new NanoQLException("sample number <= 3, unsufficient");
        }

        // QuantLib's Boost moments divide by the sum of weights, 0/0 where the samples weigh 0 in all.
        if (!(WeightSum() > 0.0))
        {
            throw new NanoQLException("sampleWeight_= 0, unsufficient");
        }

        double n = Samples;
        double r1 = (n - 1.0) / (n - 2.0);
        double r2 = (n + 1.0) / (n - 3.0);
        double r3 = (n - 1.0) / (n - 3.0);
        return (((3.0 + WeightedKurtosis()) * r2) - (3.0 * r3)) * r1;
    }

    /// <summary>The smallest sample.</summary>
    /// <returns>The minimum.</returns>
    /// <exception cref="NanoQLException">There's no sample.</exception>
    public double Min()
    {
        if (!(Samples > 0))
        {
            throw new NanoQLException("empty sample set");
        }

        return min;
    }

    /// <summary>The largest sample.</summary>
    /// <returns>The maximum.</returns>
    /// <exception cref="NanoQLException">There's no sample.</exception>
    public double Max()
    {
        if (!(Samples > 0))
        {
            throw new NanoQLException("empty sample set");
        }

        return max;
    }

    /// <summary>The sum of the negative samples' weights.</summary>
    /// <returns>The sum of the weights.</returns>
    public double DownsideWeightSum() => downsideSumOfWeights;

    /// <summary>
    /// The downside variance, N/(N − 1) × Σ θᵢxᵢ² / Σ wᵢ, where θ = 0 if x &gt; 0 and θ = 1 if x &lt; 0, over the
    /// negative samples.
    /// </summary>
    /// <returns>The downside variance.</returns>
    /// <exception cref="NanoQLException">The negative samples weigh 0 in all, or there are fewer than two.</exception>
    public double DownsideVariance()
    {
        if (!(DownsideWeightSum() > 0.0))
        {
            throw new NanoQLException("sampleWeight_= 0, unsufficient");
        }

        if (!(DownsideSamples > 1))
        {
            throw new NanoQLException("sample number <= 1, unsufficient");
        }

        double n = DownsideSamples;
        double r1 = n / (n - 1.0);
        return r1 * WeightedMomentFunctions.Result(downsideMoment2Sum, downsideSumOfWeights);
    }

    /// <summary>The downside deviation, the square root of the downside variance.</summary>
    /// <returns>The downside deviation.</returns>
    /// <exception cref="NanoQLException">The negative samples weigh 0 in all, or there are fewer than two.</exception>
    public double DownsideDeviation() => Math.Sqrt(DownsideVariance());

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="weight"/> is negative or NaN.</exception>
    public void Add(double value, double weight = 1.0)
    {
        if (!(weight >= 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(weight), $"negative weight ({General(weight)}) not allowed");
        }

        // acc_(value, weight = valueWeight): Boost visits the features in dependency order, so the weighted variance
        // reads the count, the sum of weights and the weighted mean the sample has updated.
        count++;
        if (value < min)
        {
            min = value;
        }

        if (max < value)
        {
            max = value;
        }

        sumOfWeights += weight;
        weightedSum += value * weight;
        weightedVariance = WeightedVarianceFunctions.Add(
            weightedVariance, count, sumOfWeights, weight, value, WeightedMeanFunctions.WeightedMean(weightedSum, sumOfWeights));
        moment2Sum = WeightedMomentFunctions.Add(moment2Sum, weight, value, 2);
        moment3Sum = WeightedMomentFunctions.Add(moment3Sum, weight, value, 3);
        moment4Sum = WeightedMomentFunctions.Add(moment4Sum, weight, value, 4);

        if (value < 0.0)
        {
            downsideCount++;
            downsideMoment2Sum = WeightedMomentFunctions.Add(downsideMoment2Sum, weight, value, 2);
            downsideSumOfWeights += weight;
        }
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
        count = downsideCount = 0;
        min = double.MaxValue;
        max = -double.MaxValue;
        sumOfWeights = weightedSum = weightedVariance = moment2Sum = moment3Sum = moment4Sum = 0.0;
        downsideMoment2Sum = downsideSumOfWeights = 0.0;
    }

    /// <summary>Reserves nothing: the accumulator keeps no sample, so adding one never allocates.</summary>
    /// <param name="n">The number of samples planned.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    public void Reserve(int n) => ArgumentOutOfRangeException.ThrowIfNegative(n);

    private double WeightedSkewness()
    {
        double mean = WeightedMeanFunctions.WeightedMean(weightedSum, sumOfWeights);
        return WeightedSkewnessFunctions.WeightedSkewness(
            WeightedMomentFunctions.Result(moment2Sum, sumOfWeights),
            WeightedMomentFunctions.Result(moment3Sum, sumOfWeights),
            mean);
    }

    private double WeightedKurtosis()
    {
        double mean = WeightedMeanFunctions.WeightedMean(weightedSum, sumOfWeights);
        return WeightedKurtosisFunctions.WeightedKurtosis(
            WeightedMomentFunctions.Result(moment2Sum, sumOfWeights),
            WeightedMomentFunctions.Result(moment3Sum, sumOfWeights),
            WeightedMomentFunctions.Result(moment4Sum, sumOfWeights),
            mean);
    }
}
