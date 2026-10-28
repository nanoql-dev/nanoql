// SPDX-FileCopyrightText: (C) Copyright John Maddock 2006.
// SPDX-FileCopyrightText: (C) Copyright Matt Borland 2024.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/beta.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's beta functions (<c>beta.hpp</c>) for <c>double</c> under the default policy: the beta function
/// <c>beta(a, b)</c>, the regularised incomplete beta <c>ibeta</c> and its complement <c>ibetac</c>, and
/// <c>ibeta_derivative</c>, on Boost's Lanczos approximation <see cref="Lanczos13m53"/>. Boost promotes <c>double</c>
/// to <c>long double</c>, which under MSVC is <c>double</c>, so its errors name the <c>long double</c> instantiation.
/// Boost's non-normalised incomplete beta (<c>beta(a, b, x)</c>, <c>betac</c>) isn't ported: no QuantLib code calls
/// it, so the implementation takes the normalised branches only. A domain error on an argument throws
/// <see cref="ArgumentOutOfRangeException"/>; an overflow and an evaluation error throw <see cref="NanoQLException"/>;
/// each keeps Boost's message. Boost's underflow, which its default policy ignores, returns 0.
/// </summary>
internal static class IncompleteBetaFunctions
{
    private const double Epsilon = 2.220446049250313E-16;
    private const double MinValue = 2.2250738585072014E-308;
    private const double MaxValue = double.MaxValue;
    private const double LogMaxValue = 709.0;
    private const double LogMinValue = -708.0;
    private const double E = 2.718281828459045235360287471352662497757;

    // Pn_size<long double>, the evaluation type's, under MSVC a double
    private const int PnSize = 50;

    /// <summary><c>beta(a, b)</c>: the beta function, Γ(a)Γ(b)/Γ(a + b).</summary>
    /// <param name="a">The first argument, positive.</param>
    /// <param name="b">The second argument, positive.</param>
    /// <returns>B(a, b).</returns>
    /// <exception cref="ArgumentOutOfRangeException">An argument isn't positive.</exception>
    /// <exception cref="NanoQLException">B(a, b) overflows.</exception>
    internal static double Beta(double a, double b) => CheckedNarrowing(BetaImp(a, b), "boost::math::beta<double>(double,double)");

    /// <summary><c>ibeta(a, b, x)</c>: the regularised incomplete beta I_x(a, b).</summary>
    /// <param name="a">The first parameter, non-negative and finite.</param>
    /// <param name="b">The second parameter, non-negative and finite, not both 0.</param>
    /// <param name="x">The argument, in [0, 1].</param>
    /// <returns>I_x(a, b).</returns>
    /// <exception cref="ArgumentOutOfRangeException">An argument is outside its domain.</exception>
    /// <exception cref="NanoQLException">A series runs out of terms.</exception>
    internal static double Ibeta(double a, double b, double x)
    {
        double derivative = 0;
        return CheckedNarrowing(IbetaImp(a, b, x, false, false, ref derivative), "boost::math::ibeta<double>(double,double,double)");
    }

    /// <summary><c>ibetac(a, b, x)</c>: the complement of the regularised incomplete beta, 1 − I_x(a, b).</summary>
    /// <param name="a">The first parameter, non-negative and finite.</param>
    /// <param name="b">The second parameter, non-negative and finite, not both 0.</param>
    /// <param name="x">The argument, in [0, 1].</param>
    /// <returns>1 − I_x(a, b).</returns>
    /// <exception cref="ArgumentOutOfRangeException">An argument is outside its domain.</exception>
    /// <exception cref="NanoQLException">A series runs out of terms.</exception>
    internal static double Ibetac(double a, double b, double x)
    {
        double derivative = 0;
        return CheckedNarrowing(IbetaImp(a, b, x, true, false, ref derivative), "boost::math::ibetac<double>(double,double,double)");
    }

    /// <summary><c>ibeta_derivative(a, b, x)</c>: ∂I_x(a, b)/∂x.</summary>
    /// <param name="a">The first parameter, positive and finite.</param>
    /// <param name="b">The second parameter, positive and finite.</param>
    /// <param name="x">The argument, in [0, 1].</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An argument is outside its domain.</exception>
    /// <exception cref="NanoQLException">The derivative is infinite.</exception>
    internal static double IbetaDerivative(double a, double b, double x) =>
        CheckedNarrowing(IbetaDerivativeImp(a, b, x), "boost::math::ibeta_derivative<double>(double,double,double)");

