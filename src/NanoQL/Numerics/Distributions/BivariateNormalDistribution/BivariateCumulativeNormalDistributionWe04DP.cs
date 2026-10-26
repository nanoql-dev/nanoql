// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2005 Gary Kennedy
// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/distributions/bivariatenormaldistribution.hpp ql/math/distributions/bivariatenormaldistribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// The cumulative bivariate normal distribution by West (2004), to near double precision: the probability that two
/// standard normals of correlation ρ fall below x and y. QuantLib's default bivariate normal.
/// </summary>
/// <remarks>
/// <para>
/// The implementation derives from G. West, "Better Approximations To Cumulative Normal Distributions", Wilmott
/// Magazine (May 2005), 70-76, a port of its C++ code, on the near double-precision algorithm of A. Genz,
/// "Numerical Computation of Rectangular Bivariate and Trivariate Normal and t Probabilities", Statistics and
/// Computing 14 (2004), 151-160; the univariate normal is <see cref="CumulativeNormalDistribution"/> and the
/// Gauss-Legendre rules are <see cref="TabulatedGaussLegendre"/>'s. Immutable. QuantLib's typedef
/// <c>BivariateCumulativeNormalDistribution</c> names this class.
/// </para>
/// <para>
/// At an infinite bound the probability is the other bound's Φ (+∞) or 0 (−∞), where QuantLib's integrand meets
/// ∞ − ∞ and returns NaN (at +∞ for ρ &gt; 0, at −∞ for ρ &lt; 0, and wherever the other bound is 0;
/// <c>Divergences/BivariateInfiniteArgumentTests.cs</c>).
/// </para>
/// </remarks>
public sealed class BivariateCumulativeNormalDistributionWe04DP
{
    private static readonly CumulativeNormalDistribution Cumnorm = new();
    private static readonly TabulatedGaussLegendre GaussLegendre20 = new(20);
    private static readonly TabulatedGaussLegendre GaussLegendre12 = GaussLegendre20.Order(12);
    private static readonly TabulatedGaussLegendre GaussLegendre6 = GaussLegendre20.Order(6);

    private readonly double correlation_;

    /// <summary>
    /// Initializes a new instance of the <see cref="BivariateCumulativeNormalDistributionWe04DP"/> class.
    /// </summary>
    /// <param name="rho">The correlation, in [−1, 1].</param>
    /// <exception cref="ArgumentException"><paramref name="rho"/> is outside [−1, 1].</exception>
    public BivariateCumulativeNormalDistributionWe04DP(double rho)
    {
        if (!(rho >= -1.0))
        {
            throw new ArgumentException($"rho must be >= -1.0 ({Show(rho)} not allowed)", nameof(rho));
        }

        if (!(rho <= 1.0))
        {
            throw new ArgumentException($"rho must be <= 1.0 ({Show(rho)} not allowed)", nameof(rho));
        }

        correlation_ = rho;
    }

