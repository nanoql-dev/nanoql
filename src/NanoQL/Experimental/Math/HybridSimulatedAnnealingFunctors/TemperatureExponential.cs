// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/hybridsimulatedannealingfunctors.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>An exponential temperature, T₀·p^k: QuantLib's <c>TemperatureExponential</c>.</summary>
[Experimental("NQL0001")]
public sealed class TemperatureExponential : IAnnealingTemperature<TemperatureExponential>
{
    private readonly Vector initialTemp;
    private readonly double power;

    /// <summary>Initializes a new instance of the <see cref="TemperatureExponential"/> class.</summary>
    /// <param name="initialTemp">The initial temperature T₀.</param>
    /// <param name="dimension">The dimension.</param>
    /// <param name="power">The base p.</param>
    public TemperatureExponential(double initialTemp, int dimension, double power = 0.95)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dimension);
        this.initialTemp = new Vector(dimension, initialTemp);
        this.power = power;
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
            newTemp[i] = initialTemp[i] * Math.Pow(power, steps[i]);
        }
    }

    /// <inheritdoc/>
    /// <remarks>Immutable: the temperature itself.</remarks>
    public TemperatureExponential Clone() => this;
}