    /// <summary>
    /// <c>ibeta_imp(a, b, x, pol, inv, true, p_derivative)</c>: the regularised incomplete beta, or its complement
    /// where <paramref name="inv"/> is set, and its derivative's numerator where asked, as the Student quantile reads
    /// them.
    /// </summary>
    /// <param name="a">The first parameter.</param>
    /// <param name="b">The second parameter.</param>
    /// <param name="x">The argument.</param>
    /// <param name="inv">Whether to return the complement.</param>
    /// <param name="wantDerivative">Whether to compute the derivative.</param>
    /// <param name="derivative">The derivative, where asked: Boost's <c>*p_derivative</c>.</param>
    /// <returns>The function's value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An argument is outside its domain.</exception>
    /// <exception cref="NanoQLException">A series runs out of terms.</exception>
    internal static double IbetaImp(double a, double b, double x, bool inv, bool wantDerivative, ref double derivative)
    {
        const string function = "boost::math::ibeta<long double>(long double, long double, long double)";

        bool invert = inv;
        double fract;
        double y = 1 - x;

        if (!double.IsFinite(a))
        {
            throw Domain(nameof(a), function, $"The argument a to the incomplete beta function must be finite (got a={Show(a)}).");
        }

        if (!double.IsFinite(b))
        {
            throw Domain(nameof(b), function, $"The argument b to the incomplete beta function must be finite (got b={Show(b)}).");
        }

        if (!(0 <= x && x <= 1))
        {
            throw Domain(nameof(x), function, $"The argument x to the incomplete beta function must be in [0,1] (got x={Show(x)}).");
        }

        if (wantDerivative)
        {
            // value not set.
            derivative = -1;
        }

        if (a < 0)
        {
            throw Domain(nameof(a), function, $"The argument a to the incomplete beta function must be >= zero (got a={Show(a)}).");
        }

        if (b < 0)
        {
            throw Domain(nameof(b), function, $"The argument b to the incomplete beta function must be >= zero (got b={Show(b)}).");
        }

        // extend to a few very special cases:
        if (a == 0)
        {
            if (b == 0)
            {
                throw Domain(
                    nameof(b), function, $"The arguments a and b to the incomplete beta function cannot both be zero, with x={Show(x)}.");
            }

            if (b > 0)
            {
                return inv ? 0 : 1;
            }
        }
        else if (b == 0)
        {
            if (a > 0)
            {
                return inv ? 1 : 0;
            }
        }

        if (x == 0)
        {
            if (wantDerivative)
            {
                derivative = (a == 1) ? 1 : (a < 1) ? MaxValue / 2 : MinValue * 2;
            }

            return invert ? 1 : 0;
        }

        if (x == 1)
        {
            if (wantDerivative)
            {
                derivative = (b == 1) ? 1 : (b < 1) ? MaxValue / 2 : MinValue * 2;
            }

            return !invert ? 1 : 0;
        }

        if ((a == 0.5) && (b == 0.5))
        {
            // We have an arcsine distribution:
            if (wantDerivative)
            {
                derivative = 1 / (Math.PI * Math.Sqrt(y * x));
            }

            return invert ? Math.Asin(Math.Sqrt(y)) / (Math.PI / 2) : Math.Asin(Math.Sqrt(x)) / (Math.PI / 2);
        }

        if (a == 1)
        {
            (a, b) = (b, a);
            (x, y) = (y, x);
            invert = !invert;
        }

        if (b == 1)
        {
            //
            // Special case see: http://functions.wolfram.com/GammaBetaErf/BetaRegularized/03/01/01/
            //
            if (a == 1)
            {
                if (wantDerivative)
                {
                    derivative = 1;
                }

                return invert ? y : x;
            }

            if (wantDerivative)
            {
                derivative = a * Math.Pow(x, a - 1);
            }

            if (y < 0.5)
            {
                return invert ? -Expm1(a * Log1pFunctions.Log1p(-y)) : Math.Exp(a * Log1pFunctions.Log1p(-y));
            }

            return invert ? -Powm1Functions.Powm1(x, a) : Math.Pow(x, a);
        }

        if (Min(a, b) <= 1)
        {
            if (x > 0.5)
            {
                (a, b) = (b, a);
                (x, y) = (y, x);
                invert = !invert;
            }

            if (Max(a, b) <= 1)
            {
                // Both a,b < 1:
                if ((a >= Min(0.2, b)) || (Math.Pow(x, a) <= 0.9))
                {
                    if (!invert)
                    {
                        fract = IbetaSeries(a, b, x, 0, wantDerivative, ref derivative, y);
                    }
                    else
                    {
                        fract = -1;
                        invert = false;
                        fract = -IbetaSeries(a, b, x, fract, wantDerivative, ref derivative, y);
                    }
                }
                else
                {
                    (a, b) = (b, a);
                    (x, y) = (y, x);
                    invert = !invert;
                    if (y >= 0.3)
                    {
                        if (!invert)
                        {
                            fract = IbetaSeries(a, b, x, 0, wantDerivative, ref derivative, y);
                        }
                        else
                        {
                            fract = -1;
                            invert = false;
                            fract = -IbetaSeries(a, b, x, fract, wantDerivative, ref derivative, y);
                        }
                    }
                    else
                    {
                        // Sidestep on a, and then use the series representation:
                        double prefix = 1;
                        fract = IbetaAStep(a, b, x, y, 20, wantDerivative, ref derivative);
                        if (!invert)
                        {
                            fract = BetaSmallBLargeASeries(a + 20, b, x, y, fract, prefix);
                        }
                        else
                        {
                            fract -= 1;
                            invert = false;
                            fract = -BetaSmallBLargeASeries(a + 20, b, x, y, fract, prefix);
                        }
                    }
                }
            }
            else
            {
                // One of a, b < 1 only:
                if ((b <= 1) || ((x < 0.1) && (Math.Pow(b * x, a) <= 0.7)))
                {
                    if (!invert)
                    {
                        fract = IbetaSeries(a, b, x, 0, wantDerivative, ref derivative, y);
                    }
                    else
                    {
                        fract = -1;
                        invert = false;
                        fract = -IbetaSeries(a, b, x, fract, wantDerivative, ref derivative, y);
                    }
                }
                else
                {
                    (a, b) = (b, a);
                    (x, y) = (y, x);
                    invert = !invert;

                    if (y >= 0.3)
                    {
                        if (!invert)
                        {
                            fract = IbetaSeries(a, b, x, 0, wantDerivative, ref derivative, y);
                        }
                        else
                        {
                            fract = -1;
                            invert = false;
                            fract = -IbetaSeries(a, b, x, fract, wantDerivative, ref derivative, y);
                        }
                    }
                    else if (a >= 15)
                    {
                        if (!invert)
                        {
                            fract = BetaSmallBLargeASeries(a, b, x, y, 0, 1);
                        }
                        else
                        {
                            fract = -1;
                            invert = false;
                            fract = -BetaSmallBLargeASeries(a, b, x, y, fract, 1);
                        }
                    }
                    else
                    {
                        // Sidestep to improve errors:
                        double prefix = 1;
                        fract = IbetaAStep(a, b, x, y, 20, wantDerivative, ref derivative);
                        if (!invert)
                        {
                            fract = BetaSmallBLargeASeries(a + 20, b, x, y, fract, prefix);
                        }
                        else
                        {
                            fract -= 1;
                            invert = false;
                            fract = -BetaSmallBLargeASeries(a + 20, b, x, y, fract, prefix);
                        }
                    }
                }
            }
        }
        else
        {
            // Both a,b >= 1:
            double lambda;
            if (a < b)
            {
                lambda = a - ((a + b) * x);
            }
            else
            {
                lambda = ((a + b) * y) - b;
            }

            if (lambda < 0)
            {
                (a, b) = (b, a);
                (x, y) = (y, x);
                invert = !invert;
            }

            if (b < 40)
            {
                if ((Math.Floor(a) == a) && (Math.Floor(b) == b) && (a < int.MaxValue - 100) && (y != 1))
                {
                    // relate to the binomial distribution and use a finite sum:
                    double k = a - 1;
                    double n = b + k;
                    fract = BinomialCcdf(n, k, x, y);
                }
                else if (b * x <= 0.7)
                {
                    if (!invert)
                    {
                        fract = IbetaSeries(a, b, x, 0, wantDerivative, ref derivative, y);
                    }
                    else
                    {
                        fract = -1;
                        invert = false;
                        fract = -IbetaSeries(a, b, x, fract, wantDerivative, ref derivative, y);
                    }
                }
                else if (a > 15)
                {
                    // sidestep so we can use the series representation:
                    int n = TruncFunctions.ITrunc(Math.Floor(b));
                    if (n == b)
                    {
                        --n;
                    }

                    double bbar = b - n;
                    double prefix = 1;
                    double unused = 0;
                    fract = IbetaAStep(bbar, a, y, x, n, false, ref unused);
                    fract = BetaSmallBLargeASeries(a, bbar, x, y, fract, 1);
                    fract /= prefix;
                }
                else
                {
                    // the normalised case's two pochhammer-free sidesteps
                    int n = TruncFunctions.ITrunc(Math.Floor(b));
                    double bbar = b - n;
                    if (bbar <= 0)
                    {
                        --n;
                        bbar += 1;
                    }

                    double unused = 0;
                    fract = IbetaAStep(bbar, a, y, x, n, false, ref unused);
                    fract += IbetaAStep(a, bbar, x, y, 20, false, ref unused);
                    if (invert)
                    {
                        // Note this line would need changing if we ever enable this branch in non-normalized case
                        fract -= 1;
                    }

                    fract = BetaSmallBLargeASeries(a + 20, bbar, x, y, fract, 1);
                    if (invert)
                    {
                        fract = -fract;
                        invert = false;
                    }
                }
            }
            else
            {
                // a and b both large:
                bool useAsym = false;
                double ma = Max(a, b);
                double xa = ma == a ? x : y;
                double saddle = ma / (a + b);
                double powers = 0;
                if ((ma > 1e-5f / Epsilon) && (ma / Min(a, b) < (xa < saddle ? 2 : 15)))
                {
                    if (a == b)
                    {
                        useAsym = true;
                    }
                    else
                    {
                        powers = Math.Exp((Math.Log(x / (a / (a + b))) * a) + (Math.Log(y / (b / (a + b))) * b));
                        if (powers < Epsilon)
                        {
                            useAsym = true;
                        }
                    }
                }

                if (useAsym)
                {
                    fract = IbetaLargeAb(a, b, x, y, invert);
                    if (fract * Epsilon < powers)
                    {
                        // Erf approximation failed, correction term is too large, fall back:
                        fract = IbetaFraction2(a, b, x, y, wantDerivative, ref derivative);
                    }
                    else
                    {
                        invert = false;
                    }
                }
                else
                {
                    fract = IbetaFraction2(a, b, x, y, wantDerivative, ref derivative);
                }
            }
        }

        if (wantDerivative)
        {
            if (derivative < 0)
            {
                derivative = IbetaPowerTerms(a, b, x, y, 1, function);
            }

            double div = y * x;

            if (derivative != 0)
            {
                if (MaxValue * div < derivative)
                {
                    // overflow, return an arbitrarily large value:
                    derivative = MaxValue / 2;
                }
                else
                {
                    derivative /= div;
                }
            }
        }

        return invert ? 1 - fract : fract;
    }

