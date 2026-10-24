// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2007 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/distributions/chisquaredistribution.hpp ql/math/distributions/chisquaredistribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The cumulative non-central chi-squared distribution with df degrees of freedom and non-centrality ncp, by the
/// series of Poisson-weighted central terms (Ding's algorithm, as R's <c>pnchisq</c> sums it).
/// </summary>
/// <remarks>
/// <para>
/// QuantLib 1.44's series returns values past its range (<c>chisquaredistribution.cpp:33-91</c>): where its first term
/// or its Poisson weight exp(−ncp/2) falls below the normal range of doubles, it runs on subnormal numbers, then zeros
/// (at df 10 and x 1,547 it reads 1.81 where the answer is 1; from ncp 1,490.3 every term is 0 and the sum 0); and where
/// its loop ends at 10,000 iterations with its bound's denominator never positive, the negative bound passes as
/// converged. Here the series is QuantLib's master's, upstream's #2908 (<c>b76016ff5</c>): it skips the underflowing
/// weights and terms by their logarithms, returns 1 where a Chernoff bound puts the upper tail below 1e-12, runs to
/// 1,000,000 iterations, and refuses a loop that ends before its bound can be computed ("didn't converge"). A
/// documented divergence from 1.44 (the S-van plan's VR11, <c>Divergences/ChiSquaredSeriesRefusalTests.cs</c>), checked
/// by upstream's case against Boost's distribution (<c>DistributionsTests.TestNonCentralChiSquareLargeArguments</c>);
/// every value inside 1.44's range is QuantLib's bit for bit.
/// </para>
/// <para>
/// QuantLib's large-df branch tests |x/2 − df/2| without assigning it and starts from t = 0 (<c>:48-52</c>), as
/// #2908 keeps it; above df 1.1e15 and within 1.5e-8 of df its bound then never falls to 1e-12 within the iterations,
/// so it is refused, and its missing assignment needs no fix of its own.
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
    /// The series doesn't converge in its 1,000,000 iterations, or the non-centrality's half isn't below
    /// <c>QL_MAX_INTEGER</c> where its Poisson weight leaves the normal range of doubles.
    /// </exception>
    public double Evaluate(double x)
    {
        if (x <= 0.0)
        {
            return 0.0;
        }

        const double errmax = 1e-12;
        const ulong itrmax = 1000000;
        double minLog = Math.Log(NumericLimits.QL_MIN_POSITIVE_REAL);
        double lam = 0.5 * ncp_;
        double x2 = 0.5 * x;
        double f2 = 0.5 * df_;

        // For large ncp or x, the Poisson weights u and the first terms t
        // of the series underflow to zero, and so would the whole sum.
        // We use their logarithms (which increase with n up to lam and
        // x2-f2, respectively) to find the first representable ones and
        // skip the corresponding (negligible) terms.
        double LogU(ulong n) => (n * Math.Log(lam)) - lam - GammaFunctions.LGamma(n + 1.0);
        double LogT(ulong n) => ((f2 + n) * Math.Log(x2)) - x2 - GammaFunctions.LGamma(f2 + n + 1.0);
        const ulong never = ulong.MaxValue;
        ulong FirstT(ulong from)
        {
            double peak = Min(Math.Floor(x2 - f2), from + itrmax);
            if (peak < from)
            {
                return LogT(from) >= minLog ? from : never;
            }

            ulong lo = from, hi = (ulong)peak;
            if (LogT(hi) < minLog)
            {
                return never;
            }

            while (lo < hi)
            {
                ulong mid = lo + ((hi - lo) / 2);
                if (LogT(mid) >= minLog)
                {
                    hi = mid;
                }
                else
                {
                    lo = mid + 1;
                }
            }

            return lo;
        }

        bool uSmall = -lam < minLog;
        double u = uSmall ? 0.0 : Math.Exp(-lam);
        double v = u;

        double t = 0.0;
        bool tSmall = false;
        if (f2 * NumericLimits.QL_EPSILON > 0.125
            && Math.Abs(x2 - f2) < Math.Sqrt(NumericLimits.QL_EPSILON) * f2)
        {
            t = Math.Exp((1 - t) * (2 - (t / (f2 + 1)))) / Math.Sqrt(2.0 * MathConstants.M_PI * (f2 + 1.0));
        }
        else
        {
            double lt = (f2 * Math.Log(x2)) - x2 - new GammaFunction().LogValue(f2 + 1);
            tSmall = lt < minLog;
            if (!tSmall)
            {
                t = Math.Exp(lt);
            }
        }

        if (tSmall && x > df_ + ncp_)
        {
            // Chernoff bound on the upper tail.  If it's negligible,
            // return 1 instead of summing about x/2 terms to get there.
            double w = (df_ + Math.Sqrt((df_ * df_) + (4.0 * x * ncp_))) / (2.0 * x);
            double logBound = (-0.5 * (1.0 - w) * x) - (0.5 * df_ * Math.Log(w)) + (0.5 * ncp_ * (1.0 - w) / w);
            if (logBound < Math.Log(errmax))
            {
                return 1.0;
            }
        }

        double ans = v * t;

        ulong n = 1;
        ulong nT = tSmall ? FirstT(n) : 0;
        if (uSmall)
        {
            // jump to the first representable Poisson weight
            if (!(lam < NumericLimits.QL_MAX_INTEGER))
            {
                throw new NanoQLException("non-centrality parameter too large");
            }

            ulong lo = n, hi = (ulong)lam;
            while (lo < hi)
            {
                ulong mid = lo + ((hi - lo) / 2);
                if (LogU(mid) >= minLog)
                {
                    hi = mid;
                }
                else
                {
                    lo = mid + 1;
                }
            }

            n = lo;
            u = v = Math.Exp(LogU(n));
            double lt = LogT(n);
            tSmall = lt < minLog;
            t = tSmall ? 0.0 : Math.Exp(lt);
            nT = tSmall ? FirstT(n + 1) : 0;
            ans = v * t;
            n++;
        }

        ulong nMax = n + itrmax;

        bool flag = false;
        double f_2n = df_ + (2.0 * n);
        double f_x_2n = df_ - x + (2.0 * n);

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
                    if (tSmall)
                    {
                        if (n >= nT)
                        {
                            t = Math.Exp(LogT(n));
                            tSmall = false;
                        }
                    }
                    else
                    {
                        t *= x / f_2n;
                    }

                    ans += v * t;
                    n++;
                    f_2n += 2.0;
                    f_x_2n += 2.0;
                    if (!flag && n <= nMax)
                    {
                        break;
                    }
                }

                toBound = false;
                bound = t * x / f_x_2n;
                if (bound <= errmax || n > nMax)
                {
                    goto L_End;
                }
            }
        }

    L_End:
        // f_x_2n <= 0 means that we ran out of iterations before
        // the error bound could even be computed
        if (f_x_2n <= 0 || bound > errmax)
        {
            throw new NanoQLException("didn't converge");
        }

        return ans;
    }

    // std::min as C++ defines it, (b < a) ? b : a
    private static double Min(double a, double b) => b < a ? b : a;
}
