// SPDX-FileCopyrightText: Copyright (C) 2020 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/exponentialintegrals.hpp ql/math/integrals/exponentialintegrals.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using System.Numerics;

namespace NanoQL.Numerics;

/// <summary>
/// The sine, cosine and exponential integrals, QuantLib's <c>ExponentialIntegral</c> namespace: Si and Ci, real and
/// complex; E1 and Ei, complex. References: B. Rowe et al., "GALSIM: The modular galaxy image simulation toolkit",
/// https://arxiv.org/abs/1407.7676; V. Pegoraro, P. Slusallek, "On the Evaluation of the Complex-Valued Exponential
/// Integral", https://www.sci.utah.edu/~vpegorar/research/2011_JGT.pdf.
/// </summary>
public static class ExponentialIntegralsFunctions
{
    // exponentialintegrals.hpp's M_EULER_MASCHERONI.
    private const double M_EULER_MASCHERONI = 0.5772156649015328606065121;

    /// <summary>The sine integral, Si(x) = ∫₀ˣ sin(t)/t dt.</summary>
    /// <param name="x">The argument.</param>
    /// <returns>Si(x).</returns>
    public static double Si(double x)
    {
        if (x < 0)
        {
            return -Si(-x);
        }
        else if (x <= 4.0)
        {
            double x2 = x * x;

            return x * (1 + (x2 * (-4.54393409816329991e-2 + (x2 * (1.15457225751016682e-3 + (x2 *
                (-1.41018536821330254e-5 + (x2 * (9.43280809438713025e-8 + (x2 * (-3.53201978997168357e-10 + (x2 *
                (7.08240282274875911e-13 - (x2 * 6.05338212010422477e-16)))))))))))))) / (1 + (x2 *
                (1.01162145739225565e-2 + (x2 * (4.99175116169755106e-5 + (x2 * (1.55654986308745614e-7 + (x2 *
                (3.28067571055789734e-10 + (x2 * (4.5049097575386581e-13 + (x2 * 3.21107051193712168e-16))))))))))));
        }
        else
        {
            return MathConstants.M_PI_2 - (F(x) * Math.Cos(x)) - (G(x) * Math.Sin(x));
        }
    }

    /// <summary>The cosine integral, Ci(x) = γ + ln x + ∫₀ˣ (cos(t) − 1)/t dt, for x ≥ 0.</summary>
    /// <param name="x">The argument, non-negative.</param>
    /// <returns>Ci(x).</returns>
    /// <exception cref="ArgumentException"><paramref name="x"/> is negative.</exception>
    public static double Ci(double x)
    {
        if (!(x >= 0))
        {
            throw new ArgumentException("x < 0 => Ci(x) = Ci(-x) + i*pi", nameof(x));
        }

        if (x <= 4.0)
        {
            double x2 = x * x;

            return M_EULER_MASCHERONI + Math.Log(x) + (x2 * (-0.25 + (x2 * (7.51851524438898291e-3 + (x2 *
                (-1.27528342240267686e-4 + (x2 * (1.05297363846239184e-6 + (x2 * (-4.68889508144848019e-9 + (x2 *
                (1.06480802891189243e-11 - (x2 * 9.93728488857585407e-15)))))))))))) / (1 + (x2 *
                (1.1592605689110735e-2 + (x2 * (6.72126800814254432e-5 + (x2 * (2.55533277086129636e-7 + (x2 *
                (6.97071295760958946e-10 + (x2 * (1.38536352772778619e-12 + (x2 * (1.89106054713059759e-15 + (x2 *
                1.39759616731376855e-18)))))))))))))));
        }
        else
        {
            return (F(x) * Math.Sin(x)) - (G(x) * Math.Cos(x));
        }
    }

    /// <summary>The exponential integral Ei(z), on the principal branch.</summary>
    /// <param name="z">The argument, its real part below ln(QL_MAX_REAL).</param>
    /// <returns>Ei(z); −∞ at 0.</returns>
    /// <exception cref="ArgumentException">The argument's real part isn't below ln(QL_MAX_REAL).</exception>
    /// <exception cref="NanoQLException">The series doesn't converge.</exception>
    public static Complex Ei(Complex z) => Ei(z, new Complex(0.0, 0.0));

