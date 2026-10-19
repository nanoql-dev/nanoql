// SPDX-FileCopyrightText: (C) Copyright Nick Thompson 2021.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/tools/cubic_roots.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's <c>cubic_roots</c> (<c>cubic_roots.hpp</c>): the real roots of a cubic, by Numerical Recipes' method,
/// each polished by one Halley step. <c>cubic_root_residual</c> and <c>cubic_root_condition_number</c>, which nothing
/// nanoql ports calls, aren't ported.
/// </summary>
/// <remarks>
/// Each <c>fma</c> is <see cref="Math.FusedMultiplyAdd"/>, and the operations keep Boost's order, so the roots are
/// Boost's bit for bit. <c>std::sort</c> of the three roots is MSVC's for three elements, an insertion sort with
/// <c>&lt;</c>, which leaves a NaN where it is; <see cref="Array.Sort(Array)"/> would move it first. A missing root is
/// C++'s <c>quiet_NaN()</c>.
/// </remarks>
internal static class CubicRootsFunctions
{
    /// <summary>The real roots of a·x³ + b·x² + c·x + d, sorted: <c>cubic_roots(a, b, c, d)</c>.</summary>
    /// <param name="a">The coefficient of x³.</param>
    /// <param name="b">The coefficient of x².</param>
    /// <param name="c">The coefficient of x.</param>
    /// <param name="d">The constant.</param>
    /// <returns>Three roots; a root that isn't real is NaN.</returns>
    internal static double[] CubicRoots(double a, double b, double c, double d)
    {
        double nan = RootsFunctions.QuietNaN;
        double[] roots = [nan, nan, nan];
        if (a == 0)
        {
            // bx^2 + cx + d = 0:
            if (b == 0)
            {
                // cx + d = 0:
                if (c == 0)
                {
                    if (d != 0)
                    {
                        // No solutions:
                        return roots;
                    }

                    roots[0] = 0;
                    roots[1] = 0;
                    roots[2] = 0;
                    return roots;
                }

                roots[0] = -d / c;
                return roots;
            }

            (double q0, double q1) = RootsFunctions.QuadraticRoots(b, c, d);
            roots[0] = q0;
            roots[1] = q1;
            return roots;
        }

        if (d == 0)
        {
            (double q0, double q1) = RootsFunctions.QuadraticRoots(a, b, c);
            roots[0] = q0;
            roots[1] = q1;
            roots[2] = 0;
            Sort(roots);
            return roots;
        }

        double p = b / a;
        double q = c / a;
        double r = d / a;
        double bigQ = ((p * p) - (3 * q)) / 9;
        double bigR = ((2 * p * p * p) - (9 * p * q) + (27 * r)) / 54;
        if (bigR * bigR < bigQ * bigQ * bigQ)
        {
            double rtQ = Math.Sqrt(bigQ);
            double theta = Math.Acos(bigR / (bigQ * rtQ)) / 3;
            double st = Math.Sin(theta);
            double ct = Math.Cos(theta);
            roots[0] = (-2 * rtQ * ct) - (p / 3);
            roots[1] = (-rtQ * (-ct + (Math.Sqrt(3.0) * st))) - (p / 3);
            roots[2] = (rtQ * (ct + (Math.Sqrt(3.0) * st))) - (p / 3);
        }
        else
        {
            // Boost: in this branch three real roots mean a double root, (x+1)^2(x-2) for one.
            double arg = (bigR * bigR) - (bigQ * bigQ * bigQ);
            double bigA = (bigR >= 0 ? -1 : 1) * Math.Cbrt(Math.Abs(bigR) + Math.Sqrt(arg));
            double bigB = 0;
            if (bigA != 0)
            {
                bigB = bigQ / bigA;
            }

            roots[0] = bigA + bigB - (p / 3);

            // Yes, we're comparing floats for equality:
            // Any perturbation pushes the roots into the complex plane; out of the
            // bailiwick of this routine.
            if (bigA == bigB || arg == 0)
            {
                roots[1] = -bigA - (p / 3);
                roots[2] = -bigA - (p / 3);
            }
        }

        // Root polishing, by Horner's method.
        for (int i = 0; i < roots.Length; i++)
        {
            double root = roots[i];
            double f = Math.FusedMultiplyAdd(a, root, b);
            f = Math.FusedMultiplyAdd(f, root, c);
            f = Math.FusedMultiplyAdd(f, root, d);
            double df = Math.FusedMultiplyAdd(3 * a, root, 2 * b);
            df = Math.FusedMultiplyAdd(df, root, c);
            if (df != 0)
            {
                double d2f = Math.FusedMultiplyAdd(6 * a, root, 2 * b);
                double denom = (2 * df * df) - (f * d2f);
                if (denom != 0)
                {
                    root -= 2 * f * df / denom;
                }
                else
                {
                    root -= f / df;
                }
            }

            roots[i] = root;
        }

        Sort(roots);
        return roots;
    }

    // MSVC's std::sort below its threshold: an insertion sort with operator<.
    private static void Sort(double[] values)
    {
        for (int i = 1; i < values.Length; i++)
        {
            double value = values[i];
            if (value < values[0])
            {
                Array.Copy(values, 0, values, 1, i);
                values[0] = value;
            }
            else
            {
                int j = i;
                while (value < values[j - 1])
                {
                    values[j] = values[j - 1];
                    j--;
                }

                values[j] = value;
            }
        }
    }
}
