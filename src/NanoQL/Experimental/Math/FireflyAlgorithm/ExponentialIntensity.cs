// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/fireflyalgorithm.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>An exponentially decreasing intensity, (β₀ − β_min) e^(−γd) + β_min: QuantLib's <c>ExponentialIntensity</c>.</summary>
[Experimental("NQL0001")]
public sealed class ExponentialIntensity : FireflyAlgorithmIntensity
{
    private readonly double beta0, betaMin, gamma;

    /// <summary>Initializes a new instance of the <see cref="ExponentialIntensity"/> class.</summary>
    /// <param name="beta0">The intensity at distance 0, β₀.</param>
    /// <param name="betaMin">The intensity far away, β_min.</param>
    /// <param name="gamma">The decay rate, γ.</param>
    public ExponentialIntensity(double beta0, double betaMin, double gamma)
    {
        this.beta0 = beta0;
        this.betaMin = betaMin;
        this.gamma = gamma;
    }

    /// <inheritdoc/>
    public override FireflyAlgorithmIntensity Clone() => new ExponentialIntensity(beta0, betaMin, gamma);

    /// <inheritdoc/>
    protected override double IntensityImpl(double valueX, double valueY, double distance) =>
        ((beta0 - betaMin) * Math.Exp(-gamma * distance)) + betaMin;
}
