// SPDX-FileCopyrightText: Copyright (C) 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/cevrndcalculator.hpp ql/methods/finitedifferences/utilities/cevrndcalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The risk-neutral density of the constant elasticity of variance (CEV) process df = α·f^β·dW with an absorbing
/// boundary at f = 0, by the non-central chi-squared distribution (D. R. Brecher and A. E. Lindsay, "Results on the CEV
/// Process, Past and Present"), with the probability mass absorbed at zero.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib refuses β = 1, where δ = (1 − 2β)/(1 − β) divides by zero, and near it Boost's non-central chi-squared
/// refuses or runs for seconds. Here β = 1 is the lognormal density of the driftless forward, the CEV density's limit,
/// with no mass at zero; for 0 &lt; |β − 1| &lt; 1e-3 the constructor refuses. A documented divergence (the S-van plan's
/// VR10, <c>Divergences/CevNearOneTests.cs</c>).
/// </para>
/// <para>
/// The inverse first solves Sankaran's approximation by Brent's method for its guess; where that fails, QuantLib's
/// <c>catch (...)</c> starts from the forward instead, here on <see cref="NanoQLException"/>, the type of every failure
/// QuantLib's solver and Boost's distribution raise there. That second Brent can step below zero, where QuantLib's
/// distribution reads X(f) = f^(2(1 − β)) as NaN and Boost refuses it, so QuantLib throws at a quantile that exists
/// (β 0.95, σ 200%, five years, the median); here the density and distribution are 0 below zero, the absorbing
/// boundary, and QuantLib's values at and above it stand. A documented divergence (a run find,
/// <c>Divergences/CevDensityBelowZeroTests.cs</c>), checked by the quantile's own distribution.
/// </para>
/// </remarks>
public sealed class CEVRNDCalculator : RiskNeutralDensityCalculator
{
    // the band around β = 1 the non-central chi-squared can't reach (VR10)
    private const double NearOne = 1e-3;

    private readonly double f0;
    private readonly double alpha;
    private readonly double beta;
    private readonly double delta;
    private readonly double x0;

    // β = 1 exactly: the lognormal limit
    private readonly bool lognormal;

    /// <summary>Initializes a new instance of the <see cref="CEVRNDCalculator"/> class.</summary>
    /// <param name="f0">The forward's value today.</param>
    /// <param name="alpha">The volatility parameter α.</param>
    /// <param name="beta">The elasticity β.</param>
    /// <exception cref="NanoQLException">β lies within 1e-3 of 1 without being 1.</exception>
    public CEVRNDCalculator(double f0, double alpha, double beta)
    {
        this.f0 = f0;
        this.alpha = alpha;
        this.beta = beta;
        delta = (1.0 - (2.0 * beta)) / (1.0 - beta);
        x0 = X(f0);
        lognormal = beta == 1.0;
        if (!lognormal && Math.Abs(beta - 1.0) < NearOne)
        {
            throw new NanoQLException("the CEV density near beta = 1 is outside the non-central chi-squared's range");
        }
    }

