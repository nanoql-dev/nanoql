// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2007 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/distributions/chisquaredistribution.hpp ql/math/distributions/chisquaredistribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// The cumulative non-central chi-squared distribution with df degrees of freedom and non-centrality ncp, by the
/// series of Poisson-weighted central terms (Ding's algorithm, as R's <c>pnchisq</c> sums it).
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's series returns values past its range (<c>chisquaredistribution.cpp:33-91</c>), which are refused here
/// (a documented divergence), every other value QuantLib's bit for bit:
/// </para>
/// <list type="bullet">
/// <item><description>Where the loop ends at its 10,000 iterations with its bound never computed on a positive
/// denominator, QuantLib's negative bound passes as converged, so above df + 20,000 the partial sum returns (at df
/// 1.2e15 every input above df returns about 0.0012); refused with QuantLib's own "didn't
/// converge".</description></item>
/// <item><description>Where the series' first term falls below the normal range of doubles above df, the series runs on
/// subnormal numbers, then zeros (at df 10 and ncp 0 QuantLib reads up to 2.0010 at x 1,547.2, and 0 beyond, where the
/// answer is 1); refused. Below df the answer is then under about 1e-300, and QuantLib's value
/// stands.</description></item>
/// <item><description>Where the Poisson weight's start, exp(−ncp/2), falls below the normal range, from ncp 1,416.8,
/// every term carries fewer bits, and from ncp 1,490.3 every term is 0 and passes as converged (at df 30, ncp 1,500 and
/// x 1,530 QuantLib returns 0 where Sankaran's approximation gives 0.5051); refused for every x &gt;
/// 0.</description></item>
/// </list>
/// <para>
/// QuantLib's large-df branch tests |x/2 − df/2| without assigning it and starts from t = 0 (<c>:48-52</c>); it then
/// always reaches the first refusal, so its missing assignment needs no fix of its own.
/// </para>
/// </remarks>
public sealed class NonCentralCumulativeChiSquareDistribution
{
    private readonly double df_, ncp_;

    /// <summary>Initializes a new instance of the <see cref="NonCentralCumulativeChiSquareDistribution"/>
    /// class.</summary>
    /// <param name="df">The degrees of freedom.</param>
    /// <param name="ncp">The non-centrality.</param>
    public NonCentralCumulativeChiSquareDistribution(double df, double ncp)
    {
        df_ = df;
        ncp_ = ncp;
    }

    /// <summary>The probability of a value below <paramref name="x"/>.</summary>
    /// <param name="x">The point; 0 at or below 0.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="NanoQLException">
    /// The series doesn't converge in its 10,000 iterations, or its first term above df or its Poisson weight is
    /// outside the normal range of doubles.
    /// </exception>
    public double Evaluate(double x)
    {
        if (x <= 0.0)
        {
            return 0.0;
        }

        const double errmax = 1e-12;
        const int itrmax = 10000;
        double lam = 0.5 * ncp_;

        double u = Math.Exp(-lam);
        if (u < NumericLimits.QL_MIN_POSITIVE_REAL)
        {
            throw new NanoQLException(
                $"the series' Poisson weight exp(-ncp/2) is outside the range of doubles at ncp {Show(ncp_)}");
        }

        double v = u;
        double x2 = 0.5 * x;
        double f2 = 0.5 * df_;
        double f_x_2n = df_ - x;

        double t = 0.0;
        if (f2 * NumericLimits.QL_EPSILON > 0.125
            && Math.Abs(x2 - f2) < Math.Sqrt(NumericLimits.QL_EPSILON) * f2)
        {
            t = Math.Exp((1 - t) * (2 - (t / (f2 + 1)))) / Math.Sqrt(2.0 * MathConstants.M_PI * (f2 + 1.0));
        }
        else
        {
            t = Math.Exp((f2 * Math.Log(x2)) - x2 - new GammaFunction().LogValue(f2 + 1));
        }

        if (x > df_ && t < NumericLimits.QL_MIN_POSITIVE_REAL)
        {
            throw new NanoQLException($"the series' first term is outside the range of doubles at x {Show(x)}");
        }

        double ans = v * t;

        bool flag = false;
        int n = 1;
        double f_2n = df_ + 2.0;
        f_x_2n += 2.0;

        double bound;
        for (;;)
        {
            // QuantLib jumps into the inner loop's bound (label L10) once the denominator is positive
            bool toBound = false;
            if (f_x_2n > 0)
            {
                flag = true;
                toBound = true;
            }

            for (;;)
            {
                if (!toBound)
                {
                    u *= lam / n;
                    v += u;
                    t *= x / f_2n;
                    ans += v * t;
                    n++;
                    f_2n += 2.0;
                    f_x_2n += 2.0;
                    if (!flag && n <= itrmax)
                    {
                        break;
                    }
                }

                toBound = false;
                bound = t * x / f_x_2n;
                if (bound <= errmax || n > itrmax)
                {
                    goto L_End;
                }
            }
        }

    L_End:
        // QuantLib refuses only a bound above errmax; a loop ended at itrmax before its denominator turned positive
        // reads a negative bound, refused here too
        if (!flag || bound > errmax)
        {
            throw new NanoQLException("didn't converge");
        }

        return ans;
    }

    private static string Show(double value) => value.ToString(CultureInfo.InvariantCulture);
}
