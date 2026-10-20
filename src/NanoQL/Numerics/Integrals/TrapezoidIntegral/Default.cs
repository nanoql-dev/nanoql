// SPDX-FileCopyrightText: Copyright (C) 2003 Roman Gitlin
// SPDX-FileCopyrightText: Copyright (C) 2003 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/trapezoidintegral.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib's default trapezoid policy: each refinement halves the intervals, adding the integrand at their midpoints.
/// </summary>
public readonly struct Default : ITrapezoidIntegrationPolicy
{
    /// <inheritdoc/>
    public static double Integrate(Func<double, double> f, double a, double b, double I, int N)
    {
        double sum = 0.0;
        double dx = (b - a) / N;
        double x = a + (dx / 2.0);
        for (int i = 0; i < N; x += dx, ++i)
        {
            sum += f(x);
        }

        return (I + (dx * sum)) / 2.0;
    }

    /// <inheritdoc/>
    public static int NbEvalutions() => 2;
}
