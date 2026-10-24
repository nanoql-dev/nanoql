// SPDX-FileCopyrightText: (C) Copyright John Maddock 2006.
// SPDX-FileCopyrightText: (C) Copyright Matt Borland 2024.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/detail/igamma_inverse.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using System.Globalization;
using static NanoQL.Numerics.PolynomialHorner2Functions;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's inverses of the regularised incomplete gamma (<c>detail/igamma_inverse.hpp</c>): <c>gamma_p_inv</c>
/// and <c>gamma_q_inv</c>, A. R. DiDonato and A. H. Morris's first guess ("Computation of the Incomplete Gamma
/// Function Ratios and their Inverse", ACM TOMS 12(4), 1986) refined by Halley's method.
/// </summary>
/// <remarks>
/// Boost's inverses take their arguments' type, <c>double</c>, so their errors name it; the incomplete gamma, Γ and
/// ln Γ they call promote to <c>long double</c>, MSVC's <c>double</c>. Under the default policy a domain error on an
/// argument throws <see cref="ArgumentOutOfRangeException"/>, an overflow (p = 1, q = 0) and an evaluation error
/// <see cref="NanoQLException"/>, each with Boost's message; a root below the smallest normal value is an underflow,
/// which the policy ignores, returning 0.
/// </remarks>
internal static class IgammaInverseFunctions
{
    // constants::euler<double>()
    private const double Euler = 0.577215664901532860606512090082402431;

    // tools::epsilon<double>(), tools::min_value<double>(), tools::max_value<double>()
    private const double Epsilon = 2.220446049250313E-16;
    private const double MinValue = 2.2250738585072014E-308;
    private const double MaxValue = double.MaxValue;

    // policies::digits<double, policy<>>()
    private const int Digits = 53;

    private static ReadOnlySpan<double> InverseSA => [3.31125922108741, 11.6616720288968, 4.28342155967104, 0.213623493715853];

    private static ReadOnlySpan<double> InverseSB =>
        [1, 6.61053765625462, 6.40691597760039, 1.27364489782223, 0.3611708101884203e-1];

    /// <summary><c>gamma_p_inv(a, p)</c>: the x at which P(a, x) = p.</summary>
    /// <param name="a">The shape, positive.</param>
    /// <param name="p">The probability, in [0, 1].</param>
    /// <returns>x.</returns>
    /// <exception cref="ArgumentOutOfRangeException">a isn't positive, or p is outside [0, 1].</exception>
    /// <exception cref="NanoQLException">p is 1, an overflow; or Halley's method fails.</exception>
    internal static double GammaPInv(double a, double p)
    {
        const string function = "boost::math::gamma_p_inv<double>(double, double)";

        if (a <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(a),
                $"Error in function {function}: Argument a in the incomplete gamma function inverse must be >= 0 "
                + $"(got a={Show(a)}).");
        }

        if ((p < 0) || (p > 1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(p),
                $"Error in function {function}: Probability must be in the range [0,1] in the incomplete gamma function "
                + $"inverse (got p={Show(p)}).");
        }

        if (p == 1)
        {
            throw new NanoQLException($"Error in function {function}: Overflow Error");
        }

        if (p == 0)
        {
            return 0;
        }

        // Boost returns the guess where it has 10 digits and the type 36 digits or fewer: a double's 53 always refine it.
        double guess = FindInverseGamma(a, p, 1 - p, out _);
        double lower = MinValue;
        if (guess <= lower)
        {
            guess = MinValue;
        }

        //
        // Work out how many digits to converge to, normally this is
        // 2/3 of the digits in T, but if the first derivative is very
        // large convergence is slow, so we'll bump it up to full
        // precision to prevent premature termination of the root-finding routine.
        //
        int digits = (Digits / 2) - 1;
        if ((a < 0.125) && (Math.Abs(GammaFunctions.GammaPDerivative(a, guess)) > 1 / Math.Sqrt(Epsilon)))
        {
            digits = Digits - 2;
        }

        //
        // Go ahead and iterate:
        //
        ulong maxIter = RootsFunctions.MaxRootIterations;
        guess = RootsFunctions.HalleyIterate(new GammaPInverseFunc(a, p, false), guess, lower, MaxValue, digits, ref maxIter);
        RootsFunctions.CheckRootIterations(function, maxIter);
        if (guess == lower)
        {
            // raise_underflow_error: ignored by the default policy, which returns 0
            guess = 0;
        }

