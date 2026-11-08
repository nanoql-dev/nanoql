// SPDX-FileCopyrightText: Copyright (C) 2014 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/modifiedbessel.hpp ql/math/modifiedbessel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Numerics;

namespace NanoQL.Numerics;

/// <summary>
/// The modified Bessel functions of the first and second kind, I_ν(x) and K_ν(x), real and complex, by the series
/// expansions in e.g. http://www.mhtlab.uwaterloo.ca/courses/me755/web_chap4.pdf; the exponentially weighted versions
/// return the function's value times e^(−x) (I) and e^(x) (K).
/// </summary>
/// <remarks>
/// QuantLib's templates over <c>Real</c> and <c>std::complex&lt;Real&gt;</c> are written twice here, each complex
/// operation in QuantLib's order: .NET's complex products and quotients agree with MSVC's bit for bit, its complex
/// <c>Exp</c>, <c>Log</c>, <c>Sqrt</c>, <c>Abs</c> and <c>Pow</c> within a few units in the last place.
/// </remarks>
public static class ModifiedBesselFunctions
{
    // Euler-Mascheroni constant
    private const double EulerGamma = 0.57721566490153286060651209008240243;

    /// <summary>I_ν(x), for x ≥ 0.</summary>
    /// <param name="nu">The order ν.</param>
    /// <param name="x">The argument, non-negative.</param>
    /// <returns>I_ν(x).</returns>
    /// <exception cref="ArgumentException"><paramref name="x"/> is negative.</exception>
    /// <exception cref="NanoQLException">The series doesn't converge in 1,000 terms.</exception>
    public static double ModifiedBesselFunction_i(double nu, double x)
    {
        if (!(x >= 0.0))
        {
            throw new ArgumentException("negative argument requires complex version of modifiedBesselFunction", nameof(x));
        }

        return IImpl(nu, x, false);
    }

    /// <summary>I_ν(z); a real non-negative argument takes the real function.</summary>
    /// <param name="nu">The order ν.</param>
    /// <param name="z">The argument.</param>
    /// <returns>I_ν(z).</returns>
    /// <exception cref="NanoQLException">The series doesn't converge in 1,000 terms.</exception>
    public static Complex ModifiedBesselFunction_i(double nu, Complex z)
    {
        if (z.Imaginary == 0.0 && z.Real >= 0.0)
        {
            return new Complex(ModifiedBesselFunction_i(nu, z.Real), 0.0);
        }

        return IImpl(nu, z, false);
    }

    /// <summary>K_ν(x).</summary>
    /// <param name="nu">The order ν.</param>
    /// <param name="x">The argument.</param>
    /// <returns>K_ν(x).</returns>
    /// <exception cref="NanoQLException">A series doesn't converge in 1,000 terms.</exception>
    public static double ModifiedBesselFunction_k(double nu, double x) => KImpl(nu, x, false);

    /// <summary>K_ν(z); a real non-negative argument takes the real function.</summary>
    /// <param name="nu">The order ν.</param>
    /// <param name="z">The argument.</param>
    /// <returns>K_ν(z).</returns>
    /// <exception cref="NanoQLException">A series doesn't converge in 1,000 terms.</exception>
    public static Complex ModifiedBesselFunction_k(double nu, Complex z)
    {
        if (z.Imaginary == 0.0 && z.Real >= 0.0)
        {
            return new Complex(ModifiedBesselFunction_k(nu, z.Real), 0.0);
        }

        return KImpl(nu, z, false);
    }

    /// <summary>e^(−x)·I_ν(x), for x ≥ 0.</summary>
    /// <param name="nu">The order ν.</param>
    /// <param name="x">The argument, non-negative.</param>
    /// <returns>e^(−x)·I_ν(x).</returns>
    /// <exception cref="ArgumentException"><paramref name="x"/> is negative.</exception>
    /// <exception cref="NanoQLException">The series doesn't converge in 1,000 terms.</exception>
    public static double ModifiedBesselFunction_i_exponentiallyWeighted(double nu, double x)
    {
        if (!(x >= 0.0))
        {
            throw new ArgumentException("negative argument requires complex version of modifiedBesselFunction", nameof(x));
        }

        return IImpl(nu, x, true);
    }

