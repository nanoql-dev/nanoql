// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/statistics/sequencestatistics.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>
/// Statistics of N-dimensional samples: each component's one-dimensional statistics, kept by a
/// <typeparamref name="TStats"/> per component, and the samples' covariance and correlation.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-mmp plan's MMPR2): <see cref="Add"/> and <see cref="Reset"/> change it, and it isn't
/// thread-safe. Its dimension is set by the constructor, by <see cref="Reset"/>, or by the first sample added to a
/// statistics of dimension 0, QuantLib's lazy dimension.
/// </para>
/// <para>
/// Each member of <see cref="IStatistics"/> and of the Gaussian layer (<see cref="GaussianStatisticsExtensions"/>) is
/// lifted over the components, returning one value per component; the empirical risk measures, which need a statistics
/// that keeps its samples, are extension members on statistics over an <see cref="IEmpiricalStatistics"/>
/// (<see cref="GenericSequenceStatisticsExtensions"/>), as QuantLib's template compiles a lifted member only where its
/// type parameter has it.
/// </para>
/// <para>
/// An extension point (U13): QuantLib's protected members are protected here, <see cref="Dimension"/>,
/// <see cref="ComponentStatistics"/> and <see cref="QuadraticSum"/>, and <see cref="Add"/> and <see cref="Reset"/> are
/// virtual. The constructor, and <see cref="Add"/> where the first sample sets the dimension, reset this class's own
/// state, not a derived class's, as QuantLib's call its own <c>reset</c>: a derived override isn't run before its
/// class's constructor.
/// </para>
/// </remarks>
/// <typeparam name="TStats">The one-dimensional statistics of each component.</typeparam>
[WorkingType]
public class GenericSequenceStatistics<TStats>
    where TStats : IStatistics, new()
{
    private int dimension;
    private TStats[] stats = [];
    private Matrix quadraticSum = new();

    /// <summary>Initializes a new instance of the <see cref="GenericSequenceStatistics{TStats}"/> class.</summary>
    /// <param name="dimension">The samples' dimension; 0 to take it from the first sample.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dimension"/> is negative.</exception>
    public GenericSequenceStatistics(int dimension = 0)
    {
        ResetCore(dimension);
    }

    /// <summary>Gets the samples' dimension, 0 before the first sample of a statistics built without one.</summary>
    public int Size => dimension;

    /// <summary>Gets the number of samples collected.</summary>
    public int Samples => stats.Length == 0 ? 0 : stats[0].Samples;

    /// <summary>Gets the dimension, QuantLib's protected <c>dimension_</c>.</summary>
    protected int Dimension => dimension;

    /// <summary>Gets each component's statistics, QuantLib's protected <c>stats_</c>: a view of the held
    /// array.</summary>
    protected ReadOnlySpan<TStats> ComponentStatistics => stats;

    /// <summary>
    /// Gets the weighted sum of the samples' outer products, Σ wₖ xₖ xₖᵀ, QuantLib's protected <c>quadraticSum_</c>: a
    /// read-only view of its elements, row by row.
    /// </summary>
    protected ReadOnlySpan<double> QuadraticSum => quadraticSum.AsSpan();

    /// <summary>The sum of the samples' weights.</summary>
    /// <returns>The sum of the weights, 0 with no component.</returns>
    public double WeightSum() => stats.Length == 0 ? 0.0 : stats[0].WeightSum();

    /// <summary>
    /// The covariance matrix, N/(N − 1) (Σ wₖ xₖ xₖᵀ / Σ wₖ − m mᵀ), m the components' means.
    /// </summary>
    /// <returns>A new matrix.</returns>
    /// <exception cref="NanoQLException">The weights sum to 0 or less, or there are fewer than two samples.</exception>
    public Matrix Covariance()
    {
        double sampleWeight = WeightSum();
        if (!(sampleWeight > 0.0))
        {
            throw new NanoQLException("sampleWeight=0, unsufficient");
        }

        double sampleNumber = Samples;
        if (!(sampleNumber > 1.0))
        {
            throw new NanoQLException("sample number <=1, unsufficient");
        }

        ImmutableArray<double> m = Mean();
        double inv = 1.0 / sampleWeight;

        Matrix result = inv * quadraticSum;
        result -= MatrixFunctions.OuterProduct(m.AsSpan(), m.AsSpan());

        result *= sampleNumber / (sampleNumber - 1.0);
        return result;
    }

    /// <summary>
    /// The correlation matrix: the covariance scaled by the components' standard deviations, 1 on the diagonal and
    /// between two components of no variance, 0 between one of no variance and another.
    /// </summary>
    /// <returns>A new matrix.</returns>
    /// <exception cref="NanoQLException">The weights sum to 0 or less, or there are fewer than two samples.</exception>
    public Matrix Correlation()
    {
        Matrix correlation = Covariance();
        Vector variances = correlation.Diagonal();
        for (int i = 0; i < dimension; i++)
        {
            for (int j = 0; j < dimension; j++)
            {
                if (i == j)
                {
                    if (variances[i] == 0.0)
                    {
                        correlation[i, j] = 1.0;
                    }
                    else
                    {
                        correlation[i, j] *= 1.0 / Math.Sqrt(variances[i] * variances[j]);
                    }
                }
                else
                {
                    if (variances[i] == 0.0 && variances[j] == 0)
                    {
                        correlation[i, j] = 1.0;
                    }
                    else if (variances[i] == 0.0 || variances[j] == 0.0)
                    {
                        correlation[i, j] = 0.0;
                    }
                    else
                    {
                        correlation[i, j] *= 1.0 / Math.Sqrt(variances[i] * variances[j]);
                    }
                }
            }
        }

        return correlation;
    }

    /// <summary>Each component's mean.</summary>
    /// <returns>The means.</returns>
    public ImmutableArray<double> Mean() => Lift(static s => s.Mean());

    /// <summary>Each component's variance.</summary>
    /// <returns>The variances.</returns>
    public ImmutableArray<double> Variance() => Lift(static s => s.Variance());

    /// <summary>Each component's standard deviation.</summary>
    /// <returns>The standard deviations.</returns>
    public ImmutableArray<double> StandardDeviation() => Lift(static s => s.StandardDeviation());

    /// <summary>Each component's error estimate on its mean.</summary>
    /// <returns>The error estimates.</returns>
    public ImmutableArray<double> ErrorEstimate() => Lift(static s => s.ErrorEstimate());

    /// <summary>Each component's skewness.</summary>
    /// <returns>The skewnesses.</returns>
    public ImmutableArray<double> Skewness() => Lift(static s => s.Skewness());

    /// <summary>Each component's excess kurtosis.</summary>
    /// <returns>The kurtoses.</returns>
    public ImmutableArray<double> Kurtosis() => Lift(static s => s.Kurtosis());

    /// <summary>Each component's smallest sample.</summary>
    /// <returns>The minima.</returns>
    public ImmutableArray<double> Min() => Lift(static s => s.Min());

    /// <summary>Each component's largest sample.</summary>
    /// <returns>The maxima.</returns>
    public ImmutableArray<double> Max() => Lift(static s => s.Max());

    /// <summary>Each component's Gaussian-assumption percentile.</summary>
    /// <param name="y">The percentile, in (0, 1).</param>
    /// <returns>The percentiles.</returns>
    public ImmutableArray<double> GaussianPercentile(double y) => Lift(s => s.GaussianPercentile(y));

    /// <summary>Each component's Gaussian-assumption potential upside at a percentile.</summary>
    /// <param name="percentile">The percentile, in [0.9, 1).</param>
    /// <returns>The potential upsides.</returns>
    public ImmutableArray<double> GaussianPotentialUpside(double percentile) =>
        Lift(s => s.GaussianPotentialUpside(percentile));

    /// <summary>Each component's Gaussian-assumption value-at-risk at a percentile.</summary>
    /// <param name="percentile">The percentile, in [0.9, 1).</param>
    /// <returns>The values-at-risk.</returns>
    public ImmutableArray<double> GaussianValueAtRisk(double percentile) => Lift(s => s.GaussianValueAtRisk(percentile));

    /// <summary>Each component's Gaussian-assumption expected shortfall at a percentile.</summary>
    /// <param name="percentile">The percentile, in [0.9, 1).</param>
    /// <returns>The expected shortfalls.</returns>
    public ImmutableArray<double> GaussianExpectedShortfall(double percentile) =>
        Lift(s => s.GaussianExpectedShortfall(percentile));

    /// <summary>Each component's Gaussian-assumption probability of falling below a target.</summary>
    /// <param name="target">The target.</param>
    /// <returns>The shortfalls.</returns>
    public ImmutableArray<double> GaussianShortfall(double target) => Lift(s => s.GaussianShortfall(target));

    /// <summary>Each component's Gaussian-assumption average shortfall below a target.</summary>
    /// <param name="target">The target.</param>
    /// <returns>The average shortfalls.</returns>
    public ImmutableArray<double> GaussianAverageShortfall(double target) =>
        Lift(s => s.GaussianAverageShortfall(target));

    /// <summary>
    /// Resets the statistics to no sample. A positive dimension sets the dimension, resetting each component's
    /// statistics where it is the current one and replacing them where it isn't; 0 sets the dimension to 0, so that
    /// the next sample sets it, and leaves the components' statistics as they are, as QuantLib's does.
    /// </summary>
    /// <remarks>
    /// After a reset to 0 the lifted members return no value until the next sample sets the dimension, where QuantLib's
    /// return their last call's values (<c>sequencestatistics.hpp:178-185</c>), a stale read.
    /// </remarks>
    /// <param name="dimension">The samples' dimension.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dimension"/> is negative.</exception>
    public virtual void Reset(int dimension = 0) => ResetCore(dimension);

    /// <summary>
    /// Adds a sample, with a weight. The first sample of a statistics of dimension 0 sets its dimension. Allocates
    /// nothing once the dimension is set: the outer product is added in place, wₖ (xᵢ xⱼ) into each element, QuantLib's
    /// arithmetic.
    /// </summary>
    /// <param name="sample">The sample, one value per component.</param>
    /// <param name="weight">Its weight.</param>
    /// <exception cref="NanoQLException">
    /// The sample is empty where the dimension is 0, or its size isn't the dimension.
    /// </exception>
    public virtual void Add(ReadOnlySpan<double> sample, double weight = 1.0)
    {
        if (dimension == 0)
        {
            // stat wasn't initialized yet
            if (sample.IsEmpty)
            {
                throw new NanoQLException("sample error: end<=begin");
            }

            ResetCore(sample.Length);
        }

        if (sample.Length != dimension)
        {
            throw new NanoQLException($"sample size mismatch: {dimension} required, {sample.Length} provided");
        }

        Span<double> q = quadraticSum.AsSpan();
        for (int i = 0; i < dimension; ++i)
        {
            double xi = sample[i];
            Span<double> row = q.Slice(i * dimension, dimension);
            for (int j = 0; j < dimension; ++j)
            {
                row[j] += weight * (xi * sample[j]);
            }
        }

        for (int i = 0; i < dimension; ++i)
        {
            stats[i].Add(sample[i], weight);
        }
    }

    // QuantLib's reset, which its constructor and add call: this class's own, not a derived override.
    private void ResetCore(int dimension)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dimension);

        // (re-)initialize
        if (dimension > 0)
        {
            if (dimension == this.dimension)
            {
                for (int i = 0; i < this.dimension; ++i)
                {
                    stats[i].Reset();
                }
            }
            else
            {
                this.dimension = dimension;
                stats = new TStats[dimension];
                for (int i = 0; i < dimension; ++i)
                {
                    stats[i] = new TStats();
                }
            }

            quadraticSum = new Matrix(this.dimension, this.dimension, 0.0);
        }
        else
        {
            this.dimension = dimension;
        }
    }

    /// <summary>One value per component, from each component's statistics.</summary>
    /// <param name="f">The one-dimensional member.</param>
    /// <returns>The values.</returns>
    internal ImmutableArray<double> Lift(Func<TStats, double> f)
    {
        var results = new double[dimension];
        for (int i = 0; i < dimension; i++)
        {
            results[i] = f(stats[i]);
        }

        return ImmutableArray.Create(results);
    }
}
