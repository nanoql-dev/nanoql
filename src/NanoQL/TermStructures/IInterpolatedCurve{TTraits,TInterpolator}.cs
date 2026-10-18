// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/interpolatedcurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// An interpolated curve of the kind its traits name, over the given interpolator: QuantLib's
/// <c>Traits::curve&lt;Interpolator&gt;::type</c>, which a piecewise curve on those traits derives from.
/// </summary>
/// <typeparam name="TTraits">The traits naming the curve kind (<see cref="Discount"/>, <see cref="ZeroYield"/>, …).</typeparam>
/// <typeparam name="TInterpolator">The interpolator.</typeparam>
/// <remarks>
/// The question QuantLib's code asks, whether a curve is an <c>InterpolatedDiscountCurve&lt;LogLinear&gt;</c>,
/// bootstrapped or not, is <c>curve is IInterpolatedCurve&lt;Discount, LogLinear&gt;</c>: the interpolated curve of the
/// kind and the piecewise curve on its traits both implement it. An extension point: a curve kind of one's own
/// implements it with its traits.
/// </remarks>
public interface IInterpolatedCurve<TTraits, TInterpolator>
    where TTraits : struct
    where TInterpolator : struct, IInterpolator
{
    /// <summary>The times of the curve's nodes, from the reference date.</summary>
    /// <returns>The times.</returns>
    ImmutableArray<double> Times();

    /// <summary>The dates of the curve's nodes.</summary>
    /// <returns>The dates.</returns>
    ImmutableArray<Date> Dates();

    /// <summary>The curve's data at its nodes, as its traits read them (discount factors, rates).</summary>
    /// <returns>The data.</returns>
    ImmutableArray<double> Data();

    /// <summary>The curve's nodes: each date with the data at it.</summary>
    /// <returns>The nodes.</returns>
    ImmutableArray<KeyValuePair<Date, double>> Nodes();
}
