// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/gaussianorthogonalpolynomial.hpp ql/math/integrals/gaussianorthogonalpolynomial.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The generalized Gauss-Laguerre polynomial, with weight w(x; s) = x^s·e^(−x) on [0, ∞), s &gt; −1.</summary>
public sealed class GaussLaguerrePolynomial : GaussianOrthogonalPolynomial
{
    private readonly double s_;

    /// <summary>Initializes a new instance of the <see cref="GaussLaguerrePolynomial"/> class.</summary>
    /// <param name="s">The weight's power, above −1.</param>
    /// <exception cref="ArgumentException"><paramref name="s"/> isn't above −1.</exception>
    public GaussLaguerrePolynomial(double s = 0.0)
    {
        if (!(s > -1.0))
        {
            throw new ArgumentException("s must be bigger than -1", nameof(s));
        }

        s_ = s;
    }

    /// <inheritdoc/>
    public override double Mu_0() => Math.Exp(new GammaFunction().LogValue(s_ + 1));

    /// <inheritdoc/>
    public override double Alpha(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        return (2 * i) + 1 + s_;
    }

    /// <inheritdoc/>
    public override double Beta(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        return i * (i + s_);
    }

    /// <inheritdoc/>
    public override double W(double x) => Math.Pow(x, s_) * Math.Exp(-x);

    /// <inheritdoc/>
    public override double LogW(double x) => (s_ * Math.Log(x)) - x;
}
