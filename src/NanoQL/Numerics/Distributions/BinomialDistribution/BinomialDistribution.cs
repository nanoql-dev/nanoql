// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/distributions/binomialdistribution.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The binomial probability distribution: the probability of k successes in n trials of probability
/// p.</summary>
public sealed class BinomialDistribution
{
    private readonly long n_;
    private readonly double logP_, logOneMinusP_;

    /// <summary>Initializes a new instance of the <see cref="BinomialDistribution"/> class.</summary>
    /// <param name="p">The probability of a success, in [0, 1].</param>
    /// <param name="n">The number of trials, in [0, 2³² − 1].</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is outside [0, 2³² − 1].</exception>
    /// <exception cref="ArgumentException"><paramref name="p"/> is outside [0, 1].</exception>
    public BinomialDistribution(double p, long n)
    {
        BinomialDistributionFunctions.CheckBigNatural(n, nameof(n));
        n_ = n;

        if (p == 0.0)
        {
            logP_ = -NumericLimits.QL_MAX_REAL;
            logOneMinusP_ = 0.0;
        }
        else if (p == 1.0)
        {
            logP_ = 0.0;
            logOneMinusP_ = -NumericLimits.QL_MAX_REAL;
        }
        else
        {
            if (!(p > 0))
            {
                throw new ArgumentException("negative p not allowed", nameof(p));
            }

            if (!(p < 1.0))
            {
                throw new ArgumentException("p>1.0 not allowed", nameof(p));
            }

            logP_ = Math.Log(p);
            logOneMinusP_ = Math.Log(1.0 - p);
        }
    }

    /// <summary>The probability of <paramref name="k"/> successes.</summary>
    /// <param name="k">The number of successes, in [0, 2³² − 1].</param>
    /// <returns>The probability; 0 above the number of trials.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="k"/> is outside [0, 2³² − 1].</exception>
    public double Evaluate(long k)
    {
        BinomialDistributionFunctions.CheckBigNatural(k, nameof(k));
        if (k > n_)
        {
            return 0.0;
        }

        if (logP_ == 0.0)
        {
            // p==1.0
            return k == n_ ? 1.0 : 0.0;
        }
        else if (logOneMinusP_ == 0.0)
        {
            // p==0.0
            return k == 0 ? 1.0 : 0.0;
        }
        else
        {
            return Math.Exp(BinomialDistributionFunctions.BinomialCoefficientLn(n_, k)
                            + (k * logP_) + ((n_ - k) * logOneMinusP_));
        }
    }
}
