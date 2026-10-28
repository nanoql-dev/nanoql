// SPDX-FileCopyrightText: Copyright John Maddock 2006-7, 2013-20.
// SPDX-FileCopyrightText: Copyright Paul A. Bristow 2007, 2013-14.
// SPDX-FileCopyrightText: Copyright Nikhar Agrawal 2013-14
// SPDX-FileCopyrightText: Copyright Christopher Kormanyos 2013-14, 2020, 2024
// SPDX-FileCopyrightText: Copyright Matt Borland 2024.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/gamma.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's gamma functions (<c>gamma.hpp</c>) for <c>double</c> under the default policy: <c>tgamma</c>,
/// <c>lgamma</c>, <c>tgamma1pm1</c>, the regularised incomplete gammas <c>gamma_p</c> and <c>gamma_q</c>,
/// <c>gamma_p_derivative</c> and <c>tgamma_delta_ratio</c>, on Boost's Lanczos approximation
/// <see cref="Lanczos13m53"/>. Boost promotes
/// <c>double</c> to <c>long double</c>, which under MSVC is <c>double</c>, so its errors name the
/// <c>long double</c> instantiation; the public entries' last check, an infinite result's overflow as it narrows to
/// the caller's type, names <c>double</c>. A domain error on an argument throws <see cref="ArgumentOutOfRangeException"/>;
/// a pole, an overflow and an evaluation error throw <see cref="NanoQLException"/>; each keeps Boost's message. Boost's
/// underflow, which its default policy ignores, returns 0.
/// </summary>
internal static class GammaFunctions
{
    private const double Euler = 0.577215664901532860606512090082402431042;
    private const double RootTwoPi = 2.506628274631000502415765284811045253;
    private const double RootPi = 1.772453850905516027298167483341145182;
    private const double RootEpsilon = 0.1490116119384765625e-7;
    private const double Epsilon = 2.220446049250313E-16;
    private const double LogMaxValue = 709.0;
    private const double LogMinValue = -708.0;
    private const double MaxValue = double.MaxValue;

    /// <summary><c>tgamma(z)</c>: Γ(z).</summary>
    /// <param name="z">The argument, not a non-positive integer.</param>
    /// <returns>Γ(z).</returns>
    /// <exception cref="NanoQLException">z is a non-positive integer (a pole), or Γ(z) overflows.</exception>
    internal static double TGamma(double z) => CheckedNarrowing(GammaImp(z), "boost::math::tgamma<double>(double)");

    /// <summary><c>lgamma(z)</c>: ln |Γ(z)|.</summary>
    /// <param name="z">The argument, not a non-positive integer.</param>
    /// <returns>ln |Γ(z)|.</returns>
    /// <exception cref="NanoQLException">z is a non-positive integer (a pole), or ln |Γ(z)| overflows.</exception>
    internal static double LGamma(double z) => CheckedNarrowing(LgammaImp(z, out _), "boost::math::lgamma<double>(double)");

    /// <summary><c>lgamma(z, &amp;sign)</c>: ln |Γ(z)| and the sign of Γ(z).</summary>
    /// <param name="z">The argument, not a non-positive integer.</param>
    /// <param name="sign">Γ(z)'s sign, 1 or −1.</param>
    /// <returns>ln |Γ(z)|.</returns>
    /// <exception cref="NanoQLException">z is a non-positive integer (a pole), or ln |Γ(z)| overflows.</exception>
    internal static double LGamma(double z, out int sign) =>
        CheckedNarrowing(LgammaImp(z, out sign), "boost::math::lgamma<double>(double)");

    /// <summary><c>tgamma1pm1(z)</c>: Γ(1 + z) − 1, accurate near z = 0.</summary>
    /// <param name="z">The argument.</param>
    /// <returns>Γ(1 + z) − 1.</returns>
    /// <exception cref="NanoQLException">Γ(1 + z) overflows or 1 + z is a pole.</exception>
    internal static double TGamma1pm1(double z) =>
        CheckedNarrowing(TGammaP1M1Imp(z), "boost::math::tgamma1pm1<%!%>(double)"); // Boost's name, as written

    /// <summary><c>gamma_p(a, x)</c>: the regularised lower incomplete gamma P(a, x).</summary>
    /// <param name="a">The shape, positive.</param>
    /// <param name="x">The argument, non-negative.</param>
    /// <returns>P(a, x).</returns>
    /// <exception cref="ArgumentOutOfRangeException">a isn't positive, or x is negative.</exception>
    /// <exception cref="NanoQLException">A series runs out of terms.</exception>
    internal static double GammaP(double a, double x)
    {
        double derivative = 0;
        return GammaIncompleteImp(a, x, true, false, false, ref derivative);
    }

    /// <summary><c>gamma_q(a, x)</c>: the regularised upper incomplete gamma Q(a, x).</summary>
    /// <param name="a">The shape, positive.</param>
    /// <param name="x">The argument, non-negative.</param>
    /// <returns>Q(a, x).</returns>
    /// <exception cref="ArgumentOutOfRangeException">a isn't positive, or x is negative.</exception>
    /// <exception cref="NanoQLException">A series runs out of terms.</exception>
    internal static double GammaQ(double a, double x)
    {
        double derivative = 0;
        return GammaIncompleteImp(a, x, true, true, false, ref derivative);
    }

