// SPDX-FileCopyrightText: Copyright (C) 2014 Michal Kaut
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/distributions/bivariatestudenttdistribution.hpp ql/math/distributions/bivariatestudenttdistribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// The bivariate cumulative Student t-distribution, implemented following the formulas from Dunnett, C.W. and Sobel, M.
/// (1954). A bivariate generalization of Student t-distribution with tables for certain special cases. Biometrika 41,
/// 153–169.
/// </summary>
/// <remarks>
/// Immutable. QuantLib's x(m, h, k) returns 0 wherever its denominator is below 1e-8, the limit case for ρ = ±1, which
/// flips x from 1 to 0 within about 1e-4 of the line h = ρk at and near |ρ| = 1: errors to 1.9e-5 and negative
/// probabilities (−1.75e-5 at n 2, ρ −1, (0, −9.9e-5)). nanoql takes the limit only at an exact 0/0, where the sign
/// of h − ρk, 0, multiplies x (a documented divergence, found under R8; <c>Divergences/BivariateStudentTests.cs</c>);
/// QuantLib's values, bit for bit, wherever its denominator is at least 1e-8.
/// </remarks>
public sealed class BivariateCumulativeStudentDistribution
{
    private readonly int n;
    private readonly double rho;

    /// <summary>Initializes a new instance of the <see cref="BivariateCumulativeStudentDistribution"/> class.</summary>
    /// <param name="n">The degrees of freedom, positive.</param>
    /// <param name="rho">The correlation, in [−1, 1].</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="n"/> isn't positive, or <paramref name="rho"/> is outside [−1, 1].
    /// </exception>
    public BivariateCumulativeStudentDistribution(int n, double rho)
    {
        if (!(n > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(n), n, "degrees of freedom must be positive");
        }

        if (!(rho >= -1.0 && rho <= 1.0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(rho), rho, string.Create(CultureInfo.InvariantCulture, $"rho must be in [-1, 1] ({rho} not allowed)"));
        }

        this.n = n;
        this.rho = rho;
    }

    /// <summary>The cumulative distribution function.</summary>
    /// <param name="x">The first variable's bound.</param>
    /// <param name="y">The second variable's bound.</param>
    /// <returns>P(X ≤ x, Y ≤ y).</returns>
    public double Evaluate(double x, double y) => Pn(x, y, n, rho);

    private static double Sign(double val) => val == 0.0 ? 0.0 : (val < 0.0 ? -1.0 : 1.0);

    // unlike the atan2 function in C++ that gives results in [-pi,pi], this returns a value in [0, 2*pi]
    private static double Arctan(double x, double y)
    {
        double res = Math.Atan2(x, y);
        return res >= 0.0 ? res : res + (2 * MathConstants.M_PI);
    }

    // function x(m,h,k) defined on top of page 155
    private static double Fx(double m, double h, double k, double rho)
    {
        double unCor = 1 - (rho * rho);
        double sub = Math.Pow(h - (rho * k), 2);
        double denom = sub + (unCor * (m + (k * k)));
        if (!(denom > 0.0))
        {
            // limit case for rho = +/-1.0, an exact 0/0, where the sign of h - rho k multiplying x is 0; QuantLib's
            // cut at 1e-8 misprices a band beside the line h = rho k
            return 0.0;
        }

        return sub / (sub + (unCor * (m + (k * k))));
    }

