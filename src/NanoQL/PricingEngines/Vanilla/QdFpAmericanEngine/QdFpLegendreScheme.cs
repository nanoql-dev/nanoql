// SPDX-FileCopyrightText: Copyright (C) 2022 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/qdfpamericanengine.hpp ql/pricingengines/vanilla/qdfpamericanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Gauss-Legendre (l, m, n)-p scheme: Gauss-Legendre integration of order l within every fixed-point step; m
/// fixed-point steps, the first a partial Jacobi-Newton one, the rest naive Richardson ones; n Chebyshev nodes
/// interpolating the exercise boundary; Gauss-Legendre integration of order p converting the boundary into the price.
/// </summary>
/// <remarks>An extension point, as in QuantLib: <see cref="QdFpLegendreTanhSinhScheme"/> derives from it.</remarks>
public class QdFpLegendreScheme : QdFpIterationScheme
{
    private readonly int m;
    private readonly int n;
    private readonly Integrator fpIntegrator;
    private readonly Integrator exerciseBoundaryIntegrator;

    /// <summary>Initializes a new instance of the <see cref="QdFpLegendreScheme"/> class.</summary>
    /// <param name="l">The order of the Gauss-Legendre integration within every fixed-point step.</param>
    /// <param name="m">The number of fixed-point steps, at least 1.</param>
    /// <param name="n">The number of Chebyshev nodes, at least 1.</param>
    /// <param name="p">The order of the Gauss-Legendre integration converting the boundary into the price.</param>
    /// <exception cref="ArgumentException">An order is 0.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An argument is negative.</exception>
    /// <exception cref="NanoQLException"><paramref name="m"/> or <paramref name="n"/> is 0.</exception>
    public QdFpLegendreScheme(int l, int m, int n, int p)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(m);
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        this.m = m;
        this.n = n;
        fpIntegrator = new GaussLegendreIntegrator(l);
        exerciseBoundaryIntegrator = new GaussLegendreIntegrator(p);

        if (!(this.m > 0))
        {
            throw new NanoQLException("at least one fixed point iteration step is needed");
        }

        if (!(this.n > 0))
        {
            throw new NanoQLException("at least one interpolation point is needed");
        }
    }

    /// <inheritdoc/>
    public override int GetNumberOfChebyshevInterpolationNodes() => n;

    /// <inheritdoc/>
    public override int GetNumberOfNaiveFixedPointSteps() => m - 1;

    /// <inheritdoc/>
    public override int GetNumberOfJacobiNewtonFixedPointSteps() => 1;

    /// <inheritdoc/>
    public override Integrator GetFixedPointIntegrator() => fpIntegrator;

    /// <inheritdoc/>
    public override Integrator GetExerciseBoundaryToPriceIntegrator() => exerciseBoundaryIntegrator;
}
