// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2019 SoftSolutions! S.r.l.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/bootstraptraits.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A yield curve's bootstrap traits, which also carry the curve kind they bootstrap: its discount formula and its
/// maximum-date rule.
/// </summary>
/// <remarks>
/// QuantLib's traits name their curve type (<c>Discount::curve&lt;I&gt;::type</c> is
/// <c>InterpolatedDiscountCurve&lt;I&gt;</c>), which the piecewise curve derives from. A C# class can't derive from a
/// type computed from a type argument, so the traits carry the kind's formula and rule as static members, which the
/// interpolated curve and the piecewise curve of that kind both call: each formula exists once.
/// </remarks>
public interface IYieldTraits : IBootstrapTraits<YieldTermStructure>
{
    /// <summary>
    /// The curve kind's discount factor at a time, from its nodes and their interpolation, extrapolating past the last
    /// node as the kind does: QuantLib's <c>discountImpl</c> of the kind's interpolated curve.
    /// </summary>
    /// <param name="times">The nodes' times; the last one bounds the interpolation's range.</param>
    /// <param name="data">The nodes' data.</param>
    /// <param name="interpolation">The interpolation of the data.</param>
    /// <param name="t">The time.</param>
    /// <returns>The discount factor.</returns>
    static abstract double DiscountImpl(
        ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t);

    /// <summary>The curve kind's maximum date: QuantLib's <c>maxDate()</c> of the kind's interpolated curve.</summary>
    /// <param name="dates">The nodes' dates.</param>
    /// <param name="maxDate">The latest date a bootstrap's helpers need, or <see langword="null"/> for none.</param>
    /// <returns>The maximum date.</returns>
    static abstract Date MaxDate(ReadOnlySpan<Date> dates, Date? maxDate);
}
