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
/// The cumulative bivariate normal distribution by Drezner's 1978 algorithm, to six decimal places: the probability
/// that two standard normals of correlation ρ fall below a and b.
/// </summary>
/// <remarks>
/// <para>
/// For this implementation see E. G. Haug, "Option Pricing Formulas", McGraw-Hill (1998). Immutable.
/// <see cref="BivariateCumulativeNormalDistributionWe04DP"/> is QuantLib's default bivariate normal, to near double
/// precision.
/// </para>
/// <para>
/// QuantLib's fix after v1.44 (#2880) is taken: at ρ = 1 the probability is Φ(min(a, b)) and at ρ = −1
/// max(0, Φ(a) + Φ(b) − 1), where QuantLib 1.44 divides by zero; the recursion's derived correlations take hypot
/// forms, which avoid the cancellation near |ρ| = 1, and a one-ulp overshoot of ±1 is clamped, where QuantLib 1.44
/// throws (<c>Divergences/BivariateDr78DegenerateCorrelationsTests.cs</c>).
/// </para>
/// </remarks>
public sealed class BivariateCumulativeNormalDistributionDr78
{
    private static readonly CumulativeNormalDistribution CumNormalDist = new();

    private readonly double rho_;
    private readonly double rho2_;

    /// <summary>
    /// Initializes a new instance of the <see cref="BivariateCumulativeNormalDistributionDr78"/> class.
    /// </summary>
    /// <param name="rho">The correlation, in [−1, 1].</param>
    /// <exception cref="ArgumentException"><paramref name="rho"/> is outside [−1, 1].</exception>
    public BivariateCumulativeNormalDistributionDr78(double rho)
    {
        if (!(rho >= -1.0))
        {
            throw new ArgumentException($"rho must be >= -1.0 ({Show(rho)} not allowed)", nameof(rho));
        }

        if (!(rho <= 1.0))
        {
            throw new ArgumentException($"rho must be <= 1.0 ({Show(rho)} not allowed)", nameof(rho));
        }

        rho_ = rho;
        rho2_ = rho * rho;
    }

    private static ReadOnlySpan<double> X =>
    [
        0.24840615,
        0.39233107,
        0.21141819,
        0.03324666,
        0.00082485334,
    ];

    private static ReadOnlySpan<double> Y =>
    [
        0.10024215,
        0.48281397,
        1.06094980,
        1.77972940,
        2.66976040000,
    ];

    /// <summary>
    /// The probability that the two normals fall below <paramref name="a"/> and <paramref name="b"/>.
    /// </summary>
    /// <param name="a">The first bound.</param>
    /// <param name="b">The second bound.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="NanoQLException">A bound is NaN ("case not handled").</exception>
    public double Evaluate(double a, double b)
    {
        double cumNormDistA = CumNormalDist.Evaluate(a);
        double cumNormDistB = CumNormalDist.Evaluate(b);
        double maxCumNormDistAB = Max(cumNormDistA, cumNormDistB);
        double minCumNormDistAB = Min(cumNormDistA, cumNormDistB);

        if (rho_ == 1.0)
        {
            return minCumNormDistAB;
        }

        if (rho_ == -1.0)
        {
            return Max(0.0, cumNormDistA + cumNormDistB - 1.0);
        }

        if (1.0 - maxCumNormDistAB < 1e-15)
        {
            return minCumNormDistAB;
        }

        if (minCumNormDistAB < 1e-15)
        {
            return minCumNormDistAB;
        }

        double a1 = a / Math.Sqrt(2.0 * (1.0 - rho2_));
        double b1 = b / Math.Sqrt(2.0 * (1.0 - rho2_));

        double result;

        if (a <= 0.0 && b <= 0 && rho_ <= 0)
        {
            ReadOnlySpan<double> x_ = X;
            ReadOnlySpan<double> y_ = Y;
            double sum = 0.0;
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    sum += x_[i] * x_[j] *
                        Math.Exp((a1 * ((2.0 * y_[i]) - a1)) + (b1 * ((2.0 * y_[j]) - b1))
                                 + (2.0 * rho_ * (y_[i] - a1) * (y_[j] - b1)));
                }
            }

            result = Math.Sqrt(1.0 - rho2_) / Math.PI * sum;
        }
        else if (a <= 0 && b >= 0 && rho_ >= 0)
        {
            var bivCumNormalDist = new BivariateCumulativeNormalDistributionDr78(-rho_);
            result = cumNormDistA - bivCumNormalDist.Evaluate(a, -b);
        }
        else if (a >= 0.0 && b <= 0.0 && rho_ >= 0.0)
        {
            var bivCumNormalDist = new BivariateCumulativeNormalDistributionDr78(-rho_);
            result = cumNormDistB - bivCumNormalDist.Evaluate(-a, b);
        }
        else if (a >= 0.0 && b >= 0.0 && rho_ <= 0.0)
        {
            result = cumNormDistA + cumNormDistB - 1.0 + Evaluate(-a, -b);
        }
        else if (a * b * rho_ > 0.0)
        {
            // D^2 = (rho*a - b)^2 + a^2*(1 - rho^2), and likewise for b.
            // The hypot forms avoid cancellation near |rho| = 1.
            double sqrtOneMinusRho2 = Math.Sqrt((1.0 - rho_) * (1.0 + rho_));
            double maxCorrelation = Math.BitIncrement(1.0);
            double rho1 = ((rho_ * a) - b) * (a > 0.0 ? 1.0 : -1.0) /
                double.Hypot((rho_ * a) - b, a * sqrtOneMinusRho2);

            // Clamp only finite one-ulp overshoots of the correlation range.
            if (Math.Abs(rho1) == maxCorrelation)
            {
                rho1 = Math.Clamp(rho1, -1.0, 1.0);
            }

            var bivCumNormalDist = new BivariateCumulativeNormalDistributionDr78(rho1);

            double rho2 = ((rho_ * b) - a) * (b > 0.0 ? 1.0 : -1.0) /
                double.Hypot((rho_ * b) - a, b * sqrtOneMinusRho2);
            if (Math.Abs(rho2) == maxCorrelation)
            {
                rho2 = Math.Clamp(rho2, -1.0, 1.0);
            }

            var cbnd2 = new BivariateCumulativeNormalDistributionDr78(rho2);

            double delta = (1.0 - ((a > 0.0 ? 1.0 : -1.0) * (b > 0.0 ? 1.0 : -1.0))) / 4.0;

            result = bivCumNormalDist.Evaluate(a, 0.0) + cbnd2.Evaluate(b, 0.0) - delta;
        }
        else
        {
            throw new NanoQLException("case not handled");
        }

        return result;
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);

    // std::max and std::min
    private static double Max(double a, double b) => a < b ? b : a;

    private static double Min(double a, double b) => b < a ? b : a;
}