    /// <summary><c>gamma_p_derivative(a, x)</c>: ∂P(a, x)/∂x.</summary>
    /// <param name="a">The shape, positive.</param>
    /// <param name="x">The argument, non-negative.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="ArgumentOutOfRangeException">a isn't positive, or x is negative.</exception>
    /// <exception cref="NanoQLException">The derivative is infinite: at x = 0 for a &lt; 1, or past the largest
    /// double.</exception>
    internal static double GammaPDerivative(double a, double x)
    {
        const string function = "boost::math::gamma_p_derivative<long double>(long double, long double)";

        //
        // Usual error checks first:
        //
        if (a <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(a),
                $"Error in function {function}: Argument a to the incomplete gamma function must be greater than zero "
                + $"(got a={Show(a)}).");
        }

        if (x < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Error in function {function}: Argument x to the incomplete gamma function must be >= 0 (got x={Show(x)}).");
        }

        //
        // Now special cases:
        //
        if (x == 0)
        {
            return (a > 1) ? 0 : (a == 1) ? 1 : throw new NanoQLException($"Error in function {function}: Overflow Error");
        }

        //
        // Normal case:
        //
        double f1 = RegularisedGammaPrefix(a, x);
        if (f1 == 0)
        {
            // Underflow in calculation, use logs instead:
            f1 = a * Math.Log(x) - x - LGamma(a) - Math.Log(x);
            f1 = Math.Exp(f1);
        }
        else
        {
            f1 /= x;
        }

