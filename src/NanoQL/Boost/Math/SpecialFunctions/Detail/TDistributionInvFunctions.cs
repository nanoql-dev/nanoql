// SPDX-FileCopyrightText: Copyright John Maddock 2007.
// SPDX-FileCopyrightText: Copyright Paul A. Bristow 2007
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/detail/t_distribution_inv.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's inverse of Student's t (<c>detail/t_distribution_inv.hpp</c>) for <c>double</c>: the closed forms at
/// 1, 2, 4 and 6 degrees of freedom, Hill's approximation and the tail series elsewhere, and the fast quantile's one
/// Halley step on the incomplete beta. Boost evaluates in <c>long double</c>, which under MSVC is <c>double</c>.
/// </summary>
/// <remarks>
/// The degrees of freedom are an integer of at least 1, the only ones QuantLib passes (its <c>Integer n</c>): Boost's
/// branches for a non-integer below 3 (its body series) and below 2 (the incomplete beta's inverse) aren't ported, so
/// a non-integer is refused.
/// </remarks>
internal static class TDistributionInvFunctions
{
    private const double RootTwo = 1.414213562373095048801688724209698078569671875376948073176679737990732478462;

    /// <summary>
    /// <c>fast_students_t_quantile(df, p, pol)</c>: Student's t quantile, Boost's fast method, accurate to about 15
    /// digits everywhere and a couple of epsilon at double precision in the central region.
    /// </summary>
    /// <param name="df">The degrees of freedom, an integer of at least 1.</param>
    /// <param name="p">The probability, in (0, 1).</param>
    /// <returns>The quantile.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="df"/> isn't an integer of at least 1.</exception>
    /// <exception cref="NanoQLException">The quantile overflows, or a series runs out of terms.</exception>
    internal static double FastStudentsTQuantile(double df, double p)
    {
        if (!(df >= 1 && Math.Floor(df) == df))
        {
            throw new ArgumentOutOfRangeException(
                nameof(df),
                "the degrees of freedom (" + df.ToString(CultureInfo.InvariantCulture)
                + ") must be an integer of at least 1: Boost's routes for others aren't ported");
        }

        double t = FastStudentsTQuantileImp(df, p);
        return Math.Abs(t) > double.MaxValue
            ? throw new NanoQLException(
                "Error in function boost::math::students_t_quantile<double>(double,double,double): Overflow Error")
            : t;
    }

    // inverse_students_t_hill(ndf, u, pol)
    private static double InverseStudentsTHill(double ndf, double u)
    {
        double a, b, c, d, q, x, y;

        if (ndf > 1e20f)
        {
            return -ErfInvFunctions.ErfcInv(2 * u) * RootTwo;
        }

        a = 1 / (ndf - 0.5f);
        b = 48 / (a * a);
        c = ((((((20700 * a) / b) - 98) * a) - 16) * a) + 96.36f;
        d = ((((94.5f / (b + c)) - 3) / b) + 1) * Math.Sqrt(a * Math.PI / 2) * ndf;
        y = Math.Pow(d * 2 * u, 2 / ndf);

        if (y > (0.05f + a))
        {
            //
            // Asymptotic inverse expansion about normal:
            //
            x = -ErfInvFunctions.ErfcInv(2 * u) * RootTwo;
            y = x * x;

            if (ndf < 5)
            {
                c += 0.3f * (ndf - 4.5f) * (x + 0.6f);
            }

            c += (((((((0.05f * d * x) - 5) * x) - 7) * x) - 2) * x) + b;
            y = ((((((((((0.4f * y) + 6.3f) * y) + 36) * y) + 94.5f) / c) - y - 3) / b) + 1) * x;
            y = Expm1(a * y * y);
        }
        else
        {
            y = (((((1 / ((((ndf + 6) / (ndf * y)) - (0.089f * d) - 0.822f) * (ndf + 2) * 3)) + (0.5 / (ndf + 4))) * y) - 1)
                * (ndf + 1) / (ndf + 2)) + (1 / y);
        }

        q = Math.Sqrt(ndf * y);

        return -q;
    }

