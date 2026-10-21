// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/meshers/fdmmesher.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The mesher of a finite-difference grid: each point's location and its distances to its neighbours, in each
/// direction of a layout.
/// </summary>
/// <remarks>
/// Immutable once built (the S-eq plan's SEQR1). An extension point, as in QuantLib: later units derive their meshers
/// from it.
/// </remarks>
public abstract class FdmMesher
{
    /// <summary>Initializes a new instance of the <see cref="FdmMesher"/> class.</summary>
    /// <param name="layout">The layout of the grid's points.</param>
    /// <exception cref="ArgumentNullException"><paramref name="layout"/> is <see langword="null"/>.</exception>
    protected FdmMesher(FdmLinearOpLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        Layout = layout;
    }

    /// <summary>Gets the layout of the grid's points.</summary>
    public FdmLinearOpLayout Layout { get; }

    /// <summary>The distance from a point to its upper neighbour in a direction.</summary>
    /// <param name="iter">The point.</param>
    /// <param name="direction">The direction.</param>
    /// <returns>The distance; QuantLib's <c>Null&lt;Real&gt;()</c> at the upper edge.</returns>
    public abstract double Dplus(FdmLinearOpIterator iter, int direction);

    /// <summary>The distance from a point to its lower neighbour in a direction.</summary>
    /// <param name="iter">The point.</param>
    /// <param name="direction">The direction.</param>
    /// <returns>The distance; QuantLib's <c>Null&lt;Real&gt;()</c> at the lower edge.</returns>
    public abstract double Dminus(FdmLinearOpIterator iter, int direction);

    /// <summary>A point's location in a direction.</summary>
    /// <param name="iter">The point.</param>
    /// <param name="direction">The direction.</param>
    /// <returns>The location.</returns>
    public abstract double Location(FdmLinearOpIterator iter, int direction);

    /// <summary>Every point's location in a direction, in storage order.</summary>
    /// <param name="direction">The direction.</param>
    /// <returns>A new vector of the layout's size.</returns>
    public abstract Vector Locations(int direction);
}
