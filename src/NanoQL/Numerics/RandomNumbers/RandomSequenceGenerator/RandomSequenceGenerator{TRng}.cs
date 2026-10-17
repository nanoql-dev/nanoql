// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/randomsequencegenerator.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics;
using NanoQL.Methods;

namespace NanoQL.Numerics;

/// <summary>
/// A random sequence generator based on a pseudo-random number generator <typeparamref name="TRng"/>: each sequence
/// is the generator's next <see cref="Dimension"/> draws.
/// </summary>
/// <remarks>
/// <para>
/// Do not use it with a low-discrepancy sequence generator. QuantLib's seed constructor is
/// <see cref="RandomSequenceGenerator.Create{TRng}(int, long)"/>, for a generator that can be built from a seed, and
/// its <c>nextInt32Sequence</c> is <see cref="RandomSequenceGenerator.NextInt32Sequence{TRng}"/>, for a generator
/// that draws integers.
/// </para>
/// <para>
/// A mutable working type: each draw changes it, and it is not thread-safe. A draw returns a view of its buffer, valid
/// until its next draw. It copies the generator it is given, so the caller's later draws change nothing in it.
/// </para>
/// </remarks>
/// <typeparam name="TRng">The generator of the sequence's elements.</typeparam>
public class RandomSequenceGenerator<TRng> : IRsg
    where TRng : class, IRng
{
    private readonly TRng rng;
    private readonly double[] sequence;
    private double weight;

    /// <summary>Initializes a new instance of the <see cref="RandomSequenceGenerator{TRng}"/> class.</summary>
    /// <param name="dimensionality">The dimension of each sequence, at least 1.</param>
    /// <param name="rng">The generator, which the sequence generator copies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rng"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dimensionality"/> is below 1.</exception>
    public RandomSequenceGenerator(int dimensionality, TRng rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (dimensionality <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dimensionality), dimensionality, "dimensionality must be greater than 0");
        }

        this.rng = (TRng)rng.Clone();
        sequence = new double[dimensionality];
        weight = 1.0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RandomSequenceGenerator{TRng}"/> class from a copy of another,
    /// for a subclass's <see cref="Clone"/>.
    /// </summary>
    /// <param name="source">The generator to copy, deeply.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    protected RandomSequenceGenerator(RandomSequenceGenerator<TRng> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        rng = (TRng)source.rng.Clone();
        sequence = (double[])source.sequence.Clone();
        weight = source.weight;
    }

    // QuantLib's seed constructor, which builds its generator and checks no dimensionality: the companion's Create
    // and the named class adopt the generator they have just built, with no copy. adopt is always true; it only
    // tells this constructor from the copying one.
    internal RandomSequenceGenerator(int dimensionality, TRng rng, bool adopt)
    {
        Debug.Assert(adopt, "the adopting constructor takes the generator without a copy");
        ArgumentOutOfRangeException.ThrowIfNegative(dimensionality);
        this.rng = rng;
        sequence = new double[dimensionality];
        weight = 1.0;
    }

    /// <summary>Gets the dimension of each sequence.</summary>
    public int Dimension => sequence.Length;

    // The generator itself, for the companion's NextInt32Sequence.
    internal TRng Generator => rng;

    /// <summary>Draws the next sequence: the generator's next draws, their weights multiplied.</summary>
    /// <returns>The sequence, a view of the generator's buffer valid until its next draw, and its weight.</returns>
    public Sample<ReadOnlyMemory<double>> NextSequence()
    {
        weight = 1.0;
        for (int i = 0; i < sequence.Length; i++)
        {
            Sample<double> x = rng.Next();
            sequence[i] = x.Value;
            weight *= x.Weight;
        }

        return new(sequence, weight);
    }

    /// <summary>Gets the sequence the last draw gave.</summary>
    /// <returns>The sequence, a view of the generator's buffer valid until its next draw, and its weight.</returns>
    public Sample<ReadOnlyMemory<double>> LastSequence() => new(sequence, weight);

    /// <summary>
    /// Copies the sequence generator deeply, its generator included: the copy's next draw equals this one's, into its
    /// own buffer, and the two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public virtual RandomSequenceGenerator<TRng> Clone() => new(this);

    /// <inheritdoc/>
    IRsg IRsg.Clone() => Clone();
}