    // beta_imp(a, b, lanczos13m53): the beta function by the Lanczos approximation
    private static double BetaImp(double a, double b)
    {
        const string function = "boost::math::beta<long double>(long double,long double)";
        if (a <= 0)
        {
            throw Domain(nameof(a), function, $"The arguments to the beta function must be greater than zero (got a={Show(a)}).");
        }

        if (b <= 0)
        {
            throw Domain(nameof(b), function, $"The arguments to the beta function must be greater than zero (got b={Show(b)}).");
        }

        double result;

        double prefix = 1;
        double c = a + b;

        // Special cases:
        if ((c == a) && (b < Epsilon))
        {
            return 1 / b;
        }
        else if ((c == b) && (a < Epsilon))
        {
            return 1 / a;
        }

        if (b == 1)
        {
            return 1 / a;
        }
        else if (a == 1)
        {
            return 1 / b;
        }
        else if (c < Epsilon)
        {
            result = c / a;
            result /= b;
            return result;
        }

        if (a < b)
        {
            (a, b) = (b, a);
        }

        // Lanczos calculation:
        double agh = a + Lanczos13m53.G - 0.5f;
        double bgh = b + Lanczos13m53.G - 0.5f;
        double cgh = c + Lanczos13m53.G - 0.5f;
        result = Lanczos13m53.LanczosSumExpGScaled(a)
            * (Lanczos13m53.LanczosSumExpGScaled(b) / Lanczos13m53.LanczosSumExpGScaled(c));
        double ambh = a - 0.5f - b;
        if ((Math.Abs(b * ambh) < (cgh * 100)) && (a > 100))
        {
            // Special case where the base of the power term is close to 1
            // compute (1+x)^y instead:
            result *= Math.Exp(ambh * Log1pFunctions.Log1p(-b / cgh));
        }
        else
        {
            result *= Math.Pow(agh / cgh, a - 0.5 - b);
        }

        if (cgh > 1e10f)
        {
            // this avoids possible overflow, but appears to be marginally less accurate:
            result *= Math.Pow((agh / cgh) * (bgh / cgh), b);
        }
        else
        {
            result *= Math.Pow((agh * bgh) / (cgh * cgh), b);
        }

        result *= Math.Sqrt(E / bgh);

        // If a and b were originally less than 1 we need to scale the result:
        result *= prefix;

        return result;
    }

