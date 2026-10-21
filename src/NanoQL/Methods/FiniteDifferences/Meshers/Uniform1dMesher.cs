// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/meshers/uniform1dmesher.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>A one-dimensional mesher of equally spaced points from a start to an end.</summary>
/// <remarks>Immutable.</remarks>
public sealed class Uniform1dMesher : Fdm1dMesher
{
    /// <summary>Initializes a new instance of the <see cref="Uniform1dMesher"/> class.</summary>
    /// <param name="start">The first point.</param>
    /// <param name="end">The last point.</param>
    /// <param name="size">The number of points, at least 2.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="end"/> isn't above <paramref name="start"/>, or <paramref name="size"/> is 0 or 1: QuantLib
    /// writes past an empty mesher's arrays, and leaves a one-point mesher's distances at its null value.
    /// </exception>
    public Uniform1dMesher(double start, double end, int size)
        : base(size)
    {
        if (!(end > start))
        {
            throw new ArgumentException("end must be larger than start", nameof(end));
        }

        // QuantLib's size-1 wraps at 0, and its loop writes an empty vector; at 1, dplus and dminus keep Null<Real>()
        RequireTwoPoints(size, nameof(size));

        double dx = (end - start) / (size - 1);

        for (int i = 0; i < size - 1; ++i)
        {
            locations[i] = start + (i * dx);
            dplus[i] = dminus[i + 1] = dx;
        }

        locations[^1] = end;
        dplus[^1] = dminus[0] = NullReal;
    }
}
