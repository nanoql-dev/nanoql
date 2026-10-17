// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004 Walter Penschke
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/distributions/poissondistribution.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>The Poisson distribution: the probability of an integer k for a mean μ.</summary>
public sealed class PoissonDistribution
{
    // QuantLib also stores log(mu), which nothing reads: the probability takes the logarithm itself.
    private readonly double mu;

    /// <summary>Initializes a new instance of the <see cref="PoissonDistribution"/> class.</summary>
    /// <param name="mu">The mean, non-negative.</param>
    /// <exception cref="ArgumentException"><paramref name="mu"/> is negative or NaN.</exception>
    public PoissonDistribution(double mu)
    {
        if (!(mu >= 0.0))
        {
            throw new ArgumentException(
                $"mu must be non negative ({mu.ToString(CultureInfo.InvariantCulture)} not allowed)", nameof(mu));
        }

        this.mu = mu;
    }

    /// <summary>The probability of <paramref name="k"/>, μ^k e^(−μ) / k!.</summary>
    /// <param name="k">The integer, QuantLib's <c>BigNatural</c>: in [0, 4294967295].</param>
    /// <returns>The probability; for μ = 0, 1 at k = 0 and 0 elsewhere.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="k"/> is outside [0, 4294967295].</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="k"/> is 4294967295 and μ isn't 0: QuantLib's ln k! takes ln Γ(k + 1) with k + 1 wrapping to 0.
    /// </exception>
    public double Evaluate(long k)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(k);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(k, (long)uint.MaxValue);
        uint n = (uint)k;
        if (mu == 0.0)
        {
            if (n == 0)
            {
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        double logFactorial = Factorial.Ln(n);
        return Math.Exp(n * Math.Log(mu) - logFactorial - mu);
    }
}
