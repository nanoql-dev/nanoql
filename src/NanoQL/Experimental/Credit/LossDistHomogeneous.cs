// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/lossdistribution.hpp ql/experimental/credit/lossdistribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// The loss distribution of a homogeneous pool, equal volumes and varying default probabilities, built exactly by
/// convolving one name at a time: Xiaofong Ma, "Numerical Methods for the Valuation of Synthetic Collateralized Debt
/// Obligations", PhD Thesis, University of Toronto, 2007 (formula 2.1), avoiding the numerical instability of John Hull
/// and Alan White, "Valuation of a CDO and nth to default CDS without Monte Carlo simulation", Journal of Derivatives
/// 12, 2, 2004.
/// </summary>
/// <param name="nBuckets">The number of buckets.</param>
/// <param name="maximum">The largest loss.</param>
/// <remarks>Immutable (the S-xcr plan's XR5): <see cref="EvaluateWithDetails"/> returns QuantLib's mutable by-products
/// with the distribution.</remarks>
[Experimental("NQL0001")]
public sealed class LossDistHomogeneous(int nBuckets, double maximum) : LossDist
{
    /// <inheritdoc/>
    public override int Buckets => nBuckets;

    /// <inheritdoc/>
    public override double Maximum => maximum;

    /// <summary>The distribution of the names' loss, each of one volume.</summary>
    /// <param name="volume">Each name's volume.</param>
    /// <param name="p">The names' default probabilities.</param>
    /// <returns>The distribution.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="p"/> is <see langword="null"/>.</exception>
    public Distribution Evaluate(double volume, IReadOnlyList<double> p) => EvaluateWithDetails(volume, p).Distribution;

    /// <summary>The distribution of the names' loss, each of one volume, with the probability of each number of
    /// defaults.</summary>
    /// <param name="volume">Each name's volume.</param>
    /// <param name="p">The names' default probabilities.</param>
    /// <returns>The distribution and its by-products.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="p"/> is <see langword="null"/>.</exception>
    public LossDistResult EvaluateWithDetails(double volume, IReadOnlyList<double> p)
    {
        ArgumentNullException.ThrowIfNull(p);
        int n = p.Count;
        double[] probability = new double[n + 1];
        double[] prev;
        probability[0] = 1.0;
        for (int k = 0; k < n; k++)
        {
            prev = (double[])probability.Clone();
            probability[0] = prev[0] * (1.0 - p[k]);
            for (int i = 1; i <= k; i++)
            {
                probability[i] = (prev[i - 1] * p[k]) + (prev[i] * (1.0 - p[k]));
            }

            probability[k + 1] = prev[k] * p[k];
        }

        double[] excessProbability = new double[n + 1];
        excessProbability[n] = probability[n];
        for (int k = n - 1; k >= 0; k--)
        {
            excessProbability[k] = excessProbability[k + 1] + probability[k];
        }

        var dist = new Distribution(nBuckets, 0.0, maximum);
        for (int i = 0; i <= n; i++)
        {
            if (volume * i <= maximum)
            {
                int bucket = dist.Locate(volume * i);
                dist.AddDensity(bucket, probability[i] / dist.Dx(bucket));
                dist.AddAverage(bucket, volume * i);
            }
        }

        dist.Normalize();

        return new LossDistResult(dist, n, volume, [.. probability], [.. excessProbability]);
    }

    /// <summary>The distribution of the names' loss, on the first name's volume.</summary>
    /// <param name="volumes">The names' volumes, the first read.</param>
    /// <param name="probabilities">The names' default probabilities.</param>
    /// <returns>The distribution.</returns>
    /// <exception cref="ArgumentNullException">A list is <see langword="null"/>.</exception>
    public override Distribution Evaluate(IReadOnlyList<double> volumes, IReadOnlyList<double> probabilities)
    {
        ArgumentNullException.ThrowIfNull(volumes);
        return Evaluate(volumes[0], probabilities);
    }
}
