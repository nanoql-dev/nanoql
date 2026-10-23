// SPDX-FileCopyrightText: Copyright (C) 2026 Junjie Guo
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/flatextrapolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A 1-D interpolation extrapolated flat: a decorator that reads another interpolation inside its range and its
/// boundary values outside it.
/// </summary>
/// <remarks>
/// Its points and its range are the decorated interpolation's. Outside the range, the value is the nearer end's, the
/// first and second derivatives are 0, and the primitive grows by the end's value per unit of x. The decorated
/// interpolation is read with extrapolation allowed, so a NaN point, which no move puts in range, reads it at NaN. Its
/// own range check is the base's: enable extrapolation on it, or pass <c>allowExtrapolation</c>, to read outside the
/// range.
/// </remarks>
public sealed class FlatExtrapolator : Interpolation
{
    private readonly Interpolation decoratedInterpolation;

    /// <summary>Initializes a new instance of the <see cref="FlatExtrapolator"/> class.</summary>
    /// <param name="decoratedInterpolation">The interpolation it reads.</param>
    /// <exception cref="ArgumentNullException"><paramref name="decoratedInterpolation"/> is null.</exception>
    public FlatExtrapolator(Interpolation decoratedInterpolation)
        : base(Decorated(decoratedInterpolation).XValues().AsSpan(), decoratedInterpolation.YValues().AsSpan(), 1)
    {
        this.decoratedInterpolation = decoratedInterpolation;
    }

    /// <inheritdoc/>
    protected override double ValueCore(double x) => decoratedInterpolation.Evaluate(Bind(x), true);

    /// <inheritdoc/>
    protected override double PrimitiveCore(double x)
    {
        if (x < XMin)
        {
            return decoratedInterpolation.Primitive(XMin, true) + (decoratedInterpolation.Evaluate(XMin, true) * (x - XMin));
        }

        if (x > XMax)
        {
            return decoratedInterpolation.Primitive(XMax, true) + (decoratedInterpolation.Evaluate(XMax, true) * (x - XMax));
        }

        return decoratedInterpolation.Primitive(x, true);
    }

    /// <inheritdoc/>
    protected override double DerivativeCore(double x)
    {
        if (x < XMin || x > XMax)
        {
            return 0.0;
        }

        return decoratedInterpolation.Derivative(x, true);
    }

    /// <inheritdoc/>
    protected override double SecondDerivativeCore(double x)
    {
        if (x < XMin || x > XMax)
        {
            return 0.0;
        }

        return decoratedInterpolation.SecondDerivative(x, true);
    }

    private static Interpolation Decorated(Interpolation decoratedInterpolation)
    {
        ArgumentNullException.ThrowIfNull(decoratedInterpolation);
        return decoratedInterpolation;
    }

    // std::clamp(x, xMin(), xMax()), which keeps a NaN x.
    private double Bind(double x) => x < XMin ? XMin : XMax < x ? XMax : x;
}
