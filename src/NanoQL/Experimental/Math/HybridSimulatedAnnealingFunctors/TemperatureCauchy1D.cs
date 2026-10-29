// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/hybridsimulatedannealingfunctors.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A temperature for the Cauchy sampler, T₀/k^(1/n): QuantLib's <c>TemperatureCauchy1D</c>.
/// </summary>
[Experimental("NQL0001")]
public sealed class TemperatureCauchy1D : IAnnealingTemperature<TemperatureCauchy1D>
{
    private readonly double inverseN;
    private readonly Vector initialTemp;

    /// <summary>Initializes a new instance of the <see cref="TemperatureCauchy1D"/> class.</summary>
    /// <param name="initialTemp">The initial temperature T₀.</param>
    /// <param name="dimension">The dimension.</param>
    public TemperatureCauchy1D(double initialTemp, int dimension)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dimension);
        inverseN = 1.0 / dimension;
        this.initialTemp = new Vector(dimension, initialTemp);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The vectors' sizes differ, with QuantLib's text.</exception>
    public void Update(Vector newTemp, Vector currTemp, Vector steps)
    {
        ArgumentNullException.ThrowIfNull(newTemp);
        ArgumentNullException.ThrowIfNull(currTemp);
        ArgumentNullException.ThrowIfNull(steps);
        if (currTemp.Size != initialTemp.Size || currTemp.Size != newTemp.Size)
        {
            throw new ArgumentException("Incompatible input");
        }

        for (int i = 0; i < initialTemp.Size; i++)
        {
            newTemp[i] = initialTemp[i] / Math.Pow(steps[i], inverseN);
        }
    }

    /// <inheritdoc/>
    /// <remarks>Immutable: the temperature itself.</remarks>
    public TemperatureCauchy1D Clone() => this;
}
