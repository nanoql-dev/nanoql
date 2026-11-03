// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/randomizedlds.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;

namespace NanoQL.Numerics;

/// <summary>
/// The randomized (random shift) low-discrepancy sequence: it random-shifts a uniform low-discrepancy sequence of
/// dimension N by adding, modulo 1 in each coordinate, a pseudo-random uniform deviate in (0, 1)^N. It is used for
/// implementing randomized quasi-Monte Carlo.
/// </summary>
/// <remarks>
/// <para>
/// The uniform low-discrepancy sequence is supplied by <typeparamref name="TLds"/>; the uniform pseudo-random sequence
/// by <typeparamref name="TPrng"/>; both must have the same dimension N. Inverting the two is possible, but it doesn't
/// make sense. The construction from a seed is <see cref="RandomizedLDS"/>'s.
/// </para>
/// <para>
/// A mutable working type: each draw changes it, and it is not thread-safe. A draw returns a view of its buffer, valid
/// until its next draw. It copies the generators it is given, so the caller's later draws change nothing in it.
/// </para>
/// <para>
/// A coordinate whose sum is exactly 1.0 wraps to 0.0 here, so every point lies in [0, 1)^N; QuantLib's test is
/// <c>x &gt; 1.0</c>, which leaves it at 1.0 (<c>Divergences/RandomizedLdsTests.cs</c>).
/// </para>
/// </remarks>
/// <typeparam name="TLds">The low-discrepancy sequence generator.</typeparam>
/// <typeparam name="TPrng">The pseudo-random sequence generator.</typeparam>
[WorkingType]
public sealed class RandomizedLDS<TLds, TPrng> : IRsg
    where TLds : class, IRsg
    where TPrng : class, IRsg
{
    private readonly TLds pristineLdsg;
    private readonly TPrng prsg;
    private readonly int dimension;
    private readonly double[] x;
    private readonly double[] randomizer;
    private TLds ldsg;
    private double weight;
    private double randomizerWeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="RandomizedLDS{TLds, TPrng}"/> class, its randomizing vector the
    /// pseudo-random generator's first draw.
    /// </summary>
    /// <param name="ldsg">The low-discrepancy sequence generator, which the generator copies.</param>
    /// <param name="prsg">The pseudo-random sequence generator, which the generator copies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ldsg"/> or <paramref name="prsg"/> is null.</exception>
    /// <exception cref="ArgumentException">The two generators' dimensions differ.</exception>
    public RandomizedLDS(TLds ldsg, TPrng prsg)
    {
        ArgumentNullException.ThrowIfNull(ldsg);
        ArgumentNullException.ThrowIfNull(prsg);
        this.ldsg = (TLds)ldsg.Clone();
        pristineLdsg = (TLds)ldsg.Clone();
        this.prsg = (TPrng)prsg.Clone();
        dimension = this.ldsg.Dimension;
        if (this.prsg.Dimension != dimension)
        {
            throw new ArgumentException(
                $"generator mismatch: {dimension}-dim low discrepancy and {this.prsg.Dimension}-dim pseudo random",
                nameof(prsg));
        }

        x = new double[dimension];
        randomizer = new double[dimension];
        weight = 1.0;
        NextRandomizerCore();
    }

    private RandomizedLDS(RandomizedLDS<TLds, TPrng> source)
    {
        ldsg = (TLds)source.ldsg.Clone();
        pristineLdsg = (TLds)source.pristineLdsg.Clone();
        prsg = (TPrng)source.prsg.Clone();
        dimension = source.dimension;
        x = (double[])source.x.Clone();
        randomizer = (double[])source.randomizer.Clone();
        weight = source.weight;
        randomizerWeight = source.randomizerWeight;
    }

    /// <summary>Gets the dimension of each sequence.</summary>
    public int Dimension => dimension;

    /// <summary>Draws the next point: the low-discrepancy sequence's next point shifted by the randomizing
    /// vector.</summary>
    /// <returns>
    /// The point, a view of the generator's buffer valid until its next draw, and its weight, the product of the two
    /// generators' weights.
    /// </returns>
    public Sample<ReadOnlyMemory<double>> NextSequence()
    {
        Sample<ReadOnlyMemory<double>> sample = ldsg.NextSequence();
        ReadOnlySpan<double> value = sample.Value.Span;
        weight = randomizerWeight * sample.Weight;
        for (int i = 0; i < dimension; i++)
        {
            x[i] = randomizer[i] + value[i];
            if (x[i] >= 1.0)
            {
                x[i] -= 1.0;
            }
        }

        return new(x, weight);
    }

    /// <summary>Gets the point the last draw gave.</summary>
    /// <returns>The point, a view of the generator's buffer valid until its next draw, and its weight.</returns>
    public Sample<ReadOnlyMemory<double>> LastSequence() => new(x, weight);

    /// <summary>
    /// Updates the randomizing vector, the pseudo-random generator's next draw, and restarts the low-discrepancy
    /// sequence from its start.
    /// </summary>
    public void NextRandomizer()
    {
        NextRandomizerCore();
        ldsg = (TLds)pristineLdsg.Clone();
    }

    /// <summary>
    /// Copies the generator deeply, both its generators included: the copy's next draw equals this one's, into its own
    /// buffer, and the two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public RandomizedLDS<TLds, TPrng> Clone() => new(this);

    /// <inheritdoc/>
    IRsg IRsg.Clone() => Clone();

    private void NextRandomizerCore()
    {
        Sample<ReadOnlyMemory<double>> draw = prsg.NextSequence();
        draw.Value.Span.CopyTo(randomizer);
        randomizerWeight = draw.Weight;
    }
}
