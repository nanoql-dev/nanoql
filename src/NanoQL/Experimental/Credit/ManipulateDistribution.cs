// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/distribution.hpp ql/experimental/credit/distribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>Operations on <see cref="Distribution"/>s.</summary>
/// <remarks>
/// <see cref="Convolve"/> is the distribution of the sum: its bucket k holds the products of the buckets i and k − i,
/// in buckets of the inputs' width. QuantLib's assigns each product where it should add it
/// (<c>distribution.cpp:309</c>) and spreads its n₁ + n₂ − 1 buckets over [0, xmax₁ + xmax₂], one bucket wider, so its
/// result's mass isn't 1 (an unused class; <c>Divergences/DistributionMembersTests.cs</c>).
/// </remarks>
[Experimental("NQL0001")]
public static class ManipulateDistribution
{
    /// <summary>The distribution of the sum of two independent variables, of equal constant buckets from 0.</summary>
    /// <param name="d1">The first distribution.</param>
    /// <param name="d2">The second distribution.</param>
    /// <returns>The convolution, of n₁ + n₂ − 1 buckets of the inputs' width.</returns>
    /// <exception cref="ArgumentNullException">A distribution is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The bucket sizes differ or vary, or a distribution doesn't start at 0
    /// (QuantLib's texts).</exception>
    public static Distribution Convolve(Distribution d1, Distribution d2)
    {
        ArgumentNullException.ThrowIfNull(d1);
        ArgumentNullException.ThrowIfNull(d2);
        (List<double> dx1, List<double> density1, double xmin1, _) = d1.Buckets();
        (List<double> dx2, List<double> density2, double xmin2, _) = d2.Buckets();

        // force equal constant bucket sizes
        if (dx1[0] != dx2[0])
        {
            throw new ArgumentException("bucket sizes differ in d1 and d2", nameof(d2));
        }

        for (int i = 1; i < d1.Size; i++)
        {
            if (dx1[i] != dx1[i - 1])
            {
                throw new ArgumentException("bucket size varies in d1", nameof(d1));
            }
        }

        for (int i = 1; i < d2.Size; i++)
        {
            if (dx2[i] != dx2[i - 1])
            {
                throw new ArgumentException("bucket size varies in d2", nameof(d2));
            }
        }

        // force offset 0
        if (!(xmin1 == 0.0 && xmin2 == 0.0))
        {
            throw new ArgumentException("distributions offset larger than 0", nameof(d1));
        }

        int n = d1.Size + d2.Size - 1;
        var dist = new Distribution(n, 0.0, n * dx1[0]);
        double[] densities = new double[n];
        for (int i1 = 0; i1 < d1.Size; i1++)
        {
            double dx = dx1[i1];
            for (int i2 = 0; i2 < d2.Size; i2++)
            {
                densities[i1 + i2] += density1[i1] * density2[i2] * dx;
            }
        }

        dist.SetConvolution(densities);
        return dist;
    }
}
