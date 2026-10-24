// SPDX-FileCopyrightText: Copyright (c) 2006 Xiaogang Zhang
// SPDX-FileCopyrightText: Copyright (c) 2024 Matt Borland
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/detail/bessel_ik.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's modified Bessel functions I_v and K_v of real order (<c>detail/bessel_ik.hpp</c>): Temme's method
/// (J. Comput. Phys. 19, 324, 1975), K by Temme's series for x ≤ 2 and Steed's continued fraction above, carried up
/// to the order by recurrence, and I from K through the Wronskian with the continued fraction for I_(v+1)/I_v; and
/// the small-argument series of I.
/// </summary>
/// <remarks>
/// Boost's evaluation type here is <c>long double</c>, MSVC's <c>double</c>, so its errors name <c>long double</c>.
/// Under the default policy a series out of terms (an evaluation error) and an overflow throw
/// <see cref="NanoQLException"/> with Boost's message. Boost counts the series' terms in <c>unsigned long</c>, 32 bits
/// under MSVC, and the products of those counts wrap there as Boost's do.
/// </remarks>
internal static class BesselIkFunctions
{
    /// <summary><c>need_i</c>: I is wanted.</summary>
    internal const int NeedI = 1;

    /// <summary><c>need_k</c>: K is wanted.</summary>
    internal const int NeedK = 2;

    // tools::epsilon, min_value, max_value, log_min_value and forth_root_epsilon of long double, MSVC's double
    private const double Epsilon = 2.220446049250313E-16;
    private const double MinValue = 2.2250738585072014E-308;
    private const double MaxValue = double.MaxValue;
    private const double LogMinValue = -708.0;
    private const double ForthRootEpsilon = 0.0001220703125;

    // constants::euler<long double>() and root_pi<long double>()
    private const double Euler = 0.577215664901532860606512090082402431;
    private const double RootPi = 1.772453850905516027298167483341145182;

    /// <summary>
    /// <c>bessel_i_small_z_series(v, x)</c>: I_v(x) by its power series, (x/2)^v/Γ(v + 1) times
    /// Σ (x²/4)^k/(k!·(v + 1)_k), for x small against v.
    /// </summary>
    /// <param name="v">The order.</param>
    /// <param name="x">The argument.</param>
    /// <returns>I_v(x).</returns>
    /// <exception cref="NanoQLException">The series runs out of terms, or Γ(v + 1) overflows.</exception>
    internal static double BesselISmallZSeries(double v, double x)
    {
        double prefix;
        if (v < UncheckedFactorialFunctions.MaxFactorial)
        {
            prefix = Math.Pow(x / 2, v) / GammaFunctions.TGamma(v + 1);
        }
        else
        {
            prefix = v * Math.Log(x / 2) - GammaFunctions.LGamma(v + 1);
            prefix = Math.Exp(prefix);
        }

        if (prefix == 0)
        {
            return prefix;
        }

        var s = new CylBesselISmallZ(v, x);
        ulong maxIter = SeriesFunctions.MaxSeriesIterations;

        double result = SeriesFunctions.SumSeries(ref s, Epsilon, ref maxIter, 0);

        SeriesFunctions.CheckSeriesIterations(
            "boost::math::bessel_j_small_z_series<long double>(long double,long double)", maxIter);
        return prefix * result;
    }

