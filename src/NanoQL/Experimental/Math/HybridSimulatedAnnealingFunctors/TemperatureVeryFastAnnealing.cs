// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/hybridsimulatedannealingfunctors.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The very fast annealing's temperature, T₀·e^(−c·k^(1/n)), for the very fast annealing's sampler: QuantLib's
/// <c>TemperatureVeryFastAnnealing</c>.
/// </summary>
/// <remarks>c = −ln(T_f/T₀)·K^(−1/n), so the temperature reaches T_f at K steps.</remarks>
[Experimental("NQL0001")]
public sealed class TemperatureVeryFastAnnealing : IAnnealingTemperature<TemperatureVeryFastAnnealing>
{
    private readonly double inverseN;
    private readonly Vector initialTemp, finalTemp, exponent;

    /// <summary>Initializes a new instance of the <see cref="TemperatureVeryFastAnnealing"/> class.</summary>
    /// <param name="initialTemp">The initial temperature T₀.</param>
    /// <param name="finalTemp">The final temperature T_f.</param>
    /// <param name="maxSteps">The steps K to the final temperature.</param>
    /// <param name="dimension">The dimension n.</param>
    public TemperatureVeryFastAnnealing(double initialTemp, double finalTemp, double maxSteps, int dimension)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dimension);
        inverseN = 1.0 / dimension;
        this.initialTemp = new Vector(dimension, initialTemp);
        this.finalTemp = new Vector(dimension, finalTemp);
        exponent = new Vector(dimension, 0.0);
        double coeff = Math.Pow(maxSteps, -inverseN);
        for (int i = 0; i < this.initialTemp.Size; i++)
        {
            exponent[i] = -Math.Log(this.finalTemp[i] / this.initialTemp[i]) * coeff;
        }
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
            newTemp[i] = initialTemp[i] * Math.Exp(-exponent[i] * Math.Pow(steps[i], inverseN));
        }
    }

    /// <inheritdoc/>
    /// <remarks>Immutable: the temperature itself.</remarks>
    public TemperatureVeryFastAnnealing Clone() => this;
}
