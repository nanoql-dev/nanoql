// SPDX-FileCopyrightText: Copyright (C) 2023 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/expm1.hpp ql/math/expm1.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Numerics;

namespace NanoQL.Numerics;

/// <summary>The complex versions of expm1 and log1p, accurate near zero.</summary>
public static class Expm1Functions
{
    /// <summary>e^z − 1, accurate for small |z|.</summary>
    /// <param name="z">The argument.</param>
    /// <returns>e^z − 1.</returns>
    public static Complex Expm1(Complex z)
    {
        if (Complex.Abs(z) < 1.0)
        {
            double a = z.Real, b = z.Imaginary;
            double exp_1 = StdExpm1(a);
            double cos_1 = -2 * Squared(Math.Sin(0.5 * b));

            return new Complex(
                (exp_1 * cos_1) + exp_1 + cos_1,
                Math.Sin(b) * Math.Exp(a));
        }
        else
        {
            return ComplexFunctions.Exp(z) - 1.0;
        }
    }

    /// <summary>ln(1 + z), accurate for small |z|.</summary>
    /// <param name="z">The argument.</param>
    /// <returns>ln(1 + z).</returns>
    public static Complex Log1p(Complex z)
    {
        double a = z.Real, b = z.Imaginary;
        if (Math.Abs(a) < 0.5 && Math.Abs(b) < 0.5)
        {
            return new Complex(
                0.5 * StdLog1p((a * a) + (2 * a) + (b * b)),
                (1.0 + z).Phase);
        }
        else
        {
            return Complex.Log(1.0 + z);
        }
    }

    /// <summary>
    /// <c>std::expm1</c>, which .NET doesn't provide in the C runtime's accuracy (<c>double.ExpM1</c> computes
    /// <c>Math.Exp(x) − 1</c>): Kahan's formulation, as CPython's <c>_Py_expm1</c> writes it, u = e^x, and x where u
    /// rounds to 1, −1 where u − 1 does, and (u − 1)·x/ln u otherwise; +∞ where e^x overflows, as the C runtime's.
    /// </summary>
    /// <param name="x">The argument.</param>
    /// <returns>e^x − 1.</returns>
    internal static double StdExpm1(double x)
    {
        double u = Math.Exp(x);
        if (u == 1.0)
        {
            return x;
        }

        if (double.IsPositiveInfinity(u))
        {
            return u;
        }

        double um1 = u - 1.0;
        if (um1 == -1.0)
        {
            return -1.0;
        }

        return um1 * x / Math.Log(u);
    }

    // std::log1p (§2's row for a C runtime function .NET lacks): Goldberg's, as Log1pFunctions writes it.
    private static double StdLog1p(double x)
    {
        double u = 1.0 + x;
        if (u == 1.0)
        {
            return x;
        }

        if (double.IsPositiveInfinity(u))
        {
            return Math.Log(u);
        }

        return Math.Log(u) * x / (u - 1.0);
    }

    private static double Squared(double x) => x * x;
}
