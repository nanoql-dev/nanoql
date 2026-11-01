// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/polarstudenttrng.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The polar Student t random-number generator: Bailey's polar method ("Polar Generation of Random Variates With the
/// t-Distribution", Mathematics of Computation 62-206, 1994), in the variant of "Random Number Generation and Monte
/// Carlo Methods" (Springer, 2003, page 185), with uniforms remapped to [−1, 1] so that no draw sets the sign.
/// </summary>
/// <remarks>
/// A mutable working type (§2.7): each draw changes it, and it isn't thread-safe. A rejection method: not for a
/// low-discrepancy generator. Its draws are raw Student t, of variance ν/(ν − 2). Its seed constructor is
/// <see cref="PolarStudentTRng.Create{TUrng}"/>, a seed in [1, 4294967295] with no default (S2R4), where QuantLib's
/// defaults to 0, the clock (the S-xcr plan's XR4 (a)).
/// </remarks>
/// <typeparam name="TUrng">The uniform generator, of deviates in (0, 1).</typeparam>
[Experimental("NQL0001")]
[WorkingType]
public sealed class PolarStudentTRng<TUrng> : IRng
    where TUrng : class, IRng
{
    private readonly TUrng uniformGenerator;
    private readonly double degFreedom;

    /// <summary>Initializes a new instance of the <see cref="PolarStudentTRng{TUrng}"/> class.</summary>
    /// <param name="degFreedom">The degrees of freedom, positive.</param>
    /// <param name="urng">The uniform generator, which the generator copies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="urng"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="degFreedom"/> isn't positive ("Invalid degrees of freedom
    /// parameter.").</exception>
    public PolarStudentTRng(double degFreedom, TUrng urng)
        : this(degFreedom, (TUrng)(urng ?? throw new ArgumentNullException(nameof(urng))).Clone(), adopt: true)
    {
    }

    // A generator over a uniform generator it adopts, shared with the caller: the polar factor sampler's one stream.
    internal PolarStudentTRng(double degFreedom, TUrng urng, bool adopt)
    {
        _ = adopt;
        if (!(degFreedom > 0))
        {
            throw new ArgumentException("Invalid degrees of freedom parameter.", nameof(degFreedom));
        }

        uniformGenerator = urng;
        this.degFreedom = degFreedom;
    }

    /// <summary>A sample from the Student t distribution, of weight 1.</summary>
    /// <returns>The sample.</returns>
    public Sample<double> Next()
    {
        double u, v, rSqr;
        do
        {
            // samples remapped to [-1,1]:
            v = (2.0 * uniformGenerator.Next().Value) - 1.0;
            u = (2.0 * uniformGenerator.Next().Value) - 1.0;
            rSqr = (v * v) + (u * u);
        }
        while (rSqr >= 1.0);
        return new(u * Math.Sqrt(degFreedom * (Math.Pow(rSqr, -2.0 / degFreedom) - 1.0) / rSqr), 1.0);
    }

    /// <summary>Copies the generator deeply, its uniform generator included.</summary>
    /// <returns>The copy.</returns>
    public PolarStudentTRng<TUrng> Clone() => new(degFreedom, (TUrng)uniformGenerator.Clone(), adopt: true);

    /// <inheritdoc/>
    IRng IRng.Clone() => Clone();
}
