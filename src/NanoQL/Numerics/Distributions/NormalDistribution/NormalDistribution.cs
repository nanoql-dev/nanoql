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
/// The normal distribution's density: for average μ and standard deviation σ, f(x) = exp(−(x − μ)²/(2σ²)) / (σ√(2π)),
/// with its first derivative.
/// </summary>
/// <remarks>QuantLib's <c>GaussianDistribution</c> is an alias of this class, and isn't ported.</remarks>
public sealed class NormalDistribution
{
    // QuantLib's M_SQRT_2 (1/√2) and M_1_SQRTPI (1/√π).
    private const double M_SQRT_2 = 0.7071067811865475244008443621048490392848359376887;
    private const double M_1_SQRTPI = 0.564189583547756286948;

    private readonly double average;
    private readonly double normalizationFactor;
    private readonly double denominator;
    private readonly double derNormalizationFactor;

    /// <summary>Initializes a new instance of the <see cref="NormalDistribution"/> class.</summary>
    /// <param name="average">The average μ.</param>
    /// <param name="sigma">The standard deviation σ, positive.</param>
    /// <exception cref="ArgumentException"><paramref name="sigma"/> isn't positive.</exception>
    public NormalDistribution(double average = 0.0, double sigma = 1.0)
    {
        if (!(sigma > 0.0))
        {
            throw new ArgumentException(
                $"sigma must be greater than 0.0 ({sigma.ToString(CultureInfo.InvariantCulture)} not allowed)",
                nameof(sigma));
        }

        this.average = average;
        normalizationFactor = M_SQRT_2 * M_1_SQRTPI / sigma;
        derNormalizationFactor = sigma * sigma;
        denominator = 2.0 * derNormalizationFactor;
    }

    /// <summary>The density at <paramref name="x"/>.</summary>
    /// <param name="x">The point.</param>
    /// <returns>The density; 0 where the exponent is −690 or below.</returns>
    public double Evaluate(double x)
    {
        double deltax = x - average;
        double exponent = -(deltax * deltax) / denominator;
        // debian alpha had some strange problem in the very-low range
        return exponent <= -690.0 ? 0.0 : // exp(x) < 1.0e-300 anyway
            normalizationFactor * Math.Exp(exponent);
    }

    /// <summary>The density's first derivative at <paramref name="x"/>, −f(x)(x − μ)/σ².</summary>
    /// <param name="x">The point.</param>
    /// <returns>The derivative; 0 where the density is 0.</returns>
    public double Derivative(double x)
    {
        double density = Evaluate(x);
        return density == 0.0 ? 0.0 :
            density * (average - x) / derNormalizationFactor;
    }
}
