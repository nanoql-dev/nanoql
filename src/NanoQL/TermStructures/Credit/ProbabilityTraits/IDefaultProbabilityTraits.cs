// SPDX-FileCopyrightText: Copyright (C) 2008, 2016 Jose Aparicio
// SPDX-FileCopyrightText: Copyright (C) 2008 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/credit/probabilitytraits.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A default-probability curve's bootstrap traits, which also carry the curve kind they bootstrap: its survival
/// probability, default density and hazard rate, and its maximum-date rule.
/// </summary>
/// <remarks>
/// As <see cref="IYieldTraits"/> does for yield curves: QuantLib's piecewise curve derives from the interpolated curve
/// its traits name, which C# can't express, so the traits carry the kind's formulas, and the interpolated curve and the
/// piecewise curve of that kind both call them. Where QuantLib's interpolated curve keeps the base's hazard rate (the
/// default density over the survival probability, both read through the curve's public members, jumps included), the
/// kind's <see cref="HazardRateImpl"/> computes it so on the curve it is given. An extension point.
/// </remarks>
public interface IDefaultProbabilityTraits : IBootstrapTraits<DefaultProbabilityTermStructure>
{
    /// <summary>The curve kind's survival probability at a time, from its nodes: QuantLib's
    /// <c>survivalProbabilityImpl</c> of the kind's interpolated curve.</summary>
    /// <param name="times">The nodes' times.</param>
    /// <param name="data">The nodes' data.</param>
    /// <param name="interpolation">The interpolation of the data.</param>
    /// <param name="t">The time.</param>
    /// <returns>The probability.</returns>
    static abstract double SurvivalProbabilityImpl(
        ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t);

    /// <summary>The curve kind's default density at a time, from its nodes: QuantLib's <c>defaultDensityImpl</c> of the
    /// kind's interpolated curve.</summary>
    /// <param name="times">The nodes' times.</param>
    /// <param name="data">The nodes' data.</param>
    /// <param name="interpolation">The interpolation of the data.</param>
    /// <param name="t">The time.</param>
    /// <returns>The density.</returns>
    static abstract double DefaultDensityImpl(
        ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t);

    /// <summary>The curve kind's hazard rate at a time: QuantLib's <c>hazardRateImpl</c> of the kind's interpolated
    /// curve, from its nodes, or the base's, through the curve's public members.</summary>
    /// <param name="times">The nodes' times.</param>
    /// <param name="data">The nodes' data.</param>
    /// <param name="interpolation">The interpolation of the data.</param>
    /// <param name="t">The time.</param>
    /// <param name="curve">The curve, whose survival probability and default density the base's rate reads.</param>
    /// <returns>The rate.</returns>
    static abstract double HazardRateImpl(
        ReadOnlySpan<double> times,
        ReadOnlySpan<double> data,
        Interpolation interpolation,
        double t,
        DefaultProbabilityTermStructure curve);

    /// <summary>The curve kind's maximum date: QuantLib's <c>maxDate()</c> of the kind's interpolated curve, its last
    /// node's date.</summary>
    /// <param name="dates">The nodes' dates.</param>
    /// <returns>The maximum date.</returns>
    static abstract Date MaxDate(ReadOnlySpan<Date> dates);
}