    // ibeta_power_terms(a, b, x, y, lanczos13m53, true, pol, prefix, function): (x^a)(y^b)/Beta(a,b). Almost all of the
    // error in the incomplete beta comes from this function: particularly when a and b are large. Computing large
    // powers are *hard* though, and using logarithms just leads to horrendous cancellation errors.
    private static double IbetaPowerTerms(double a, double b, double x, double y, double prefix, string function)
    {
        double result;

        double c = a + b;

        // combine power terms with Lanczos approximation:
        double gh = Lanczos13m53.G - 0.5f;
        double agh = a + gh;
        double bgh = b + gh;
        double cgh = c + gh;
        if ((a < MinValue) || (b < MinValue))
        {
            // denominator overflows in this case
            result = 0;
        }
        else
        {
            result = Lanczos13m53.LanczosSumExpGScaled(c)
                / (Lanczos13m53.LanczosSumExpGScaled(a) * Lanczos13m53.LanczosSumExpGScaled(b));
        }

        result *= prefix;

        // combine with the leftover terms from the Lanczos approximation:
        result *= Math.Sqrt(bgh / E);
        result *= Math.Sqrt(agh / cgh);

        // l1 and l2 are the base of the exponents minus one:
        double l1 = ((x * b) - (y * a) - (y * gh)) / agh;
        double l2 = ((y * a) - (x * b) - (x * gh)) / bgh;
        if (Min(Math.Abs(l1), Math.Abs(l2)) < 0.2)
        {
            // when the base of the exponent is very near 1 we get really
            // gross errors unless extra care is taken:
            if ((l1 * l2 > 0) || (Min(a, b) < 1))
            {
                //
                // This first branch handles the simple cases where either:
                //
                // * The two power terms both go in the same direction
                // (towards zero or towards infinity).  In this case if either
                // term overflows or underflows, then the product of the two must
                // do so also.
                // *Alternatively if one exponent is less than one, then we
                // can't productively use it to eliminate overflow or underflow
                // from the other term.  Problems with spurious overflow/underflow
                // can't be ruled out in this case, but it is *very* unlikely
                // since one of the power terms will evaluate to a number close to 1.
                //
                if (Math.Abs(l1) < 0.1)
                {
                    result *= Math.Exp(a * Log1pFunctions.Log1p(l1));
                }
                else
                {
                    result *= Math.Pow((x * cgh) / agh, a);
                }

                if (Math.Abs(l2) < 0.1)
                {
                    result *= Math.Exp(b * Log1pFunctions.Log1p(l2));
                }
                else
                {
                    result *= Math.Pow((y * cgh) / bgh, b);
                }
            }
            else if (Max(Math.Abs(l1), Math.Abs(l2)) < 0.5)
            {
                //
                // Both exponents are near one and both the exponents are
                // greater than one and further these two
                // power terms tend in opposite directions (one towards zero,
                // the other towards infinity), so we have to combine the terms
                // to avoid any risk of overflow or underflow.
                //
                // We do this by moving one power term inside the other, we have:
                //
                //    (1 + l1)^a * (1 + l2)^b
                //  = ((1 + l1)*(1 + l2)^(b/a))^a
                //  = (1 + l1 + l3 + l1*l3)^a   ;  l3 = (1 + l2)^(b/a) - 1
                //                                    = exp((b/a) * log(1 + l2)) - 1
                //
                // The tricky bit is deciding which term to move inside :-)
                // By preference we move the larger term inside, so that the
                // size of the largest exponent is reduced.  However, that can
                // only be done as long as l3 (see above) is also small.
                //
                bool smallA = a < b;
                double ratio = b / a;
                if ((smallA && (ratio * l2 < 0.1)) || (!smallA && (l1 / ratio > 0.1)))
                {
                    double l3 = Expm1(ratio * Log1pFunctions.Log1p(l2));
                    l3 = l1 + l3 + (l3 * l1);
                    l3 = a * Log1pFunctions.Log1p(l3);
                    result *= Math.Exp(l3);
                }
                else
                {
                    double l3 = Expm1(Log1pFunctions.Log1p(l1) / ratio);
                    l3 = l2 + l3 + (l3 * l2);
                    l3 = b * Log1pFunctions.Log1p(l3);
                    result *= Math.Exp(l3);
                }
            }
            else if (Math.Abs(l1) < Math.Abs(l2))
            {
                // First base near 1 only:
                double l = (a * Log1pFunctions.Log1p(l1)) + (b * Math.Log((y * cgh) / bgh));
                if ((l <= LogMinValue) || (l >= LogMaxValue))
                {
                    l += Math.Log(result);
                    if (l >= LogMaxValue)
                    {
                        throw Overflow(function);
                    }

                    result = Math.Exp(l);
                }
                else
                {
                    result *= Math.Exp(l);
                }
            }
            else
            {
                // Second base near 1 only:
                double l = (b * Log1pFunctions.Log1p(l2)) + (a * Math.Log((x * cgh) / agh));
                if ((l <= LogMinValue) || (l >= LogMaxValue))
                {
                    l += Math.Log(result);
                    if (l >= LogMaxValue)
                    {
                        throw Overflow(function);
                    }

                    result = Math.Exp(l);
                }
                else
                {
                    result *= Math.Exp(l);
                }
            }
        }
        else
        {
            // general case:
            double b1 = (x * cgh) / agh;
            double b2 = (y * cgh) / bgh;
            l1 = a * Math.Log(b1);
            l2 = b * Math.Log(b2);
            if ((l1 >= LogMaxValue) || (l1 <= LogMinValue) || (l2 >= LogMaxValue) || (l2 <= LogMinValue))
            {
                // Oops, under/overflow, sidestep if we can:
                if (a < b)
                {
                    double p1 = Math.Pow(b2, b / a);

                    // arbitrary large value if the logs would fail!
                    double l3 = (b1 != 0) && (p1 != 0) ? (a * (Math.Log(b1) + Math.Log(p1))) : MaxValue;
                    if ((l3 < LogMaxValue) && (l3 > LogMinValue))
                    {
                        result *= Math.Pow(p1 * b1, a);
                    }
                    else
                    {
                        l2 += l1 + Math.Log(result);
                        if (l2 >= LogMaxValue)
                        {
                            throw Overflow(function);
                        }

                        result = Math.Exp(l2);
                    }
                }
                else
                {
                    // This protects against spurious overflow in a/b:
                    double p1 = (b1 < 1) && (b < 1) && (MaxValue * b < a) ? 0 : Math.Pow(b1, a / b);

                    // arbitrary large value if the logs would fail!
                    double l3 = (p1 != 0) && (b2 != 0) ? (Math.Log(p1) + Math.Log(b2)) * b : MaxValue;
                    if ((l3 < LogMaxValue) && (l3 > LogMinValue))
                    {
                        result *= Math.Pow(p1 * b2, b);
                    }
                    else if (result != 0)
                    {
                        // we can elude the calculation below if we're already going to be zero
                        l2 += l1 + Math.Log(result);
                        if (l2 >= LogMaxValue)
                        {
                            throw Overflow(function);
                        }

                        result = Math.Exp(l2);
                    }
                }
            }
            else
            {
                // finally the normal case:
                result *= Math.Pow(b1, a) * Math.Pow(b2, b);
            }
        }

        // a zero result, true or an underflow, which Boost's default policy ignores, returns 0
        return result;
    }

