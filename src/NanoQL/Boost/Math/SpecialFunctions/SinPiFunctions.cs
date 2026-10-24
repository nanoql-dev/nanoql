// SPDX-FileCopyrightText: Copyright (c) 2007 John Maddock
// SPDX-FileCopyrightText: Copyright (c) 2024 Matt Borland
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/sin_pi.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's <c>sin_pi</c> (<c>sin_pi.hpp</c>): sin(πx), with the argument reduced by its integer part before the
/// multiplication by π, so that it is exact at the integers and halves. Boost's policy ignores overflow here, and the
/// result lies in [−1, 1], so it raises no error.
/// </summary>
internal static class SinPiFunctions
{
    // std::numeric_limits<double>::epsilon()
    private const double Epsilon = 2.220446049250313E-16;

    /// <summary><c>sin_pi(x)</c>: sin(πx).</summary>
    /// <param name="x">The argument.</param>
    /// <returns>sin(πx).</returns>
    internal static double SinPi(double x)
    {
        if (x < 0)
        {
            return -SinPiImp(-x);
        }
        else
        {
            return SinPiImp(x);
        }
    }

    // detail::sin_pi_imp, for x not below 0 (NaN passes through)
    private static double SinPiImp(double x)
    {
        if (x < 0.5)
        {
            return Math.Sin(Math.PI * x);
        }

        bool invert;
        if (x < 1)
        {
            invert = true;
            x = -x;
        }
        else
        {
            invert = false;
        }

        double rem = Math.Floor(x);
        if (Math.Abs(Math.Floor(rem / 2) * 2 - rem) > Epsilon)
        {
            invert = !invert;
        }

        rem = x - rem;
        if (rem > 0.5)
        {
            rem = 1 - rem;
        }

        if (rem == 0.5)
        {
            return invert ? -1 : 1;
        }

        rem = Math.Sin(Math.PI * rem);
        return invert ? -rem : rem;
    }
}
