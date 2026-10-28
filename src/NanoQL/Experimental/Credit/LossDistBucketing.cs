// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/lossdistribution.hpp ql/experimental/credit/lossdistribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace NanoQL.Experimental;

/// <summary>
/// The loss distribution of varying volumes and default probabilities, independence assumed, by Hull-White bucketing:
/// John Hull and Alan White, "Valuation of a CDO and nth to default CDS without Monte Carlo simulation", Journal of
/// Derivatives 12, 2, 2004.
/// </summary>
/// <param name="nBuckets">The number of buckets.</param>
/// <param name="maximum">The largest loss.</param>
/// <param name="epsilon">The tolerance of a loss's bucket.</param>
[Experimental("NQL0001")]
public sealed class LossDistBucketing(int nBuckets, double maximum, double epsilon = 1e-6) : LossDist
{
    /// <inheritdoc/>
    public override int Buckets => nBuckets;

    /// <inheritdoc/>
    public override double Maximum => maximum;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">A list is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The lists' sizes differ ("sizes differ: …").</exception>
    /// <exception cref="NanoQLException">A bucket's average leaves the bucket ("a out of range at k=…").</exception>
    public override Distribution Evaluate(IReadOnlyList<double> volumes, IReadOnlyList<double> probabilities)
    {
        ArgumentNullException.ThrowIfNull(volumes);
        ArgumentNullException.ThrowIfNull(probabilities);
        if (volumes.Count != probabilities.Count)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"sizes differ: {volumes.Count} vs {probabilities.Count}"),
                nameof(probabilities));
        }

        double[] p = new double[nBuckets];
        double[] a = new double[nBuckets];

        p[0] = 1.0;
        a[0] = 0.0;
        double dx = maximum / nBuckets;
        for (int k = 1; k < nBuckets; k++)
        {
            a[k] = (dx * k) + (dx / 2);
        }

        for (int i = 0; i < volumes.Count; i++)
        {
            double l = volumes[i];
            double pi = probabilities[i];
            for (int k = a.Length - 1; k >= 0; k--)
            {
                if (p[k] > 0)
                {
                    int u = LocateTargetBucket(a[k] + l, k);
                    if (!(u >= 0))
                    {
                        throw new NanoQLException(string.Create(CultureInfo.InvariantCulture, $"u={u} at i={i} k={k}"));
                    }

                    if (!(u >= k))
                    {
                        throw new NanoQLException(string.Create(CultureInfo.InvariantCulture, $"u={u}<k={k} at i={i}"));
                    }

                    double dp = p[k] * pi;
                    if (u == k)
                    {
                        a[k] += pi * l;
                    }
                    else
                    {
                        // no update of a[u] and p[u] if u is beyond grid end
                        if (u < nBuckets)
                        {
                            // a[u] remains unchanged, if dp = 0
                            if (dp > 0.0)
                            {
                                // on Windows, p[u]/dp could cause a NaN for
                                // some very small values of p[k].
                                // Writing the above as (p[u]/p[k])/P prevents
                                // the NaN. What can I say?
                                double f = 1.0 / (1.0 + (p[u] / p[k] / pi));
                                a[u] = ((1.0 - f) * a[u]) + (f * (a[k] + l));
                            }

                            p[u] += dp;
                        }

                        p[k] -= dp;
                    }
                }

                if (!(a[k] + epsilon >= dx * k && a[k] < dx * (k + 1)))
                {
                    throw new NanoQLException(string.Create(CultureInfo.InvariantCulture, $"a out of range at k={k}, contract {i}"));
                }
            }
        }

        var dist = new Distribution(nBuckets, 0.0, maximum);
        for (int i = 0; i < nBuckets; i++)
        {
            dist.AddDensity(i, p[i] / dx);
            dist.AddAverage(i, a[i]);
        }

        return dist;
    }

    private int LocateTargetBucket(double loss, int i0)
    {
        if (!(loss >= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(loss), loss, string.Create(
                CultureInfo.InvariantCulture, $"loss {loss} must be >= 0"));
        }

        double dx = maximum / nBuckets;
        for (int i = i0; i < nBuckets; i++)
        {
            if (dx * i > loss + epsilon)
            {
                return i - 1;
            }
        }

        return nBuckets;
    }
}