    /// <summary>The probability absorbed at zero by time <paramref name="t"/>.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The mass at zero; 0 for δ ≥ 2, where the process never reaches zero.</returns>
    public double MassAtZero(double t)
    {
        if (lognormal)
        {
            return 0.0;
        }

        if (delta < 2.0)
        {
            return 1.0 - GammaFunctions.GammaP((-0.5 * delta) + 1.0, x0 / (2.0 * t));
        }
        else
        {
            return 0.0;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Boost's non-central chi-squared refuses its arguments, or a series runs out
    /// of terms.</exception>
    public override double Pdf(double x, double t)
    {
        if (lognormal)
        {
            return LognormalPdf(x, t);
        }

        if (x < 0.0)
        {
            // no density below the absorbing boundary, where X(f) is NaN and Boost refuses it
            return 0.0;
        }

        double y = X(x);

        if (delta < 2.0)
        {
            return NonCentralChiSquaredPdf(4.0 - delta, y / t, x0 / t) / t * 2.0 * (1.0 - beta) * y / x;
        }
        else
        {
            return NonCentralChiSquaredPdf(delta, x0 / t, y / t) / t * 2.0 * (beta - 1.0) * y / x;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Boost's non-central chi-squared refuses its arguments, or a series runs out
    /// of terms.</exception>
    public override double Cdf(double x, double t)
    {
        if (lognormal)
        {
            return LognormalCdf(x, t);
        }

        if (x < 0.0)
        {
            // no probability below the absorbing boundary, where X(f) is NaN and Boost refuses it
            return 0.0;
        }

        double y = X(x);

        if (delta < 2.0)
        {
            return 1.0 - NonCentralChiSquaredCdf(2.0 - delta, y / t, x0 / t);
        }
        else
        {
            return 1.0 - NonCentralChiSquaredCdf(delta, x0 / t, y / t);
        }
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Brent's method fails from the forward too, or Boost's quantile fails.</exception>
    public override double InvCdf(double p, double t)
    {
        if (lognormal)
        {
            return LognormalInvCdf(p, t);
        }

        if (delta < 2.0)
        {
            if (f0 < NumericLimits.QL_EPSILON || p < MassAtZero(t))
            {
                return 0.0;
            }

            double x = new InverseCumulativeNormal().Evaluate(1 - p);

            double CdfApprox(double c) => SankaranApprox(c, t, x);

            double y0 = X(f0) / t;

            try
            {
                double guess = InvX(new Brent().SetMaxEvaluations(20).Solve(CdfApprox, 1e-8, y0, 0.02 * y0) * t);

                return new InvCdfHelper(this, guess, 1e-8, 100).InverseCdf(p, t);
            }
            catch (NanoQLException)
            {
                // QuantLib's catch (...): from the forward
                return new InvCdfHelper(this, f0, 1e-8, 100).InverseCdf(p, t);
            }
        }
        else
        {
            NonCentralChiSquaredFunctions.NonCentralChiSquaredDistribution(delta, x0 / t);
            double x = t * NonCentralChiSquaredFunctions.Quantile(delta, x0 / t, 1 - p);
            return InvX(x);
        }
    }

    // boost::math::pdf(non_central_chi_squared_distribution(k, l), x), its constructor's checks first
    private static double NonCentralChiSquaredPdf(double k, double l, double x)
    {
        NonCentralChiSquaredFunctions.NonCentralChiSquaredDistribution(k, l);
        return NonCentralChiSquaredFunctions.Pdf(k, l, x);
    }

    // boost::math::cdf(non_central_chi_squared_distribution(k, l), x), its constructor's checks first
    private static double NonCentralChiSquaredCdf(double k, double l, double x)
    {
        NonCentralChiSquaredFunctions.NonCentralChiSquaredDistribution(k, l);
        return NonCentralChiSquaredFunctions.Cdf(k, l, x);
    }

    private static double Squared(double x) => x * x;

    private double X(double f) => Math.Pow(f, 2.0 * (1.0 - beta)) / Squared(alpha * (1.0 - beta));

    private double InvX(double x) => Math.Pow(x * Squared(alpha * (1.0 - beta)), 1.0 / (2.0 * (1.0 - beta)));

    private double SankaranApprox(double c, double t, double x)
    {
        double a = x0 / t;
        double b = 2.0 - delta;

        // std::max(c, -0.45*b), (c < -0.45*b) ? -0.45*b : c
        double floor = -0.45 * b;
        c = c < floor ? floor : c;

        double h = 1 - (2 * (b + c) * (b + (3 * c)) / (3 * Squared(b + (2 * c))));
        double pp = (b + (2 * c)) / Squared(b + c);
        double m = (h - 1) * (1 - (3 * h));

        double u = (Math.Pow(a / (b + c), h) - (1 + (h * pp * (h - 1 - (0.5 * (2 - h) * m * pp)))))
            / (h * Math.Sqrt(2 * pp) * (1 + (0.5 * m * pp)));

        return u - x;
    }

    // β = 1: ln f_t is normal with mean ln f0 − α²t/2 and variance α²t
    private double LognormalPdf(double x, double t)
    {
        if (!(x > 0.0))
        {
            return 0.0;
        }

        double s = alpha * Math.Sqrt(t);
        double d = (Math.Log(x / f0) + (0.5 * s * s)) / s;
        return Math.Exp(-0.5 * d * d) / (x * s * Math.Sqrt(2.0 * Math.PI));
    }

    private double LognormalCdf(double x, double t)
    {
        if (!(x > 0.0))
        {
            return 0.0;
        }

        double s = alpha * Math.Sqrt(t);
        return new MaddockCumulativeNormal().Evaluate((Math.Log(x / f0) + (0.5 * s * s)) / s);
    }

    private double LognormalInvCdf(double p, double t)
    {
        double s = alpha * Math.Sqrt(t);
        return f0 * Math.Exp((s * new MaddockInverseCumulativeNormal().Evaluate(p)) - (0.5 * s * s));
    }
}