    /// <summary>e^(−z)·I_ν(z); a real non-negative argument takes the real function.</summary>
    /// <param name="nu">The order ν.</param>
    /// <param name="z">The argument.</param>
    /// <returns>e^(−z)·I_ν(z).</returns>
    /// <exception cref="NanoQLException">The series doesn't converge in 1,000 terms.</exception>
    public static Complex ModifiedBesselFunction_i_exponentiallyWeighted(double nu, Complex z)
    {
        if (z.Imaginary == 0.0 && z.Real >= 0.0)
        {
            return new Complex(ModifiedBesselFunction_i_exponentiallyWeighted(nu, z.Real), 0.0);
        }

        return IImpl(nu, z, true);
    }

    /// <summary>e^(x)·K_ν(x).</summary>
    /// <param name="nu">The order ν.</param>
    /// <param name="x">The argument.</param>
    /// <returns>e^(x)·K_ν(x).</returns>
    /// <exception cref="NanoQLException">A series doesn't converge in 1,000 terms.</exception>
    public static double ModifiedBesselFunction_k_exponentiallyWeighted(double nu, double x) => KImpl(nu, x, true);

    /// <summary>e^(z)·K_ν(z); a real non-negative argument takes the real function.</summary>
    /// <param name="nu">The order ν.</param>
    /// <param name="z">The argument.</param>
    /// <returns>e^(z)·K_ν(z).</returns>
    /// <exception cref="NanoQLException">A series doesn't converge in 1,000 terms.</exception>
    public static Complex ModifiedBesselFunction_k_exponentiallyWeighted(double nu, Complex z)
    {
        if (z.Imaginary == 0.0 && z.Real >= 0.0)
        {
            return new Complex(ModifiedBesselFunction_k_exponentiallyWeighted(nu, z.Real), 0.0);
        }

        return KImpl(nu, z, true);
    }

    // QuantLib's Unweighted and ExponentiallyWeighted policies.
    private static double WeightSmallX(double x, bool weighted) => weighted ? Math.Exp(-x) : 1.0;

    private static double Weight1LargeX(double x, bool weighted) => weighted ? 1.0 : Math.Exp(x);

    private static double Weight2LargeX(double x, bool weighted) => weighted ? Math.Exp(-2.0 * x) : Math.Exp(-x);

    private static Complex WeightSmallX(Complex x, bool weighted) => weighted ? ComplexFunctions.Exp(-x) : new Complex(1.0, 0.0);

    private static Complex Weight1LargeX(Complex x, bool weighted) => weighted ? new Complex(1.0, 0.0) : ComplexFunctions.Exp(x);

    private static Complex Weight2LargeX(Complex x, bool weighted) => weighted ? ComplexFunctions.Exp(-2.0 * x) : ComplexFunctions.Exp(-x);

    private static double IImpl(double nu, double x, bool weighted)
    {
        if (Math.Abs(x) < 13.0)
        {
            double alpha = Math.Pow(0.5 * x, nu) / new GammaFunction().Value(1.0 + nu);
            double Y = 0.25 * x * x;
            int k = 1;
            double sum = alpha, B_k = alpha;

            while (Math.Abs(B_k *= Y / (k * (k + nu))) > Math.Abs(sum) * NumericLimits.QL_EPSILON)
            {
                sum += B_k;
                if (!(++k < 1000))
                {
                    throw new NanoQLException("max iterations exceeded");
                }
            }

            return sum * WeightSmallX(x, weighted);
        }
        else
        {
            double na_k = 1.0, sign = 1.0;
            double da_k = 1.0;

            double s1 = 1.0, s2 = 1.0;
            for (int k = 1; k < 30; ++k)
            {
                sign *= -1;
                na_k *= (4.0 * nu * nu) - (((2.0 * k) - 1.0) * ((2.0 * k) - 1.0));
                da_k *= 8.0 * k * x;
                double a_k = na_k / da_k;

                s2 += a_k;
                s1 += sign * a_k;
            }

            const double i = 0.0;
            return 1.0 / Math.Sqrt(2 * MathConstants.M_PI * x)
                   * ((Weight1LargeX(x, weighted) * s1)
                      + (i * Math.Exp(i * nu * MathConstants.M_PI) * Weight2LargeX(x, weighted) * s2));
        }
    }

