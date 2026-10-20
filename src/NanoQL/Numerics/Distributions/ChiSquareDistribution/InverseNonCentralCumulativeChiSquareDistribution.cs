// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2007 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/distributions/chisquaredistribution.hpp ql/math/distributions/chisquaredistribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The inverse of the cumulative non-central chi-squared distribution: the bracket's right end doubled from df + ncp
/// until the distribution reaches the probability, then Brent's solver within the evaluations left.
/// </summary>
public sealed class InverseNonCentralCumulativeChiSquareDistribution
{
    private readonly NonCentralCumulativeChiSquareDistribution nonCentralDist_;
    private readonly double guess_;
    private readonly int maxEvaluations_;
    private readonly double accuracy_;

    /// <summary>Initializes a new instance of the <see cref="InverseNonCentralCumulativeChiSquareDistribution"/>
    /// class.</summary>
    /// <param name="df">The degrees of freedom.</param>
    /// <param name="ncp">The non-centrality.</param>
    /// <param name="maxEvaluations">The maximum number of evaluations, bracketing and solving together.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is negative.</exception>
    public InverseNonCentralCumulativeChiSquareDistribution(
        double df, double ncp, int maxEvaluations = 10, double accuracy = 1e-8)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxEvaluations);
        nonCentralDist_ = new NonCentralCumulativeChiSquareDistribution(df, ncp);
        guess_ = df + ncp;
        maxEvaluations_ = maxEvaluations;
        accuracy_ = accuracy;
    }

    /// <summary>The value below which the distribution has probability <paramref name="x"/>.</summary>
    /// <param name="x">The probability.</param>
    /// <returns>The quantile.</returns>
    /// <exception cref="NanoQLException">
    /// The distribution refuses a point, or the solver finds no root within the evaluations left.
    /// </exception>
    public double Evaluate(double x)
    {
        // first find the right side of the interval
        double upper = guess_;
        int evaluations = maxEvaluations_;
        while (nonCentralDist_.Evaluate(upper) < x && evaluations > 0)
        {
            upper *= 2.0;
            --evaluations;
        }

        // use a Brent solver for the rest
        Brent solver = new Brent().SetMaxEvaluations(evaluations);
        return solver.Solve(
            y => nonCentralDist_.Evaluate(y) - x,
            accuracy_,
            0.75 * upper,
            (evaluations == maxEvaluations_) ? 0.0 : 0.5 * upper,
            upper);
    }
}
