// SPDX-FileCopyrightText: Copyright (c) 2017 John Maddock
// SPDX-FileCopyrightText: Copyright (c) 2024 Matt Borland
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/detail/bessel_i1.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using static NanoQL.Numerics.PolynomialHorner2Functions;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's modified Bessel function of the first kind of order one (<c>detail/bessel_i1.hpp</c>), I₁(x) for
/// x ≥ 0, at the 53-bit precision tag, which Boost selects for MSVC's <c>long double</c>: x/2 times a polynomial in
/// x²/4 below 7.75, and e^x/√x times a polynomial in 1/x above it.
/// </summary>
internal static class BesselI1Functions
{
    // Bessel I0 over[10 ^ -16, 7.75]
    // Max error in interpolated form: 5.639e-17
    // Max Error found at double precision = Poly: 1.795559e-16
    private static ReadOnlySpan<double> P1 =>
    [
        8.333333333333333803e-02,
        6.944444444444341983e-03,
        3.472222222225921045e-04,
        1.157407407354987232e-05,
        2.755731926254790268e-07,
        4.920949692800671435e-09,
        6.834657311305621830e-11,
        7.593969849687574339e-13,
        6.904822652741917551e-15,
        5.220157095351373194e-17,
        3.410720494727771276e-19,
        1.625212890947171108e-21,
        1.332898928162290861e-23,
    ];

    // Max error in interpolated form: 1.796e-16
    // Max Error found at double precision = Poly: 2.898731e-16
    private static ReadOnlySpan<double> P2 =>
    [
        3.989422804014406054e-01,
        -1.496033551613111533e-01,
        -4.675104253598537322e-02,
        -4.090895951581637791e-02,
        -5.719036414430205390e-02,
        -1.528189554374492735e-01,
        3.458284470977172076e+00,
        -2.426181371595021021e+02,
        1.178785865993440669e+04,
        -4.404655582443487334e+05,
        1.277677779341446497e+07,
        -2.903390398236656519e+08,
        5.192386898222206474e+09,
        -7.313784438967834057e+10,
        8.087824484994859552e+11,
        -6.967602516005787001e+12,
        4.614040809616582764e+13,
        -2.298849639457172489e+14,
        8.325554073334618015e+14,
        -2.067285045778906105e+15,
        3.146401654361325073e+15,
        -2.213318202179221945e+15,
    ];

    // Max error in interpolated form: 1.320e-19
    // Max Error found at double precision = Poly: 7.065357e-17
    private static ReadOnlySpan<double> P3 =>
    [
        3.989422804014314820e-01,
        -1.496033551467584157e-01,
        -4.675105322571775911e-02,
        -4.090421597376992892e-02,
        -5.843630344778927582e-02,
    ];

    /// <summary><c>bessel_i1(x)</c>: I₁(x).</summary>
    /// <param name="x">The argument, not below 0.</param>
    /// <returns>I₁(x).</returns>
    internal static double BesselI1(double x)
    {
        if (x < 7.75)
        {
            double a = x * x / 4;
            ReadOnlySpan<double> q = [1, (double)0.5f, EvaluatePolynomial(P1, a)];
            return x * EvaluatePolynomial(q, a) / 2;
        }
        else if (x < 500)
        {
            return Math.Exp(x) * EvaluatePolynomial(P2, 1 / x) / Math.Sqrt(x);
        }
        else
        {
            double ex = Math.Exp(x / 2);
            double result = ex * EvaluatePolynomial(P3, 1 / x) / Math.Sqrt(x);
            result *= ex;
            return result;
        }
    }
}