    // ibeta_series(a, b, x, s0, lanczos13m53, true, p_derivative, y, pol): the series approximation to the incomplete
    // beta, its power term combined with the Lanczos approximation
    private static double IbetaSeries(double a, double b, double x, double s0, bool wantDerivative, ref double derivative, double y)
    {
        double result;

        double c = a + b;

        // incomplete beta power term, combined with the Lanczos approximation:
        double agh = a + Lanczos13m53.G - 0.5f;
        double bgh = b + Lanczos13m53.G - 0.5f;
        double cgh = c + Lanczos13m53.G - 0.5f;
        if ((a < MinValue) || (b < MinValue))
        {
            // denorms cause overflow in the Lanzos series, result will be zero anyway
            result = 0;
        }
        else
        {
            double s1 = Lanczos13m53.LanczosSumExpGScaled(c);
            double s2 = Lanczos13m53.LanczosSumExpGScaled(a);
            double s3 = Lanczos13m53.LanczosSumExpGScaled(b);
            if ((s2 > 1) && (s3 > 1) && (MaxValue / s2 < s3))
            {
                result = (s1 / s2) / s3;
            }
            else
            {
                result = s1 / (s2 * s3);
            }
        }

        if (!double.IsFinite(result))
        {
            result = 0;
        }

        double l1 = Math.Log(cgh / bgh) * (b - 0.5f);
        double l2 = Math.Log(x * cgh / agh) * a;

        //
        // Check for over/underflow in the power terms:
        //
        if ((l1 > LogMinValue) && (l1 < LogMaxValue) && (l2 > LogMinValue) && (l2 < LogMaxValue))
        {
            if (a * b < bgh * 10)
            {
                result *= Math.Exp((b - 0.5f) * Log1pFunctions.Log1p(a / bgh));
            }
            else
            {
                result *= Math.Pow(cgh / bgh, b - 0.5);
            }

            result *= Math.Pow(x * cgh / agh, a);
            result *= Math.Sqrt(agh / E);

            if (wantDerivative)
            {
                derivative = result * Math.Pow(y, b);
            }
        }
        else
        {
            //
            // Oh dear, we need logs, and this *will* cancel:
            //
            if (result != 0)
            {
                // elude calculation when result will be zero.
                result = Math.Log(result) + l1 + l2 + ((Math.Log(agh) - 1) / 2);
                if (wantDerivative)
                {
                    derivative = Math.Exp(result + (b * Math.Log(y)));
                }

                result = Math.Exp(result);
            }
        }

        if (result < MinValue)
        {
            // Safeguard: series can't cope with denorms.
            return s0;
        }

        var s = new IbetaSeriesTerms(a, b, x, result);
        ulong maxIter = SeriesFunctions.MaxSeriesIterations;
        result = SeriesFunctions.SumSeries(ref s, Epsilon, ref maxIter, s0);
        SeriesFunctions.CheckSeriesIterations(
            "boost::math::ibeta<%1%>(%1%, %1%, %1%) in ibeta_series (with lanczos)", maxIter);
        return result;
    }

