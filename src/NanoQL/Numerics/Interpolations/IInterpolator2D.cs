// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/interpolation2d.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A 2-D interpolation factory, QuantLib's 2-D interpolator concept (<c>Bilinear</c> and the others): an instance
/// builds the interpolation over a grid and its function values.
/// </summary>
/// <remarks>
/// <para>
/// A factory is a <see langword="readonly"/> struct, stateless or holding its parameters, whose zero value is QuantLib's
/// default construction. A consumer is generic over <c>TInterpolator : struct, IInterpolator2D</c> and takes the
/// instance, where QuantLib's <c>const Interpolator&amp; i = {}</c> becomes <c>TInterpolator i = default</c>.
/// </para>
/// <para>An extension point: a factory of one's own implements it.</para>
/// </remarks>
public interface IInterpolator2D
{
    /// <summary>Builds the interpolation over the given grid and function values, which it copies.</summary>
    /// <param name="x">The x values, which must be sorted.</param>
    /// <param name="y">The y values, which must be sorted.</param>
    /// <param name="z">The function values.</param>
    /// <returns>The interpolation.</returns>
    Interpolation2D Interpolate(ReadOnlySpan<double> x, ReadOnlySpan<double> y, Matrix z);
}
