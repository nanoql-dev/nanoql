// SPDX-FileCopyrightText: Copyright (C) 2020 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/gausslaguerrecosinepolynomial.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The Gauss-Laguerre cosine polynomial: orthogonal under the weight w(x; u) = e^(−x)·(1 + cos(ux))/m₀ on [0, ∞), m₀
/// = 1 + 1/(1 + u²) normalising it, for the Gaussian quadrature of ∫₀^∞ f(x) dx.
/// </summary>
public sealed class GaussLaguerreCosinePolynomial : GaussLaguerreTrigonometricBase
{
    private readonly double m0;

    /// <summary>Initializes a new instance of the <see cref="GaussLaguerreCosinePolynomial"/> class.</summary>
    /// <param name="u">The frequency u.</param>
    public GaussLaguerreCosinePolynomial(double u)
        : base(u) => m0 = 1.0 + (1.0 / (1.0 + (u * u)));

    /// <inheritdoc/>
    public override double Moment(int i) => (TrigonometricMoment(i) + Fact(i)) / m0;

    /// <inheritdoc/>
    public override double W(double x) => Math.Exp(-x) * (1 + Math.Cos(U * x)) / m0;

    /// <inheritdoc/>
    protected override double M0() => 1 / (1 + (U * U));

    /// <inheritdoc/>
    protected override double M1() => (1 - (U * U)) / Squared(1 + (U * U));

    private static double Squared(double x) => x * x;
}
