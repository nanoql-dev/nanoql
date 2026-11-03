// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/hybridsimulatedannealingfunctors.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A Boltzmann probability that always accepts a point that improves on the current one: QuantLib's
/// <c>ProbabilityBoltzmannDownhill</c>.
/// </summary>
/// <remarks>
/// Its draws are <c>std::uniform_real_distribution&lt;Real&gt;</c> on a <c>std::mt19937</c>, as MSVC draws them, on the
/// seed, which is required: QuantLib's default seeds from the clock. A working type.
/// </remarks>
[Experimental("NQL0001")]
[WorkingType]
public sealed class ProbabilityBoltzmannDownhill : IAnnealingProbability<ProbabilityBoltzmannDownhill>
{
    private readonly MersenneTwisterUniformRng generator;

    /// <summary>Initializes a new instance of the <see cref="ProbabilityBoltzmannDownhill"/> class.</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public ProbabilityBoltzmannDownhill(long seed)
    {
        generator = new MersenneTwisterUniformRng(seed);
    }

    private ProbabilityBoltzmannDownhill(ProbabilityBoltzmannDownhill other)
    {
        generator = other.generator.Clone();
    }

    /// <inheritdoc/>
    public bool Accept(double currentValue, double newValue, Vector temp)
    {
        ArgumentNullException.ThrowIfNull(temp);
        if (newValue < currentValue)
        {
            return true;
        }

        double temperature = temp.Max();
        return 1.0 / (1.0 + Math.Exp((newValue - currentValue) / temperature)) > MsvcRandom.UniformReal(generator, 0.0, 1.0);
    }

    /// <inheritdoc/>
    public ProbabilityBoltzmannDownhill Clone() => new(this);
}
