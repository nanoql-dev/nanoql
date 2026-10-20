// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2007 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/distributions/chisquaredistribution.hpp ql/math/distributions/chisquaredistribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Sankaran's approximation of the cumulative non-central chi-squared distribution, through the normal distribution.
/// </summary>
public sealed class NonCentralCumulativeChiSquareSankaranApprox
{
    private readonly double df_, ncp_;

    /// <summary>Initializes a new instance of the <see cref="NonCentralCumulativeChiSquareSankaranApprox"/>
    /// class.</summary>
    /// <param name="df">The degrees of freedom.</param>
    /// <param name="ncp">The non-centrality.</param>
    public NonCentralCumulativeChiSquareSankaranApprox(double df, double ncp)
    {
        df_ = df;
        ncp_ = ncp;
    }

    /// <summary>The approximate probability of a value below <paramref name="x"/>.</summary>
    /// <param name="x">The point.</param>
    /// <returns>The probability.</returns>
    public double Evaluate(double x)
    {
        double h = 1 - (2 * (df_ + ncp_) * (df_ + (3 * ncp_)) / (3 * Squared(df_ + (2 * ncp_))));
        double p = (df_ + (2 * ncp_)) / Squared(df_ + ncp_);
        double m = (h - 1) * (1 - (3 * h));

        double u = (Math.Pow(x / (df_ + ncp_), h) - (1 + (h * p * (h - 1 - (0.5 * (2 - h) * m * p)))))
                   / (h * Math.Sqrt(2 * p) * (1 + (0.5 * m * p)));

        return new CumulativeNormalDistribution().Evaluate(u);
    }

    private static double Squared(double x) => x * x;
}
