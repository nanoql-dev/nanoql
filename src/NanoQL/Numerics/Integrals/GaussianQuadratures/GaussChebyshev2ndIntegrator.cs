// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2005 Gary Kennedy
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/gaussianquadratures.hpp ql/math/integrals/gaussianquadratures.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The integral over [a, b] by the Gauss-Chebyshev (second kind) quadrature of a given order, mapped from [−1, 1]:
/// QuantLib's
/// <c>detail::GaussianQuadratureIntegrator&lt;GaussChebyshev2ndIntegration&gt;</c>. It counts no evaluation, as
/// QuantLib's doesn't.
/// </summary>
public sealed class GaussChebyshev2ndIntegrator : Integrator
{
    private readonly GaussChebyshev2ndIntegration integration;

    /// <summary>Initializes a new instance of the <see cref="GaussChebyshev2ndIntegrator"/> class.</summary>
    /// <param name="n">The order, positive; QuantLib's maximum number of evaluations too.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="n"/> is zero.</exception>
    public GaussChebyshev2ndIntegrator(int n)
        : base(GaussianQuadratureIntegrator.NullReal, GaussianQuadratureIntegrator.Order(n))
    {
        integration = new GaussChebyshev2ndIntegration(n);
    }

    /// <summary>The quadrature.</summary>
    /// <returns>The quadrature, immutable.</returns>
    public GaussChebyshev2ndIntegration GetIntegration() => integration;

    /// <inheritdoc/>
    protected override IntegrationResult IntegrateCore(Func<double, double> f, double a, double b) =>
        GaussianQuadratureIntegrator.Integrate(integration, f, a, b);
}
