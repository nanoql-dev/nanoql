// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/hybridsimulatedannealingfunctors.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A sampler from the normal distribution constrained to lie within the boundaries: a value beyond one is reflected
/// back: QuantLib's <c>SamplerMirrorGaussian</c>.
/// </summary>
/// <remarks>
/// <para>
/// Each pass of QuantLib's reflection moves the point by up to the box's width w, so a draw takes about |step|/w passes
/// and never returns on a zero width, nor once w is below the point's last bit. nanoql's holds a zero-width dimension
/// at its bound, its draw still taken, so the other dimensions draw as QuantLib's (the S-xrt plan's XRR12); and on a
/// narrow one runs QuantLib's passes up to 10,000, its bits wherever they end, then folds in one step from the drawn
/// point x, the reflection into [lower, upper] of (x − lower) mod 2w, the passes' own end (XRR13;
/// <c>Conventions/AnnealingSamplerFoldTests.cs</c>).
/// </para>
/// <para>
/// Its draws are <c>std::normal_distribution&lt;Real&gt;(0.0, 1.0)</c> on a <c>std::mt19937</c>, as MSVC draws them (the
/// S-xrt plan's XRD4), on the seed, which is required: QuantLib's default seeds from the clock (XRD3). A working type.
/// </para>
/// <para>
/// A lower bound above its upper is refused, where QuantLib's reflection never ends, and so are bounds of different sizes
/// and a point whose size isn't theirs, which QuantLib's reads past (the S-xrt code review's B4-2;
/// <c>Conventions/ExperimentalMathRefusalTests.cs</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
[WorkingType]
public sealed class SamplerMirrorGaussian : IAnnealingSampler<SamplerMirrorGaussian>
{
    // QuantLib's passes before the fold in one step (XRR13)
    private const int MaxPasses = 10000;

    private readonly MersenneTwisterUniformRng generator;
    private readonly MsvcNormalDistribution distribution;
    private readonly Vector lower, upper;

    /// <summary>Initializes a new instance of the <see cref="SamplerMirrorGaussian"/> class.</summary>
    /// <param name="lower">The lower bounds.</param>
    /// <param name="upper">The upper bounds.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentException">
    /// The bounds' sizes differ, with QuantLib's very fast sampler's text, or a lower bound is above its upper (B4-2).
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public SamplerMirrorGaussian(Vector lower, Vector upper, long seed)
    {
        ArgumentNullException.ThrowIfNull(lower);
        ArgumentNullException.ThrowIfNull(upper);
        generator = new MersenneTwisterUniformRng(seed);
        distribution = new MsvcNormalDistribution(0.0, 1.0);
        this.lower = lower.Clone();
        this.upper = upper.Clone();
        if (this.lower.Size != this.upper.Size)
        {
            throw new ArgumentException("Incompatible input", nameof(upper));
        }

        for (int i = 0; i < this.lower.Size; i++)
        {
            if (this.lower[i] > this.upper[i])
            {
                throw new ArgumentException("a lower bound is above its upper bound", nameof(lower));
            }
        }
    }

    private SamplerMirrorGaussian(SamplerMirrorGaussian other)
    {
        generator = other.generator.Clone();
        distribution = other.distribution.Clone();
        lower = other.lower;
        upper = other.upper;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// The vectors' sizes differ, or differ from the bounds', with QuantLib's text.
    /// </exception>
    public void Sample(Vector newPoint, Vector currentPoint, Vector temp)
    {
        ArgumentNullException.ThrowIfNull(newPoint);
        ArgumentNullException.ThrowIfNull(currentPoint);
        ArgumentNullException.ThrowIfNull(temp);
        if (newPoint.Size != currentPoint.Size || newPoint.Size != temp.Size || newPoint.Size != lower.Size)
        {
            throw new ArgumentException("Incompatible input");
        }

        for (int i = 0; i < currentPoint.Size; i++)
        {
            double x = currentPoint[i] + (Math.Sqrt(temp[i]) * distribution.Next(generator));
            double lo = lower[i], up = upper[i];
            if (up == lo)
            {
                // a zero-width dimension holds its bound (XRR12)
                newPoint[i] = lo;
                continue;
            }

            double y = x;
            int passes = 0;
            while ((y < lo || y > up) && passes < MaxPasses)
            {
                if (y < lo)
                {
                    y = lo + lo - y;
                }
                else
                {
                    y = up + up - y;
                }

                passes++;
            }

            if (y < lo || y > up)
            {
                // the passes' end in one step, from the drawn point (XRR13)
                double w = up - lo;
                double r = Math.IEEERemainder(x - lo, 2.0 * w);
                if (r < 0.0)
                {
                    r += 2.0 * w;
                }

                y = r <= w ? lo + r : lo + ((2.0 * w) - r);
            }

            newPoint[i] = y;
        }
    }

    /// <inheritdoc/>
    public SamplerMirrorGaussian Clone() => new(this);
}
