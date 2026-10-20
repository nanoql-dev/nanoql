// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A Gaussian quadrature of order zero: a C++ library exception in QuantLib, which nanoql refuses with its own text
/// (the playbook's row for such exceptions). QuantLib's constructor allocates <c>Array e(n-1)</c>, and at order zero
/// the size wraps, so the allocation throws <c>std::bad_array_new_length</c> (<c>gaussianquadratures.cpp:41</c>), after
/// the polynomial's own checks and the node and weight arrays. nanoql refuses at that point,
/// <see cref="ArgumentException"/> naming <c>n</c>; every named quadrature and integrator passes its order to that
/// constructor.
/// </summary>
public class GaussianQuadratureOrderTests
{
    [Fact]
    public void OrderZeroIsRefusedAfterThePolynomialsChecks()
    {
        foreach (Func<object> create in new Func<object>[]
        {
            () => new GaussLaguerreIntegration(0),
            () => new GaussHermiteIntegration(0),
            () => new GaussJacobiIntegration(0, 0.5, 0.5),
            () => new GaussHyperbolicIntegration(0),
            () => new GaussLegendreIntegration(0),
            () => new GaussChebyshevIntegration(0),
            () => new GaussChebyshev2ndIntegration(0),
            () => new GaussGegenbauerIntegration(0, 1.5),
            () => new GaussLegendreIntegrator(0),
            () => new GaussChebyshevIntegrator(0),
            () => new GaussChebyshev2ndIntegrator(0),
            () => new GaussianQuadrature(0, new GaussLegendrePolynomial()),
        })
        {
            ArgumentException e = Assert.Throws<ArgumentException>(create);
            Assert.Equal("n", e.ParamName);
            Assert.StartsWith("order 0 not allowed: a Gaussian quadrature needs at least one node", e.Message, StringComparison.Ordinal);
        }

        // the polynomial's own check refuses first, as in QuantLib
        ArgumentException first = Assert.Throws<ArgumentException>(() => new GaussLaguerreIntegration(0, -2.0));
        Assert.StartsWith("s must be bigger than -1", first.Message, StringComparison.Ordinal);
    }
}
