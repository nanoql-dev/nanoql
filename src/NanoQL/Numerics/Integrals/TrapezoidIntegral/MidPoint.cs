// SPDX-FileCopyrightText: Copyright (C) 2003 Roman Gitlin
// SPDX-FileCopyrightText: Copyright (C) 2003 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/trapezoidintegral.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib's midpoint policy: each refinement splits every interval in three, adding the integrand at the outer
/// thirds' midpoints, so it refines a midpoint estimate.
/// </summary>
/// <remarks>
/// QuantLib starts it from the trapezoid's estimate, as every policy (<c>trapezoidintegral.hpp:58-82</c>), which its
/// refinement doesn't refine: the start's error then falls by a third per tripling, first order, so <c>exp</c> on
/// [0, 1] at 1e-6 takes 531,442 evaluations where the midpoint start takes 2,187, and at 1e-10 it doesn't return. Here
/// it starts from the one-interval midpoint estimate, (b − a)·f((a + b)/2), its own; a documented divergence.
/// </remarks>
public readonly struct MidPoint : ITrapezoidIntegrationPolicy
{
    /// <inheritdoc/>
    public static double Integrate(Func<double, double> f, double a, double b, double I, int N)
    {
        double sum = 0.0;
        double dx = (b - a) / N;
        double x = a + (dx / 6.0);
        double D = 2.0 * dx / 3.0;
        for (int i = 0; i < N; x += dx, ++i)
        {
            sum += f(x) + f(x + D);
        }

        return (I + (dx * sum)) / 3.0;
    }

    /// <inheritdoc/>
    public static int NbEvalutions() => 3;

    /// <summary>The one-interval midpoint estimate, (b − a)·f((a + b)/2), with its one evaluation.</summary>
    /// <param name="f">The integrand.</param>
    /// <param name="a">The lower limit.</param>
    /// <param name="b">The upper limit.</param>
    /// <returns>The estimate and its number of evaluations.</returns>
    public static IntegrationResult InitialEstimate(Func<double, double> f, double a, double b) =>
        new((b - a) * f((a + b) / 2.0), 1);
}