    // this calculates the cdf
    private static double Pn(double h, double k, int n, double rho)
    {
        double unCor = 1.0 - (rho * rho);

        double div = 4 * Math.Sqrt(n * MathConstants.M_PI);
        double xHK = Fx(n, h, k, rho);
        double xKH = Fx(n, k, h, rho);
        double divH = 1 + (h * h / n);
        double divK = 1 + (k * k / n);
        double sgnHK = Sign(h - (rho * k));
        double sgnKH = Sign(k - (rho * h));

        if (n % 2 == 0)
        {
            // n is even, equation (10)
            // first line of (10)
            double res = Arctan(Math.Sqrt(unCor), -rho) / MathConstants.M_TWOPI;

            // second line of (10)
            double dgM = 2 * (1 - xHK);  // multiplier for dgj
            double gjM = sgnHK * 2 / MathConstants.M_PI; // multiplier for g_j

            // initializations for j = 1:
            double fj = Math.Sqrt(MathConstants.M_PI / divK);
            double gj = 1 + (gjM * Arctan(Math.Sqrt(xHK), Math.Sqrt(1 - xHK)));
            double sum = fj * gj;
            if (n >= 4)
            {
                // different formulas for j = 2:
                fj *= 0.5 / divK; // (2 - 1.5) / (Real) (2 - 1) / divK;
                double dgj = gjM * Math.Sqrt(xHK * (1 - xHK));
                gj += dgj;
                sum += fj * gj;

                // and then the loop for the rest of the j's:
                for (int j = 3; j <= n / 2; ++j)
                {
                    fj *= (j - 1.5) / (double)(j - 1) / divK;
                    dgj *= (double)(j - 2) / ((2 * j) - 3) * dgM;
                    gj += dgj;
                    sum += fj * gj;
                }
            }

            res += k / div * sum;

            // third line of (10)
            dgM = 2 * (1 - xKH);
            gjM = sgnKH * 2 / MathConstants.M_PI;

            // initializations for j = 1:
            fj = Math.Sqrt(MathConstants.M_PI / divH);
            gj = 1 + (gjM * Arctan(Math.Sqrt(xKH), Math.Sqrt(1 - xKH)));
            sum = fj * gj;
            if (n >= 4)
            {
                // different formulas for j = 2:
                fj *= 0.5 / divH; // (2 - 1.5) / (Real) (2 - 1) / divK;
                double dgj = gjM * Math.Sqrt(xKH * (1 - xKH));
                gj += dgj;
                sum += fj * gj;

                // and then the loop for the rest of the j's:
                for (int j = 3; j <= n / 2; ++j)
                {
                    fj *= (j - 1.5) / (double)(j - 1) / divH;
                    dgj *= (double)(j - 2) / ((2 * j) - 3) * dgM;
                    gj += dgj;
                    sum += fj * gj;
                }
            }

            res += h / div * sum;
            return res;
        }
        else
        {
            // n is odd, equation (11)
            // first line of (11)
            double hk = h * k;
            double hkcn = hk + (rho * n);
            double sqrtExpr = Math.Sqrt((h * h) - (2 * rho * hk) + (k * k) + (n * unCor));
            double res = Arctan(
                    Math.Sqrt((double)n) * ((-(h + k) * hkcn) - ((hk - n) * sqrtExpr)),
                    ((hk - n) * hkcn) - (n * (h + k) * sqrtExpr))
                / MathConstants.M_TWOPI;

            if (n > 1)
            {
                // second line of (11)
                double mult = (1 - xHK) / 2;

                // initializations for j = 1:
                double fj = 2 / Math.Sqrt(MathConstants.M_PI) / divK;
                double dgj = sgnHK * Math.Sqrt(xHK);
                double gj = 1 + dgj;
                double sum = fj * gj;

                // and then the loop for the rest of the j's:
                for (int j = 2; j <= (n - 1) / 2; ++j)
                {
                    fj *= (double)(j - 1) / (j - 0.5) / divK;
                    dgj *= (double)((2 * j) - 3) / (j - 1) * mult;
                    gj += dgj;
                    sum += fj * gj;
                }

                res += k / div * sum;

                // third line of (11)
                mult = (1 - xKH) / 2;

                // initializations for j = 1:
                fj = 2 / Math.Sqrt(MathConstants.M_PI) / divH;
                dgj = sgnKH * Math.Sqrt(xKH);
                gj = 1 + dgj;
                sum = fj * gj;

                // and then the loop for the rest of the j's:
                for (int j = 2; j <= (n - 1) / 2; ++j)
                {
                    fj *= (double)(j - 1) / (j - 0.5) / divH;
                    dgj *= (double)((2 * j) - 3) / (j - 1) * mult;
                    gj += dgj;
                    sum += fj * gj;
                }

                res += h / div * sum;
            }

            return res;
        }
    }
}
