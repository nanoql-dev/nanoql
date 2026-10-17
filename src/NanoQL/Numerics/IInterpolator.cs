// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// An interpolation factory and its traits, QuantLib's interpolator concept (<c>Linear</c>, <c>Cubic</c> and the
/// others): an instance builds the interpolation, and the type gives whether it is global and how many points it needs.
/// </summary>
/// <remarks>
/// <para>
/// A factory is a <see langword="readonly"/> struct, stateless or holding its parameters, whose zero value is QuantLib's
/// default construction (or is refused where QuantLib has none). A consumer is generic over
/// <c>TInterpolator : struct, IInterpolator</c> and takes the instance, where QuantLib's <c>const Interpolator&amp; i =
/// {}</c> becomes <c>TInterpolator i = default</c>.
/// </para>
/// <para>
/// An extension point: a factory of one's own implements it. Its static members make it usable only as a constraint,
/// never as a type argument.
/// </para>
/// </remarks>
public interface IInterpolator
{
    /// <summary>
    /// Gets a value indicating whether the interpolation is global: whether each point's value depends on every node,
    /// as a spline's does, rather than on its neighbours only.
    /// </summary>
    static abstract bool Global { get; }

    /// <summary>Gets the fewest points the interpolation takes.</summary>
    static abstract int RequiredPoints { get; }

    /// <summary>Builds the interpolation over the given points, which it copies.</summary>
    /// <param name="x">The x values, which must be sorted.</param>
    /// <param name="y">The y values, one per x value.</param>
    /// <returns>The interpolation.</returns>
    Interpolation Interpolate(ReadOnlySpan<double> x, ReadOnlySpan<double> y);
}
