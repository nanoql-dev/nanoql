// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/meshers/fdm1dmesher.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Methods;

/// <summary>
/// A one-dimensional mesher: the locations of its points and each point's distances to its neighbours, by index.
/// </summary>
/// <remarks>
/// <para>
/// Immutable once built. An extension point, as in QuantLib: a derived mesher fills the protected
/// <see cref="locations"/>, <see cref="dplus"/> and <see cref="dminus"/> in its constructor, and never after.
/// </para>
/// <para>
/// The distance past an edge is QuantLib's <c>Null&lt;Real&gt;()</c>, 3.4028234663852886e38: the derivative operators
/// read it there, as QuantLib's do, and their edge rows don't use it.
/// </para>
/// </remarks>
public class Fdm1dMesher
{
    /// <summary>QuantLib's <c>Null&lt;Real&gt;()</c>, the distance past an edge.</summary>
    protected const double NullReal = 3.4028234663852886e38;

    /// <summary>The points' locations, of the mesher's size; written by a derived constructor alone.</summary>
    protected readonly double[] locations;

    /// <summary>Each point's distance to its upper neighbour; written by a derived constructor alone.</summary>
    protected readonly double[] dplus;

    /// <summary>Each point's distance to its lower neighbour; written by a derived constructor alone.</summary>
    protected readonly double[] dminus;

    /// <summary>Initializes a new instance of the <see cref="Fdm1dMesher"/> class of the given size.</summary>
    /// <param name="size">The number of points.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    public Fdm1dMesher(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        locations = new double[size];
        dplus = new double[size];
        dminus = new double[size];
    }

    /// <summary>Gets the number of points.</summary>
    public int Size => locations.Length;

    /// <summary>The distance from a point to its upper neighbour.</summary>
    /// <param name="index">The point's index.</param>
    /// <returns>The distance.</returns>
    public double Dplus(int index) => dplus[index];

    /// <summary>The distance from a point to its lower neighbour.</summary>
    /// <param name="index">The point's index.</param>
    /// <returns>The distance.</returns>
    public double Dminus(int index) => dminus[index];

    /// <summary>A point's location.</summary>
    /// <param name="index">The point's index.</param>
    /// <returns>The location.</returns>
    public double Location(int index) => locations[index];

    /// <summary>The points' locations.</summary>
    /// <returns>A copy of the locations.</returns>
    public ImmutableArray<double> Locations() => [.. locations];

    /// <summary>Refuses fewer than two points, where QuantLib's meshers read past their arrays or leave a distance
    /// unset.</summary>
    /// <param name="size">The number of points.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentException"><paramref name="size"/> is below 2.</exception>
    private protected static void RequireTwoPoints(int size, string paramName)
    {
        if (size < 2)
        {
            throw new ArgumentException($"a mesher needs at least two points, not {size}", paramName);
        }
    }
}
