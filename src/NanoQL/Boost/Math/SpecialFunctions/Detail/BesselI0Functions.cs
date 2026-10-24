// SPDX-FileCopyrightText: Copyright (c) 2006 Xiaogang Zhang
// SPDX-FileCopyrightText: Copyright (c) 2017 John Maddock
// SPDX-FileCopyrightText: Copyright (c) 2024 Matt Borland
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/detail/bessel_i0.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using static NanoQL.Numerics.PolynomialHorner2Functions;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's modified Bessel function of the first kind of order zero (<c>detail/bessel_i0.hpp</c>), I₀(x) for
/// x ≥ 0, at the 53-bit precision tag, which Boost selects for MSVC's <c>long double</c>: a polynomial in x²/4 below
/// 7.75, and e^x/√x times a polynomial in 1/x above it.
/// </summary>
internal static class BesselI0Functions
{
    // Bessel I0 over[10 ^ -16, 7.75]
    // Max error in interpolated form : 3.042e-18
    // Max Error found at double precision = Poly : 5.106609e-16 Cheb : 5.239199e-16
    private static ReadOnlySpan<double> P1 =>
    [
        1.00000000000000000e+00,
        2.49999999999999909e-01,
        2.77777777777782257e-02,
        1.73611111111023792e-03,
        6.94444444453352521e-05,
        1.92901234513219920e-06,
        3.93675991102510739e-08,
        6.15118672704439289e-10,
        7.59407002058973446e-12,
        7.59389793369836367e-14,
        6.27767773636292611e-16,
        4.34709704153272287e-18,
        2.63417742690109154e-20,
        1.13943037744822825e-22,
        9.07926920085624812e-25,
    ];

    // Max error in interpolated form : 1.685e-16
    // Max Error found at double precision = Poly : 2.575063e-16 Cheb : 2.247615e+00
    private static ReadOnlySpan<double> P2 =>
    [
        3.98942280401425088e-01,
        4.98677850604961985e-02,
        2.80506233928312623e-02,
        2.92211225166047873e-02,
        4.44207299493659561e-02,
        1.30970574605856719e-01,
        -3.35052280231727022e+00,
        2.33025711583514727e+02,
        -1.13366350697172355e+04,
        4.24057674317867331e+05,
        -1.23157028595698731e+07,
        2.80231938155267516e+08,
        -5.01883999713777929e+09,
        7.08029243015109113e+10,
        -7.84261082124811106e+11,
        6.76825737854096565e+12,
        -4.49034849696138065e+13,
        2.24155239966958995e+14,
        -8.13426467865659318e+14,
        2.02391097391687777e+15,
        -3.08675715295370878e+15,
        2.17587543863819074e+15,
    ];

    // Max error in interpolated form : 2.437e-18
    // Max Error found at double precision = Poly : 1.216719e-16
    private static ReadOnlySpan<double> P3 =>
    [
        3.98942280401432905e-01,
        4.98677850491434560e-02,
        2.80506308916506102e-02,
        2.92179096853915176e-02,
        4.53371208762579442e-02,
    ];

    /// <summary><c>bessel_i0(x)</c>: I₀(x).</summary>
    /// <param name="x">The argument, not below 0.</param>
    /// <returns>I₀(x).</returns>
    internal static double BesselI0(double x)
    {
        if (x < 7.75)
        {
            double a = x * x / 4;
            return a * EvaluatePolynomial(P1, a) + 1;
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
