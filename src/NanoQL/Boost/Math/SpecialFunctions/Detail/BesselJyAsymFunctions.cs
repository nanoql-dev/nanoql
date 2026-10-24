// SPDX-FileCopyrightText: Copyright (c) 2007 John Maddock
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/detail/bessel_jy_asym.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's large-argument asymptotic Bessel forms (<c>detail/bessel_jy_asym.hpp</c>): the one nanoql's port of
/// the modified Bessel I reaches, <c>asymptotic_bessel_i_large_x</c>.
/// </summary>
internal static class BesselJyAsymFunctions
{
    /// <summary>
    /// <c>asymptotic_bessel_i_large_x(v, x)</c>: I_v(x) by its expansion for x large against v, A&amp;S 9.7.1's first
    /// four terms.
    /// </summary>
    /// <param name="v">The order.</param>
    /// <param name="x">The argument, large.</param>
    /// <returns>I_v(x).</returns>
    /// <exception cref="NanoQLException">The value overflows.</exception>
    internal static double AsymptoticBesselILargeX(double v, double x)
    {
        double s = 1;
        double mu = 4 * v * v;
        double ex = 8 * x;
        double num = mu - 1;
        double denom = ex;

        s -= num / denom;

        num *= mu - 9;
        denom *= ex * 2;
        s += num / denom;

        num *= mu - 25;
        denom *= ex * 3;
        s -= num / denom;

        // Try and avoid overflow to the last minute:
        double e = Math.Exp(x / 2);

        s = e * (e * s / Math.Sqrt(2 * x * Math.PI));

        return double.IsFinite(s)
            ? s
            : throw new NanoQLException(
                "Error in function boost::math::asymptotic_bessel_i_large_x<long double>(long double,long double): "
                + "Overflow Error");
    }
}
