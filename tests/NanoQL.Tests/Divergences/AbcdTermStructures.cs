// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The term structures of Black volatilities the abcd facts fit, the S-g1d plan's sets 0 to 6 over 0.5 to 20 years
/// (<c>tools/vectors/gen/cxx/termstructures/volatility/abcdcalibration.py</c>'s): falling (0), rising (1), two whose
/// total variance shrinks (2, 3), two dips whose total variance grows (4, 5), and a dip QuantLib refuses (6); and the
/// cost's gradient a fit is checked by.
/// </summary>
internal static class AbcdTermStructures
{
    /// <summary>The maturities.</summary>
    public static readonly double[] T = [0.5, 1.0, 2.0, 3.0, 5.0, 7.0, 10.0, 15.0, 20.0];

    /// <summary>The sets' Black volatilities.</summary>
    public static readonly double[][] Sets =
    [
        [0.30, 0.28, 0.25, 0.22, 0.20, 0.19, 0.18, 0.175, 0.17],
        [0.10, 0.12, 0.15, 0.17, 0.19, 0.20, 0.21, 0.215, 0.22],
        [0.40, 0.25, 0.20, 0.21, 0.23, 0.24, 0.25, 0.255, 0.26],
        [0.25, 0.10, 0.08, 0.09, 0.12, 0.14, 0.16, 0.17, 0.18],
        [0.30, 0.25, 0.22, 0.225, 0.23, 0.235, 0.24, 0.245, 0.25],
        [0.40, 0.29, 0.22, 0.22, 0.23, 0.24, 0.25, 0.255, 0.26],
        [0.25, 0.18, 0.13, 0.12, 0.13, 0.14, 0.16, 0.17, 0.18],
    ];

    /// <summary>QuantLib's end criteria's gradient tolerance, <c>EndCriteria(10000, 1000, 1e-8, 0.3e-4,
    /// 0.3e-4)</c>.</summary>
    public const double GradientTolerance = 0.3e-4;

    /// <summary>The cost, the fit's root-mean-square error, at the parameters.</summary>
    /// <param name="vols">The Black volatilities.</param>
    /// <param name="p">a, b, c and d.</param>
    /// <returns>The cost.</returns>
    public static double Cost(double[] vols, double[] p) =>
        new AbcdCalibration(T, vols, p[0], p[1], p[2], p[3], true, true, true, true).Error();

    /// <summary>The cost's central-difference gradient at a fit, over the parameters given.</summary>
    /// <param name="vols">The Black volatilities.</param>
    /// <param name="fit">The fit.</param>
    /// <param name="parameters">The indices of the parameters, a to d as 0 to 3.</param>
    /// <returns>The gradient's components.</returns>
    public static double[] Gradient(double[] vols, AbcdCalibration fit, params int[] parameters)
    {
        double[] p = [fit.A, fit.B, fit.C, fit.D];
        const double h = 1e-6;
        return [.. parameters.Select(i =>
        {
            double[] up = [.. p], down = [.. p];
            up[i] += h;
            down[i] -= h;
            return (Cost(vols, up) - Cost(vols, down)) / (2 * h);
        })];
    }
}
