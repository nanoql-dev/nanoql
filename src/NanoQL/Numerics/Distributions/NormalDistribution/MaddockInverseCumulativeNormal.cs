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
/// Maddock's inverse cumulative normal distribution, Boost.Math's normal quantile: given a probability p between zero
/// and one, the y with F(y) = p, F the cumulative normal distribution of average μ and standard deviation σ.
/// </summary>
/// <remarks>
/// <para>
/// From the Boost documentation: these functions use a rational approximation devised by John Maddock to calculate an
/// initial approximation to the result that is accurate to ~10^-19, then only if that has insufficient accuracy
/// compared to the epsilon for type double, do we clean up the result using Halley iteration. For a double no
/// iteration runs.
/// </para>
/// <para>
/// Boost's errors keep Boost's messages: a probability outside [0, 1] or NaN throws
/// <see cref="ArgumentOutOfRangeException"/>; the quantile of 0 or 1, an overflow, and a parameter Boost refuses (an
/// average that isn't finite, an infinite σ) throw <see cref="NanoQLException"/>.
/// </para>
/// <para>
/// A configuration struct: its zero value (<c>default</c>, <c>new MaddockInverseCumulativeNormal()</c>, an unassigned
/// field or array element) is QuantLib's default construction, the standard normal (μ = 0, σ = 1).
/// </para>
/// </remarks>
public readonly struct MaddockInverseCumulativeNormal : IInverseCumulative
{
    private readonly double average;

    // QuantLib's sigma. The constructor refuses 0, so a stored 0 is the zero value, which reads as QuantLib's
    // default, 1.
    private readonly double sigma;

    /// <summary>Initializes a new instance of the <see cref="MaddockInverseCumulativeNormal"/> struct.</summary>
    /// <param name="average">The average μ.</param>
    /// <param name="sigma">The standard deviation σ, positive.</param>
    /// <exception cref="ArgumentException"><paramref name="sigma"/> isn't positive.</exception>
    public MaddockInverseCumulativeNormal(double average = 0.0, double sigma = 1.0)
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

    private double Sigma => sigma == 0.0 ? 1.0 : sigma;

    /// <summary>The inverse at <paramref name="x"/>: Boost's <c>quantile(normal_distribution(μ, σ), x)</c>.</summary>
    /// <param name="x">The probability, in (0, 1).</param>
    /// <returns>The inverse.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="x"/> is outside [0, 1], or NaN.</exception>
    /// <exception cref="NanoQLException"><paramref name="x"/> is 0 or 1, or Boost refuses the parameters.</exception>
    public double Evaluate(double x)
    {
        // boost::math::quantile(boost::math::normal_distribution<Real>(average_, sigma_), x)
        NormalFunctions.NormalDistribution(average, Sigma);
        return NormalFunctions.Quantile(average, Sigma, x);
    }
}