    // inverse_students_t_tail_series(df, v, pol): the tail series expansion, see section 6 of Shaw's paper; w is
    // calculated using Eq 60
    private static double InverseStudentsTTailSeries(double df, double v)
    {
        // Tail series expansion, see section 6 of Shaw's paper.
        // w is calculated using Eq 60:
        double w = GammaFunctions.TGammaDeltaRatio(df / 2, 0.5) * Math.Sqrt(df * Math.PI) * v;

        // define some variables:
        double np2 = df + 2;
        double np4 = df + 4;
        double np6 = df + 6;

        //
        // Calculate the coefficients d(k), these depend only on the
        // number of degrees of freedom df, so at least in theory
        // we could tabulate these for fixed df, see p15 of Shaw:
        //
        Span<double> d = stackalloc double[7];
        d.Clear();
        d[0] = 1;
        d[1] = -(df + 1) / (2 * np2);
        np2 *= df + 2;
        d[2] = -df * (df + 1) * (df + 3) / (8 * np2 * np4);
        np2 *= df + 2;
        d[3] = -df * (df + 1) * (df + 5) * ((((3 * df) + 7) * df) - 2) / (48 * np2 * np4 * np6);
        np2 *= df + 2;
        np4 *= df + 4;
        d[4] = -df * (df + 1) * (df + 7)
            * ((((((((((15 * df) + 154) * df) + 465) * df) + 286) * df) - 336) * df) + 64)
            / (384 * np2 * np4 * np6 * (df + 8));
        np2 *= df + 2;
        d[5] = -df * (df + 1) * (df + 3) * (df + 9)
            * ((((((((((((35 * df) + 452) * df) + 1573) * df) + 600) * df) - 2020) * df) + 928) * df) - 128)
            / (1280 * np2 * np4 * np6 * (df + 8) * (df + 10));
        np2 *= df + 2;
        np4 *= df + 4;
        np6 *= df + 6;
        d[6] = -df * (df + 1) * (df + 11)
            * ((((((((((((((((((((((945 * df) + 31506) * df) + 425858) * df) + 2980236) * df) + 11266745) * df) + 20675018) * df)
                + 7747124) * df) - 22574632) * df) - 8565600) * df) + 18108416) * df) - 7099392) * df) + 884736)
            / (46080 * np2 * np4 * np6 * (df + 8) * (df + 10) * (df + 12));

        //
        // Now bring everything together to provide the result,
        // this is Eq 62 of Shaw:
        //
        double rn = Math.Sqrt(df);
        double div = Math.Pow(rn * w, 1 / df);
        double power = div * div;
        double result = PolynomialHorner2Functions.EvaluatePolynomial(d, power);
        result *= rn;
        result /= div;
        return -result;
    }

    // inverse_students_t(df, u, v, pol, pexact), for an integer df of at least 1
    private static double InverseStudentsT(double df, double u, double v, out bool exact)
    {
        //
        // df = number of degrees of freedom.
        // u = probability.
        // v = 1 - u.
        // l = lanczos type to use.
        //
        bool invert = false;
        double result = 0;
        exact = false;
        if (u > v)
        {
            // function is symmetric, invert it:
            (u, v) = (v, u);
            invert = true;
        }

        bool calculateReal = true;
        if ((Math.Floor(df) == df) && (df < 20))
        {
            //
            // we have integer degrees of freedom, try for the special
            // cases first:
            //
            // ldexp(1.0f, (2 * policies::digits<T, Policy>()) / 3), as Boost writes it: 2^35
            double tolerance = Math.ScaleB(1.0f, (2 * 53) / 3);

            calculateReal = false;
            switch (TruncFunctions.ITrunc(df))
            {
                case 1:
                    {
                        //
                        // df = 1 is the same as the Cauchy distribution, see
                        // Shaw Eq 35:
                        //
                        if (u == 0.5)
                        {
                            result = 0;
                        }
                        else
                        {
                            result = -Math.Cos(Math.PI * u) / Math.Sin(Math.PI * u);
                        }

                        exact = true;
                        break;
                    }

                case 2:
                    {
                        //
                        // df = 2 has an exact result, see Shaw Eq 36:
                        //
                        result = ((2 * u) - 1) / Math.Sqrt(2 * u * v);
                        exact = true;
                        break;
                    }

                case 4:
                    {
                        //
                        // df = 4 has an exact result, see Shaw Eq 38 & 39:
                        //
                        double alpha = 4 * u * v;
                        double rootAlpha = Math.Sqrt(alpha);
                        double r = 4 * Math.Cos(Math.Acos(rootAlpha) / 3) / rootAlpha;
                        double x = Math.Sqrt(r - 4);
                        result = u - 0.5f < 0 ? -x : x;
                        exact = true;
                        break;
                    }

                case 6:
                    {
                        //
                        // We get numeric overflow in this area:
                        //
                        if (u < 1e-150)
                        {
                            return (invert ? -1 : 1) * InverseStudentsTHill(df, u);
                        }

                        //
                        // Newton-Raphson iteration of a polynomial case,
                        // choice of seed value is taken from Shaw's online
                        // supplement:
                        //
                        double a = 4 * (u - (u * u)); // 1 - 4 * (u - 0.5f) * (u - 0.5f);
                        double b = CbrtFunctions.Cbrt(a);
                        const double c = 0.85498797333834849467655443627193;
                        double p = 6 * (1 + (c * ((1 / b) - 1)));
                        double p0;
                        do
                        {
                            double p2 = p * p;
                            double p4 = p2 * p2;
                            double p5 = p * p4;
                            p0 = p;

                            // next term is given by Eq 41:
                            p = 2 * ((8 * a * p5) - (270 * p2) + 2187) / (5 * ((4 * a * p4) - (216 * p) - 243));
                        }
                        while (Math.Abs((p - p0) / p) > tolerance);

                        //
                        // Use Eq 45 to extract the result:
                        //
                        p = Math.Sqrt(p - df);
                        result = (u - 0.5f) < 0 ? -p : p;
                        break;
                    }

                default:
                    calculateReal = true;
                    break;
            }
        }

        if (calculateReal)
        {
            if (df > 0x10000000)
            {
                result = -ErfInvFunctions.ErfcInv(2 * u) * RootTwo;
                if (df >= 1e20)
                {
                    exact = true;
                }
            }
            else
            {
                // an integer df here is 3, 5 or above (1 and 2 are exact): Boost's df < 3 branch, its body and tail
                // series for a non-integer, isn't reached
                //
                // Use Hill's method except in the extreme tails
                // where we use Shaw's tail series.
                // The crossover point is roughly exponential in -df:
                //
                double mExp = Frexp(u, out int uExp);
                if (mExp > 0 && uExp < df / 0.654f)
                {
                    result = InverseStudentsTHill(df, u);
                }
                else
                {
                    result = InverseStudentsTTailSeries(df, u);
                }
            }
        }

        return invert ? -result : result;
    }

