// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/centrallimitgaussianrng.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;

namespace NanoQL.Numerics;

/// <summary>
/// The Gaussian random-number generator by the well-known fact that the sum of 12 uniform deviates in (−0.5, 0.5) is
/// approximately a Gaussian deviate with average 0 and standard deviation 1; the uniform deviates are supplied by
/// <typeparamref name="TRng"/>.
/// </summary>
/// <remarks>
/// A mutable working type: each draw changes it, and it is not thread-safe. It copies the generator it is given, so the
/// caller's later draws change nothing in it.
/// </remarks>
/// <typeparam name="TRng">The uniform generator, of deviates in (0, 1).</typeparam>
[WorkingType]
public class CLGaussianRng<TRng> : IRng
    where TRng : class, IRng
{
    private readonly TRng uniformGenerator;

    /// <summary>Initializes a new instance of the <see cref="CLGaussianRng{TRng}"/> class.</summary>
    /// <param name="uniformGenerator">The uniform generator, which the generator copies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="uniformGenerator"/> is null.</exception>
    public CLGaussianRng(TRng uniformGenerator)
    {
        ArgumentNullException.ThrowIfNull(uniformGenerator);
        this.uniformGenerator = (TRng)uniformGenerator.Clone();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CLGaussianRng{TRng}"/> class from a copy of another, for a
    /// subclass's <see cref="Clone"/>.
    /// </summary>
    /// <param name="source">The generator to copy, deeply.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    protected CLGaussianRng(CLGaussianRng<TRng> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        uniformGenerator = (TRng)source.uniformGenerator.Clone();
    }

    /// <summary>Draws a sample from a Gaussian distribution.</summary>
    /// <returns>The sample, with the product of the twelve uniform draws' weights.</returns>
    public Sample<double> Next()
    {
        double gaussPoint = -6.0, gaussWeight = 1.0;
        for (int i = 1; i <= 12; i++)
        {
            Sample<double> sample = uniformGenerator.Next();
            gaussPoint += sample.Value;
            gaussWeight *= sample.Weight;
        }

        return new(gaussPoint, gaussWeight);
    }

    /// <summary>
    /// Copies the generator deeply, its uniform generator included: the copy's next draw equals this one's, and the
    /// two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public virtual CLGaussianRng<TRng> Clone() => new(this);

    /// <inheritdoc/>
    IRng IRng.Clone() => Clone();
}
