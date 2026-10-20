// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2005 Gary Kennedy
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/gaussianquadratures.hpp ql/math/integrals/gaussianquadratures.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib's <c>detail::GaussianQuadratureIntegrator</c>, the body its three integrator typedefs share: a
/// quadrature on [−1, 1] mapped onto [a, b]. QuantLib passes <c>Null&lt;Real&gt;()</c> as the accuracy, which no
/// rule reads, and counts no evaluation.
/// </summary>
internal static class GaussianQuadratureIntegrator
{
    /// <summary>QuantLib's <c>Null&lt;Real&gt;()</c>, the integrators' absolute accuracy.</summary>
    internal const double NullReal = 3.4028234663852886e38;

    /// <summary>The integral of <paramref name="f"/> over [a, b] by the quadrature on [−1, 1].</summary>
    /// <param name="integration">The quadrature.</param>
    /// <param name="f">The integrand.</param>
    /// <param name="a">The lower limit.</param>
    /// <param name="b">The upper limit.</param>
    /// <returns>The integral, with QuantLib's count of evaluations, 0.</returns>
    internal static IntegrationResult Integrate(GaussianQuadrature integration, Func<double, double> f, double a, double b)
    {
        double c1 = 0.5 * (b - a);
        double c2 = 0.5 * (a + b);

        return new IntegrationResult(c1 * integration.Evaluate(x => f((c1 * x) + c2)), 0);
    }

    /// <summary>The order, refused when negative under its own name before the base integrator reads it.</summary>
    /// <param name="n">The order.</param>
    /// <returns><paramref name="n"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    internal static int Order(int n)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        return n;
    }
}