    // fast_students_t_quantile_imp(df, p, pol, true_type), for an integer df of at least 1
    private static double FastStudentsTQuantileImp(double df, double p)
    {
        //
        // Need to use inverse incomplete beta to get
        // required precision so not so fast:
        //
        bool invert = false;

        // Boost's non-integer df below 2, through the incomplete beta's inverse, isn't reached
        if (p > 0.5)
        {
            p = 1 - p;
            invert = true;
        }

        //
        // Get an estimate of the result:
        //
        double t = InverseStudentsT(df, p, 1 - p, out bool exact);
        if ((t == 0) || exact)
        {
            // can't do better!
            return invert ? -t : t;
        }

        //
        // Change variables to inverse incomplete beta:
        //
        double t2 = t * t;
        double xb = df / (df + t2);
        double y = t2 / (df + t2);
        double a = df / 2;

        //
        // t can be so large that x underflows,
        // just return our estimate in that case:
        //
        if (xb == 0)
        {
            return t;
        }

        //
        // Get incomplete beta and it's derivative:
        //
        double f1 = 0;
        double f0 = xb < y
            ? IncompleteBetaFunctions.IbetaImp(a, 0.5, xb, false, true, ref f1)
            : IncompleteBetaFunctions.IbetaImp(0.5, a, y, true, true, ref f1);

        // Get cdf from incomplete beta result:
        double p0 = (f0 / 2) - p;

        // Get pdf from derivative:
        double p1 = f1 * Math.Sqrt(y * xb * xb * xb / df);

        //
        // Second derivative divided by p1:
        //
        // yacas gives:
        //
        // In> PrettyForm(Simplify(D(t) (1 + t^2/v) ^ (-(v+1)/2)))
        //
        //  |                        | v + 1     |     |
        //  |                       -| ----- + 1 |     |
        //  |                        |   2       |     |
        // -|             |  2     |                   |
        //  |             | t      |                   |
        //  |             | -- + 1 |                   |
        //  | ( v + 1 ) * | v      |               * t |
        // ---------------------------------------------
        //                       v
        //
        // Which after some manipulation is:
        //
        // -p1 * t * (df + 1) / (t^2 + df)
        //
        double p2 = t * (df + 1) / ((t * t) + df);

        // Halley step:
        t = Math.Abs(t);
        t += p0 / (p1 + (p0 * p2 / 2));
        return !invert ? -t : t;
    }

    // boost::math::expm1 for long double under the default policy: std::expm1 behind Boost's checks, which Hill's
    // argument never reaches
    private static double Expm1(double x)
    {
        if (double.IsNaN(x))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                "Error in function boost::math::expm1<long double>(long double): expm1 requires a finite argument, but got "
                + x.ToString(CultureInfo.InvariantCulture));
        }

        if (x >= 709.0)
        {
            throw new NanoQLException("Error in function boost::math::expm1<long double>(long double): Overflow Error");
        }

        return Expm1Functions.StdExpm1(x);
    }

    // std::frexp for a finite, nonzero x: the mantissa in [0.5, 1) and the exponent, x = mantissa·2^exponent.
    private static double Frexp(double x, out int exponent)
    {
        exponent = Math.ILogB(x) + 1;
        return Math.ScaleB(x, -exponent);
    }
}
