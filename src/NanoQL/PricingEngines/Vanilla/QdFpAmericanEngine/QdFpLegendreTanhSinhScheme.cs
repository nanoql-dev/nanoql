// SPDX-FileCopyrightText: Copyright (C) 2022 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/qdfpamericanengine.hpp ql/pricingengines/vanilla/qdfpamericanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Legendre-tanh-sinh (l, m, n)-ε scheme: the Gauss-Legendre (l, m, n) scheme's fixed-point steps, the boundary
/// converted into the price by tanh-sinh integration to ε.
/// </summary>
public sealed class QdFpLegendreTanhSinhScheme : QdFpLegendreScheme
{
    private readonly double eps;

    /// <summary>Initializes a new instance of the <see cref="QdFpLegendreTanhSinhScheme"/> class.</summary>
    /// <param name="l">The order of the Gauss-Legendre integration within every fixed-point step.</param>
    /// <param name="m">The number of fixed-point steps, at least 1.</param>
    /// <param name="n">The number of Chebyshev nodes, at least 1.</param>
    /// <param name="eps">The accuracy of the tanh-sinh integration converting the boundary into the price.</param>
    /// <exception cref="ArgumentException">The order is 0.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An argument is negative.</exception>
    /// <exception cref="NanoQLException"><paramref name="m"/> or <paramref name="n"/> is 0.</exception>
    public QdFpLegendreTanhSinhScheme(int l, int m, int n, double eps)
        : base(l, m, n, 1) => this.eps = eps;

    /// <inheritdoc/>
    public override Integrator GetExerciseBoundaryToPriceIntegrator() => new TanhSinhIntegral(eps);
}
