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
/// Moro's inverse cumulative normal distribution: given x between zero and one, the y whose cumulative normal
/// probability, for average μ and standard deviation σ, is x. It uses the Beasley–Springer approximation in the
/// central region, with an improved Moro approximation for the tails (Boris Moro, "The Full Monte", 1995, Risk
/// Magazine).
/// </summary>
/// <remarks>
/// <para>
/// It also turns a uniform distribution into a normal one, keeping a low-discrepancy sequence's low discrepancy.
/// Peter J. Acklam's approximation, <see cref="InverseCumulativeNormal"/>, is better.
/// </para>
/// <para>
/// A configuration struct: its zero value (<c>default</c>, <c>new MoroInverseCumulativeNormal()</c>, an unassigned
/// field or array element) is QuantLib's default construction, the standard normal (μ = 0, σ = 1).
/// </para>
/// </remarks>
public readonly struct MoroInverseCumulativeNormal
{
    private const double a0_ = 2.50662823884;
    private const double a1_ = -18.61500062529;
    private const double a2_ = 41.39119773534;
    private const double a3_ = -25.44106049637;

    private const double b0_ = -8.47351093090;
    private const double b1_ = 23.08336743743;
    private const double b2_ = -21.06224101826;
    private const double b3_ = 3.13082909833;

    private const double c0_ = 0.3374754822726147;
    private const double c1_ = 0.9761690190917186;
    private const double c2_ = 0.1607979714918209;
    private const double c3_ = 0.0276438810333863;
    private const double c4_ = 0.0038405729373609;
    private const double c5_ = 0.0003951896511919;
    private const double c6_ = 0.0000321767881768;
    private const double c7_ = 0.0000002888167364;
    private const double c8_ = 0.0000003960315187;

    private readonly double average;

    // QuantLib's sigma. The constructor refuses 0, so a stored 0 is the zero value, which reads as QuantLib's
    // default, 1.
    private readonly double sigma;

    /// <summary>Initializes a new instance of the <see cref="MoroInverseCumulativeNormal"/> struct.</summary>
    /// <param name="average">The average μ.</param>
    /// <param name="sigma">The standard deviation σ, positive.</param>
    /// <exception cref="ArgumentException"><paramref name="sigma"/> isn't positive.</exception>
    public MoroInverseCumulativeNormal(double average = 0.0, double sigma = 1.0)
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

    /// <summary>The inverse at <paramref name="x"/>.</summary>
    /// <param name="x">The probability, in (0, 1).</param>
    /// <returns>The inverse.</returns>
    /// <exception cref="ArgumentException"><paramref name="x"/> isn't in (0, 1).</exception>
    public double Evaluate(double x)
    {
        if (!(x > 0.0 && x < 1.0))
        {
            throw new ArgumentException(
                $"MoroInverseCumulativeNormal({x.ToString(CultureInfo.InvariantCulture)}) undefined: must be 0<x<1",
                nameof(x));
        }

        double result;
        double temp = x - 0.5;

        // Moro's central approximation is used for |x - 0.5| < 0.42;
        // the alternative approximation is more accurate in the tails.
        const double centralRegionHalfWidth = 0.42;
        if (Math.Abs(temp) < centralRegionHalfWidth)
        {
            // Beasley and Springer, 1977
            result = temp * temp;
            result = temp *
                (((a3_ * result + a2_) * result + a1_) * result + a0_) /
                ((((b3_ * result + b2_) * result + b1_) * result + b0_) * result + 1.0);
        }
        else
        {
            // improved approximation for the tail (Moro 1995)
            if (x < 0.5)
            {
                result = x;
            }
            else
            {
                result = 1.0 - x;
            }

            result = Math.Log(-Math.Log(result));
            result = c0_ + result * (c1_ + result * (c2_ + result * (c3_ + result *
                (c4_ + result * (c5_ + result * (c6_ + result *
                (c7_ + result * c8_)))))));
            if (x < 0.5)
            {
                result = -result;
            }
        }

        return average + result * Sigma;
    }
}
