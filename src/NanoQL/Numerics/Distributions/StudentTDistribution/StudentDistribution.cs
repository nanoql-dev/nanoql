// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/distributions/studenttdistribution.hpp ql/math/distributions/studenttdistribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The Student t-distribution's probability density for n degrees of freedom (see mathworld.wolfram.com or
/// wikipedia.org): f(x) = Γ((n + 1)/2) / (√(nπ) Γ(n/2)) · (1 + x²/n)^(−(n + 1)/2).
/// </summary>
/// <remarks>
/// Immutable. QuantLib takes each gamma function as the exponential of its logarithm, which overflows from n about 342
/// (0 at 342, NaN from 343), and its denominator's product overflows earlier at large x (0 at n 330, x 30, for about
/// 1e-95). Where any of QuantLib's factors overflows, nanoql takes the gammas' ratio as Boost's
/// <c>tgamma_delta_ratio</c> and the power as exp(−(n + 1)/2 · log1p(x²/n)) (the S-core plan's CR7 (xxii);
/// <c>Divergences/StudentDistributionTests.cs</c>): within 1.1e-13 of a 60-digit evaluation for n from 342 to
/// 2³¹ − 1, where the difference of the gammas' logarithms keeps their rounding (8.5e-7 at 1e9). QuantLib's formula,
/// bit for bit, wherever its factors are finite.
/// </remarks>
public sealed class StudentDistribution
{
    private readonly int n;

    /// <summary>Initializes a new instance of the <see cref="StudentDistribution"/> class.</summary>
    /// <param name="n">The degrees of freedom, positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> isn't positive.</exception>
    public StudentDistribution(int n)
    {
        if (!(n > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(n), n, "invalid parameter for t-distribution");
        }

        this.n = n;
    }

    /// <summary>The density.</summary>
    /// <param name="x">The point.</param>
    /// <returns>f(x).</returns>
    public double Evaluate(double x)
    {
        var g = new GammaFunction();
        double lg1 = g.LogValue(0.5 * (n + 1.0));
        double lg2 = g.LogValue(0.5 * n);
        double g1 = Math.Exp(lg1);
        double g2 = Math.Exp(lg2);
        double power = Math.Pow(1.0 + (x * x / n), 0.5 * (n + 1.0));
        double denominator = g2 * power * Math.Sqrt(Math.PI * n);
        if (double.IsFinite(g1) && double.IsFinite(denominator))
        {
            return g1 / denominator;
        }

        // where QuantLib's factors overflow, the gammas' ratio Boost's and the power from log1p; n + 1 in double, as
        // n = int.MaxValue's int sum wraps
        return 1.0 / GammaFunctions.TGammaDeltaRatio(0.5 * n, 0.5)
            * Math.Exp(-0.5 * (n + 1.0) * Log1pFunctions.Log1p(x * x / n)) / Math.Sqrt(Math.PI * n);
    }
}
