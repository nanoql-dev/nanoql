// SPDX-FileCopyrightText: (C) Copyright John Maddock 2006.
// SPDX-FileCopyrightText: (C) Copyright Matt Borland 2024.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/tools/roots.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's <c>quadratic_roots</c> (<c>roots.hpp</c>), the floating-point branch of <c>detail::quadratic_roots_imp</c>
/// with its <c>detail::discriminant</c>: the part of the header <see cref="CubicRootsFunctions.CubicRoots"/> calls.
/// </summary>
/// <remarks>
/// Each <c>std::fma</c> is <see cref="Math.FusedMultiplyAdd"/>, MSVC's path; the operations keep Boost's order, so the
/// roots are Boost's bit for bit. A missing root is C++'s <c>quiet_NaN()</c>, whose sign bit is clear.
/// </remarks>
internal static class RootsFunctions
{
    // std::numeric_limits<double>::quiet_NaN(), 0x7FF8000000000000, where double.NaN has its sign bit set.
    internal static readonly double QuietNaN = BitConverter.Int64BitsToDouble(0x7FF8000000000000);

    /// <summary>The real roots of a·x² + b·x + c, in increasing order: <c>quadratic_roots(a, b, c)</c>.</summary>
    /// <param name="a">The coefficient of x².</param>
    /// <param name="b">The coefficient of x.</param>
    /// <param name="c">The constant.</param>
    /// <returns>The two roots, both NaN where there is no real root.</returns>
    internal static (double X0, double X1) QuadraticRoots(double a, double b, double c)
    {
        double nan = QuietNaN;
        if (a == 0)
        {
            if (b == 0 && c != 0)
            {
                return (nan, nan);
            }
            else if (b == 0 && c == 0)
            {
                return (0, 0);
            }

            return (-c / b, -c / b);
        }

        if (b == 0)
        {
            double x0Sq = -c / a;
            if (x0Sq < 0)
            {
                return (nan, nan);
            }

            double x = Math.Sqrt(x0Sq);
            return (-x, x);
        }

        double discriminant = Discriminant(a, b, c);

        // Is there a sane way to flush very small negative values to zero?
        // If there is I don't know of it.
        if (discriminant < 0)
        {
            return (nan, nan);
        }

        double q = -(b + Math.CopySign(Math.Sqrt(discriminant), b)) / 2.0;
        double x0 = q / a;
        double x1 = c / q;
        if (x0 < x1)
        {
            return (x0, x1);
        }

        return (x1, x0);
    }

    // detail::discriminant: b² − 4ac with its two fused products.
    private static double Discriminant(double a, double b, double c)
    {
        double w = 4 * a * c;
        double e = Math.FusedMultiplyAdd(-c, 4 * a, w);
        double f = Math.FusedMultiplyAdd(b, b, -w);
        return f + e;
    }
}
