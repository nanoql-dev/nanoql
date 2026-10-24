// SPDX-FileCopyrightText: Copyright John Maddock 2006.
// SPDX-FileCopyrightText: Copyright Matt Borland 2024.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/detail/igamma_large.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using static NanoQL.Numerics.PolynomialHorner2Functions;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's asymptotic expansion of the incomplete gamma for large a (<c>igamma_large.hpp</c>), N. M. Temme, "A Set
/// of Algorithms for the Incomplete Gamma Functions" (1994): the coefficients accurate to 53-bit mantissas, which Boost's
/// precision tag selects for <c>double</c>.
/// </summary>
internal static class IgammaLargeFunctions
{
    private static ReadOnlySpan<double> C0 =>
    [
        -0.33333333333333333, 0.083333333333333333, -0.014814814814814815, 0.0011574074074074074, 0.0003527336860670194,
        -0.00017875514403292181, 0.39192631785224378e-4, -0.21854485106799922e-5, -0.185406221071516e-5,
        0.8296711340953086e-6, -0.17665952736826079e-6, 0.67078535434014986e-8, 0.10261809784240308e-7,
        -0.43820360184533532e-8, 0.91476995822367902e-9,
    ];

    private static ReadOnlySpan<double> C1 =>
    [
        -0.0018518518518518519, -0.0034722222222222222, 0.0026455026455026455, -0.00099022633744855967,
        0.00020576131687242798, -0.40187757201646091e-6, -0.18098550334489978e-4, 0.76491609160811101e-5,
        -0.16120900894563446e-5, 0.46471278028074343e-8, 0.1378633446915721e-6, -0.5752545603517705e-7,
        0.11951628599778147e-7,
    ];

    private static ReadOnlySpan<double> C2 =>
    [
        0.0041335978835978836, -0.0026813271604938272, 0.00077160493827160494, 0.20093878600823045e-5,
        -0.00010736653226365161, 0.52923448829120125e-4, -0.12760635188618728e-4, 0.34235787340961381e-7,
        0.13721957309062933e-5, -0.6298992138380055e-6, 0.14280614206064242e-6,
    ];

    private static ReadOnlySpan<double> C3 =>
    [
        0.00064943415637860082, 0.00022947209362139918, -0.00046918949439525571, 0.00026772063206283885,
        -0.75618016718839764e-4, -0.23965051138672967e-6, 0.11082654115347302e-4, -0.56749528269915966e-5,
        0.14230900732435884e-5,
    ];

    private static ReadOnlySpan<double> C4 =>
    [
        -0.0008618882909167117, 0.00078403922172006663, -0.00029907248030319018, -0.14638452578843418e-5,
        0.66414982154651222e-4, -0.39683650471794347e-4, 0.11375726970678419e-4,
    ];

    private static ReadOnlySpan<double> C5 =>
    [
        -0.00033679855336635815, -0.69728137583658578e-4, 0.00027727532449593921, -0.00019932570516188848,
        0.67977804779372078e-4, 0.1419062920643967e-6, -0.13594048189768693e-4, 0.80184702563342015e-5,
        -0.22914811765080952e-5,
    ];

    private static ReadOnlySpan<double> C6 =>
    [
        0.00053130793646399222, -0.00059216643735369388, 0.00027087820967180448, 0.79023532326603279e-6,
        -0.81539693675619688e-4, 0.56116827531062497e-4, -0.18329116582843376e-4,
    ];

    private static ReadOnlySpan<double> C7 =>
    [
        0.00034436760689237767, 0.51717909082605922e-4, -0.00033493161081142236, 0.0002812695154763237,
        -0.00010976582244684731,
    ];

    private static ReadOnlySpan<double> C8 =>
    [
        -0.00065262391859530942, 0.00083949872067208728, -0.00043829709854172101,
    ];

    /// <summary><c>igamma_temme_large</c> at the 53-bit tag: P(a, x) for x &lt; a, Q(a, x) otherwise.</summary>
    /// <param name="a">The shape, large.</param>
    /// <param name="x">The argument, near a.</param>
    /// <returns>The smaller tail.</returns>
    internal static double IgammaTemmeLarge(double a, double x)
    {
        double sigma = (x - a) / a;
        double phi = -Log1pFunctions.Log1pmx(sigma);
        double y = a * phi;
        double z = Math.Sqrt(2 * phi);
        if (x < a)
        {
            z = -z;
        }

        Span<double> workspace = stackalloc double[10];
        workspace[0] = EvaluatePolynomial(C0, z);
        workspace[1] = EvaluatePolynomial(C1, z);
        workspace[2] = EvaluatePolynomial(C2, z);
        workspace[3] = EvaluatePolynomial(C3, z);
        workspace[4] = EvaluatePolynomial(C4, z);
        workspace[5] = EvaluatePolynomial(C5, z);
        workspace[6] = EvaluatePolynomial(C6, z);
        workspace[7] = EvaluatePolynomial(C7, z);
        workspace[8] = EvaluatePolynomial(C8, z);
        workspace[9] = -0.00059676129019274625;

        double result = EvaluatePolynomial(workspace, 1 / a);
        result *= Math.Exp(-y) / Math.Sqrt(2 * Math.PI * a);
        if (x < a)
        {
            result = -result;
        }

        result += ErfFunctions.Erfc(Math.Sqrt(y)) / 2;

        return result;
    }
}
