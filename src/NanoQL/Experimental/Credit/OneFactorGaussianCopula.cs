// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/onefactorgaussiancopula.hpp ql/experimental/credit/onefactorgaussiancopula.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The one-factor Gaussian copula: M, Z and Y standard normal, φ(x) = exp(−x²/2)/√(2π), its distribution of Y
/// analytic.
/// </summary>
/// <param name="correlation">The correlation a², in [0, 1].</param>
/// <param name="maximum">The factor grid's upper end.</param>
/// <param name="integrationSteps">The factor grid's number of steps.</param>
[Experimental("NQL0001")]
public sealed class OneFactorGaussianCopula(double correlation, double maximum = 5, int integrationSteps = 50)
    : OneFactorCopula(correlation, maximum, integrationSteps)
{
    private static readonly NormalDistribution DensityOfM = new();
    private static readonly CumulativeNormalDistribution Cumulative = new();
    private static readonly InverseCumulativeNormal InverseCumulative = new();

    /// <inheritdoc/>
    public override double Density(double m) => DensityOfM.Evaluate(m);

    /// <inheritdoc/>
    public override double CumulativeZ(double z) => Cumulative.Evaluate(z);

    /// <summary>The cumulative distribution of Y, the standard normal's.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The probability.</returns>
    public override double CumulativeY(double value) => Cumulative.Evaluate(value);

    /// <summary>The inverse cumulative distribution of Y, the standard normal's.</summary>
    /// <param name="p">The probability.</param>
    /// <returns>The value.</returns>
    public override double InverseCumulativeY(double p) => InverseCumulative.Evaluate(p);

    /// <summary>The cumulative distribution of Y by a double Euler sum of the convolution on [−10, 10] in 200 steps,
    /// QuantLib's test of the analytic one.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The probability.</returns>
    public double TestCumulativeY(double value)
    {
        double c = Correlation;

        if (c == 0)
        {
            return new CumulativeNormalDistribution().Evaluate(value);
        }

        if (c == 1)
        {
            return new CumulativeNormalDistribution().Evaluate(value);
        }

        var dz = new NormalDistribution();
        var dm = new NormalDistribution();

        double minimum = -10;
        double maximum = +10;
        const int steps = 200;

        double delta = (maximum - minimum) / steps;
        double cumulated = 0;
        if (c < 0.5)
        {
            // outer integral -> 1 for c -> 0
            // inner integral -> CumulativeNormal()(y) for c-> 0
            for (double m = minimum; m < maximum; m += delta)
            {
                for (double z = minimum; z < (value - (Math.Sqrt(c) * m)) / Math.Sqrt(1.0 - c); z += delta)
                {
                    cumulated += dm.Evaluate(m) * dz.Evaluate(z);
                }
            }
        }
        else
        {
            // outer integral -> 1 for c -> 1
            // inner integral -> CumulativeNormal()(y) for c-> 1
            for (double z = minimum; z < maximum; z += delta)
            {
                for (double m = minimum; m < (value - (Math.Sqrt(1.0 - c) * z)) / Math.Sqrt(c); m += delta)
                {
                    cumulated += dm.Evaluate(m) * dz.Evaluate(z);
                }
            }
        }

        cumulated *= delta * delta;

        return cumulated;
    }
}
