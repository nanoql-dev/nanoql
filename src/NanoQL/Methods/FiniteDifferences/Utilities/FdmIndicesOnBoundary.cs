// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/fdmindicesonboundary.hpp ql/methods/finitedifferences/utilities/fdmindicesonboundary.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Methods;

/// <summary>The indices of a layout's points on one side of one direction: the grid's boundary layer there.</summary>
/// <remarks>
/// Immutable; the indices are fixed at construction. A side of <see cref="BoundaryConditionSide.None"/> matches no
/// point, so its indices are QuantLib's: the hyperplane's count of zeros.
/// </remarks>
public sealed class FdmIndicesOnBoundary
{
    private readonly ImmutableArray<int> indices;

    /// <summary>Initializes a new instance of the <see cref="FdmIndicesOnBoundary"/> class.</summary>
    /// <param name="layout">The layout.</param>
    /// <param name="direction">The direction.</param>
    /// <param name="side">The side.</param>
    /// <exception cref="ArgumentNullException"><paramref name="layout"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The direction isn't one of the layout's, where QuantLib writes past its vector's end.
    /// </exception>
    public FdmIndicesOnBoundary(FdmLinearOpLayout layout, int direction, BoundaryConditionSide side)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (direction < 0 || direction >= layout.Dim.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(direction), direction, $"direction ({direction}) out of range [0, {layout.Dim.Length})");
        }

        int[] newDim = [.. layout.Dim];
        newDim[direction] = 1;
        int hyperSize = 1;
        foreach (int d in newDim)
        {
            hyperSize *= d;
        }

        var result = new int[hyperSize];
        int i = 0;
        FdmLinearOpIterator iter = layout.Begin();
        for (int k = 0; k < layout.Size; ++k, iter.Increment())
        {
            if ((side == BoundaryConditionSide.Lower && iter.Coordinates[direction] == 0)
                || (side == BoundaryConditionSide.Upper && iter.Coordinates[direction] == layout.Dim[direction] - 1))
            {
                if (!(hyperSize > i))
                {
                    throw new NanoQLException("index missmatch");
                }

                result[i++] = iter.Index;
            }
        }

        indices = [.. result];
    }

    /// <summary>Gets the indices.</summary>
    public ImmutableArray<int> Indices => indices;
}