    /// <summary>The exponential integral E1(z) = −Ei(−z), on the principal branch.</summary>
    /// <param name="z">The argument.</param>
    /// <returns>E1(z).</returns>
    /// <exception cref="ArgumentException">The argument's real part isn't above −ln(QL_MAX_REAL).</exception>
    /// <exception cref="NanoQLException">The series doesn't converge.</exception>
    public static Complex E1(Complex z)
    {
        if (z.Imaginary < 0.0)
        {
            return -Ei(-z, new Complex(0.0, -MathConstants.M_PI));
        }
        else if (z.Imaginary > 0.0 || z.Real < 0.0)
        {
            return -Ei(-z, new Complex(0.0, MathConstants.M_PI));
        }
        else
        {
            return -Ei(-z);
        }
    }

    /// <summary>The complex sine integral, Si(z).</summary>
    /// <param name="z">The argument.</param>
    /// <returns>Si(z).</returns>
    /// <exception cref="NanoQLException">A series doesn't converge.</exception>
    /// <remarks>Reference:
    /// https://functions.wolfram.com/GammaBetaErf/ExpIntegralEi/introductions/ExpIntegrals/ShowAll.html.</remarks>
    public static Complex Si(Complex z)
    {
        if (Complex.Abs(z) <= 0.2)
        {
            Complex s = new(0.0, 0.0), nn = z;
            int k;
            for (k = 2; k < 100 && s != s + nn; ++k)
            {
                s += nn;
                nn *= -z * z / (((2.0 * k) - 2) * ((2 * k) - 1) * ((2 * k) - 1)) * ((2.0 * k) - 3);
            }

            if (!(k < 100))
            {
                throw new NanoQLException($"series conversion issue for Si({Show(z)})");
            }

            return s;
        }
        else
        {
            var i = new Complex(0.0, 1.0);
            return 0.5 * i * (E1(-i * z) - E1(i * z)
                              - new Complex(0.0, ((z.Real >= 0 && z.Imaginary >= 0) || (z.Real > 0 && z.Imaginary < 0))
                                  ? MathConstants.M_PI : -MathConstants.M_PI));
        }
    }

    /// <summary>The complex cosine integral, Ci(z).</summary>
    /// <param name="z">The argument.</param>
    /// <returns>Ci(z).</returns>
    /// <exception cref="NanoQLException">A series doesn't converge.</exception>
    public static Complex Ci(Complex z)
    {
        var i = new Complex(0.0, 1.0);

        double accImag = 0.0;
        if (z.Real < 0.0 && z.Imaginary >= 0.0)
        {
            accImag = MathConstants.M_PI;
        }
        else if (z.Real <= 0.0 && z.Imaginary <= 0.0)
        {
            accImag = -MathConstants.M_PI;
        }

        return (-0.5 * (E1(-i * z) + E1(i * z))) + new Complex(0.0, accImag);
    }

    // Reference:
    // Rowe et al: GALSIM: The modular galaxy image simulation toolkit
    // https://arxiv.org/abs/1407.7676
    private static double F(double x)
    {
        double x2 = 1.0 / (x * x);

        return (1 + (x2 * (7.44437068161936700618e2 + (x2 * (1.96396372895146869801e5 + (x2 *
            (2.37750310125431834034e7 + (x2 * (1.43073403821274636888e9 + (x2 * (4.33736238870432522765e10 + (x2 *
            (6.40533830574022022911e11 + (x2 * (4.20968180571076940208e12 + (x2 * (1.00795182980368574617e13 + (x2 *
            (4.94816688199951963482e12 - (x2 * 4.94701168645415959931e11)))))))))))))))))))) / (x * (1 + (x2 *
            (7.46437068161927678031e2 + (x2 * (1.97865247031583951450e5 + (x2 * (2.41535670165126845144e7 + (x2 *
            (1.47478952192985464958e9 + (x2 * (4.58595115847765779830e10 + (x2 * (7.08501308149515401563e11 + (x2 *
            (5.06084464593475076774e12 + (x2 * (1.43468549171581016479e13 + (x2 *
            1.11535493509914254097e13)))))))))))))))))));
    }