    // ibeta_fraction2(a, b, x, y, pol, true, p_derivative): the incomplete beta by its continued fraction
    private static double IbetaFraction2(double a, double b, double x, double y, bool wantDerivative, ref double derivative)
    {
        double result = IbetaPowerTerms(a, b, x, y, 1, "boost::math::ibeta<%1%>(%1%, %1%, %1%)");
        if (wantDerivative)
        {
            derivative = result;
        }

        if (result == 0)
        {
            return result;
        }

        var f = new IbetaFraction2Terms(a, b, x, y);
        ulong maxTerms = SeriesFunctions.MaxSeriesIterations;
        double fract = FractionFunctions.ContinuedFractionB(ref f, Epsilon, ref maxTerms);
        SeriesFunctions.CheckSeriesIterations("boost::math::ibeta", maxTerms);
        return result / fract;
    }

    // ibeta_a_step(a, b, x, y, k, pol, true, p_derivative): the difference between ibeta(a,b,x) and ibeta(a+k,b,x)
    private static double IbetaAStep(double a, double b, double x, double y, int k, bool wantDerivative, ref double derivative)
    {
        double prefix = IbetaPowerTerms(a, b, x, y, 1, "boost::math::ibeta<%1%>(%1%, %1%, %1%)");
        if (wantDerivative)
        {
            derivative = prefix;
        }

        prefix /= a;
        if (prefix == 0)
        {
            return prefix;
        }

        double sum = 1;
        double term = 1;

        // series summation from 0 to k-1:
        for (int i = 0; i < k - 1; ++i)
        {
            term *= (a + b + i) * x / (a + i + 1);
            sum += term;
        }

        prefix *= sum;

        return prefix;
    }

