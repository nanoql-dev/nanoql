// SPDX-FileCopyrightText: Copyright (C) 2012 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/glued1dmesher.hpp ql/experimental/finitedifferences/glued1dmesher.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A one-dimensional mesher joining two: the left mesher's points, then the right one's, a point the two share kept
/// once.
/// </summary>
/// <remarks>Immutable, as every mesher. Experimental, as in QuantLib.</remarks>
[Experimental("NQL0001")]
public sealed class Glued1dMesher : Fdm1dMesher
{
    /// <summary>Initializes a new instance of the <see cref="Glued1dMesher"/> class.</summary>
    /// <param name="leftMesher">The left mesher.</param>
    /// <param name="rightMesher">The right mesher, starting at or after the left one's last point.</param>
    /// <exception cref="ArgumentNullException">A mesher is null.</exception>
    /// <exception cref="ArgumentException">A mesher has no points, which QuantLib reads unchecked, or the left one's
    /// last point is after the right one's first.</exception>
    public Glued1dMesher(Fdm1dMesher leftMesher, Fdm1dMesher rightMesher)
        : base(GluedSize(leftMesher, rightMesher))
    {
        bool commonPoint = ComparisonFunctions.Close(leftMesher.Location(leftMesher.Size - 1), rightMesher.Location(0));

        if (!(leftMesher.Location(leftMesher.Size - 1) <= rightMesher.Location(0)))
        {
            throw new ArgumentException(
                $"left meshers rightmost point ({Utilities.DataFormattersFunctions.General(leftMesher.Location(leftMesher.Size - 1))}) may not be greater "
                    + $"than right meshers leftmost point ({Utilities.DataFormattersFunctions.General(rightMesher.Location(0))})",
                nameof(rightMesher));
        }

        for (int i = 0; i < leftMesher.Size; ++i)
        {
            locations[i] = leftMesher.Location(i);
        }

        for (int i = commonPoint ? 1 : 0, j = leftMesher.Size; i < rightMesher.Size; ++i, ++j)
        {
            locations[j] = rightMesher.Location(i);
        }

        for (int i = 0; i < locations.Length - 1; ++i)
        {
            dplus[i] = dminus[i + 1] = locations[i + 1] - locations[i];
        }

        dplus[^1] = dminus[0] = NullReal;
    }

    // QuantLib's size: the two meshers' points, a common one counted once.
    private static int GluedSize(Fdm1dMesher leftMesher, Fdm1dMesher rightMesher)
    {
        ArgumentNullException.ThrowIfNull(leftMesher);
        ArgumentNullException.ThrowIfNull(rightMesher);
        if (leftMesher.Size == 0)
        {
            throw new ArgumentException("the left mesher has no points", nameof(leftMesher));
        }

        if (rightMesher.Size == 0)
        {
            throw new ArgumentException("the right mesher has no points", nameof(rightMesher));
        }

        bool commonPoint = ComparisonFunctions.Close(leftMesher.Location(leftMesher.Size - 1), rightMesher.Location(0));
        return leftMesher.Size + rightMesher.Size - (commonPoint ? 1 : 0);
    }
}