    /// <summary>
    /// <c>bessel_ik(v, x, &amp;I, &amp;K, kind)</c>: I_v(x) and K_v(x) by Temme's method, I_v of negative order by
    /// the reflection formula.
    /// </summary>
    /// <param name="v">The order.</param>
    /// <param name="x">The argument, positive.</param>
    /// <param name="kind"><see cref="NeedI"/>, <see cref="NeedK"/> or both: the results an overflow is raised on.</param>
    /// <param name="resultI">I_v(x); NaN where only K is wanted.</param>
    /// <param name="resultK">K_v(x); 0 where it overflows and isn't wanted.</param>
    /// <exception cref="NanoQLException">A series runs out of terms, or a wanted result overflows.</exception>
    internal static void BesselIk(double v, double x, int kind, out double resultI, out double resultK)
    {
        // Kv1 = K_(v+1), fv = I_(v+1) / I_v
        // Ku1 = K_(u+1), fu = I_(u+1) / I_u
        double u, iv, kv, kv1, ku, ku1, fv;
        double w, current, prev, next;
        bool reflect = false;
        uint n, k;
        int orgKind = kind;

        const string function = "boost::math::bessel_ik<long double>(long double,long double)";

        if (v < 0)
        {
            reflect = true;
            v = -v;                             // v is non-negative from here
            kind |= NeedK;
        }

        double scale = 1;
        double scaleSign = 1;

        n = (uint)RoundFunctions.IRound(v);
        u = v - n;                              // -1/2 <= u < 1/2

        if (((kind & NeedI) == 0) && (Math.Abs(4 * v * v - 25) / (8 * x) < ForthRootEpsilon))
        {
            // A&S 9.7.2
            iv = double.NaN; // any value will do
            double mu = 4 * v * v;
            double eightZ = 8 * x;
            kv = 1 + (mu - 1) / eightZ + (mu - 1) * (mu - 9) / (2 * eightZ * eightZ)
                + (mu - 1) * (mu - 9) * (mu - 25) / (6 * eightZ * eightZ * eightZ);
            kv *= Math.Exp(-x) * RootPi / Math.Sqrt(2 * x);
        }
        else
        {
            // x is positive until reflection
            w = 1 / x;                                 // Wronskian
            if (x <= 2)                                // x in (0, 2]
            {
                TemmeIk(u, x, out ku, out ku1);           // Temme series
            }
            else                                       // x in (2, \infty)
            {
                Cf2Ik(u, x, out ku, out ku1);             // continued fraction CF2_ik
            }

            prev = ku;
            current = ku1;
            for (k = 1; k <= n; k++)                   // forward recurrence for K
            {
                double fact = 2 * (u + k) / x;

                // Check for overflow: if (max - |prev|) / fact > max, then overflow
                // (max - |prev|) / fact > max
                // max * (1 - fact) > |prev|
                // if fact < 1: safe to compute overflow check
                // if fact >= 1:  won't overflow
                bool willOverflow = (fact < 1) && MaxValue * (1 - fact) > Math.Abs(prev);
                if (!willOverflow && ((MaxValue - Math.Abs(prev)) / fact < Math.Abs(current)))
                {
                    prev /= current;
                    scale /= current;
                    scaleSign *= double.IsNegative(current) ? -1 : 1;
                    current = 1;
                }

                next = fact * current + prev;
                prev = current;
                current = next;
            }

            kv = prev;
            kv1 = current;
            if ((kind & NeedI) != 0)
            {
                double lim = (4 * v * v + 10) / (8 * x);
                lim *= lim;
                lim *= lim;
                lim /= 24;
                if ((lim < Epsilon * 10) && (x > 100))
                {
                    // x is huge compared to v, CF1 may be very slow
                    // to converge so use asymptotic expansion for large
                    // x case instead.  Note that the asymptotic expansion
                    // isn't very accurate - so it's deliberately very hard
                    // to get here - probably we're going to overflow:
                    iv = BesselJyAsymFunctions.AsymptoticBesselILargeX(v, x);
                }
                else if ((v > 0) && (x / v < 0.25))
                {
                    iv = BesselISmallZSeries(v, x);
                }
                else
                {
                    Cf1Ik(v, x, out fv);                           // continued fraction CF1_ik
                    iv = scale * w / (kv * fv + kv1);                  // Wronskian relation
                }
            }
            else
            {
                iv = double.NaN; // any value will do
            }
        }

        if (reflect && ((kind & NeedI) != 0))
        {
            double z = u + n % 2;
            double fact = (2 / Math.PI) * (SinPiFunctions.SinPi(z) * kv);
            if (fact == 0)
            {
                resultI = iv;
            }
            else if (MaxValue * scale < fact)
            {
                resultI = (orgKind & NeedI) != 0
                    ? throw new NanoQLException($"Error in function {function}: Overflow Error")
                    : 0;
            }
            else
            {
                resultI = iv + fact / scale;   // reflection formula
            }
        }
        else
        {
            resultI = iv;
        }

        if (MaxValue * scale < kv)
        {
            resultK = (orgKind & NeedK) != 0 ? throw new NanoQLException($"Error in function {function}: Overflow Error") : 0;
        }
        else
        {
            resultK = kv / scale;
        }
    }

