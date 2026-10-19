// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/flatextrapolation2d.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A 2-D interpolation extrapolated flat: a decorator that reads another interpolation at the point moved onto its
/// grid's edges.
/// </summary>
/// <remarks>
/// Its grid, its function values and its locations are the decorated interpolation's. Outside the grid, each coordinate
/// is moved to the nearer end of its range, and the decorated interpolation is read there without extrapolation, so a
/// NaN coordinate, which no move puts in range, is refused unless the decorated interpolation allows extrapolation. Its
/// own range check is the base's: enable extrapolation on it, or pass <c>allowExtrapolation</c>, to read outside the
/// grid.
/// </remarks>
public sealed class FlatExtrapolator2D : Interpolation2D
{
    private readonly Interpolation2D decoratedInterpolation;

    /// <summary>Initializes a new instance of the <see cref="FlatExtrapolator2D"/> class.</summary>
    /// <param name="decoratedInterpolation">The interpolation it reads.</param>
    /// <exception cref="ArgumentNullException"><paramref name="decoratedInterpolation"/> is null.</exception>
    public FlatExtrapolator2D(Interpolation2D decoratedInterpolation)
        : base(decoratedInterpolation ?? throw new ArgumentNullException(nameof(decoratedInterpolation)))
    {
        this.decoratedInterpolation = decoratedInterpolation;
    }

    /// <inheritdoc/>
    protected override double ValueCore(double x, double y)
    {
        x = BindX(x);
        y = BindY(y);
        return decoratedInterpolation.Evaluate(x, y);
    }

    private double BindX(double x)
    {
        if (x < XMin)
        {
            return XMin;
        }

        if (x > XMax)
        {
            return XMax;
        }

        return x;
    }

    private double BindY(double y)
    {
        if (y < YMin)
        {
            return YMin;
        }

        if (y > YMax)
        {
            return YMax;
        }

        return y;
    }
}
