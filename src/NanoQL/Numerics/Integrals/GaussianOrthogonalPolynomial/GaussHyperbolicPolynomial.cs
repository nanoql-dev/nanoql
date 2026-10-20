// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/gaussianorthogonalpolynomial.hpp ql/math/integrals/gaussianorthogonalpolynomial.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The Gauss hyperbolic polynomial, with weight w(x) = 1/cosh(x) on ℝ.</summary>
public sealed class GaussHyperbolicPolynomial : GaussianOrthogonalPolynomial
{
    /// <inheritdoc/>
    public override double Mu_0() => MathConstants.M_PI;

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
        return i != 0 ? MathConstants.M_PI_2 * MathConstants.M_PI_2 * i * i : MathConstants.M_PI;
    }

    /// <inheritdoc/>
    public override double W(double x) => 1 / Math.Cosh(x);

    /// <inheritdoc/>
    public override double LogW(double x)
    {
        // cosh overflows above |x| of about 710, which would make the
        // inherited log(w(x)) return -infinity. Use
        // log(cosh x) = |x| - log 2 + log1p(exp(-2|x|)) instead.
        double ax = Math.Abs(x);
        return -(ax - MathConstants.M_LN2 + StdLog1p(Math.Exp(-2.0 * ax)));
    }

    // std::log1p (§2's row for a C runtime function .NET lacks): Goldberg's, as Log1pFunctions writes it.
    private static double StdLog1p(double x)
    {
        double u = 1.0 + x;
        if (u == 1.0)
        {
            return x;
        }

        if (double.IsPositiveInfinity(u))
        {
            return Math.Log(u);
        }

        return Math.Log(u) * x / (u - 1.0);
    }
}
