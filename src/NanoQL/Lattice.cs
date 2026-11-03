// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/numericalmethod.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL;

/// <summary>The lattice (tree, finite-differences) base class: a numerical method that rolls discretized assets back.</summary>
/// <remarks>
/// <para>
/// A lattice is immutable once built, and thread-safe, so one lattice can be built once and passed to many
/// calculations. The assets it rolls back are working types (<see cref="DiscretizedAsset"/>), owned by one
/// calculation: the lattice's members change the asset they are given, never the lattice.
/// </para>
/// <para>
/// The numerical method interface is used by discretized assets and implemented by developers writing numerical
/// methods; users call the corresponding members of <see cref="DiscretizedAsset"/> instead. An extension point, as in
/// QuantLib: the tree lattices derive from it.
/// </para>
/// </remarks>
public abstract class Lattice
{
    /// <summary>Initializes a new instance of the <see cref="Lattice"/> class.</summary>
    /// <param name="timeGrid">The lattice's time grid.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeGrid"/> is null.</exception>
    protected Lattice(TimeGrid timeGrid)
    {
        ArgumentNullException.ThrowIfNull(timeGrid);
        TimeGrid = timeGrid;
    }

    /// <summary>Gets the lattice's time grid.</summary>
    public TimeGrid TimeGrid { get; }

    /// <summary>Initializes an asset at the given time.</summary>
    /// <param name="asset">The asset, which changes.</param>
    /// <param name="time">The time.</param>
    public abstract void Initialize(DiscretizedAsset asset, double time);

    /// <summary>Rolls an asset back until the given time, performing any needed adjustment.</summary>
    /// <param name="asset">The asset, which changes.</param>
    /// <param name="to">The time to roll back to.</param>
    public abstract void Rollback(DiscretizedAsset asset, double to);

    /// <summary>Rolls an asset back until the given time, but doesn't perform the final adjustment.</summary>
    /// <param name="asset">The asset, which changes.</param>
    /// <param name="to">The time to roll back to.</param>
    public abstract void PartialRollback(DiscretizedAsset asset, double to);

    /// <summary>Computes the present value of an asset.</summary>
    /// <param name="asset">The asset.</param>
    /// <returns>The present value.</returns>
    public abstract double PresentValue(DiscretizedAsset asset);

    /// <summary>The lattice's values of the underlying variable at a time.</summary>
    /// <param name="t">The time.</param>
    /// <returns>A new vector, one value per node.</returns>
    public abstract Vector Grid(double t);

    /// <summary>
    /// The lattice's values of the underlying variable at a time, written into the caller's span, so that a working
    /// asset reads them at each step without allocating. This default copies <see cref="Grid(double)"/>; a lattice that
    /// can write its nodes directly overrides it.
    /// </summary>
    /// <param name="t">The time.</param>
    /// <param name="destination">The span the values go to, at least one per node.</param>
    /// <returns>The number of values written, one per node.</returns>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than the grid.</exception>
    public virtual int Grid(double t, Span<double> destination)
    {
        Vector grid = Grid(t);
        if (!(destination.Length >= grid.Size))
        {
            throw new ArgumentException($"a destination of {grid.Size} values needed, not {destination.Length}", nameof(destination));
        }

        grid.AsSpan().CopyTo(destination);
        return grid.Size;
    }
}
