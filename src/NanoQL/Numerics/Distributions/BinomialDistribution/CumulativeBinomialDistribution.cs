// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/distributions/binomialdistribution.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The cumulative binomial distribution: the probability of at most k successes in n trials of probability p, through
/// the incomplete beta function.
/// </summary>
public sealed class CumulativeBinomialDistribution
{
    private readonly long n_;
    private readonly double p_;

    /// <summary>Initializes a new instance of the <see cref="CumulativeBinomialDistribution"/> class.</summary>
    /// <param name="p">The probability of a success, in [0, 1].</param>
    /// <param name="n">The number of trials, in [0, 2³² − 1].</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is outside [0, 2³² − 1].</exception>
    /// <exception cref="ArgumentException"><paramref name="p"/> is outside [0, 1].</exception>
    public CumulativeBinomialDistribution(double p, long n)
    {
        BinomialDistributionFunctions.CheckBigNatural(n, nameof(n));
        n_ = n;
        p_ = p;

        if (!(p >= 0))
        {
            throw new ArgumentException("negative p not allowed", nameof(p));
        }

        if (!(p <= 1.0))
        {
            throw new ArgumentException("p>1.0 not allowed", nameof(p));
        }
    }

    /// <summary>The probability of at most <paramref name="k"/> successes.</summary>
    /// <param name="k">The number of successes, in [0, 2³² − 1].</param>
    /// <returns>The probability; 1 at or above the number of trials.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="k"/> is outside [0, 2³² − 1].</exception>
    /// <exception cref="NanoQLException">The incomplete beta function's continued fraction doesn't
    /// converge.</exception>
    public double Evaluate(long k)
    {
        BinomialDistributionFunctions.CheckBigNatural(k, nameof(k));
        if (k >= n_)
        {
            return 1.0;
        }
        else
        {
            return 1.0 - BetaFunctions.IncompleteBetaFunction(k + 1, n_ - k, p_);
        }
    }
}
