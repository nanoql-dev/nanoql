// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/hybridsimulatedannealingfunctors.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The very fast annealing's sampler, for the very fast annealing's temperature; the parameter space must be bounded
/// above and below: QuantLib's <c>SamplerVeryFastAnnealing</c>.
/// </summary>
/// <remarks>
/// <para>
/// Each coordinate is x + y(upper − lower), y = sign(u − ½)·T·((1 + 1/T)^|2u − 1| − 1), redrawn until inside its
/// bounds. Its draws are <c>std::uniform_real_distribution&lt;Real&gt;</c> on a <c>std::mt19937</c>, as MSVC draws
/// them, on the seed, which is required: QuantLib's default seeds from the clock. A working type.
/// </para>
/// <para>
/// A step is at most the box's width w: below it for every draw that moves a point up, and w itself only at u = 0,
/// which moves it down. So no draw brings in a point outside the box by w or more, nor one off a zero-width box's
/// bound; and at T = 0 no draw but u = 0 moves a point at all. QuantLib's loop never ends there: such a point is
/// refused, and so is a lower bound above its upper (<c>Conventions/ExperimentalMathRefusalTests.cs</c>). Fixed
/// (<c>Divergences/VeryFastAnnealingSamplerTests.cs</c>): each coordinate is drawn at least once, where QuantLib's
/// starts the loop from lower − 1, which rounds to the lower bound itself at every magnitude from 2⁵⁴ and at some from
/// 2⁵³, and then returns that bound undrawn; and a temperature whose reciprocal overflows, below about 5.6e-309, takes
/// y as sign·(T^(1 − |2u − 1|) − T), the same value rewritten, where QuantLib's y is infinite and never lands in the
/// box, and its 0·∞ at T = 0 is NaN, which its loop returns. A temperature of 0 so holds the point, the distribution's
/// limit. Above T 100, where 1 + 1/T loses the digits of 1/T (QuantLib's step is 2.3e-2 off at 1e14 and 0 from about
/// 1e16, so the point is held), y is sign·T·expm1(|2u − 1|·log1p(1/T)), the same value. Elsewhere QuantLib's
/// arithmetic.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
[WorkingType]
public sealed class SamplerVeryFastAnnealing : IAnnealingSampler<SamplerVeryFastAnnealing>
{
    private readonly Vector lower, upper;
    private readonly MersenneTwisterUniformRng generator;

    /// <summary>Initializes a new instance of the <see cref="SamplerVeryFastAnnealing"/> class.</summary>
    /// <param name="lower">The lower bounds.</param>
    /// <param name="upper">The upper bounds.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentException">
    /// The bounds' sizes differ, with QuantLib's text, or a lower bound is above its upper.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public SamplerVeryFastAnnealing(Vector lower, Vector upper, long seed)
    {
        ArgumentNullException.ThrowIfNull(lower);
        ArgumentNullException.ThrowIfNull(upper);
        generator = new MersenneTwisterUniformRng(seed);
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

    private SamplerVeryFastAnnealing(SamplerVeryFastAnnealing other)
    {
        generator = other.generator.Clone();
        lower = other.lower;
        upper = other.upper;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// The vectors' sizes differ, with QuantLib's text, or no draw brings the current point into the box.
    /// </exception>
    public void Sample(Vector newPoint, Vector currentPoint, Vector temp)
    {
        ArgumentNullException.ThrowIfNull(newPoint);
        ArgumentNullException.ThrowIfNull(currentPoint);
        ArgumentNullException.ThrowIfNull(temp);
        if (newPoint.Size != currentPoint.Size || newPoint.Size != lower.Size || newPoint.Size != temp.Size)
        {
            throw new ArgumentException("Incompatible input");
        }

        for (int i = 0; i < currentPoint.Size; i++)
        {
            // a point outside the box: |y| reaches its most only at |2u − 1| = 1, u = 0, which moves the point down,
            // and at T = 0 no other draw moves it
            double t = temp[i];
            if (currentPoint[i] < lower[i] || currentPoint[i] > upper[i])
            {
                double reach = Step(t, 1.0) * (upper[i] - lower[i]);
                if (t == 0.0 || currentPoint[i] + reach <= lower[i] || currentPoint[i] - reach >= upper[i])
                {
                    throw new ArgumentException(
                        "no draw reaches the sampler's bounds from the current point", nameof(currentPoint));
                }
            }

            // drawn at least once
            do
            {
                double draw = MsvcRandom.UniformReal(generator, 0.0, 1.0);
                double sign = (0.5 < draw ? 1 : 0) - (draw < 0.5 ? 1 : 0);
                double y = sign * Step(t, Math.Abs((2 * draw) - 1.0));
                newPoint[i] = currentPoint[i] + (y * (upper[i] - lower[i]));
            }
            while (newPoint[i] < lower[i] || newPoint[i] > upper[i]);
        }
    }

    /// <inheritdoc/>
    public SamplerVeryFastAnnealing Clone() => new(this);

    // T·((1 + 1/T)^e − 1), QuantLib's; where 1/T overflows, the same value as T^(1 − e)·(1 + T)^e − T, (1 + T)^e then
    // 1; above T 100, as T·expm1(e·log1p(1/T)), keeping the digits 1 + 1/T loses
    private static double Step(double t, double e)
    {
        double inverse = 1.0 / t;
        if (double.IsPositiveInfinity(inverse))
        {
            return Math.Pow(t, 1.0 - e) - t;
        }

        return inverse < 1e-2
            ? t * Expm1Functions.StdExpm1(e * Log1pFunctions.Log1p(inverse))
            : t * (Math.Pow(1.0 + inverse, e) - 1.0);
    }
}