    // temme_ik: K_v(x) and K_(v+1)(x) for |v| <= 1/2 and x <= 2 by Temme's series (J. Comput. Phys. 21, 343, 1976)
    private static void TemmeIk(double v, double x, out double resultK, out double k1)
    {
        double f, h, p, q, coef, sum, sum1, tolerance;
        double a, b, c, d, sigma, gamma1, gamma2;
        uint k;

        // |x| <= 2, Temme series converge rapidly
        // |x| > 2, the larger the |x|, the slower the convergence
        double gp = GammaFunctions.TGamma1pm1(v);
        double gm = GammaFunctions.TGamma1pm1(-v);

        a = Math.Log(x / 2);
        b = Math.Exp(v * a);
        sigma = -a * v;
        c = Math.Abs(v) < Epsilon ? 1 : SinPiFunctions.SinPi(v) / (v * Math.PI);
        d = Math.Abs(sigma) < Epsilon ? 1 : Math.Sinh(sigma) / sigma;
        gamma1 = Math.Abs(v) < Epsilon ? -Euler : ((double)0.5f / v) * (gp - gm) * c;
        gamma2 = (2 + gp + gm) * c / 2;

        // initial values
        p = (gp + 1) / (2 * b);
        q = (1 + gm) * b / 2;
        f = (Math.Cosh(sigma) * gamma1 + d * (-a) * gamma2) / c;
        h = p;
        coef = 1;
        sum = coef * f;
        sum1 = coef * h;

        // series summation
        tolerance = Epsilon;
        for (k = 1; k < SeriesFunctions.MaxSeriesIterations; k++)
        {
            f = (k * f + p + q) / ((k * k) - v * v);
            p /= k - v;
            q /= k + v;
            h = p - k * f;
            coef *= x * x / (4u * k);
            sum += coef * f;
            sum1 += coef * h;
            if (Math.Abs(coef * f) < Math.Abs(sum) * tolerance)
            {
                break;
            }
        }

        SeriesFunctions.CheckSeriesIterations("boost::math::bessel_ik<long double>(long double,long double) in temme_ik", k);

        resultK = sum;
        k1 = 2 * sum1 / x;
    }

    // CF1_ik: f_v = I_(v+1)/I_v by its continued fraction (A&S 9.1.73), by Lentz's method (Appl. Opt. 15, 668, 1976)
    private static void Cf1Ik(double v, double x, out double fv)
    {
        double c, d, f, a, b, delta, tiny, tolerance;
        uint k;

        // |x| <= |v|, CF1_ik converges rapidly
        // |x| > |v|, CF1_ik needs O(|x|) iterations to converge

        // modified Lentz's method, see
        // Lentz, Applied Optics, vol 15, 668 (1976)
        tolerance = 2 * Epsilon;
        tiny = Math.Sqrt(MinValue);
        c = f = tiny;                           // b0 = 0, replace with tiny
        d = 0;
        for (k = 1; k < SeriesFunctions.MaxSeriesIterations; k++)
        {
            a = 1;
            b = 2 * (v + k) / x;
            c = b + a / c;
            d = b + a * d;
            if (c == 0)
            {
                c = tiny;
            }

            if (d == 0)
            {
                d = tiny;
            }

            d = 1 / d;
            delta = c * d;
            f *= delta;
            if (Math.Abs(delta - 1) <= tolerance)
            {
                break;
            }
        }

        SeriesFunctions.CheckSeriesIterations("boost::math::bessel_ik<long double>(long double,long double) in CF1_ik", k);

        fv = f;
    }