        return CheckedNarrowing(f1, "boost::math::gamma_p_derivative<double>(double, double)");
    }

    /// <summary>
    /// <c>detail::gamma_incomplete_imp</c>: P(a, x), Q(a, x) or their non-normalised forms, and with
    /// <paramref name="wantDerivative"/> the derivative ∂P/∂x, as Boost's pointer argument returns it.
    /// </summary>
    /// <param name="a">The shape, positive.</param>
    /// <param name="x">The argument, non-negative.</param>
    /// <param name="normalised">Whether the regularised form.</param>
    /// <param name="invert">Whether the upper function.</param>
    /// <param name="wantDerivative">Whether the derivative is asked for.</param>
    /// <param name="derivative">The derivative, when asked for.</param>
    /// <returns>The function.</returns>
    /// <exception cref="ArgumentOutOfRangeException">a isn't positive, or x is negative.</exception>
    /// <exception cref="NanoQLException">A series runs out of terms, or the non-normalised value overflows.</exception>
    internal static double GammaIncompleteImp(
        double a, double x, bool normalised, bool invert, bool wantDerivative, ref double derivative)
    {
        const string name = "boost::math::gamma_p<long double>(long double, long double)";
        if (a <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(a),
                $"Error in function {name}: Argument a to the incomplete gamma function must be greater than zero "
                + $"(got a={Show(a)}).");
        }

        if (x < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Error in function {name}: Argument x to the incomplete gamma function must be >= 0 (got x={Show(x)}).");
        }

        double result;

        if (x > 0 && a >= UncheckedFactorialFunctions.MaxFactorial && !normalised)
        {
            //
            // When we're computing the non-normalized incomplete gamma
            // and a is large the result is rather hard to compute unless
            // we use logs.  There are really two options - if x is a long
            // way from a in value then we can reliably use methods 2 and 4
            // below in logarithmic form and go straight to the result.
            // Otherwise we let the regularized gamma take the strain
            // (the result is unlikely to underflow in the central region anyway)
            // and combine with lgamma in the hopes that we get a finite result.
            //
            if (invert && (a * 4 < x))
            {
                // This is method 4 below, done in logs:
                result = a * Math.Log(x) - x;
                result += Math.Log(UpperGammaFraction(a, x, Epsilon));
            }
            else if (!invert && (a > 4 * x))
            {
                // This is method 2 below, done in logs:
                result = a * Math.Log(x) - x;
                result += Math.Log(LowerGammaSeries(a, x, 0) / a);
            }
            else
            {
                result = GammaIncompleteImpFinal(a, x, true, invert, wantDerivative, ref derivative);
                if (result == 0)
                {
                    if (invert)
                    {
                        // Try http://functions.wolfram.com/06.06.06.0039.01
                        result = 1 + 1 / (12 * a) + 1 / (288 * a * a);
                        result = Math.Log(result) - a + (a - 0.5) * Math.Log(a) + Math.Log(RootTwoPi);
                    }
                    else
                    {
                        // This is method 2 below, done in logs, we're really outside the
                        // range of this method, but since the result is almost certainly
                        // infinite, we should probably be OK:
                        result = a * Math.Log(x) - x;
                        result += Math.Log(LowerGammaSeries(a, x, 0) / a);
                    }
                }
                else
                {
                    result = Math.Log(result) + LGamma(a);
                }
            }

            if (result > LogMaxValue)
            {
                throw new NanoQLException($"Error in function {name}: Overflow Error");
            }

            return Math.Exp(result);
        }

        // If no special handling is required then we proceeds as normal
        return GammaIncompleteImpFinal(a, x, normalised, invert, wantDerivative, ref derivative);
    }

    /// <summary>
    /// <c>detail::regularised_gamma_prefix</c>: x^a·e^(−x)/Γ(a), where most of the incomplete gamma's error arises.
    /// </summary>
    /// <param name="a">The shape.</param>
    /// <param name="z">The argument.</param>
    /// <returns>The prefix.</returns>
    internal static double RegularisedGammaPrefix(double a, double z)
    {
        if (z >= MaxValue || (a > 0 && z == 0))
        {
            return 0;
        }

        double agh = a + Lanczos13m53.G - 0.5;
        double prefix;
        double d = ((z - a) - Lanczos13m53.G + 0.5) / agh;

        if (a < 1)
        {
            //
            // We have to treat a < 1 as a special case because our Lanczos
            // approximations are optimised against the factorials with a > 1,
            // and for high precision types especially (128-bit reals for example)
            // very small values of a can give rather erroneous results for gamma
            // unless we do this:
            //
            // TODO: is this still required?  Lanczos approx should be better now?
            //
            if ((z <= LogMinValue) || (a < 1 / MaxValue))
            {
                // Oh dear, have to use logs, should be free of cancellation errors though:
                return Math.Exp(a * Math.Log(z) - z - LgammaImp(a, out _));
            }
            else
            {
                // direct calculation, no danger of overflow as gamma(a) < 1/a
                // for small a.
                return Math.Pow(z, a) * Math.Exp(-z) / GammaImp(a);
            }
        }
        else if ((Math.Abs(d * d * a) <= 100) && (a > 150))
        {
            // special case for large a and a ~ z.
            prefix = a * Log1pFunctions.Log1pmx(d) + z * (0.5 - Lanczos13m53.G) / agh;
            prefix = Math.Exp(prefix);
        }
        else
        {
            //
            // general case.
            // direct computation is most accurate, but use various fallbacks
            // for different parts of the problem domain:
            //
            double alz = a * Math.Log(z / agh);
            double amz = a - z;
            if ((Min(alz, amz) <= LogMinValue) || (Max(alz, amz) >= LogMaxValue))
            {
                double amza = amz / a;
                if ((Min(alz, amz) / 2 > LogMinValue) && (Max(alz, amz) / 2 < LogMaxValue))
                {
                    // compute square root of the result and then square it:
                    double sq = Math.Pow(z / agh, a / 2) * Math.Exp(amz / 2);
                    prefix = sq * sq;
                }
                else if ((Min(alz, amz) / 4 > LogMinValue) && (Max(alz, amz) / 4 < LogMaxValue) && (z > a))
                {
                    // compute the 4th root of the result then square it twice:
                    double sq = Math.Pow(z / agh, a / 4) * Math.Exp(amz / 4);
                    prefix = sq * sq;
                    prefix *= prefix;
                }
                else if ((amza > LogMinValue) && (amza < LogMaxValue))
                {
                    prefix = Math.Pow((z * Math.Exp(amza)) / agh, a);
                }
                else
                {
                    prefix = Math.Exp(alz + amz);
                }
            }
            else
            {
                prefix = Math.Pow(z / agh, a) * Math.Exp(amz);
            }
        }

        prefix *= Math.Sqrt(agh / Math.E) / Lanczos13m53.LanczosSumExpGScaled(a);
        return prefix;
    }

    /// <summary><c>detail::lower_gamma_series</c>: the series of the lower incomplete gamma, to be scaled.</summary>
    /// <param name="a">The shape.</param>
    /// <param name="z">The argument.</param>
    /// <param name="initValue">The sum's initial value.</param>
    /// <returns>The series' sum.</returns>
    /// <exception cref="NanoQLException">The series runs out of terms.</exception>
    internal static double LowerGammaSeries(double a, double z, double initValue)
    {
        // Multiply result by ((z^a) * (e^-z) / a) to get the full
        // lower incomplete integral. Then divide by tgamma(a)
        // to get the normalised value.
        var s = new LowerIncompleteGammaSeries(a, z);
        ulong maxIter = SeriesFunctions.MaxSeriesIterations;
        double result = SeriesFunctions.SumSeries(ref s, Epsilon, ref maxIter, initValue);
        SeriesFunctions.CheckSeriesIterations("boost::math::detail::lower_gamma_series<long double>(long double)", maxIter);
        return result;
    }

    /// <summary><c>detail::upper_gamma_fraction</c>: the continued fraction of the upper incomplete gamma.</summary>
    /// <param name="a">The shape.</param>
    /// <param name="z">The argument.</param>
    /// <param name="eps">The fraction's accuracy.</param>
    /// <returns>The fraction.</returns>
    internal static double UpperGammaFraction(double a, double z, double eps)
    {
        // Multiply result by z^a * e^-z to get the full
        // upper incomplete integral.  Divide by tgamma(z)
        // to normalise.
        var f = new UpperIncompleteGammaFract(a, z);
        return 1 / (z - a + 1 + FractionFunctions.ContinuedFractionA(ref f, eps));
    }

    /// <summary><c>detail::full_igamma_prefix</c>: x^a·e^(−x), the non-normalised functions' prefix.</summary>
    /// <param name="a">The shape.</param>
    /// <param name="z">The argument.</param>
    /// <returns>The prefix.</returns>
    /// <exception cref="NanoQLException">The prefix overflows.</exception>
    internal static double FullIgammaPrefix(double a, double z)
    {
        if (z > MaxValue || (a > 0 && z == 0))
        {
            return 0;
        }

        double alz = a * Math.Log(z);

        double prefix;

        if (z >= 1)
        {
            if ((alz < LogMaxValue) && (-z > LogMinValue))
            {
                prefix = Math.Pow(z, a) * Math.Exp(-z);
            }
            else if (a >= 1)
            {
                prefix = Math.Pow(z / Math.Exp(z / a), a);
            }
            else
            {
                prefix = Math.Exp(alz - z); // LCOV_EXCL_LINE defensive programming, can probably never get here?
            }
        }
        else
        {
            if (alz > LogMinValue)
            {
                prefix = Math.Pow(z, a) * Math.Exp(-z);
            }

            // Defensive programming, can probably never get here, very hard to prove though!
            else if (z / a < LogMaxValue)
            {
                prefix = Math.Pow(z / Math.Exp(z / a), a);
            }
            else
            {
                prefix = Math.Exp(alz - z);
            }
        }

        //
        // This error handling isn't very good: it happens after the fact
        // rather than before it...
        // Typically though this method is used when the result is small, we should probably not overflow here...
        //
        if (double.IsInfinity(prefix))
        {
            throw new NanoQLException(
                "Error in function boost::math::detail::full_igamma_prefix<long double>(long double, long double): Result "
                + "of incomplete gamma function is too large to represent.");
        }

        return prefix;
    }

    /// <summary><c>tgamma_delta_ratio(z, delta)</c>: the ratio Γ(z)/Γ(z + δ).</summary>
    /// <param name="z">The first argument.</param>
    /// <param name="delta">The increment.</param>
    /// <returns>Γ(z)/Γ(z + δ).</returns>
    /// <exception cref="NanoQLException">A gamma function on the way has a pole or overflows, or the ratio
    /// does.</exception>
    internal static double TGammaDeltaRatio(double z, double delta) =>
        CheckedNarrowing(TgammaDeltaRatioImp(z, delta), "boost::math::tgamma_delta_ratio<double>(double, double)");

    // tgamma_delta_ratio_imp_lanczos_final(z, delta, pol, lanczos13m53)
    private static double TgammaDeltaRatioImpLanczosFinal(double z, double delta)
    {
        double zgh = z + Lanczos13m53.G - 0.5;
        double result;
        if (z + delta == z)
        {
            // Given delta < z * eps
            // and zgh > z
            // Then this must follow:
            // We have:
            // result = exp((constants::half<T>() - z) * boost::math::log1p(delta / zgh, pol));
            // 0.5 - z == -z
            // log1p(delta / zgh) = delta / zgh = delta / z
            // multiplying we get -delta.
            result = Math.Exp(-delta);
        }
        else
        {
            if (Math.Abs(delta) < 10)
            {
                result = Math.Exp((0.5 - z) * Log1pFunctions.Log1p(delta / zgh));
            }
            else
            {
                result = Math.Pow(zgh / (zgh + delta), z - 0.5);
            }

            // Split the calculation up to avoid spurious overflow:
            result *= Lanczos13m53.LanczosSum(z) / Lanczos13m53.LanczosSum(z + delta);
        }

        result *= Math.Pow(2.718281828459045235360287471352662497757 / (zgh + delta), delta);
        return result;
    }

    // tgamma_delta_ratio_imp_lanczos(z, delta, pol, lanczos13m53)
    private static double TgammaDeltaRatioImpLanczos(double z, double delta)
    {
        if (z < Epsilon)
        {
            //
            // We get spurious numeric overflow unless we're very careful, this
            // can occur either inside Lanczos::lanczos_sum(z) or in the
            // final combination of terms, to avoid this, split the product up
            // into 2 (or 3) parts:
            //
            // G(z) / G(L) = 1 / (z * G(L)) ; z < eps, L = z + delta = delta
            //    z * G(L) = z * G(lim) * (G(L)/G(lim)) ; lim = largest factorial
            //
            if (UncheckedFactorialFunctions.MaxFactorial < delta)
            {
                double ratio = TgammaDeltaRatioImpLanczosFinal(delta, UncheckedFactorialFunctions.MaxFactorial - delta);
                ratio *= z;
                ratio *= UncheckedFactorialFunctions.UncheckedFactorial(UncheckedFactorialFunctions.MaxFactorial - 1);
                return 1 / ratio;
            }
            else
            {
                return 1 / (z * TGamma(z + delta));
            }
        }

        return TgammaDeltaRatioImpLanczosFinal(z, delta);
    }

    // tgamma_delta_ratio_imp(z, delta, pol)
    private static double TgammaDeltaRatioImp(double z, double delta)
    {
        if ((z <= 0) || (z + delta <= 0))
        {
            // This isn't very sophisticated, or accurate, but it does work:
            return TGamma(z) / TGamma(z + delta);
        }

        if (Math.Floor(delta) == delta)
        {
            if (Math.Floor(z) == z)
            {
                //
                // Both z and delta are integers, see if we can just use table lookup
                // of the factorials to get the result:
                //
                if ((z <= UncheckedFactorialFunctions.MaxFactorial) && (z + delta <= UncheckedFactorialFunctions.MaxFactorial))
                {
                    return UncheckedFactorialFunctions.UncheckedFactorial((int)((uint)TruncFunctions.ITrunc(z) - 1))
                        / UncheckedFactorialFunctions.UncheckedFactorial((int)((uint)TruncFunctions.ITrunc(z + delta) - 1));
                }
            }

            if (Math.Abs(delta) < 20)
            {
                //
                // delta is a small integer, we can use a finite product:
                //
                if (delta == 0)
                {
                    return 1;
                }

                if (delta < 0)
                {
                    z -= 1;
                    double result = z;
                    while ((delta += 1) != 0)
                    {
                        z -= 1;
                        result *= z;
                    }

                    return result;
                }
                else
                {
                    double result = 1 / z;
                    while ((delta -= 1) != 0)
                    {
                        z += 1;
                        result /= z;
                    }

                    return result;
                }
            }
        }

        return TgammaDeltaRatioImpLanczos(z, delta);
    }

    // tgamma(z), with Lanczos support
    private static double GammaImpFinal(double z)
    {
        const string function = "boost::math::tgamma<long double>(long double)";

        double result = 1;

        if (z <= 0)
        {
            // shift z to > 1:
            while (z < 0)
            {
                result /= z;
                z += 1;
            }
        }

        if ((Math.Floor(z) == z) && (z < UncheckedFactorialFunctions.MaxFactorial))
        {
            result *= UncheckedFactorialFunctions.UncheckedFactorial((int)z - 1);
        }
        else if (z < RootEpsilon)
        {
            if (z < 1 / MaxValue)
            {
                throw new NanoQLException($"Error in function {function}: Overflow Error");
            }

            result *= 1 / z - Euler;
        }
        else
        {
            result *= Lanczos13m53.LanczosSum(z);
            double zgh = z + Lanczos13m53.G - 0.5;
            double lzgh = Math.Log(zgh);
            if (z * lzgh > LogMaxValue)
            {
                // we're going to overflow unless this is done with care:
                if (lzgh * z / 2 > LogMaxValue)
                {
                    throw new NanoQLException($"Error in function {function}: Result of tgamma is too large to represent.");
                }

                double hp = Math.Pow(zgh, (z / 2) - 0.25);
                result *= hp / Math.Exp(zgh);
                if (MaxValue / hp < result)
                {
                    throw new NanoQLException($"Error in function {function}: Result of tgamma is too large to represent.");
                }

                result *= hp;
            }
            else
            {
                result *= Math.Pow(zgh, z - 0.5) / Math.Exp(zgh);
            }
        }

        return result;
    }

    private static double GammaImp(double z)
    {
        const string function = "boost::math::tgamma<long double>(long double)";

        double result;

        if (z <= 0)
        {
            if (Math.Floor(z) == z)
            {
                throw new NanoQLException(
                    $"Error in function {function}: Evaluation of tgamma at a negative integer {Show(z)}.");
            }

            if (z <= -20)
            {
                try
                {
                    result = GammaImpFinal(-z) * SinPx(z);
                }
                catch (NanoQLException)
                {
                    // Boost's catch of std::overflow_error, the only error GammaImpFinal raises here; the underflow
                    // error that follows is ignored by the default policy, which returns 0
                    return 0;
                }

                // Result can never be small: tgamma[-z] is always larger than sinpx[z] is small.
                result = -Math.PI / result;
                if (result == 0)
                {
                    return 0;
                }

                return result;
            }
        }

        return GammaImpFinal(z);
    }

    private static double LgammaImpFinal(double z, out int sign)
    {
        const string function = "boost::math::lgamma<long double>(long double)";

        double result;
        int sresult = 1;

        if (z < RootEpsilon)
        {
            if (z == 0)
            {
                throw new NanoQLException($"Error in function {function}: Evaluation of lgamma at {Show(z)}.");
            }

            if (4 * Math.Abs(z) < Epsilon)
            {
                result = -Math.Log(Math.Abs(z));
            }
            else
            {
                result = Math.Log(Math.Abs(1 / z - Euler));
            }

            if (z < 0)
            {
                sresult = -1;
            }
        }
        else if (z < 15)
        {
            result = LgammaSmallFunctions.LgammaSmallImp(z, z - 1, z - 2);
        }
        else if ((z >= 3) && (z < 100))
        {
            // taking the log of tgamma reduces the error, no danger of overflow here:
            result = Math.Log(GammaImp(z));
        }
        else
        {
            // regular evaluation:
            double zgh = z + Lanczos13m53.G - 0.5;
            result = Math.Log(zgh) - 1;
            result *= z - (double)0.5f;

            //
            // Only add on the lanczos sum part if we're going to need it:
            //
            if (result * Epsilon < 20)
            {
                result += Math.Log(Lanczos13m53.LanczosSumExpGScaled(z));
            }
        }

        sign = sresult;
        return result;
    }

    private static double LgammaImp(double z, out int sign)
    {
        if (z <= -RootEpsilon)
        {
            const string function = "boost::math::lgamma<long double>(long double)";

            int sresult = 1;

            // reflection formula:
            if (Math.Floor(z) == z)
            {
                throw new NanoQLException(
                    $"Error in function {function}: Evaluation of lgamma at a negative integer {Show(z)}.");
            }

            double t = SinPx(z);
            z = -z;
            if (t < 0)
            {
                t = -t;
            }
            else
            {
                sresult = -sresult;
            }

            double result = Math.Log(Math.PI) - LgammaImpFinal(z, out _) - Math.Log(t);

            sign = sresult;

            return result;
        }
        else
        {
            return LgammaImpFinal(z, out sign);
        }
    }

    // This helper calculates tgamma(dz+1)-1 without cancellation errors,
    // used by the upper incomplete gamma with z < 1:
    private static double TGammaP1M1Imp(double dz)
    {
        double result;
        if (dz < 0)
        {
            if (dz < -0.5)
            {
                // Best method is simply to subtract 1 from tgamma:
                result = TGamma(1 + dz) - 1;
            }
            else
            {
                // Use expm1 on lgamma:
                result = Expm1Functions.StdExpm1(
                    -Log1pFunctions.Log1p(dz) + LgammaSmallFunctions.LgammaSmallImp(dz + 2, dz + 1, dz));
            }
        }
        else
        {
            if (dz < 2)
            {
                // Use expm1 on lgamma:
                result = Expm1Functions.StdExpm1(LgammaSmallFunctions.LgammaSmallImp(dz + 1, dz, dz - 1));
            }
            else
            {
                // Best method is simply to subtract 1 from tgamma:
                result = TGamma(1 + dz) - 1;
            }
        }

        return result;
    }

    // Ad hoc function calculates x * sin(pi * x),
    // taking extra care near when x is near a whole number.
    private static double SinPx(double z)
    {
        int sign = 1;
        if (z < 0)
        {
            z = -z;
        }

        double fl = Math.Floor(z);
        double dist;
        if (IsOdd(fl))
        {
            fl += 1;
            dist = fl - z;
            sign = -sign;
        }
        else
        {
            dist = z - fl;
        }

        if (dist > 0.5)
        {
            dist = 1 - dist;
        }

        double result = Math.Sin(dist * Math.PI);
        return sign * z * result;
    }

    // is_odd, for a type convertible to int: the int's low bit
    private static bool IsOdd(double v)
    {
        int i = (int)v;
        return (i & 1) != 0;
    }

    // Compute the full upper fraction (Q) when a is very small:
    private static double TGammaSmallUpperPart(
        double a, double x, out double pgam, bool invert, bool wantDerivative, ref double pderivative)
    {
        double result = TGamma1pm1(a);

        pgam = (result + 1) / a;
        double p = Powm1Functions.Powm1(x, a);
        result -= p;
        result /= a;
        var s = new SmallGamma2Series(a, x);
        ulong maxIter = SeriesFunctions.MaxSeriesIterations - 10;
        p += 1;
        if (wantDerivative)
        {
            pderivative = p / (pgam * Math.Exp(x));
        }

        double initValue = invert ? pgam : 0;
        result = -p * SeriesFunctions.SumSeries(ref s, Epsilon, ref maxIter, (initValue - result) / p);
        SeriesFunctions.CheckSeriesIterations(
            "boost::math::tgamma_small_upper_part<long double>(long double, long double)", maxIter);
        if (invert)
        {
            result = -result;
        }

        return result;
    }

    // Calculates normalised Q when a is an integer:
    private static double FiniteGammaQ(double a, double x, bool wantDerivative, ref double pderivative)
    {
        double e = Math.Exp(-x);
        double sum = e;
        if (sum != 0)
        {
            double term = sum;
            for (uint n = 1; n < a; ++n)
            {
                term /= n;
                term *= x;
                sum += term;
            }
        }

        if (wantDerivative)
        {
            pderivative = e * Math.Pow(x, a) / UncheckedFactorialFunctions.UncheckedFactorial((int)(a - 1));
        }

        return sum;
    }

    // Calculates normalised Q when a is a half-integer:
    private static double FiniteHalfGammaQ(double a, double x, bool wantDerivative, ref double pDerivative)
    {
        double e = ErfFunctions.Erfc(Math.Sqrt(x));
        if ((e != 0) && (a > 1))
        {
            double term = Math.Exp(-x) / Math.Sqrt(Math.PI * x);
            term *= x;
            const double half = 1.0 / 2;
            term /= half;
            double sum = term;
            for (uint n = 2; n < a; ++n)
            {
                term /= n - half;
                term *= x;
                sum += term;
            }

            e += sum;
            if (wantDerivative)
            {
                pDerivative = 0;
            }
        }
        else if (wantDerivative)
        {
            // We'll be dividing by x later, so calculate derivative * x:
            pDerivative = Math.Sqrt(x) * Math.Exp(-x) / RootPi;
        }

        return e;
    }

    // Asymptotic approximation for large argument, see: https://dlmf.nist.gov/8.11#E2
    private static double IncompleteTgammaLargeX(double a, double x)
    {
        var s = new IncompleteTgammaLargeXSeries(a, x);
        ulong maxIter = SeriesFunctions.MaxSeriesIterations;
        double result = SeriesFunctions.SumSeries(ref s, Epsilon, ref maxIter, 0.0);
        SeriesFunctions.CheckSeriesIterations("boost::math::tgamma<long double>(long double,long double)", maxIter);
        return result;
    }

    // Main incomplete gamma entry point, handles all four incomplete gamma's:
    private static double GammaIncompleteImpFinal(
        double a, double x, bool normalised, bool invert, bool wantDerivative, ref double pDerivative)
    {
        double result = 0;

        bool isInt, isHalfInt;
        bool isSmallA = (a < 30) && (a <= x + 1) && (x < LogMaxValue);
        if (isSmallA)
        {
            double fa = Math.Floor(a);
            isInt = fa == a;
            isHalfInt = !isInt && (Math.Abs(fa - a) == (double)0.5f);
        }
        else
        {
            isInt = isHalfInt = false;
        }

        int evalMethod;

        if (x == 0)
        {
            evalMethod = 2;
        }
        else if (isInt && (x > 0.6))
        {
            // calculate Q via finite sum:
            invert = !invert;
            evalMethod = 0;
        }
        else if (isHalfInt && (x > 0.2))
        {
            // calculate Q via finite sum for half integer a:
            invert = !invert;
            evalMethod = 1;
        }
        else if ((x < RootEpsilon) && (a > 1))
        {
            evalMethod = 6;
        }
        else if ((x > 1000) && ((a < x) || (Math.Abs(a - 50) / x < 1)))
        {
            // calculate Q via asymptotic approximation:
            invert = !invert;
            evalMethod = 7;
        }
        else if (x < 0.5)
        {
            //
            // Changeover criterion chosen to give a changeover at Q ~ 0.33
            //
            if (-0.4 / Math.Log(x) < a)
            {
                evalMethod = 2;
            }
            else
            {
                evalMethod = 3;
            }
        }
        else if (x < 1.1)
        {
            //
            // Changeover here occurs when P ~ 0.75 or Q ~ 0.25:
            //
            if (x * (double)0.75f < a)
            {
                evalMethod = 2;
            }
            else
            {
                evalMethod = 3;
            }
        }
        else
        {
            //
            // Begin by testing whether we're in the "bad" zone
            // where the result will be near 0.5 and the usual
            // series and continued fractions are slow to converge:
            //
            bool useTemme = false;
            if (normalised && (a > 20))
            {
                double sigma = Math.Abs((x - a) / a);
                if (a > 200)
                {
                    //
                    // This limit is chosen so that we use Temme's expansion
                    // only if the result would be larger than about 10^-6.
                    // Below that the regular series and continued fractions
                    // converge OK, and if we use Temme's method we get increasing
                    // errors from the dominant erfc term as it's (inexact) argument
                    // increases in magnitude.
                    //
                    if (20 / a > sigma * sigma)
                    {
                        useTemme = true;
                    }
                }
                else
                {
                    // Note in this zone we can't use Temme's expansion for
                    // types longer than an 80-bit real:
                    // it would require too many terms in the polynomials.
                    if (sigma < 0.4)
                    {
                        useTemme = true;
                    }
                }
            }

            if (useTemme)
            {
                evalMethod = 5;
            }
            else
            {
                //
                // Regular case where the result will not be too close to 0.5.
                //
                // Changeover here occurs at P ~ Q ~ 0.5
                // Note that series computation of P is about x2 faster than continued fraction
                // calculation of Q, so try and use the CF only when really necessary, especially
                // for small x.
                //
                if (x - (1 / (3 * x)) < a)
                {
                    evalMethod = 2;
                }
                else
                {
                    evalMethod = 4;
                    invert = !invert;
                }
            }
        }

        switch (evalMethod)
        {
            case 0:
                {
                    result = FiniteGammaQ(a, x, wantDerivative, ref pDerivative);
                    if (!normalised)
                    {
                        result *= TGamma(a);
                    }

                    break;
                }

            case 1:
                {
                    result = FiniteHalfGammaQ(a, x, wantDerivative, ref pDerivative);
                    if (!normalised)
                    {
                        result *= TGamma(a);
                    }

                    if (wantDerivative && (pDerivative == 0))
                    {
                        pDerivative = RegularisedGammaPrefix(a, x);
                    }

                    break;
                }

            case 2:
                {
                    // Compute P:
                    result = normalised ? RegularisedGammaPrefix(a, x) : FullIgammaPrefix(a, x);
                    if (wantDerivative)
                    {
                        pDerivative = result;
                    }

                    if (result != 0)
                    {
                        //
                        // If we're going to be inverting the result then we can
                        // reduce the number of series evaluations by quite
                        // a few iterations if we set an initial value for the
                        // series sum based on what we'll end up subtracting it from
                        // at the end.
                        // Have to be careful though that this optimization doesn't
                        // lead to spurious numeric overflow.  Note that the
                        // scary/expensive overflow checks below are more often
                        // than not bypassed in practice for "sensible" input
                        // values:
                        //
                        double initValue = 0;
                        bool optimisedInvert = false;
                        if (invert)
                        {
                            initValue = normalised ? 1 : TGamma(a);

                            if (normalised || (result >= 1) || (MaxValue * result > initValue))
                            {
                                initValue /= result;
                                if (normalised || (a < 1) || (MaxValue / a > initValue))
                                {
                                    initValue *= -a;
                                    optimisedInvert = true;
                                }
                                else
                                {
                                    initValue = 0; // LCOV_EXCL_LINE  Unreachable for any "sensible" floating point type.
                                }
                            }
                            else
                            {
                                initValue = 0;
                            }
                        }

                        result *= LowerGammaSeries(a, x, initValue) / a;
                        if (optimisedInvert)
                        {
                            invert = false;
                            result = -result;
                        }
                    }

                    break;
                }

            case 3:
                {
                    // Compute Q:
                    invert = !invert;
                    result = TGammaSmallUpperPart(a, x, out double g, invert, wantDerivative, ref pDerivative);
                    invert = false;
                    if (normalised)
                    {
                        result /= g;
                    }

                    break;
                }

            case 4:
                {
                    // Compute Q:
                    result = normalised ? RegularisedGammaPrefix(a, x) : FullIgammaPrefix(a, x);
                    if (wantDerivative)
                    {
                        pDerivative = result;
                    }

                    if (result != 0)
                    {
                        result *= UpperGammaFraction(a, x, Epsilon);
                    }

                    break;
                }

            case 5:
                {
                    //
                    // Use compile time dispatch to the appropriate
                    // Temme asymptotic expansion.  This may be dead code
                    // if T does not have numeric limits support, or has
                    // too many digits for the most precise version of
                    // these expansions, in that case we'll be calling
                    // an empty function.
                    //
                    result = IgammaLargeFunctions.IgammaTemmeLarge(a, x);
                    if (x >= a)
                    {
                        invert = !invert;
                    }

                    if (wantDerivative)
                    {
                        pDerivative = RegularisedGammaPrefix(a, x);
                    }

                    break;
                }

            case 6:
                {
                    // x is so small that P is necessarily very small too,
                    // use http://functions.wolfram.com/GammaBetaErf/GammaRegularized/06/01/05/01/01/
                    if (!normalised)
                    {
                        result = Math.Pow(x, a) / a;
                    }
                    else
                    {
                        try
                        {
                            result = Math.Pow(x, a) / TGamma(a + 1);
                        }
                        catch (NanoQLException)
                        {
                            // Boost's catch of std::overflow_error, the only error tgamma(a + 1) raises here
                            result = 0;
                        }
                    }

                    result *= 1 - a * x / (a + 1);
                    if (wantDerivative)
                    {
                        pDerivative = RegularisedGammaPrefix(a, x);
                    }

                    break;
                }

            case 7:
                {
                    // x is large,
                    // Compute Q:
                    result = normalised ? RegularisedGammaPrefix(a, x) : FullIgammaPrefix(a, x);
                    if (wantDerivative)
                    {
                        pDerivative = result;
                    }

                    result /= x;
                    if (result != 0)
                    {
                        result *= IncompleteTgammaLargeX(a, x);
                    }

                    break;
                }
        }

        if (normalised && (result > 1))
        {
            result = 1;
        }

        if (invert)
        {
            double gam = normalised ? 1 : TGamma(a);
            result = gam - result;
        }

        if (wantDerivative)
        {
            if ((x == 0) || ((x < 1) && (MaxValue * x < pDerivative)))
            {
                // overflow, just return an arbitrarily large value:
                pDerivative = MaxValue / 2;
            }
            else
            {
                pDerivative /= x;
            }
        }

        return result;
    }

    // std::min's and std::max's semantics, as BOOST_MATH_GPU_SAFE_MIN and MAX are on the host
    private static double Min(double a, double b) => b < a ? b : a;

    // policies::checked_narrowing_cast to the caller's double from long double, MSVC's double: Boost's public entries
    // end in it, and only an infinite value overflows
    private static double CheckedNarrowing(double value, string function) =>
        Math.Abs(value) > MaxValue ? throw new NanoQLException($"Error in function {function}: Overflow Error") : value;

    private static double Max(double a, double b) => a < b ? b : a;

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);

    private struct UpperIncompleteGammaFract(double a1, double z1) : FractionFunctions.IGenerator
    {
        private readonly double a = a1;
        private double z = z1 - a1 + 1;
        private int k;

        public (double A, double B) Next()
        {
            ++k;
            z += 2;
            return (k * (a - k), z);
        }
    }

    private struct LowerIncompleteGammaSeries(double a1, double z1) : SeriesFunctions.ITerms
    {
        private readonly double z = z1;
        private double a = a1;
        private double result = 1;

        public double Next()
        {
            double r = result;
            a += 1;
            result *= z / a;
            return r;
        }
    }

    // Series representation for upper fraction when z is small:
    private struct SmallGamma2Series(double a0, double x0) : SeriesFunctions.ITerms
    {
        private readonly double x = -x0;
        private double result = -x0;
        private double apn = a0 + 1;
        private int n = 1;

        public double Next()
        {
            double r = result / apn;
            result *= x;
            result /= ++n;
            apn += 1;
            return r;
        }
    }

    private struct IncompleteTgammaLargeXSeries(double a0, double x0) : SeriesFunctions.ITerms
    {
        private readonly double z = x0;
        private double aPoch = a0 - 1;
        private double term = 1;

        public double Next()
        {
            double result = term;
            term *= aPoch / z;
            aPoch -= 1;
            return result;
        }
    }
}
