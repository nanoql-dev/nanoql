// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/gaussianorthogonalpolynomial.hpp ql/math/integrals/gaussianorthogonalpolynomial.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The generalized Gauss-Hermite polynomial, with weight w(x; μ) = |x|^(2μ)·e^(−x²) on ℝ, μ &gt;
/// −0.5.</summary>
public sealed class GaussHermitePolynomial : GaussianOrthogonalPolynomial
{
    private readonly double mu_;

    /// <summary>Initializes a new instance of the <see cref="GaussHermitePolynomial"/> class.</summary>
    /// <param name="mu">The weight's parameter, above −0.5.</param>
    /// <exception cref="ArgumentException"><paramref name="mu"/> isn't above −0.5.</exception>
    public GaussHermitePolynomial(double mu = 0.0)
    {
        if (!(mu > -0.5))
        {
            throw new ArgumentException("mu must be bigger than -0.5", nameof(mu));
        }

        mu_ = mu;
    }

    /// <inheritdoc/>
    public override double Mu_0() => Math.Exp(new GammaFunction().LogValue(mu_ + 0.5));

    /// <inheritdoc/>
    public override double Alpha(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        return 0.0;
    }

    /// <inheritdoc/>
    public override double Beta(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        return (i % 2) != 0 ? (i / 2.0) + mu_ : i / 2.0;
    }

    /// <inheritdoc/>
    public override double W(double x) => Math.Pow(Math.Abs(x), 2 * mu_) * Math.Exp(-x * x);

    /// <inheritdoc/>
    public override double LogW(double x) => (2.0 * mu_ * Math.Log(Math.Abs(x))) - (x * x);
}