    // beta_small_b_large_a_series(a, b, x, y, s0, mult, pol, true): the routine for a > 15, b < 1, DiDonato and Morris's
    // BGRAT, their equations 9 through 9.6
    private static double BetaSmallBLargeASeries(double a, double b, double x, double y, double s0, double mult)
    {
        //
        // Some values we'll need later, these are Eq 9.1:
        //
        double bm1 = b - 1;
        double t = a + (bm1 / 2);
        double lx, u;
        if (y < 0.35)
        {
            lx = Log1pFunctions.Log1p(-y);
        }
        else
        {
            lx = Math.Log(x);
        }

        u = -t * lx;

        // and from from 9.2:
        double prefix;
        double h = GammaFunctions.RegularisedGammaPrefix(b, u);
        if (h <= MinValue)
        {
            return s0;
        }

        prefix = h / GammaFunctions.TGammaDeltaRatio(a, b);
        prefix /= Math.Pow(t, b);
        prefix *= mult;

        //
        // now we need the quantity Pn, unfortunately this is computed
        // recursively, and requires a full history of all the previous values
        // so no choice but to declare a big table and hope it's big enough...
        //
        Span<double> p = stackalloc double[PnSize];

        // see 9.3.
        p.Clear();
        p[0] = 1;

        //
        // Now an initial value for J, see 9.6:
        //
        double j = GammaFunctions.GammaQ(b, u) / h;

        //
        // Now we can start to pull things together and evaluate the sum in Eq 9:
        //
        // Value at N = 0
        double sum = s0 + (prefix * j);

        // some variables we'll need:
        // 2*N+1
        uint tnp1 = 1;
        double lx2 = lx / 2;
        lx2 *= lx2;
        double lxp = 1;
        double t4 = 4 * t * t;
        double b2n = b;

        for (uint n = 1; n < PnSize; ++n)
        {
            //
            // begin by evaluating the next Pn from Eq 9.4:
            //
            tnp1 += 2;
            p[(int)n] = 0;
            double mbn;
            uint tmp1 = 3;
            for (uint m = 1; m < n; ++m)
            {
                mbn = (m * b) - n;
                p[(int)n] += mbn * p[(int)(n - m)] / UncheckedFactorialFunctions.UncheckedFactorial((int)tmp1);
                tmp1 += 2;
            }

            p[(int)n] /= n;
            p[(int)n] += bm1 / UncheckedFactorialFunctions.UncheckedFactorial((int)tnp1);

            //
            // Now we want Jn from Jn-1 using Eq 9.6:
            //
            j = ((b2n * (b2n + 1) * j) + ((u + b2n + 1) * lxp)) / t4;
            lxp *= lx2;
            b2n += 2;

            //
            // pull it together with Eq 9:
            //
            double r = prefix * p[(int)n] * j;
            sum += r;

            // r is always small:
            if (Math.Abs(r / Epsilon) < Math.Abs(sum))
            {
                break;
            }
        }

        return sum;
    }

    // binomial_ccdf(n, k, x, y, pol): for integer arguments we can relate the incomplete beta to the complement of the
    // binomial distribution cdf and use this finite sum.
    private static double BinomialCcdf(double n, double k, double x, double y)
    {
        double result = Math.Pow(x, n);

        if (result > MinValue)
        {
            double term = result;
            for (uint i = (uint)TruncFunctions.ITrunc(n - 1); i > k; --i)
            {
                term *= ((i + 1) * y) / ((n - i) * x);
                result += term;
            }
        }
        else
        {
            // First term underflows so we need to start at the mode of the
            // distribution and work outwards:
            int start = TruncFunctions.ITrunc(n * x);
            if (start <= k + 1)
            {
                start = TruncFunctions.ITrunc(k + 2);
            }

            result = Math.Pow(x, start) * Math.Pow(y, n - start)
                * BinomialFunctions.BinomialCoefficient((uint)TruncFunctions.ITrunc(n), (uint)start);
            if (result == 0)
            {
                // OK, starting slightly above the mode didn't work,
                // we'll have to sum the terms the old fashioned way.
                // Very hard to get here, possibly only when exponent
                // range is very limited (as with type float):
                for (uint i = (uint)(start - 1); i > k; --i)
                {
                    result += Math.Pow(x, i) * Math.Pow(y, n - i)
                        * BinomialFunctions.BinomialCoefficient((uint)TruncFunctions.ITrunc(n), (uint)TruncFunctions.ITrunc(i));
                }
            }
            else
            {
                double term = result;
                double startTerm = result;
                for (uint i = (uint)(start - 1); i > k; --i)
                {
                    term *= ((i + 1) * y) / ((n - i) * x);
                    result += term;
                }

                term = startTerm;
                for (uint i = (uint)(start + 1); i <= n; ++i)
                {
                    term *= (n - i + 1) * x / (i * y);
                    result += term;
                }
            }
        }

        return result;
    }

