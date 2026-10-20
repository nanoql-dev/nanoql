// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2007 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/distributions/chisquaredistribution.hpp ql/math/distributions/chisquaredistribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The cumulative chi-squared distribution with df degrees of freedom: P(df/2, x/2), the gamma CDF.</summary>
public sealed class CumulativeChiSquareDistribution
{
    private readonly double df_;

    /// <summary>Initializes a new instance of the <see cref="CumulativeChiSquareDistribution"/> class.</summary>
    /// <param name="df">The degrees of freedom; the gamma distribution refuses a non-positive one when
    /// evaluated.</param>
    public CumulativeChiSquareDistribution(double df)
    {
        df_ = df;
    }

    /// <summary>The probability of a value below <paramref name="x"/>.</summary>
    /// <param name="x">The point.</param>
    /// <returns>P(df/2, x/2).</returns>
    /// <exception cref="ArgumentException">The degrees of freedom aren't positive.</exception>
    /// <exception cref="NanoQLException">The gamma function's series don't converge.</exception>
    public double Evaluate(double x) => new CumulativeGammaDistribution(0.5 * df_).Evaluate(0.5 * x);
}
