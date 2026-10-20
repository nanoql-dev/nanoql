// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2005 Gary Kennedy
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/gaussianquadratures.hpp ql/math/integrals/gaussianquadratures.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The integral over [a, b] by the Gauss-Chebyshev quadrature of a given order, mapped from [−1, 1]: QuantLib's
/// <c>detail::GaussianQuadratureIntegrator&lt;GaussChebyshevIntegration&gt;</c>. It counts no evaluation, as QuantLib's
/// doesn't.
/// </summary>
public sealed class GaussChebyshevIntegrator : Integrator
{
    private readonly GaussChebyshevIntegration integration;

    /// <summary>Initializes a new instance of the <see cref="GaussChebyshevIntegrator"/> class.</summary>
    /// <param name="n">The order, positive; QuantLib's maximum number of evaluations too.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="n"/> is zero.</exception>
    public GaussChebyshevIntegrator(int n)
        : base(GaussianQuadratureIntegrator.NullReal, GaussianQuadratureIntegrator.Order(n))
    {
        integration = new GaussChebyshevIntegration(n);
    }

    /// <summary>The quadrature.</summary>
    /// <returns>The quadrature, immutable.</returns>
    public GaussChebyshevIntegration GetIntegration() => integration;

    /// <inheritdoc/>
    protected override IntegrationResult IntegrateCore(Func<double, double> f, double a, double b) =>
        GaussianQuadratureIntegrator.Integrate(integration, f, a, b);
}