    private static double G(double x)
    {
        double x2 = 1.0 / (x * x);

        return x2 * (1 + (x2 * (8.1359520115168615e2 + (x2 * (2.35239181626478200e5 + (x2 * (3.12557570795778731e7 +
            (x2 * (2.06297595146763354e9 + (x2 * (6.83052205423625007e10 + (x2 * (1.09049528450362786e12 + (x2 *
            (7.57664583257834349e12 + (x2 * (1.81004487464664575e13 + (x2 * (6.43291613143049485e12 - (x2 *
            1.36517137670871689e12)))))))))))))))))))) / (1 + (x2 * (8.19595201151451564e2 + (x2 *
            (2.40036752835578777e5 + (x2 * (3.26026661647090822e7 + (x2 * (2.23355543278099360e9 + (x2 *
            (7.87465017341829930e10 + (x2 * (1.39866710696414565e12 + (x2 * (1.17164723371736605e13 + (x2 *
            (4.01839087307656620e13 + (x2 * 3.99653257887490811e13))))))))))))))))));
    }

    private static Complex Ei(Complex z, Complex acc)
    {
        if (z.Real == 0.0 && z.Imaginary == 0.0)
        {
            return new Complex(double.NegativeInfinity, 0.0);
        }

        const double DIST = 4.5;
        const double MAX_ERROR = 5.0 * NumericLimits.QL_EPSILON;

        double z_inf = Math.Log(0.01 * NumericLimits.QL_MAX_REAL) + Math.Log(100.0);
        if (!(z.Real < z_inf))
        {
            throw new ArgumentException($"argument error {Show(z)}", nameof(z));
        }

        double z_asym = 2.0 - (1.035 * Math.Log(MAX_ERROR));

        double abs_z = Complex.Abs(z);

        static bool Match(Complex z1, Complex z2)
        {
            Complex d = z1 - z2;
            return Math.Abs(d.Real) <= MAX_ERROR * Math.Abs(z1.Real)
                   && Math.Abs(d.Imaginary) <= MAX_ERROR * Math.Abs(z1.Imaginary);
        }

        if (z.Real > z_inf)
        {
            return (ComplexFunctions.Exp(z) / z) + acc;
        }

        if (abs_z > 1.1 * z_asym)
        {
            Complex ei = acc + new Complex(0.0, SignFunctions.Sign(z.Imaginary) * MathConstants.M_PI);
            Complex s = ComplexFunctions.Exp(z) / z;
            for (int i = 1; i <= Math.Floor(abs_z) + 1; ++i)
            {
                if (Match(ei + s, ei))
                {
                    return ei + s;
                }

                ei += s;
                s *= i / z;
            }

            throw new NanoQLException($"series conversion issue for Ei({Show(z)})");
        }

        if (abs_z > DIST && (z.Real < 0 || Math.Abs(z.Imaginary) > DIST))
        {
            var ei = new Complex(0.0, 0.0);
            for (int k = 47; k >= 1; --k)
            {
                ei = -(double)(k * k) / ((2.0 * k) + 1.0 - z + ei);
            }

            return acc + new Complex(0.0, SignFunctions.Sign(z.Imaginary) * MathConstants.M_PI)
                   - (ComplexFunctions.Exp(z) / (1.0 - z + ei));
        }

        Complex sum = new(0.0, 0.0), sn = z;
        double nn = 1.0;

        int n;
        for (n = 2; n < 1000 && sum + (sn * nn) != sum; ++n)
        {
            sum += sn * nn;

            if ((n & 1) != 0)
            {
                nn += 1 / ((2.0 * (n / 2)) + 1);
            }

            sn *= -z / (double)(2 * n);
        }

        if (!(n < 1000))
        {
            throw new NanoQLException($"series conversion issue for Ei({Show(z)})");
        }

        Complex r = M_EULER_MASCHERONI + acc + Complex.Log(z) + (ComplexFunctions.Exp(0.5 * z) * sum);

        if (z.Imaginary != 0.0)
        {
            return r;
        }
        else
        {
            return new Complex(r.Real, acc.Imaginary);
        }
    }

    // operator<< on std::complex: (real,imaginary).
    private static string Show(Complex z) =>
        $"({z.Real.ToString(CultureInfo.InvariantCulture)},{z.Imaginary.ToString(CultureInfo.InvariantCulture)})";
}
