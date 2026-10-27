// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/statistics/sequencestatistics.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>
/// The members of <see cref="GenericSequenceStatistics{TStats}"/> that need more of its type parameter than its class
/// does, lifted over the components: the empirical risk measures, where each component's statistics keeps its samples
/// (<see cref="IEmpiricalStatistics"/>, with the members of <see cref="RiskStatisticsExtensions"/>), and the downside
/// variance and deviation of <see cref="IncrementalStatistics"/>, its own. QuantLib's template compiles a lifted member
/// only where its type parameter has it; C# 14 extension members with these constraints keep that.
/// </summary>
public static class GenericSequenceStatisticsExtensions
{
    extension<TStats>(GenericSequenceStatistics<TStats> statistics)
        where TStats : IEmpiricalStatistics, new()
    {
        /// <summary>Each component's empirical percentile.</summary>
        /// <param name="y">The percentile, in (0, 1].</param>
        /// <returns>The percentiles.</returns>
        public ImmutableArray<double> Percentile(double y) => Checked(statistics).Lift(s => s.Percentile(y));

        /// <summary>Each component's potential upside at a percentile.</summary>
        /// <param name="percentile">The percentile, in [0.9, 1).</param>
        /// <returns>The potential upsides.</returns>
        public ImmutableArray<double> PotentialUpside(double percentile) =>
            Checked(statistics).Lift(s => s.PotentialUpside(percentile));

        /// <summary>Each component's value-at-risk at a percentile.</summary>
        /// <param name="percentile">The percentile, in [0.9, 1).</param>
        /// <returns>The values-at-risk.</returns>
        public ImmutableArray<double> ValueAtRisk(double percentile) =>
            Checked(statistics).Lift(s => s.ValueAtRisk(percentile));

        /// <summary>Each component's expected shortfall at a percentile.</summary>
        /// <param name="percentile">The percentile, in [0.9, 1).</param>
        /// <returns>The expected shortfalls.</returns>
        public ImmutableArray<double> ExpectedShortfall(double percentile) =>
            Checked(statistics).Lift(s => s.ExpectedShortfall(percentile));

        /// <summary>Each component's regret: the variance of its observations below a target.</summary>
        /// <param name="target">The target.</param>
        /// <returns>The regrets.</returns>
        public ImmutableArray<double> Regret(double target) => Checked(statistics).Lift(s => s.Regret(target));

        /// <summary>Each component's probability of falling below a target.</summary>
        /// <param name="target">The target.</param>
        /// <returns>The shortfalls.</returns>
        public ImmutableArray<double> Shortfall(double target) => Checked(statistics).Lift(s => s.Shortfall(target));

        /// <summary>Each component's average shortfall below a target.</summary>
        /// <param name="target">The target.</param>
        /// <returns>The average shortfalls.</returns>
        public ImmutableArray<double> AverageShortfall(double target) =>
            Checked(statistics).Lift(s => s.AverageShortfall(target));

        /// <summary>Each component's semi-variance, the variance of its observations below its mean.</summary>
        /// <returns>The semi-variances.</returns>
        public ImmutableArray<double> SemiVariance() => Checked(statistics).Lift(static s => s.SemiVariance());

        /// <summary>Each component's semi-deviation.</summary>
        /// <returns>The semi-deviations.</returns>
        public ImmutableArray<double> SemiDeviation() => Checked(statistics).Lift(static s => s.SemiDeviation());

        /// <summary>Each component's downside variance, the variance of its observations below 0.</summary>
        /// <returns>The downside variances.</returns>
        public ImmutableArray<double> DownsideVariance() => Checked(statistics).Lift(static s => s.DownsideVariance());

        /// <summary>Each component's downside deviation.</summary>
        /// <returns>The downside deviations.</returns>
        public ImmutableArray<double> DownsideDeviation() =>
            Checked(statistics).Lift(static s => s.DownsideDeviation());
    }

    extension(GenericSequenceStatistics<IncrementalStatistics> statistics)
    {
        /// <summary>Each component's downside variance, <see cref="IncrementalStatistics.DownsideVariance"/>.</summary>
        /// <returns>The downside variances.</returns>
        public ImmutableArray<double> DownsideVariance() => Checked(statistics).Lift(static s => s.DownsideVariance());

        /// <summary>Each component's downside deviation,
        /// <see cref="IncrementalStatistics.DownsideDeviation"/>.</summary>
        /// <returns>The downside deviations.</returns>
        public ImmutableArray<double> DownsideDeviation() =>
            Checked(statistics).Lift(static s => s.DownsideDeviation());
    }

    private static GenericSequenceStatistics<T> Checked<T>(GenericSequenceStatistics<T> statistics)
        where T : IStatistics, new()
    {
        ArgumentNullException.ThrowIfNull(statistics);
        return statistics;
    }
}