        return guess;
    }

    /// <summary><c>gamma_q_inv(a, q)</c>: the x at which Q(a, x) = q.</summary>
    /// <param name="a">The shape, positive.</param>
    /// <param name="q">The probability, in [0, 1].</param>
    /// <returns>x.</returns>
    /// <exception cref="ArgumentOutOfRangeException">a isn't positive, or q is outside [0, 1].</exception>
    /// <exception cref="NanoQLException">q is 0, an overflow; or Halley's method fails.</exception>
    internal static double GammaQInv(double a, double q)
    {
        const string function = "boost::math::gamma_q_inv<double>(double, double)";

        if (a <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(a),
                $"Error in function {function}: Argument a in the incomplete gamma function inverse must be >= 0 "
                + $"(got a={Show(a)}).");
        }

        if ((q < 0) || (q > 1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(q),
                $"Error in function {function}: Probability must be in the range [0,1] in the incomplete gamma function "
                + $"inverse (got q={Show(q)}).");
        }

        if (q == 0)
        {
            throw new NanoQLException($"Error in function {function}: Overflow Error");
        }

        if (q == 1)
        {
            return 0;
        }

        // Boost returns the guess where it has 10 digits and the type 36 digits or fewer: a double's 53 always refine it.
        double guess = FindInverseGamma(a, 1 - q, q, out _);
        double lower = MinValue;
        if (guess <= lower)
        {
            guess = MinValue;
        }

        //
        // Work out how many digits to converge to, normally this is
        // 2/3 of the digits in T, but if the first derivative is very
        // large convergence is slow, so we'll bump it up to full
        // precision to prevent premature termination of the root-finding routine.
        //
        int digits = (Digits / 2) - 1;
        if ((a < 0.125) && (Math.Abs(GammaFunctions.GammaPDerivative(a, guess)) > 1 / Math.Sqrt(Epsilon)))
        {
            digits = Digits;
        }

        //
        // Go ahead and iterate:
        //
        ulong maxIter = RootsFunctions.MaxRootIterations;
        guess = RootsFunctions.HalleyIterate(new GammaPInverseFunc(a, q, true), guess, lower, MaxValue, digits, ref maxIter);
        RootsFunctions.CheckRootIterations(function, maxIter);
        if (guess == lower)
        {
            // raise_underflow_error: ignored by the default policy, which returns 0
            guess = 0;
        }

        return guess;
    }

    // find_inverse_s: DiDonato and Morris's equation 32, the normal deviate's rational approximation
    private static double FindInverseS(double p, double q)
    {
        //
        // Computation of the Incomplete Gamma Function Ratios and their Inverse
        // ARMIDO R. DIDONATO and ALFRED H. MORRIS, JR.
        // ACM Transactions on Mathematical Software, Vol. 12, No. 4,
        // December 1986, Pages 377-393.
        //
        // See equation 32.
        //
        double t;
        if (p < 0.5)
        {
            t = Math.Sqrt(-2 * Math.Log(p));
        }
        else
        {
            t = Math.Sqrt(-2 * Math.Log(q));
        }

        double s = t - EvaluatePolynomial(InverseSA, t) / EvaluatePolynomial(InverseSB, t);
        if (p < 0.5)
        {
            s = -s;
        }

        return s;
    }

    // didonato_SN: DiDonato and Morris's equation 34, the series' first N + 1 terms, stopped below the tolerance
    private static double DidonatoSN(double a, double x, uint n, double tolerance)
    {
        //
        // Computation of the Incomplete Gamma Function Ratios and their Inverse
        // ARMIDO R. DIDONATO and ALFRED H. MORRIS, JR.
        // ACM Transactions on Mathematical Software, Vol. 12, No. 4,
        // December 1986, Pages 377-393.
        //
        // See equation 34.
        //
        double sum = 1;
        if (n >= 1)
        {
            double partial = x / (a + 1);
            sum += partial;
            for (uint i = 2; i <= n; ++i)
            {
                partial *= x / (a + i);
                sum += partial;
                if (partial < tolerance)
                {
                    break;
                }
            }
        }

        return sum;
    }

    // find_inverse_gamma: DiDonato and Morris's first guess, with whether it has 10 digits already
    private static double FindInverseGamma(double a, double p, double q, out bool has10Digits)
    {
        //
        // In order to understand what's going on here, you will
        // need to refer to:
        //
        // Computation of the Incomplete Gamma Function Ratios and their Inverse
        // ARMIDO R. DIDONATO and ALFRED H. MORRIS, JR.
        // ACM Transactions on Mathematical Software, Vol. 12, No. 4,
        // December 1986, Pages 377-393.
        //
        double result;
        has10Digits = false;

        if (a == 1)
        {
            result = -Math.Log(q);
        }
        else if (a < 1)
        {
            double g = GammaFunctions.TGamma(a);
            double b = q * g;
            if ((b > 0.6) || ((b >= 0.45) && (a >= 0.3)))
            {
                // DiDonato & Morris Eq 21:
                //
                // There is a slight variation from DiDonato and Morris here:
                // the first form given here is unstable when p is close to 1,
                // making it impossible to compute the inverse of Q(a,x) for small
                // q.  Fortunately the second form works perfectly well in this case.
                //
                double u;
                if ((b * q > 1e-8) && (q > 1e-5))
                {
                    u = Math.Pow(p * g * a, 1 / a);
                }
                else
                {
                    u = Math.Exp((-q / a) - Euler);
                }

                result = u / (1 - (u / (a + 1)));
            }
            else if ((a < 0.3) && (b >= 0.35))
            {
                // DiDonato & Morris Eq 22:
                double t = Math.Exp(-Euler - b);
                double u = t * Math.Exp(t);
                result = t * Math.Exp(u);
            }
            else if ((b > 0.15) || (a >= 0.3))
            {
                // DiDonato & Morris Eq 23:
                double y = -Math.Log(b);
                double u = y - (1 - a) * Math.Log(y);
                result = y - (1 - a) * Math.Log(u) - Math.Log(1 + (1 - a) / (1 + u));
            }
            else if (b > 0.1)
            {
                // DiDonato & Morris Eq 24:
                double y = -Math.Log(b);
                double u = y - (1 - a) * Math.Log(y);
                result = y - (1 - a) * Math.Log(u)
                    - Math.Log((u * u + 2 * (3 - a) * u + (2 - a) * (3 - a)) / (u * u + (5 - a) * u + 2));
            }
            else
            {
                // DiDonato & Morris Eq 25:
                double y = -Math.Log(b);
                result = Eq25(a, y);
                if (b < (double)1e-28f)
                {
                    has10Digits = true;
                }
            }
        }
        else
        {
            // DiDonato and Morris Eq 31:
            double s = FindInverseS(p, q);

            double s2 = s * s;
            double s3 = s2 * s;
            double s4 = s2 * s2;
            double s5 = s4 * s;
            double ra = Math.Sqrt(a);

            double w = a + s * ra + (s * s - 1) / 3;
            w += (s3 - 7 * s) / (36 * ra);
            w -= (3 * s4 + 7 * s2 - 16) / (810 * a);
            w += (9 * s5 + 256 * s3 - 433 * s) / (38880 * a * ra);

            if ((a >= 500) && (Math.Abs(1 - w / a) < 1e-6))
            {
                result = w;
                has10Digits = true;
            }
            else if (p > 0.5)
            {
                if (w < 3 * a)
                {
                    result = w;
                }
                else
                {
                    double d = Max(2, a * (a - 1));
                    double lg = GammaFunctions.LGamma(a);
                    double lb = Math.Log(q) + lg;
                    if (lb < -d * 2.3)
                    {
                        // DiDonato and Morris Eq 25:
                        result = Eq25(a, -lb);
                    }
                    else
                    {
                        // DiDonato and Morris Eq 33:
                        double u = -lb + (a - 1) * Math.Log(w) - Math.Log(1 + (1 - a) / (1 + w));
                        result = -lb + (a - 1) * Math.Log(u) - Math.Log(1 + (1 - a) / (1 + u));
                    }
                }
            }
            else
            {
                double z = w;
                double ap1 = a + 1;
                double ap2 = a + 2;
                if (w < (double)0.15f * ap1)
                {
                    // DiDonato and Morris Eq 35:
                    double v = Math.Log(p) + GammaFunctions.LGamma(ap1);
                    z = Math.Exp((v + w) / a);
                    s = Log1pFunctions.Log1p(z / ap1 * (1 + z / ap2));
                    z = Math.Exp((v + z - s) / a);
                    s = Log1pFunctions.Log1p(z / ap1 * (1 + z / ap2));
                    z = Math.Exp((v + z - s) / a);
                    s = Log1pFunctions.Log1p(z / ap1 * (1 + z / ap2 * (1 + z / (a + 3))));
                    z = Math.Exp((v + z - s) / a);
                }

                if ((z <= 0.01 * ap1) || (z > 0.7 * ap1))
                {
                    result = z;
                    if (z <= 0.002 * ap1)
                    {
                        has10Digits = true;
                    }
                }
                else
                {
                    // DiDonato and Morris Eq 36:
                    double ls = Math.Log(DidonatoSN(a, z, 100, 1e-4));
                    double v = Math.Log(p) + GammaFunctions.LGamma(ap1);
                    z = Math.Exp((v + z - ls) / a);
                    result = z * (1 - (a * Math.Log(z) - z - v + ls) / (a - z));
                }
            }
        }

        return result;
    }

    // DiDonato and Morris's equation 25 at y, which Boost writes out twice: in Q's far tail for a < 1, and for a > 1
    // where ln q + ln Γ(a) lies far below zero
    private static double Eq25(double a, double y)
    {
        double c1 = (a - 1) * Math.Log(y);
        double c1_2 = c1 * c1;
        double c1_3 = c1_2 * c1;
        double c1_4 = c1_2 * c1_2;
        double a_2 = a * a;
        double a_3 = a_2 * a;

        double c2 = (a - 1) * (1 + c1);
        double c3 = (a - 1) * (-(c1_2 / 2) + (a - 2) * c1 + (3 * a - 5) / 2);
        double c4 = (a - 1) * ((c1_3 / 3) - (3 * a - 5) * c1_2 / 2 + (a_2 - 6 * a + 7) * c1 + (11 * a_2 - 46 * a + 47) / 6);
        double c5 = (a - 1) * (-(c1_4 / 4)
                               + (11 * a - 17) * c1_3 / 6
                               + (-3 * a_2 + 13 * a - 13) * c1_2
                               + (2 * a_3 - 25 * a_2 + 72 * a - 61) * c1 / 2
                               + (25 * a_3 - 195 * a_2 + 477 * a - 379) / 12);

        double y_2 = y * y;
        double y_3 = y_2 * y;
        double y_4 = y_2 * y_2;
        return y + c1 + (c2 / y) + (c3 / y_2) + (c4 / y_3) + (c5 / y_4);
    }

    // std::max as C++ defines it, (a < b) ? b : a: the first argument where the comparison fails, a NaN among them.
    private static double Max(double a, double b) => a < b ? b : a;

    private static string Show(double value) => value.ToString(CultureInfo.InvariantCulture);

    // gamma_p_inverse_func: P(x) − p and its first two derivatives, or with invert Q(x) − q and its
    private readonly struct GammaPInverseFunc : RootsFunctions.ISecondOrderFunction
    {
        private readonly double a;
        private readonly double p;
        private readonly bool invert;

        internal GammaPInverseFunc(double a, double p, bool invert)
        {
            //
            // If p is too near 1 then P(x) - p suffers from cancellation
            // errors causing our root-finding algorithms to "thrash", better
            // to invert in this case and calculate Q(x) - (1-p) instead.
            //
            // Of course if p is *very* close to 1, then the answer we get will
            // be inaccurate anyway (because there's not enough information in p)
            // but at least we will converge on the (inaccurate) answer quickly.
            //
            if (p > 0.9)
            {
                p = 1 - p;
                invert = !invert;
            }

            this.a = a;
            this.p = p;
            this.invert = invert;
        }

        public (double F0, double F1, double F2) Evaluate(double x)
        {
            //
            // Calculate P(x) - p and the first two derivates, or if the invert
            // flag is set, then Q(x) - q and it's derivatives.
            //
            double ft = 0;
            double f = GammaFunctions.GammaIncompleteImp(a, x, true, invert, true, ref ft);
            double f1 = ft;
            double f2;
            double div = (a - x - 1) / x;
            f2 = f1;
            if (Math.Abs(div) > 1)
            {
                if (MaxValue / Math.Abs(div) < f2)
                {
                    // overflow:
                    f2 = -MaxValue / 2;
                }
                else
                {
                    f2 *= div;
                }
            }
            else
            {
                f2 *= div;
            }

            if (invert)
            {
                f1 = -f1;
                f2 = -f2;
            }

            return (f - p, f1, f2);
        }
    }
}
