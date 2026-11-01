// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/statistics/discrepancystatistics.hpp ql/math/statistics/discrepancystatistics.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Statistics of N-dimensional samples with the L² discrepancy: <see cref="SequenceStatistics"/> with
/// <see cref="Discrepancy"/>, updated with each sample added.
/// </summary>
/// <remarks>
/// <para>
/// A working type on S-mmp's seam (the S-core plan's CR5 (b)): it overrides <see cref="Add"/> and <see cref="Reset"/>.
/// Dimension 1 is refused, as in QuantLib.
/// </para>
/// <para>
/// Two fixes of QuantLib's, each documented (<c>Divergences/DiscrepancyStatisticsTests.cs</c>): QuantLib computes its
/// constants for the dimension at construction or reset, so a statistics of dimension 0, whose first sample sets it,
/// keeps dimension 0's and returns a wrong discrepancy (0.666 for a 3-dimensional sample whose discrepancy is 0.00592);
/// nanoql computes them when the dimension is fixed (CR7 (viii)), and refuses a first sample of dimension 1 there, with
/// QuantLib's text, before the base fixes the dimension (CR7 (xxxvii)). And QuantLib pairs the components' stored
/// samples by position, which a sorting read of the components' statistics (a percentile sorts in place) breaks; nanoql
/// keeps its own copy of the points (CR7 (ix)).
/// </para>
/// </remarks>
[WorkingType]
public sealed class DiscrepancyStatistics : SequenceStatistics
{
    private readonly List<double[]> points = [];
    private double adiscr;
    private double cdiscr;
    private double bdiscr;
    private double ddiscr;
    private int constantsDimension;

    /// <summary>Initializes a new instance of the <see cref="DiscrepancyStatistics"/> class.</summary>
    /// <param name="dimension">The samples' dimension, not 1; 0 to take it from the first sample.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dimension"/> is negative or 1.</exception>
    public DiscrepancyStatistics(int dimension)
        : base(CheckDimension(dimension))
    {
        ResetConstants(dimension);
    }

    /// <summary>The L² discrepancy of the samples added.</summary>
    /// <returns>The discrepancy; NaN with no sample.</returns>
    public double Discrepancy()
    {
        long n = Samples;
        return Math.Sqrt((adiscr / (n * n)) - (bdiscr / n * cdiscr) + ddiscr);
    }

    /// <summary>
    /// Adds a sample, with a weight, which the discrepancy doesn't read. The first sample of a statistics of dimension
    /// 0 sets its dimension, and the discrepancy's constants with it.
    /// </summary>
    /// <param name="sample">The sample, one value per component.</param>
    /// <param name="weight">Its weight.</param>
    /// <exception cref="ArgumentException">The sample sets the dimension to 1.</exception>
    /// <exception cref="NanoQLException">
    /// The sample is empty where the dimension is 0, or its size isn't the dimension.
    /// </exception>
    public override void Add(ReadOnlySpan<double> sample, double weight = 1.0)
    {
        if (Dimension == 0 && sample.Length == 1)
        {
            throw new ArgumentException("dimension==1 not allowed", nameof(sample));
        }

        base.Add(sample, weight);
        int dimension = Dimension;
        if (constantsDimension != dimension)
        {
            ResetConstants(dimension);
        }

        int n = Samples;

        double rik, rjk, temp = 1.0;
        int k, m;
        for (k = 0; k < dimension; ++k)
        {
            // i=N
            rik = sample[k];
            temp *= 1.0 - (rik * rik);
        }

        cdiscr += temp;

        for (m = 0; m < n - 1; m++)
        {
            double[] point = points[m];
            temp = 1.0;
            for (k = 0; k < dimension; ++k)
            {
                // running i=1..(N-1)
                rik = point[k];

                // fixed j=N
                rjk = sample[k];
                temp *= 1.0 - Max(rik, rjk);
            }

            adiscr += temp;

            temp = 1.0;
            for (k = 0; k < dimension; ++k)
            {
                // fixed i=N
                rik = sample[k];

                // running j=1..(N-1)
                rjk = point[k];
                temp *= 1.0 - Max(rik, rjk);
            }

            adiscr += temp;
        }

        temp = 1.0;
        for (k = 0; k < dimension; ++k)
        {
            // fixed i=N, j=N
            rik = rjk = sample[k];
            temp *= 1.0 - Max(rik, rjk);
        }

        adiscr += temp;
        points.Add(sample.ToArray());
    }

    /// <summary>
    /// Resets the statistics to no sample. A positive dimension sets the dimension; 0 keeps the current one, as
    /// QuantLib's does, or leaves a statistics of dimension 0 to take it from its next sample.
    /// </summary>
    /// <param name="dimension">The samples' dimension, not 1; 0 to keep the current one.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dimension"/> is negative or 1.</exception>
    public override void Reset(int dimension = 0)
    {
        if (dimension == 0)
        {
            // if no size given, keep the current one
            dimension = Dimension;
        }

        base.Reset(CheckDimension(dimension));
        ResetConstants(dimension);
    }

    private static int CheckDimension(int dimension) =>
        dimension == 1
            ? throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "dimension==1 not allowed")
            : dimension;

    // std::max's semantics
    private static double Max(double a, double b) => a < b ? b : a;

    // the sums restarted, and the constants for a fixed dimension; for dimension 0 they wait for the first sample
    private void ResetConstants(int dimension)
    {
        points.Clear();
        adiscr = 0.0;
        cdiscr = 0.0;
        constantsDimension = dimension;
        bdiscr = dimension > 0 ? 1.0 / Math.Pow(2.0, dimension - 1) : double.NaN;
        ddiscr = dimension > 0 ? 1.0 / Math.Pow(3.0, dimension) : double.NaN;
    }
}
