// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004 Walter Penschke
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/distributions/poissondistribution.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The cumulative Poisson distribution: the probability of an integer up to k for a mean μ, 1 − P(k + 1, μ), from
/// the incomplete gamma function.
/// </summary>
/// <remarks>
/// For this implementation see "Numerical Recipes in C", 2nd edition, Press, Teukolsky, Vetterling, Flannery,
/// chapter 6.
/// </remarks>
public sealed class CumulativePoissonDistribution
{
    private readonly double mu;

    /// <summary>Initializes a new instance of the <see cref="CumulativePoissonDistribution"/> class.</summary>
    /// <param name="mu">The mean. QuantLib doesn't check it here: a negative or NaN mean is refused on evaluation.</param>
    public CumulativePoissonDistribution(double mu)
    {
        this.mu = mu;
    }

    /// <summary>The probability of an integer up to <paramref name="k"/>.</summary>
    /// <param name="k">The integer, QuantLib's <c>BigNatural</c>: in [0, 4294967295].</param>
    /// <returns>The cumulative probability.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="k"/> is outside [0, 4294967295].</exception>
    /// <exception cref="ArgumentException">
    /// The mean is negative or NaN; or <paramref name="k"/> is 4294967295, where QuantLib's k + 1 wraps to 0.
    /// </exception>
    /// <exception cref="NanoQLException">The incomplete gamma function doesn't converge.</exception>
    public double Evaluate(long k)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(k);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(k, (long)uint.MaxValue);

        // k + 1 in QuantLib's 32-bit BigNatural, which wraps at 2^32.
        uint a = unchecked((uint)k + 1);
        return 1.0 - IncompleteGammaFunctions.IncompleteGammaFunction(a, mu);
    }
}
