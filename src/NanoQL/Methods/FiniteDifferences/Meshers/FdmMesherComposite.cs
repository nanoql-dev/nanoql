// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/meshers/fdmmeshercomposite.hpp ql/methods/finitedifferences/meshers/fdmmeshercomposite.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>A mesher composed of one one-dimensional mesher per direction.</summary>
/// <remarks>Immutable once built.</remarks>
public sealed class FdmMesherComposite : FdmMesher
{
    private readonly ImmutableArray<Fdm1dMesher> mesher;

    /// <summary>Initializes a new instance of the <see cref="FdmMesherComposite"/> class on a given layout.</summary>
    /// <param name="layout">The layout.</param>
    /// <param name="mesher">The one-dimensional meshers, one per direction.</param>
    /// <exception cref="ArgumentNullException">An argument or a mesher is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A mesher's size isn't its direction's dimension, or there are more meshers than dimensions, where QuantLib
    /// reads the layout's dimensions past their end.
    /// </exception>
    public FdmMesherComposite(FdmLinearOpLayout layout, IEnumerable<Fdm1dMesher> mesher)
        : base(layout)
    {
        this.mesher = Meshers(mesher);

        // QuantLib reads layout->dim()[i] past its end
        if (this.mesher.Length > layout.Dim.Length)
        {
            throw new ArgumentException(
                $"more 1d meshers ({this.mesher.Length}) than the layout's dimensions ({layout.Dim.Length})",
                nameof(mesher));
        }

        for (int i = 0; i < this.mesher.Length; ++i)
        {
            if (!(this.mesher[i].Size == layout.Dim[i]))
            {
                throw new ArgumentException($"size of 1d mesher {i} does not fit to layout", nameof(mesher));
            }
        }
    }

    /// <summary>Initializes a new instance of the <see cref="FdmMesherComposite"/> class, its layout the meshers'
    /// sizes.</summary>
    /// <param name="mesher">The one-dimensional meshers, one per direction.</param>
    /// <exception cref="ArgumentNullException">The meshers, or one of them, is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">No mesher is given.</exception>
    public FdmMesherComposite(IEnumerable<Fdm1dMesher> mesher)
        : this(Meshers(mesher))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FdmMesherComposite"/> class in one direction.</summary>
    /// <param name="mesher">The mesher.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mesher"/> is <see langword="null"/>.</exception>
    public FdmMesherComposite(Fdm1dMesher mesher)
        : this(Meshers([mesher]))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FdmMesherComposite"/> class in two directions.</summary>
    /// <param name="m1">The first direction's mesher.</param>
    /// <param name="m2">The second direction's mesher.</param>
    /// <exception cref="ArgumentNullException">A mesher is <see langword="null"/>.</exception>
    public FdmMesherComposite(Fdm1dMesher m1, Fdm1dMesher m2)
        : this(Meshers([m1, m2]))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FdmMesherComposite"/> class in three directions.</summary>
    /// <param name="m1">The first direction's mesher.</param>
    /// <param name="m2">The second direction's mesher.</param>
    /// <param name="m3">The third direction's mesher.</param>
    /// <exception cref="ArgumentNullException">A mesher is <see langword="null"/>.</exception>
    public FdmMesherComposite(Fdm1dMesher m1, Fdm1dMesher m2, Fdm1dMesher m3)
        : this(Meshers([m1, m2, m3]))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FdmMesherComposite"/> class in four directions.</summary>
    /// <param name="m1">The first direction's mesher.</param>
    /// <param name="m2">The second direction's mesher.</param>
    /// <param name="m3">The third direction's mesher.</param>
    /// <param name="m4">The fourth direction's mesher.</param>
    /// <exception cref="ArgumentNullException">A mesher is <see langword="null"/>.</exception>
    public FdmMesherComposite(Fdm1dMesher m1, Fdm1dMesher m2, Fdm1dMesher m3, Fdm1dMesher m4)
        : this(Meshers([m1, m2, m3, m4]))
    {
    }

    private FdmMesherComposite(ImmutableArray<Fdm1dMesher> mesher)
        : base(LayoutOf(mesher))
    {
        this.mesher = mesher;
    }

    /// <inheritdoc/>
    public override double Dplus(FdmLinearOpIterator iter, int direction)
    {
        ArgumentNullException.ThrowIfNull(iter);
        return mesher[direction].Dplus(iter.Coordinates[direction]);
    }

    /// <inheritdoc/>
    public override double Dminus(FdmLinearOpIterator iter, int direction)
    {
        ArgumentNullException.ThrowIfNull(iter);
        return mesher[direction].Dminus(iter.Coordinates[direction]);
    }

    /// <inheritdoc/>
    public override double Location(FdmLinearOpIterator iter, int direction)
    {
        ArgumentNullException.ThrowIfNull(iter);
        return mesher[direction].Location(iter.Coordinates[direction]);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="direction"/> isn't one of the mesher's, where QuantLib reads past its meshers.
    /// </exception>
    public override Vector Locations(int direction)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(direction);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(direction, mesher.Length);
        var retVal = new Vector(Layout.Size);
        Fdm1dMesher m = mesher[direction];
        FdmLinearOpIterator iter = Layout.Begin();
        for (int i = 0; i < Layout.Size; ++i, iter.Increment())
        {
            retVal[iter.Index] = m.Location(iter.Coordinates[direction]);
        }

        return retVal;
    }

    /// <summary>The one-dimensional meshers, one per direction.</summary>
    /// <returns>The meshers.</returns>
    public ImmutableArray<Fdm1dMesher> GetFdm1dMeshers() => mesher;

    private static ImmutableArray<Fdm1dMesher> Meshers(IEnumerable<Fdm1dMesher> mesher)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ImmutableArray<Fdm1dMesher> m = [.. mesher];
        if (m.Any(x => x is null))
        {
            throw new ArgumentNullException(nameof(mesher), "a 1d mesher is null");
        }

        return m;
    }

    private static FdmLinearOpLayout LayoutOf(ImmutableArray<Fdm1dMesher> meshers) =>
        new(meshers.Select(m => m.Size));
}
