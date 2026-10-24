// SPDX-FileCopyrightText: Copyright (C) 2022 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/qdfpamericanengine.hpp ql/pricingengines/vanilla/qdfpamericanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>
/// The tanh-sinh (m, n)-ε scheme: m fixed-point steps, the first a partial Jacobi-Newton one, the rest naive Richardson
/// ones; n Chebyshev nodes interpolating the exercise boundary; tanh-sinh integration to ε throughout.
/// </summary>
/// <remarks>
/// No fixed-point step is refused with the Gauss-Legendre scheme's text, where QuantLib doesn't check it here and its
/// m − 1 naive steps wrap to 2⁶⁴ − 1, so the engine never ends: a documented divergence
/// (<c>Divergences/QdFpTanhSinhSchemeTests.cs</c>). No node is QuantLib's: the engine refuses it when it prices.
/// </remarks>
public sealed class QdFpTanhSinhIterationScheme : QdFpIterationScheme
{
    private readonly int m;
    private readonly int n;
    private readonly Integrator integrator;

    /// <summary>Initializes a new instance of the <see cref="QdFpTanhSinhIterationScheme"/> class.</summary>
    /// <param name="m">The number of fixed-point steps.</param>
    /// <param name="n">The number of Chebyshev nodes.</param>
    /// <param name="eps">The accuracy of the tanh-sinh integrations.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="m"/> or <paramref name="n"/> is negative.</exception>
    /// <exception cref="NanoQLException"><paramref name="m"/> is 0.</exception>
    public QdFpTanhSinhIterationScheme(int m, int n, double eps)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(m);
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        this.m = m;
        this.n = n;
        integrator = new TanhSinhIntegral(eps);

        // the Gauss-Legendre scheme's check, which QuantLib leaves out here (m − 1 wraps)
        if (!(this.m > 0))
        {
            throw new NanoQLException("at least one fixed point iteration step is needed");
        }
    }

    /// <inheritdoc/>
    public override int GetNumberOfChebyshevInterpolationNodes() => n;

    /// <inheritdoc/>
    public override int GetNumberOfNaiveFixedPointSteps() => m - 1;

    /// <inheritdoc/>
    public override int GetNumberOfJacobiNewtonFixedPointSteps() => 1;

    /// <inheritdoc/>
    public override Integrator GetFixedPointIntegrator() => integrator;

    /// <inheritdoc/>
    public override Integrator GetExerciseBoundaryToPriceIntegrator() => integrator;
}
