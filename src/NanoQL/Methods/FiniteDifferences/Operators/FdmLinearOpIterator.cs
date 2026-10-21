// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmlinearopiterator.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>
/// An iterator over a finite-difference layout's points: the point's index in storage order and its coordinate in each
/// direction, the first direction running fastest.
/// </summary>
/// <remarks>
/// A working type (the S-eq plan's SEQD17): mutable, owned by one computation and not thread-safe.
/// <see cref="Increment"/> advances it in place, as QuantLib's <c>operator++</c> does; <see cref="Reset"/> returns it
/// to the first point, so that a walk repeated every time step allocates nothing. <see cref="FdmLinearOpLayout.Begin"/>
/// returns a new one, as QuantLib's <c>begin()</c> does.
/// </remarks>
public sealed class FdmLinearOpIterator
{
    private readonly int[] dim;
    private readonly int[] coordinates;
    private int index;

    /// <summary>Initializes a new instance of the <see cref="FdmLinearOpIterator"/> class at an index alone.</summary>
    /// <param name="index">The index; the iterator has no coordinates, as QuantLib's end iterator has none.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
    public FdmLinearOpIterator(int index = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        this.index = index;
        dim = [];
        coordinates = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FdmLinearOpIterator"/> class at the first point of a layout of the
    /// given dimensions.
    /// </summary>
    /// <param name="dim">The number of points in each direction.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dim"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is negative.</exception>
    public FdmLinearOpIterator(IEnumerable<int> dim)
    {
        ArgumentNullException.ThrowIfNull(dim);
        this.dim = [.. dim];
        foreach (int d in this.dim)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(d, nameof(dim));
        }

        coordinates = new int[this.dim.Length];
        index = 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FdmLinearOpIterator"/> class at the given coordinates and index.
    /// </summary>
    /// <param name="dim">The number of points in each direction.</param>
    /// <param name="coordinates">The coordinates, one per direction.</param>
    /// <param name="index">The index the coordinates have in storage order.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A dimension, a coordinate or the index is negative.</exception>
    public FdmLinearOpIterator(IEnumerable<int> dim, IEnumerable<int> coordinates, int index)
    {
        ArgumentNullException.ThrowIfNull(dim);
        ArgumentNullException.ThrowIfNull(coordinates);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        this.dim = [.. dim];
        this.coordinates = [.. coordinates];
        foreach (int d in this.dim)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(d, nameof(dim));
        }

        foreach (int c in this.coordinates)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(c, nameof(coordinates));
        }

        this.index = index;
    }

    /// <summary>Gets the point's index in storage order.</summary>
    public int Index => index;

    /// <summary>Gets the point's coordinates, one per direction.</summary>
    public ReadOnlySpan<int> Coordinates => coordinates;

    /// <summary>Advances the iterator to the next point: QuantLib's <c>operator++</c>.</summary>
    public void Increment()
    {
        ++index;
        for (int i = 0; i < dim.Length; ++i)
        {
            if (++coordinates[i] == dim[i])
            {
                coordinates[i] = 0;
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>Returns the iterator to the first point, its index and coordinates zero.</summary>
    public void Reset()
    {
        index = 0;
        Array.Clear(coordinates);
    }

    /// <summary>Whether two iterators are at the same index: QuantLib's <c>operator==</c>.</summary>
    /// <param name="other">The other iterator.</param>
    /// <returns><see langword="true"/> if the indices are equal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    public bool IsAtSameIndex(FdmLinearOpIterator other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return index == other.index;
    }
}
