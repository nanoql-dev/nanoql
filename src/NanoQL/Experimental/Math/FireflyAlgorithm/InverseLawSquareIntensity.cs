// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/fireflyalgorithm.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>An inverse-square-law intensity, (β₀ − β_min)/(d + ε) + β_min: QuantLib's <c>InverseLawSquareIntensity</c>.</summary>
[Experimental("NQL0001")]
public sealed class InverseLawSquareIntensity : FireflyAlgorithmIntensity
{
    private readonly double beta0, betaMin;

    /// <summary>Initializes a new instance of the <see cref="InverseLawSquareIntensity"/> class.</summary>
    /// <param name="beta0">The intensity's scale, β₀ − β_min its excess at unit distance.</param>
    /// <param name="betaMin">The intensity far away, β_min.</param>
    public InverseLawSquareIntensity(double beta0, double betaMin)
    {
        this.beta0 = beta0;
        this.betaMin = betaMin;
    }

    /// <inheritdoc/>
    public override FireflyAlgorithmIntensity Clone() => new InverseLawSquareIntensity(beta0, betaMin);

    /// <inheritdoc/>
    protected override double IntensityImpl(double valueX, double valueY, double distance) =>
        ((beta0 - betaMin) / (distance + NumericLimits.QL_EPSILON)) + betaMin;
}
