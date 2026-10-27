// SPDX-FileCopyrightText: Copyright (C) 2005 Gary Kennedy
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/statistics/convergencestatistics.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>
/// A one-dimensional statistics with a convergence table: the mean, stored each time the number of samples reaches the
/// next of the sampling rule's sizes.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's <c>ConvergenceStatistics&lt;T, U&gt;</c> derives from its type parameter, which C# can't; this holds its
/// statistics and forwards <see cref="IStatistics"/> to it (the S-mmp plan's MMPR2 (c)). A working type:
/// <see cref="Add"/> and <see cref="Reset"/> change it, and it isn't thread-safe.
/// </para>
/// <para>
/// <see cref="Statistics"/> is the held statistics itself: a sample added through it reaches the statistics but not
/// the table.
/// </para>
/// </remarks>
/// <typeparam name="TStats">The statistics held.</typeparam>
/// <typeparam name="TRule">The sampling rule, <see cref="DoublingConvergenceSteps"/> for QuantLib's
/// default.</typeparam>
public sealed class ConvergenceStatistics<TStats, TRule> : IStatistics
    where TStats : IStatistics, new()
    where TRule : struct, IConvergenceSteps
{
    private readonly TStats statistics;
    private readonly List<(int Samples, double Mean)> table = [];
    private TRule samplingRule;
    private int nextSampleSize;

    /// <summary>Initializes a new instance of the <see cref="ConvergenceStatistics{TStats, TRule}"/> class.</summary>
    /// <param name="rule">The sampling rule.</param>
    public ConvergenceStatistics(TRule rule = default)
        : this(new TStats(), rule)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConvergenceStatistics{TStats, TRule}"/> class on the given
    /// statistics, which it holds and resets, as QuantLib's constructor resets the copy it takes.
    /// </summary>
    /// <param name="statistics">The statistics to hold.</param>
    /// <param name="rule">The sampling rule.</param>
    public ConvergenceStatistics(TStats statistics, TRule rule = default)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        this.statistics = statistics;
        samplingRule = rule;
        Reset();
    }

    /// <summary>Gets the held statistics: a sample added through it isn't recorded in the table.</summary>
    public TStats Statistics => statistics;

    /// <inheritdoc/>
    public int Samples => statistics.Samples;

    /// <summary>
    /// Gets the convergence table: each stored number of samples with the mean at that number, in the order stored.
    /// </summary>
    public ImmutableArray<(int Samples, double Mean)> ConvergenceTable => [.. table];

    /// <inheritdoc/>
    public double WeightSum() => statistics.WeightSum();

    /// <inheritdoc/>
    public double Mean() => statistics.Mean();

    /// <inheritdoc/>
    public double Variance() => statistics.Variance();

    /// <inheritdoc/>
    public double StandardDeviation() => statistics.StandardDeviation();

    /// <inheritdoc/>
    public double ErrorEstimate() => statistics.ErrorEstimate();

    /// <inheritdoc/>
    public double Skewness() => statistics.Skewness();

    /// <inheritdoc/>
    public double Kurtosis() => statistics.Kurtosis();

    /// <inheritdoc/>
    public double Min() => statistics.Min();

    /// <inheritdoc/>
    public double Max() => statistics.Max();

    /// <summary>
    /// Adds a sample, with a weight, and stores the mean in the table where the number of samples reaches the next
    /// size.
    /// </summary>
    /// <param name="value">The sample.</param>
    /// <param name="weight">Its weight.</param>
    public void Add(double value, double weight = 1.0)
    {
        statistics.Add(value, weight);
        if (statistics.Samples == nextSampleSize)
        {
            table.Add((statistics.Samples, statistics.Mean()));
            nextSampleSize = samplingRule.NextSamples(nextSampleSize);
        }
    }

    /// <summary>Adds samples, each with weight 1.</summary>
    /// <param name="data">The samples.</param>
    public void AddSequence(ReadOnlySpan<double> data)
    {
        foreach (double x in data)
        {
            Add(x);
        }
    }

    /// <summary>Adds samples, each with its weight.</summary>
    /// <param name="data">The samples.</param>
    /// <param name="weights">Their weights, at least as many as the samples.</param>
    /// <exception cref="ArgumentException">There are fewer weights than samples.</exception>
    public void AddSequence(ReadOnlySpan<double> data, ReadOnlySpan<double> weights)
    {
        if (weights.Length < data.Length)
        {
            throw new ArgumentException(
                $"{weights.Length} weights given for {data.Length} samples", nameof(weights));
        }

        for (int i = 0; i < data.Length; ++i)
        {
            Add(data[i], weights[i]);
        }
    }

    /// <summary>Resets the statistics and the table, the next mean stored at the rule's initial size.</summary>
    public void Reset()
    {
        statistics.Reset();
        nextSampleSize = samplingRule.InitialSamples;
        table.Clear();
    }

    /// <inheritdoc/>
    public void Reserve(int n) => statistics.Reserve(n);
}
