// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/distributions/studenttdistribution.hpp ql/math/distributions/studenttdistribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The Student t-distribution's cumulative distribution function for n degrees of freedom (see mathworld.wolfram.com):
/// F(x) = ½ + ½·sgn(x)·[I(1; n/2, ½) − I(n/(n + x²); n/2, ½)], I(z; a, b) the regularized incomplete beta function.
/// </summary>
/// <remarks>
/// Immutable. QuantLib's formula loses digits in two places, which nanoql computes otherwise (the S-core plan's CR7
/// (xxiii); <c>Divergences/StudentDistributionTests.cs</c>). Where n/(n + x²) exceeds ½, near the centre and everywhere
/// as n grows, its incomplete beta takes its complement at 1 − n/(n + x²), losing the argument's digits (6.4e-10
/// relative at n 1e6 and 4.6e-9 at 1e8, at x 1.5); there nanoql takes I(x²/(n + x²); ½, n/2), the same value by the
/// beta function's reflection, its argument computed directly. And below the centre, ½ − ½·(1 − I) cancels as F(x)
/// falls, to 0 where F(x) is below about 1e-16 (at n 30, x −30); there nanoql takes ½·I(n/(n + x²); n/2, ½), or its
/// reflection's complement, directly. Both routes take Boost's regularised incomplete beta (<c>ibeta</c>, ported for
/// the quantile), as QuantLib's own loses digits in its log-gamma prefactor as n grows (7.6e-10 at n 1e6 with the
/// complementary argument alone): within 8e-14 relative of a high-precision evaluation for n from 1 to 1e9.
/// At n 1 below the centre Boost's complement is its arcsine form on 1 − z, which rounds 1 − z, so there nanoql
/// takes ½·(1 − I(z; ½, ½)), F lying in [¼, ½]: within 2.9e-14 of the Cauchy distribution's ½ + atan(x)/π, where
/// QuantLib's value is 0.5 at x −1e-8, 6.4e-9 off. QuantLib's formula above the centre where n/(n + x²) is at most
/// ½, bit for bit.
/// </remarks>
public sealed class CumulativeStudentDistribution
{
    private readonly int n;

    /// <summary>Initializes a new instance of the <see cref="CumulativeStudentDistribution"/> class.</summary>
    /// <param name="n">The degrees of freedom, positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> isn't positive.</exception>
    public CumulativeStudentDistribution(int n)
    {
        if (!(n > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(n), n, "invalid parameter for t-distribution");
        }

        this.n = n;
    }

    /// <summary>The cumulative distribution function.</summary>
    /// <param name="x">The point.</param>
    /// <returns>F(x).</returns>
    /// <exception cref="NanoQLException">The incomplete beta's continued fraction doesn't converge.</exception>
    public double Evaluate(double x)
    {
        double xx = 1.0 * n / ((x * x) + n);
        if (xx > 0.5)
        {
            // I(1; n/2, ½) − I(xx; n/2, ½) = I(1 − xx; ½, n/2), 1 − xx taken as x²/(n + x²): F = ½ ± ½·I, the lower
            // half as ½·(1 − I), the complement's own
            double z = x * x / ((x * x) + n);
            return x > 0
                ? 0.5 + (0.5 * IncompleteBetaFunctions.Ibeta(0.5, 0.5 * n, z))
                : n == 1 ? 0.5 * (1.0 - IncompleteBetaFunctions.Ibeta(0.5, 0.5, z))
                : 0.5 * IncompleteBetaFunctions.Ibetac(0.5, 0.5 * n, z);
        }

        if (!(x > 0))
        {
            // the lower tail, F = ½·I(xx; n/2, ½), where QuantLib's ½ − ½·(1 − I) cancels
            return 0.5 * IncompleteBetaFunctions.Ibeta(0.5 * n, 0.5, xx);
        }

        // QuantLib's formula above the centre, its sign 1
        return 0.5 + (0.5 * (BetaFunctions.IncompleteBetaFunction(0.5 * n, 0.5, 1.0)
                             - BetaFunctions.IncompleteBetaFunction(0.5 * n, 0.5, xx)));
    }
}