    /// <summary>
    /// The probability that the two normals fall below <paramref name="x"/> and <paramref name="y"/>.
    /// </summary>
    /// <param name="x">The first bound.</param>
    /// <param name="y">The second bound.</param>
    /// <returns>The probability.</returns>
    public double Evaluate(double x, double y)
    {
        /* The implementation is described at section 2.4 "Hybrid
           Numerical Integration Algorithms" of "Numerical Computation
           of Rectangular Bivariate an Trivariate Normal and t
           Probabilities", Genz (2004), Statistics and Computing 14,
           151-160. (available at
           www.sci.wsu.edu/math/faculty/henz/homepage)

           The Gauss-Legendre quadrature have been extracted to
           TabulatedGaussLegendre (x,w zero-based)

           Tthe functions ot be integrated numerically have been moved
           to classes eqn3 and eqn6

           Change some magic numbers to M_PI */

        // an infinite bound: the other's marginal, or nothing
        if (x == double.NegativeInfinity || y == double.NegativeInfinity)
        {
            return 0.0;
        }

        if (x == double.PositiveInfinity)
        {
            return Cumnorm.Evaluate(y);
        }

        if (y == double.PositiveInfinity)
        {
            return Cumnorm.Evaluate(x);
        }

        TabulatedGaussLegendre gaussLegendreQuad = GaussLegendre20;
        if (Math.Abs(correlation_) < 0.3)
        {
            gaussLegendreQuad = GaussLegendre6;
        }
        else if (Math.Abs(correlation_) < 0.75)
        {
            gaussLegendreQuad = GaussLegendre12;
        }

        double h = -x;
        double k = -y;
        double hk = h * k;
        double bvn = 0.0;

        if (Math.Abs(correlation_) < 0.925)
        {
            if (Math.Abs(correlation_) > 0)
            {
                double asr = Math.Asin(correlation_);
                var f = new Eqn3(h, k, asr);
                bvn = gaussLegendreQuad.Evaluate(f.Evaluate);
                bvn *= asr * (0.25 / Math.PI);
            }

            bvn += Cumnorm.Evaluate(-h) * Cumnorm.Evaluate(-k);
        }
        else
        {
            if (correlation_ < 0)
            {
                k *= -1;
                hk *= -1;
            }

            if (Math.Abs(correlation_) < 1)
            {
                double ass = (1 - correlation_) * (1 + correlation_);
                double a = Math.Sqrt(ass);
                double bs = (h - k) * (h - k);
                double c = (4 - hk) / 8;
                double d = (12 - hk) / 16;
                double asr = -((bs / ass) + hk) / 2;
                if (asr > -100)
                {
                    bvn = a * Math.Exp(asr) *
                        (1 - (c * (bs - ass) * (1 - (d * bs / 5)) / 3) +
                         (c * d * ass * ass / 5));
                }

                if (-hk < 100)
                {
                    double b = Math.Sqrt(bs);
                    bvn -= Math.Exp(-hk / 2) * 2.506628274631 *
                        Cumnorm.Evaluate(-b / a) * b *
                        (1 - (c * bs * (1 - (d * bs / 5)) / 3));
                }

                a /= 2;
                var f = new Eqn6(a, c, d, bs, hk);
                bvn += gaussLegendreQuad.Evaluate(f.Evaluate);
                bvn /= -2.0 * Math.PI;
            }

            if (correlation_ > 0)
            {
                bvn += Cumnorm.Evaluate(-(h < k ? k : h));
            }
            else
            {
                bvn *= -1;
                if (k > h)
                {
                    // evaluate cumnorm where it is most precise, that
                    // is in the lower tail because of double accuracy
                    // around 0.0 vs around 1.0
                    if (h >= 0)
                    {
                        bvn += Cumnorm.Evaluate(-h) - Cumnorm.Evaluate(-k);
                    }
                    else
                    {
                        bvn += Cumnorm.Evaluate(k) - Cumnorm.Evaluate(h);
                    }
                }
            }
        }

        return bvn;
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);

    /* Relates to eqn3 Genz 2004 */
    private sealed class Eqn3(double h, double k, double asr)
    {
        private readonly double hk_ = h * k;
        private readonly double asr_ = asr;
        private readonly double hs_ = ((h * h) + (k * k)) / 2;

        public double Evaluate(double x)
        {
            double sn = Math.Sin(asr_ * (-x + 1) * 0.5);
            return Math.Exp(((sn * hk_) - hs_) / (1.0 - (sn * sn)));
        }
    }

    /* Relates to eqn6 Genz 2004 */
    private sealed class Eqn6(double a, double c, double d, double bs, double hk)
    {
        public double Evaluate(double x)
        {
            double xs = a * (-x + 1);
            xs = Math.Abs(xs * xs);
            double rs = Math.Sqrt(1 - xs);
            double asr = -((bs / xs) + hk) / 2;
            if (asr > -100.0)
            {
                return a * Math.Exp(asr) *
                       ((Math.Exp(-hk * (1 - rs) / (2 * (1 + rs))) / rs) -
                        (1 + (c * xs * (1 + (d * xs)))));
            }
            else
            {
                return 0.0;
            }
        }
    }
}