    private static Complex IImpl(double nu, Complex x, bool weighted)
    {
        if (Complex.Abs(x) < 13.0)
        {
            Complex alpha = ComplexFunctions.Pow(0.5 * x, nu) / new GammaFunction().Value(1.0 + nu);
            Complex Y = 0.25 * x * x;
            int k = 1;
            Complex sum = alpha, B_k = alpha;

            while (Complex.Abs(B_k *= Y / (k * (k + nu))) > Complex.Abs(sum) * NumericLimits.QL_EPSILON)
            {
                sum += B_k;
                if (!(++k < 1000))
                {
                    throw new NanoQLException("max iterations exceeded");
                }
            }

            return sum * WeightSmallX(x, weighted);
        }
        else
        {
            double na_k = 1.0, sign = 1.0;
            var da_k = new Complex(1.0, 0.0);

            Complex s1 = new(1.0, 0.0), s2 = new(1.0, 0.0);
            for (int k = 1; k < 30; ++k)
            {
                sign *= -1;
                na_k *= (4.0 * nu * nu) - (((2.0 * k) - 1.0) * ((2.0 * k) - 1.0));
                da_k *= 8.0 * k * x;
                Complex a_k = na_k / da_k;

                s2 += a_k;
                s1 += sign * a_k;
            }

            var i = new Complex(0.0, 1.0);
            return 1.0 / Complex.Sqrt(2 * MathConstants.M_PI * x)
                   * ((Weight1LargeX(x, weighted) * s1)
                      + (i * ComplexFunctions.Exp(i * nu * MathConstants.M_PI) * Weight2LargeX(x, weighted) * s2));
        }
    }

    // psi(m) for positive integer m: psi(1) = -gamma and
    // psi(m) = -gamma + sum_{j=1}^{m-1} 1/j. The integer-order series
    // below only ever needs the digamma function at positive integers,
    // so the general function is not required.
    private static double DigammaInteger(int m)
    {
        double s = -EulerGamma;
        for (int j = 1; j < m; ++j)
        {
            s += 1.0 / j;
        }

        return s;
    }

    // K_n for integer n, Abramowitz & Stegun 9.6.11. The
    // I_(-nu) - I_nu route cannot be used here: sin(nu*pi) vanishes at
    // integer order, so the quotient is 0/0 at nu = 0 and catastrophically
    // ill-conditioned at nu = 1, 2, ...
    private static double KIntegerImpl(int n, double x, bool weighted)
    {
        double half = 0.5 * x;
        double y = half * half;

        double sum1 = 0.0;
        if (n > 0)
        {
            // (1/2) (x/2)^-n sum_{k=0}^{n-1} ((n-k-1)!/k!) (-x^2/4)^k
            double coeff = 1.0;
            for (int j = 1; j < n; ++j)
            {
                coeff *= j; // (n-1)!
            }

            double minusY = 1.0;
            double kFactorial = 1.0;
            for (int k = 0; k < n; ++k)
            {
                if (k > 0)
                {
                    kFactorial *= k;
                    coeff /= n - k;
                    minusY *= -y;
                }

                sum1 += coeff / kFactorial * minusY;
            }

            sum1 *= 0.5 * Math.Pow(half, -(double)n);
        }

        double sign = (n % 2 == 0) ? -1.0 : 1.0;
        double sum2 = sign * Math.Log(half) * IImpl(n, x, false);

        double sum3 = 0.0, yPower = 1.0;
        double kFactorial3 = 1.0, nkFactorial = 1.0;
        for (int j = 1; j <= n; ++j)
        {
            nkFactorial *= j; // n!
        }

        for (int k = 0; k < 1000; ++k)
        {
            if (k > 0)
            {
                kFactorial3 *= k;
                nkFactorial *= n + k;
                yPower *= y;
            }

            double term = (DigammaInteger(k + 1) + DigammaInteger(n + k + 1)) * yPower / (kFactorial3 * nkFactorial);
            sum3 += term;
            if (k > 2 && Math.Abs(term) <= Math.Abs(sum3) * NumericLimits.QL_EPSILON)
            {
                break;
            }
        }

        sum3 *= -sign * 0.5 * Math.Pow(half, (double)n);

        return (sum1 + sum2 + sum3) * WeightSmallX(x, weighted);
    }