    // ibeta_large_ab(a, b, x, y, invert, true, pol): large arguments, symetric case, see https://dlmf.nist.gov/8.18
    private static double IbetaLargeAb(double a, double b, double x, double y, bool invert)
    {
        double x0 = a / (a + b);
        double y0 = b / (a + b);
        double nu = (x0 * Math.Log(x / x0)) + (y0 * Math.Log(y / y0));

        //
        // Above compution is unstable, force nu to zero if
        // something went wrong:
        //
        if ((nu > 0) || (x == x0) || (y == y0))
        {
            nu = 0;
        }

        nu = Math.Sqrt(-2 * nu);

        //
        // As per https://dlmf.nist.gov/8.18#E10 we need to make sure we have the correct root:
        //
        if ((nu != 0) && (nu / (x - x0) < 0))
        {
            nu = -nu;
        }

        // The correction term in https://dlmf.nist.gov/8.18#E9 is badly unstable, and often
        // makes the compution worse not better, so Boost excludes it.
        double mul = 1;

        return mul * (invert
            ? (1 + ErfFunctions.Erf(-nu * Math.Sqrt((a + b) / 2))) / 2
            : ErfFunctions.Erfc(-nu * Math.Sqrt((a + b) / 2)) / 2);
    }

    // ibeta_derivative_imp(a, b, x, pol)
    private static double IbetaDerivativeImp(double a, double b, double x)
    {
        const string function = "ibeta_derivative<long double>(long double,long double,long double)";

        //
        // start with the usual error checks:
        //
        if (!double.IsFinite(a))
        {
            throw Domain(nameof(a), function, $"The argument a to the incomplete beta function must be finite (got a={Show(a)}).");
        }

        if (!double.IsFinite(b))
        {
            throw Domain(nameof(b), function, $"The argument b to the incomplete beta function must be finite (got b={Show(b)}).");
        }

        if (!(0 <= x && x <= 1))
        {
            throw Domain(nameof(x), function, $"The argument x to the incomplete beta function must be in [0,1] (got x={Show(x)}).");
        }

        if (a <= 0)
        {
            throw Domain(
                nameof(a), function, $"The argument a to the incomplete beta function must be greater than zero (got a={Show(a)}).");
        }

        if (b <= 0)
        {
            throw Domain(
                nameof(b), function, $"The argument b to the incomplete beta function must be greater than zero (got b={Show(b)}).");
        }

        //
        // Now the corner cases:
        //
        if (x == 0)
        {
            return (a > 1) ? 0 : (a == 1) ? 1 / BetaImp(a, b) : throw Overflow(function);
        }
        else if (x == 1)
        {
            return (b > 1) ? 0 : (b == 1) ? 1 / BetaImp(a, b) : throw Overflow(function);
        }

        //
        // Now the regular cases:
        //
        double y = (1 - x) * x;
        if (!double.IsInfinity(1 / y))
        {
            return IbetaPowerTerms(a, b, x, 1 - x, 1 / y, function);
        }

        return (a > 1) ? 0 : (a == 1) ? 1 / BetaImp(a, b) : throw Overflow(function);
    }

    // boost::math::expm1 for long double under the default policy: std::expm1 behind Boost's checks, which the
    // arguments here (logarithms of numbers in (0, 1], scaled) never reach
    private static double Expm1(double x)
    {
        if (double.IsNaN(x))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Error in function boost::math::expm1<long double>(long double): expm1 requires a finite argument, but got {Show(x)}");
        }

        if (x >= LogMaxValue)
        {
            throw Overflow("boost::math::expm1<long double>(long double)");
        }

        return Expm1Functions.StdExpm1(x);
    }

    // std::min's and std::max's semantics, as BOOST_MATH_GPU_SAFE_MIN and MAX are on the host
    private static double Min(double a, double b) => b < a ? b : a;

    private static double Max(double a, double b) => a < b ? b : a;

    // policies::checked_narrowing_cast to the caller's double from long double, MSVC's double: Boost's public entries
    // end in it, and only an infinite value overflows
    private static double CheckedNarrowing(double value, string function) =>
        Math.Abs(value) > MaxValue ? throw Overflow(function) : value;

    private static ArgumentOutOfRangeException Domain(string paramName, string function, string message) =>
        new(paramName, $"Error in function {function}: {message}");

    private static NanoQLException Overflow(string function) => new($"Error in function {function}: Overflow Error");

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);

    // ibeta_series_t: the series' terms
    private struct IbetaSeriesTerms(double a, double b, double x, double mult) : SeriesFunctions.ITerms
    {
        private readonly double x = x;
        private double result = mult;
        private double apn = a;
        private double poch = 1 - b;
        private int n = 1;

        public double Next()
        {
            double r = result / apn;
            apn += 1;
            result *= poch * x / n;
            ++n;
            poch += 1;
            return r;
        }
    }

    // ibeta_fraction2_t: the continued fraction's terms
    private struct IbetaFraction2Terms(double a, double b, double x, double y) : FractionFunctions.IGenerator
    {
        private readonly double a = a;
        private readonly double b = b;
        private readonly double x = x;
        private readonly double y = y;
        private int m = 0;

        public (double A, double B) Next()
        {
            double denom = a + (2 * m) - 1;
            double aN = (m * (a + m - 1) / denom) * ((a + b + m - 1) / denom) * (b - m) * x * x;

            double bN = m;
            bN += (m * (b - m) * x) / (a + (2 * m) - 1);
            bN += ((a + m) * ((a * y) - (b * x) + 1 + (m * (2 - x)))) / (a + (2 * m) + 1);

            ++m;

            return (aN, bN);
        }
    }
}
