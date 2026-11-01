// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/hybridsimulatedannealingfunctors.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A sampler from the normal distribution, x + √T·z, so the parameter space must have support on the whole real line:
/// QuantLib's <c>SamplerGaussian</c>.
/// </summary>
/// <remarks>
/// Its draws are <c>std::normal_distribution&lt;Real&gt;(0.0, 1.0)</c> on a <c>std::mt19937</c>, as MSVC draws them (the
/// S-xrt plan's XRD4), on the seed, which is required: QuantLib's default seeds from the clock (XRD3). A working type.
/// </remarks>
[Experimental("NQL0001")]
[WorkingType]
public sealed class SamplerGaussian : IAnnealingSampler<SamplerGaussian>
{
    private readonly MersenneTwisterUniformRng generator;
    private readonly MsvcNormalDistribution distribution;

    /// <summary>Initializes a new instance of the <see cref="SamplerGaussian"/> class.</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public SamplerGaussian(long seed)
    {
        generator = new MersenneTwisterUniformRng(seed);
        distribution = new MsvcNormalDistribution(0.0, 1.0);
    }

    private SamplerGaussian(SamplerGaussian other)
    {
        generator = other.generator.Clone();
        distribution = other.distribution.Clone();
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The vectors' sizes differ, with QuantLib's text.</exception>
    public void Sample(Vector newPoint, Vector currentPoint, Vector temp)
    {
        ArgumentNullException.ThrowIfNull(newPoint);
        ArgumentNullException.ThrowIfNull(currentPoint);
        ArgumentNullException.ThrowIfNull(temp);
        if (newPoint.Size != currentPoint.Size || newPoint.Size != temp.Size)
        {
            throw new ArgumentException("Incompatible input");
        }
        for (int i = 0; i < currentPoint.Size; i++)
        {
            newPoint[i] = currentPoint[i] + (Math.Sqrt(temp[i]) * distribution.Next(generator));
        }
    }

    /// <inheritdoc/>
    public SamplerGaussian Clone() => new(this);
}
