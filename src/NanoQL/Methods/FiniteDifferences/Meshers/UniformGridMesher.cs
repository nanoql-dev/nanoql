// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/meshers/uniformgridmesher.hpp ql/methods/finitedifferences/meshers/uniformgridmesher.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>A uniform grid mesher: in each direction of a layout, its points evenly spaced between two
/// boundaries.</summary>
/// <remarks>Immutable (the S-eq plan's SEQR1).</remarks>
public sealed class UniformGridMesher : FdmMesher
{
    private readonly double[] dx;
    private readonly double[][] locations;

    /// <summary>Initializes a new instance of the <see cref="UniformGridMesher"/> class.</summary>
    /// <param name="layout">The layout of the grid's points.</param>
    /// <param name="boundaries">The lowest and highest point in each direction, one pair per direction.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There isn't one pair per direction, with QuantLib's message.</exception>
    public UniformGridMesher(FdmLinearOpLayout layout, IEnumerable<(double Min, double Max)> boundaries)
        : base(layout)
    {
        ArgumentNullException.ThrowIfNull(boundaries);
        (double Min, double Max)[] b = [.. boundaries];
        int dims = layout.Dim.Length;
        if (!(b.Length == dims))
        {
            throw new ArgumentException("inconsistent boundaries given", nameof(boundaries));
        }

        dx = new double[dims];
        locations = new double[dims][];
        for (int i = 0; i < dims; ++i)
        {
            dx[i] = (b[i].Max - b[i].Min) / (layout.Dim[i] - 1);

            locations[i] = new double[layout.Dim[i]];
            for (int j = 0; j < layout.Dim[i]; ++j)
            {
                locations[i][j] = b[i].Min + (j * dx[i]);
            }
        }
    }

    /// <inheritdoc/>
    public override double Dplus(FdmLinearOpIterator iter, int direction) => dx[direction];

    /// <inheritdoc/>
    public override double Dminus(FdmLinearOpIterator iter, int direction) => dx[direction];

    /// <inheritdoc/>
    public override double Location(FdmLinearOpIterator iter, int direction)
    {
        ArgumentNullException.ThrowIfNull(iter);
        return locations[direction][iter.Coordinates[direction]];
    }

    /// <inheritdoc/>
    public override Vector Locations(int direction)
    {
        var retVal = new Vector(Layout.Size);

        FdmLinearOpIterator iter = Layout.Begin();
        for (int i = 0; i < Layout.Size; ++i, iter.Increment())
        {
            retVal[iter.Index] = locations[direction][iter.Coordinates[direction]];
        }

        return retVal;
    }
}
