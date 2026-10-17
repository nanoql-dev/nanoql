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
/// The cumulative normal distribution: for average μ and standard deviation σ, the probability of a value below x,
/// from the error function, with the normal density as its derivative.
/// </summary>
/// <remarks>
/// The lower tail uses an asymptotic expansion where the error function loses precision, below a probability of
/// 1e-8. For this implementation see M. Abramowitz and I. Stegun, Handbook of Mathematical Functions, Dover
/// Publications, New York (1972).
/// </remarks>
public sealed class CumulativeNormalDistribution
{
    // QuantLib's M_SQRT_2, 1/√2.
    private const double M_SQRT_2 = 0.7071067811865475244008443621048490392848359376887;

    private readonly double average;
    private readonly double sigma;
    private readonly NormalDistribution gaussian = new();
    private readonly ErrorFunction errorFunction = new();

    /// <summary>Initializes a new instance of the <see cref="CumulativeNormalDistribution"/> class.</summary>
    /// <param name="average">The average μ.</param>
    /// <param name="sigma">The standard deviation σ, positive.</param>
    /// <exception cref="ArgumentException"><paramref name="sigma"/> isn't positive.</exception>
    public CumulativeNormalDistribution(double average = 0.0, double sigma = 1.0)
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

    /// <summary>The probability of a value below <paramref name="x"/>.</summary>
    /// <param name="x">The point.</param>
    /// <returns>The probability, in [0, 1].</returns>
    public double Evaluate(double x)
    {
        double z = (x - average) / sigma;

        double result = 0.5 * (1.0 + errorFunction.Evaluate(z * M_SQRT_2));
        if (result <= 1e-8)
        {
            // todo: investigate the threshold level
            // Asymptotic expansion for very negative z following (26.2.12)
            // on page 408 in M. Abramowitz and A. Stegun,
            // Pocketbook of Mathematical Functions, ISBN 3-87144818-4.
            // (QuantLib's local x is xi here: the parameter carries the header's name, x.)
            double sum = 1.0, zsqr = z * z, i = 1.0, g = 1.0, xi, y,
                a = NumericLimits.QL_MAX_REAL, lasta;
            do
            {
                lasta = a;
                xi = (4.0 * i - 3.0) / zsqr;
                y = xi * ((4.0 * i - 1) / zsqr);
                a = g * (xi - y);
                sum -= a;
                g *= y;
                ++i;
                a = Math.Abs(a);
            }
            while (lasta > a && a >= Math.Abs(sum * NumericLimits.QL_EPSILON));

            result = -gaussian.Evaluate(z) / z * sum;
        }

        return result;
    }

    /// <summary>The derivative, the normal density at <paramref name="x"/>.</summary>
    /// <param name="x">The point.</param>
    /// <returns>The density.</returns>
    public double Derivative(double x)
    {
        double xn = (x - average) / sigma;
        return gaussian.Evaluate(xn) / sigma;
    }
}