    private static Complex KIntegerImpl(int n, Complex x, bool weighted)
    {
        Complex half = 0.5 * x;
        Complex y = half * half;

        var sum1 = new Complex(0.0, 0.0);
        if (n > 0)
        {
            // (1/2) (x/2)^-n sum_{k=0}^{n-1} ((n-k-1)!/k!) (-x^2/4)^k
            double coeff = 1.0;
            for (int j = 1; j < n; ++j)
            {
                coeff *= j; // (n-1)!
            }

            var minusY = new Complex(1.0, 0.0);
            double kFactorial = 1.0;
            for (int k = 0; k < n; ++k)
            {
                if (k > 0)
                {
                    kFactorial *= k;
                    coeff /= n - k;
                    minusY *= -y;
                }

                sum1 += coeff / kFactorial * minusY;
            }

            sum1 *= 0.5 * ComplexFunctions.Pow(half, -(double)n);
        }

        double sign = (n % 2 == 0) ? -1.0 : 1.0;
        Complex sum2 = sign * Complex.Log(half) * IImpl(n, x, false);

        Complex sum3 = new(0.0, 0.0), yPower = new(1.0, 0.0);
        double kFactorial3 = 1.0, nkFactorial = 1.0;
        for (int j = 1; j <= n; ++j)
        {
            nkFactorial *= j; // n!
        }

        for (int k = 0; k < 1000; ++k)
        {
            if (k > 0)
            {
                kFactorial3 *= k;
                nkFactorial *= n + k;
                yPower *= y;
            }

            Complex term = (DigammaInteger(k + 1) + DigammaInteger(n + k + 1)) * yPower / (kFactorial3 * nkFactorial);
            sum3 += term;
            if (k > 2 && Complex.Abs(term) <= Complex.Abs(sum3) * NumericLimits.QL_EPSILON)
            {
                break;
            }
        }

        sum3 *= -sign * 0.5 * ComplexFunctions.Pow(half, (double)n);

        return (sum1 + sum2 + sum3) * WeightSmallX(x, weighted);
    }

    private static double KImpl(double nu, double x, bool weighted)
    {
        if (Math.Abs(x) >= 13.0)
        {
            // K cannot be recovered from the difference of the two I
            // series out here: that asymptotic expansion depends on nu
            // only through nu*nu, so I_(-nu) and I_nu are identical and
            // the difference is exactly zero rather than merely
            // ill-conditioned. Sum the K expansion directly instead; it
            // shares the coefficients of the I expansion but without the
            // alternating sign, and having no sin(nu*pi) denominator it
            // is also unaffected by integer order. Note this only bites
            // for real arguments: for std::complex the i*exp(i*nu*pi)
            // term above is non-zero, so the two I values differ there
            // and the subtraction remains usable.
            double na_k = 1.0;
            double da_k = 1.0, s = 1.0;

            for (int k = 1; k < 30; ++k)
            {
                na_k *= (4.0 * nu * nu) - (((2.0 * k) - 1.0) * ((2.0 * k) - 1.0));
                da_k *= 8.0 * k * x;
                s += na_k / da_k;
            }

            return Math.Sqrt(MathConstants.M_PI / (2.0 * x)) * Weight2LargeX(x, weighted) * s;
        }

        // At integer order sin(nu*pi) vanishes together with the
        // numerator: the singularity is removable, but evaluating the
        // quotient directly gives 0/0 at nu = 0 and a result wrong by
        // many orders of magnitude, with an arbitrary sign, at
        // nu = 1, 2, ... Use the integer-order series instead. The
        // threshold is deliberately tight: away from it the quotient
        // below is the more accurate of the two.
        double nearest = Math.Floor(Math.Abs(nu) + 0.5);
        if (Math.Abs(Math.Abs(nu) - nearest) < 1e-9)
        {
            // K is even in nu, so K_(-n) = K_n
            return KIntegerImpl(checked((int)nearest), x, weighted);
        }

        return MathConstants.M_PI_2 * (IImpl(-nu, x, weighted) - IImpl(nu, x, weighted))
               / Math.Sin(MathConstants.M_PI * nu);
    }

    private static Complex KImpl(double nu, Complex x, bool weighted)
    {
        if (Complex.Abs(x) >= 13.0)
        {
            double na_k = 1.0;
            Complex da_k = new(1.0, 0.0), s = new(1.0, 0.0);

            for (int k = 1; k < 30; ++k)
            {
                na_k *= (4.0 * nu * nu) - (((2.0 * k) - 1.0) * ((2.0 * k) - 1.0));
                da_k *= 8.0 * k * x;
                s += na_k / da_k;
            }

            return Complex.Sqrt(MathConstants.M_PI / (2.0 * x)) * Weight2LargeX(x, weighted) * s;
        }

        double nearest = Math.Floor(Math.Abs(nu) + 0.5);
        if (Math.Abs(Math.Abs(nu) - nearest) < 1e-9)
        {
            // K is even in nu, so K_(-n) = K_n
            return KIntegerImpl(checked((int)nearest), x, weighted);
        }

        return MathConstants.M_PI_2 * (IImpl(-nu, x, weighted) - IImpl(nu, x, weighted))
               / Math.Sin(MathConstants.M_PI * nu);
    }
}
