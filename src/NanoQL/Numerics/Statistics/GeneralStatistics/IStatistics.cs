// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/statistics/generalstatistics.hpp ql/math/statistics/generalstatistics.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A statistics accumulator: the members QuantLib's Monte Carlo framework and Gaussian risk measures read of their
/// statistics type parameter, which both of QuantLib's accumulators have. <see cref="GeneralStatistics"/> keeps its
/// samples; <see cref="IncrementalStatistics"/> keeps running sums.
/// </summary>
/// <remarks>
/// An accumulator is a working type: <see cref="Add"/>, <see cref="Reset"/> and <see cref="Reserve"/> change it, and it
/// isn't thread-safe. The Gaussian risk measures of <see cref="GaussianStatisticsExtensions"/> apply to it; the
/// empirical ones of <see cref="RiskStatisticsExtensions"/> to a sample-keeping one, <see cref="IEmpiricalStatistics"/>.
/// </remarks>
[WorkingType]
public interface IStatistics : ISummaryStatistics
{
    /// <summary>Gets the number of samples collected.</summary>
    int Samples { get; }

    /// <summary>The sum of the samples' weights.</summary>
    /// <returns>The sum of the weights.</returns>
    double WeightSum();

    /// <summary>The variance, N/(N − 1) times the weighted mean of the squared deviations from the mean.</summary>
    /// <returns>The variance.</returns>
    double Variance();

    /// <summary>The error estimate on the mean, σ/√N.</summary>
    /// <returns>The error estimate.</returns>
    double ErrorEstimate();

    /// <summary>The skewness, 0 for a Gaussian distribution.</summary>
    /// <returns>The skewness.</returns>
    double Skewness();

    /// <summary>The excess kurtosis, 0 for a Gaussian distribution.</summary>
    /// <returns>The excess kurtosis.</returns>
    double Kurtosis();

    /// <summary>The smallest sample.</summary>
    /// <returns>The minimum.</returns>
    double Min();

    /// <summary>The largest sample.</summary>
    /// <returns>The maximum.</returns>
    double Max();

    /// <summary>Adds a sample, with a weight.</summary>
    /// <param name="value">The sample.</param>
    /// <param name="weight">Its weight, positive or 0.</param>
    void Add(double value, double weight = 1.0);

    /// <summary>Resets the accumulator to an empty set.</summary>
    void Reset();

    /// <summary>
    /// Informs the accumulator of a planned number of samples, so that adding up to that many allocates nothing:
    /// QuantLib's <c>GeneralStatistics::reserve</c>. An accumulator that keeps no samples reserves nothing.
    /// </summary>
    /// <param name="n">The number of samples planned.</param>
    void Reserve(int n);
}
