// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2010 Kakhkhor Abdijalilov
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/distributions/normaldistribution.hpp ql/math/distributions/normaldistribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// Maddock's cumulative normal distribution, Boost.Math's normal cdf: for average μ and standard deviation σ, the
/// probability of a value below x.
/// </summary>
/// <remarks>
/// Boost's errors keep Boost's messages: a NaN throws <see cref="ArgumentOutOfRangeException"/>; a parameter Boost
/// refuses (an average that isn't finite, an infinite σ) throws <see cref="NanoQLException"/>.
/// </remarks>
public sealed class MaddockCumulativeNormal
{
    private readonly double average;
    private readonly double sigma;

    /// <summary>Initializes a new instance of the <see cref="MaddockCumulativeNormal"/> class.</summary>
    /// <param name="average">The average μ.</param>
    /// <param name="sigma">The standard deviation σ, positive.</param>
    /// <exception cref="ArgumentException"><paramref name="sigma"/> isn't positive.</exception>
    public MaddockCumulativeNormal(double average = 0.0, double sigma = 1.0)
    {
        if (!(sigma > 0.0))
        {
            throw new ArgumentException(
                $"sigma must be greater than 0.0 ({sigma.ToString(CultureInfo.InvariantCulture)} not allowed)",
                nameof(sigma));
        }

        this.average = average;
        this.sigma = sigma;
    }

    /// <summary>The probability of a value below <paramref name="x"/>: Boost's <c>cdf(normal_distribution(μ, σ), x)</c>.</summary>
    /// <param name="x">The point; −∞ gives 0 and +∞ gives 1.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="x"/> is NaN.</exception>
    /// <exception cref="NanoQLException">Boost refuses the parameters.</exception>
    public double Evaluate(double x)
    {
        // boost::math::cdf(boost::math::normal_distribution<Real>(average_, sigma_), x)
        NormalFunctions.NormalDistribution(average, sigma);
        return NormalFunctions.Cdf(average, sigma, x);
    }
}