    // CF2_ik: K_v(x) and K_(v+1)(x) for x > 1 by the continued fraction z1/z0 = U(v + 1.5, 2v + 1, 2x)/U(v + 0.5,
    // 2v + 1, 2x) (Thompson and Barnett, Comput. Phys. Commun. 47, 245, 1987), by Steed's algorithm
    private static void Cf2Ik(double v, double x, out double kv, out double kv1)
    {
        double s, c, qq, d, f, a, b, q, delta, tolerance, current, prev;
        uint k;

        // |x| >= |v|, CF2_ik converges rapidly
        // |x| -> 0, CF2_ik fails to converge

        // Steed's algorithm, see Thompson and Barnett,
        // Journal of Computational Physics, vol 64, 490 (1986)
        tolerance = Epsilon;
        a = v * v - (double)0.25f;
        b = 2 * (x + 1);                              // b1
        d = 1 / b;                                    // D1 = 1 / b1
        f = delta = d;                                // f1 = delta1 = D1, coincidence
        prev = 0;                                     // q0
        current = 1;                                  // q1
        qq = c = -a;                                  // Q1 = C1 because q1 = 1
        s = 1 + qq * delta;                           // S1

        for (k = 2; k < SeriesFunctions.MaxSeriesIterations; k++)     // starting from 2
        {
            // continued fraction f = z1 / z0
            a -= 2u * (k - 1);
            b += 2;
            d = 1 / (b + a * d);
            delta *= b * d - 1;
            f += delta;

            // series summation S = 1 + \sum_{n=1}^{\infty} C_n * z_n / z_0
            q = (prev - (b - 2) * current) / a;
            prev = current;
            current = q;                        // forward recurrence for q
            c *= -a / k;
            qq += c * q;
            s += qq * delta;

            //
            // Under some circumstances q can grow very small and C very
            // large, leading to under/overflow.  This is particularly an
            // issue for types which have many digits precision but a narrow
            // exponent range.  A typical example being a "double double" type.
            // To avoid this situation we can normalise q (and related prev/current)
            // and C.  All other variables remain unchanged in value.  A typical
            // test case occurs when x is close to 2, for example cyl_bessel_k(9.125, 2.125).
            //
            if (q < Epsilon)
            {
                c *= q;
                prev /= q;
                current /= q;
                q = 1;
            }

            // S converges slower than f
            if (Math.Abs(qq * delta) < Math.Abs(s) * tolerance)
            {
                break;
            }
        }

        SeriesFunctions.CheckSeriesIterations("boost::math::bessel_ik<long double>(long double,long double) in CF2_ik", k);

        if (-x < LogMinValue)
        {
            kv = Math.Exp((double)0.5f * Math.Log(Math.PI / (2 * x)) - x - Math.Log(s));
        }
        else
        {
            kv = Math.Sqrt(Math.PI / (2 * x)) * Math.Exp(-x) / s;
        }

        kv1 = kv * ((double)0.5f + v + x + (v * v - (double)0.25f) * f) / x;
    }

    // cyl_bessel_i_small_z: the power series' terms (x²/4)^k/(k!·(v + 1)_k)
    private struct CylBesselISmallZ(double v, double z) : SeriesFunctions.ITerms
    {
        private readonly double v = v;
        private readonly double mult = z * z / 4;
        private uint k;
        private double term = 1;

        public double Next()
        {
            double result = term;
            ++k;
            term *= mult / k;
            term /= k + v;
            return result;
        }
    }
}
