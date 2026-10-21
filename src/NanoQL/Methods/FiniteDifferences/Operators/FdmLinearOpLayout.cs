// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmlinearoplayout.hpp ql/methods/finitedifferences/operators/fdmlinearoplayout.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Methods;

/// <summary>
/// The memory layout of a finite-difference operator's points: the number of points in each direction, the first
/// direction contiguous, each later one spaced by the product of the earlier dimensions.
/// </summary>
/// <remarks>Immutable once built (the S-eq plan's SEQR1).</remarks>
public sealed class FdmLinearOpLayout
{
    private readonly ImmutableArray<int> dim;
    private readonly ImmutableArray<int> spacing;

    /// <summary>Initializes a new instance of the <see cref="FdmLinearOpLayout"/> class.</summary>
    /// <param name="dim">The number of points in each direction.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dim"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is negative.</exception>
    /// <exception cref="ArgumentException">
    /// No dimension is given, where QuantLib writes the first spacing of an empty vector, undefined.
    /// </exception>
    public FdmLinearOpLayout(IEnumerable<int> dim)
    {
        ArgumentNullException.ThrowIfNull(dim);
        this.dim = [.. dim];
        foreach (int d in this.dim)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(d, nameof(dim));
        }

        // QuantLib's spacing_[0] = 1 on an empty vector, then spacing_.back(), is undefined
        if (this.dim.IsEmpty)
        {
            throw new ArgumentException("a layout needs at least one dimension", nameof(dim));
        }

        int[] s = new int[this.dim.Length];
        s[0] = 1;
        for (int i = 1; i < s.Length; ++i)
        {
            s[i] = s[i - 1] * this.dim[i - 1];
        }

        spacing = [.. s];
        Size = spacing[^1] * this.dim[^1];
    }

    /// <summary>Gets the number of points in each direction.</summary>
    public ImmutableArray<int> Dim => dim;

    /// <summary>Gets each direction's spacing in storage order.</summary>
    public ImmutableArray<int> Spacing => spacing;

    /// <summary>Gets the number of points.</summary>
    public int Size { get; }

    /// <summary>A new iterator at the first point: QuantLib's <c>begin()</c>.</summary>
    /// <returns>The iterator.</returns>
    public FdmLinearOpIterator Begin() => new(dim);

    /// <summary>The iterator past the last point: QuantLib's <c>end()</c>, an index without coordinates.</summary>
    /// <returns>The iterator.</returns>
    public FdmLinearOpIterator End() => new(Size);

    /// <summary>The index of a point in storage order.</summary>
    /// <param name="coordinates">The point's coordinates.</param>
    /// <returns>The index.</returns>
    public int Index(ReadOnlySpan<int> coordinates)
    {
        // std::inner_product over the coordinates, with the spacing
        int result = 0;
        for (int i = 0; i < coordinates.Length; ++i)
        {
            result += coordinates[i] * spacing[i];
        }

        return result;
    }

    /// <summary>
    /// The index of a point's neighbour <paramref name="offset"/> steps away in direction <paramref name="i"/>,
    /// reflected at the grid's edges.
    /// </summary>
    /// <param name="iterator">The point.</param>
    /// <param name="i">The direction.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The neighbour's index.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="iterator"/> is <see langword="null"/>.</exception>
    public int Neighbourhood(FdmLinearOpIterator iterator, int i, int offset)
    {
        ArgumentNullException.ThrowIfNull(iterator);
        ReadOnlySpan<int> coordinates = iterator.Coordinates;
        int myIndex = iterator.Index - (coordinates[i] * spacing[i]);

        int coorOffset = coordinates[i] + offset;
        if (coorOffset < 0)
        {
            coorOffset = -coorOffset;
        }
        else if (coorOffset >= dim[i])
        {
            coorOffset = (2 * (dim[i] - 1)) - coorOffset;
        }

        return myIndex + (coorOffset * spacing[i]);
    }

    /// <summary>
    /// The index of a point's neighbour <paramref name="offset1"/> steps away in direction <paramref name="i1"/> and
    /// <paramref name="offset2"/> in direction <paramref name="i2"/>, reflected at the grid's edges.
    /// </summary>
    /// <param name="iterator">The point.</param>
    /// <param name="i1">The first direction.</param>
    /// <param name="offset1">The offset in the first direction.</param>
    /// <param name="i2">The second direction.</param>
    /// <param name="offset2">The offset in the second direction.</param>
    /// <returns>The neighbour's index.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="iterator"/> is <see langword="null"/>.</exception>
    public int Neighbourhood(FdmLinearOpIterator iterator, int i1, int offset1, int i2, int offset2)
    {
        ArgumentNullException.ThrowIfNull(iterator);
        ReadOnlySpan<int> coordinates = iterator.Coordinates;
        int myIndex = iterator.Index
            - (coordinates[i1] * spacing[i1])
            - (coordinates[i2] * spacing[i2]);

        int coorOffset1 = coordinates[i1] + offset1;
        if (coorOffset1 < 0)
        {
            coorOffset1 = -coorOffset1;
        }
        else if (coorOffset1 >= dim[i1])
        {
            coorOffset1 = (2 * (dim[i1] - 1)) - coorOffset1;
        }

        int coorOffset2 = coordinates[i2] + offset2;
        if (coorOffset2 < 0)
        {
            coorOffset2 = -coorOffset2;
        }
        else if (coorOffset2 >= dim[i2])
        {
            coorOffset2 = (2 * (dim[i2] - 1)) - coorOffset2;
        }

        return myIndex + (coorOffset1 * spacing[i1]) + (coorOffset2 * spacing[i2]);
    }

    /// <summary>
    /// A new iterator at a point's neighbour <paramref name="offset"/> steps away in direction <paramref name="i"/>,
    /// reflected at the grid's edges.
    /// </summary>
    /// <param name="iterator">The point.</param>
    /// <param name="i">The direction.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The neighbour's iterator.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="iterator"/> is <see langword="null"/>.</exception>
    public FdmLinearOpIterator IterNeighbourhood(FdmLinearOpIterator iterator, int i, int offset)
    {
        ArgumentNullException.ThrowIfNull(iterator);
        int[] coordinates = iterator.Coordinates.ToArray();

        int coorOffset = coordinates[i] + offset;
        if (coorOffset < 0)
        {
            coorOffset = -coorOffset;
        }
        else if (coorOffset >= dim[i])
        {
            coorOffset = (2 * (dim[i] - 1)) - coorOffset;
        }

        coordinates[i] = coorOffset;

        return new FdmLinearOpIterator(dim, coordinates, Index(coordinates));
    }
}
