// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/inflation/inflationtraits.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A zero inflation curve's bootstrap traits, which also carry the curve kind they bootstrap: its rate formula and
/// its maximum-date rule.
/// </summary>
/// <remarks>
/// As <see cref="IYieldTraits"/> does for yield curves: QuantLib's piecewise curve derives from the interpolated curve
/// its traits name, which C# can't express, so the traits carry the kind's formula and rule, and the interpolated curve
/// and the piecewise curve of that kind both call them. An extension point.
/// </remarks>
public interface IZeroInflationTraits : IBootstrapTraits<ZeroInflationTermStructure>
{
    /// <summary>
    /// The curve kind's zero-coupon inflation rate at a time, from its nodes' interpolation, extrapolated past the last node:
    /// QuantLib's <c>zeroRateImpl</c> of the kind's interpolated curve.
    /// </summary>
    /// <param name="interpolation">The interpolation of the nodes' rates.</param>
    /// <param name="t">The time.</param>
    /// <returns>The rate.</returns>
    static abstract double ZeroRateImpl(Interpolation interpolation, double t);

    /// <summary>The curve kind's maximum date: QuantLib's <c>maxDate()</c> of the kind's interpolated curve.</summary>
    /// <param name="dates">The nodes' dates.</param>
    /// <param name="maxDate">The latest date a bootstrap's helpers need, or <see langword="null"/> for none.</param>
    /// <returns>The maximum date.</returns>
    static abstract Date MaxDate(ReadOnlySpan<Date> dates, Date? maxDate);
}
