// SPDX-FileCopyrightText: Copyright (C) 2013 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/meshers/predefined1dmesher.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>A one-dimensional mesher on given points.</summary>
/// <remarks>Immutable. The points are taken as given, in order; QuantLib checks neither their order nor their spacing.</remarks>
public sealed class Predefined1dMesher : Fdm1dMesher
{
    /// <summary>Initializes a new instance of the <see cref="Predefined1dMesher"/> class.</summary>
    /// <param name="x">The points' locations, in order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="x"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="x"/> is empty, where QuantLib writes past an empty mesher's
    /// arrays.</exception>
    public Predefined1dMesher(IEnumerable<double> x)
        : base(SizeOf(x, out double[] points))
    {
        points.CopyTo(locations, 0);

        dplus[^1] = dminus[0] = NullReal;
        for (int i = 0; i < points.Length - 1; ++i)
        {
            dplus[i] = dminus[i + 1] = points[i + 1] - points[i];
        }
    }

    private static int SizeOf(IEnumerable<double> x, out double[] points)
    {
        ArgumentNullException.ThrowIfNull(x);
        points = [.. x];
        if (points.Length == 0)
        {
            throw new ArgumentException("a mesher needs at least one point", nameof(x));
        }

        return points.Length;
    }
}
