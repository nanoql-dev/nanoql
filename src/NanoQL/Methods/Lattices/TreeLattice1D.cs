// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/lattices/lattice1d.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>A one-dimensional tree-based lattice: one underlying variable, whose value at each node it gives.</summary>
/// <remarks>
/// A derived class implements <see cref="Underlying"/> beside <see cref="TreeLattice"/>'s members. An extension point,
/// as in QuantLib: the one-factor short-rate tree derives from it, as later ports' Black-Scholes lattices will.
/// </remarks>
public abstract class TreeLattice1D : TreeLattice
{
    /// <summary>Initializes a new instance of the <see cref="TreeLattice1D"/> class.</summary>
    /// <param name="timeGrid">The lattice's time grid.</param>
    /// <param name="n">The number of branches from each node.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeGrid"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="n"/> is 0.</exception>
    protected TreeLattice1D(TimeGrid timeGrid, int n)
        : base(timeGrid, n)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TreeLattice1D"/> class that shares another's stored state prices.</summary>
    /// <param name="other">The lattice whose grid, branches and stored state prices the new one takes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    protected TreeLattice1D(TreeLattice1D other)
        : base(other)
    {
    }

    /// <summary>The underlying variable's value at each node of a time.</summary>
    /// <param name="t">The time, on the grid.</param>
    /// <returns>A new vector, one value per node.</returns>
    /// <exception cref="NanoQLException"><paramref name="t"/> isn't on the grid (the grid's own refusal).</exception>
    public override Vector Grid(double t)
    {
        int i = TimeGrid.Index(t);
        var grid = new Vector(Size(i));
        for (int j = 0; j < grid.Size; j++)
        {
            grid[j] = Underlying(i, j);
        }

        return grid;
    }

    /// <summary>
    /// The underlying variable's value at each node of a time, written into the caller's span without allocating.
    /// </summary>
    /// <param name="t">The time, on the grid.</param>
    /// <param name="destination">The span the values go to, at least one per node.</param>
    /// <returns>The number of values written, one per node.</returns>
    /// <exception cref="NanoQLException"><paramref name="t"/> isn't on the grid (the grid's own refusal).</exception>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than the grid.</exception>
    public override int Grid(double t, Span<double> destination)
    {
        int i = TimeGrid.Index(t);
        int size = Size(i);
        if (!(destination.Length >= size))
        {
            throw new ArgumentException($"a destination of {size} values needed, not {destination.Length}", nameof(destination));
        }

        for (int j = 0; j < size; j++)
        {
            destination[j] = Underlying(i, j);
        }

        return size;
    }

    /// <summary>The underlying variable's value at a node.</summary>
    /// <param name="i">The step's index on the grid.</param>
    /// <param name="index">The node's index at the step.</param>
    /// <returns>The value.</returns>
    public abstract double Underlying(int i, int index);
}
