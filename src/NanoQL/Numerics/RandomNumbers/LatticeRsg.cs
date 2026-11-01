// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/latticersg.hpp ql/math/randomnumbers/latticersg.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Methods;

namespace NanoQL.Numerics;

/// <summary>
/// The rank-1 lattice-rule low-discrepancy sequence generator: its i-th point is (i·z_j/N mod 1) in each dimension j,
/// z a generating vector (<see cref="LatticeRule"/>) and N the number of points.
/// </summary>
/// <remarks>
/// <para>
/// A mutable working type: each draw changes it, and it is not thread-safe. A draw returns a view of its buffer, valid
/// until its next draw.
/// </para>
/// <para>
/// Two departures from QuantLib's, each documented: <see cref="SkipTo"/> moves to the n-th point, as QuantLib's other
/// sequences' <c>skipTo</c> do, where QuantLib's moves n points on (the S-core plan's CR7 (xii);
/// <c>Divergences/LatticeRsgTests.cs</c>); and a dimension beyond the generating vector's, which QuantLib reads past
/// its end, and a number of points below 1, a division by 0, are refused (CR7 (xxvii)).
/// </para>
/// </remarks>
[WorkingType]
public sealed class LatticeRsg : IRsg
{
    private readonly int dimensionality;
    private readonly long n;
    private readonly ImmutableArray<double> z;
    private readonly double[] sequence;
    private long i;

    /// <summary>Initializes a new instance of the <see cref="LatticeRsg"/> class.</summary>
    /// <param name="dimensionality">The dimension of each point, from 0 to the generating vector's length.</param>
    /// <param name="z">The generating vector.</param>
    /// <param name="n">The number of points, at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dimensionality"/> is negative or beyond <paramref name="z"/>'s length, or <paramref name="n"/>
    /// is below 1.
    /// </exception>
    public LatticeRsg(int dimensionality, ImmutableArray<double> z, long n)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dimensionality);
        if (z.IsDefault || dimensionality > z.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dimensionality),
                dimensionality,
                $"the dimension ({dimensionality}) exceeds the generating vector's ({(z.IsDefault ? 0 : z.Length)})");
        }

        if (n < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(n), n, $"the number of points ({n}) must be at least 1");
        }

        this.dimensionality = dimensionality;
        this.z = z;
        this.n = n;
        sequence = new double[dimensionality];
    }

    private LatticeRsg(LatticeRsg source)
    {
        dimensionality = source.dimensionality;
        n = source.n;
        z = source.z;
        sequence = (double[])source.sequence.Clone();
        i = source.i;
    }

    /// <summary>Gets the dimension of each sequence.</summary>
    public int Dimension => dimensionality;

    /// <summary>Skips to the n-th point of the sequence: the next draw is the point of index
    /// <paramref name="index"/>.</summary>
    /// <param name="index">The point's index, from 0.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
    public void SkipTo(long index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        i = index;
    }

    /// <summary>Draws the next point.</summary>
    /// <returns>The point, a view of the generator's buffer valid until its next draw, and its weight, 1.</returns>
    public Sample<ReadOnlyMemory<double>> NextSequence()
    {
        for (int j = 0; j < dimensionality; ++j)
        {
            double theta = i * z[j] / n;
            sequence[j] = theta % 1.0;
        }

        ++i;
        return new(sequence, 1.0);
    }

    /// <summary>Gets the point the last draw gave.</summary>
    /// <returns>The point, a view of the generator's buffer valid until its next draw, and its weight, 1.</returns>
    public Sample<ReadOnlyMemory<double>> LastSequence() => new(sequence, 1.0);

    /// <summary>
    /// Copies the generator deeply: the copy's next draw equals this one's, into its own buffer, and the two change
    /// apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public LatticeRsg Clone() => new(this);

    /// <inheritdoc/>
    IRsg IRsg.Clone() => Clone();
}
