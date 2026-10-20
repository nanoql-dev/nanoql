// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/credit/survivalprobabilitystructure.hpp ql/termstructures/credit/survivalprobabilitystructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The survival-probability term structure: an adapter that lets a derived curve implement the survival probability,
/// from which the default densities and hazard rates follow.
/// </summary>
/// <remarks>An extension point.</remarks>
public abstract class SurvivalProbabilityStructure : DefaultProbabilityTermStructure
{
    /// <summary>Initializes a new instance of the <see cref="SurvivalProbabilityStructure"/> class with a fixed
    /// reference date.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="cal">The calendar, if any.</param>
    /// <param name="dc">The day counter.</param>
    /// <param name="settlementDays">The settlement days the reference date was calculated with, if any.</param>
    /// <param name="jumps">The survival-probability jumps; none when <see langword="null"/>.</param>
    /// <param name="jumpDates">The jumps' dates; at the turns of the years when <see langword="null"/> or
    /// empty.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dc"/> is null.</exception>
    /// <exception cref="ArgumentException">Jump dates are given and their number differs from the jumps', or one of
    /// them is <c>default(Date)</c>.</exception>
    protected SurvivalProbabilityStructure(
        Date referenceDate,
        Calendar? cal,
        DayCounter dc,
        int? settlementDays = null,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null)
        : base(referenceDate, cal, dc, settlementDays, jumps, jumpDates)
    {
    }

    /// <summary>
    /// The default density at a time: minus the survival probability's derivative, by a central difference over
    /// 0.0001 either side (one-sided at 0). QuantLib warns that this numerical differentiation "might be inefficient
    /// and inaccurate"; derived curves may override it.
    /// </summary>
    /// <param name="t">The time.</param>
    /// <returns>The density.</returns>
    protected override double DefaultDensityImpl(double t)
    {
        const double Dt = 0.0001;
        double t1 = Math.Max(t - Dt, 0.0);
        double t2 = t + Dt;

        double p1 = SurvivalProbabilityImpl(t1);
        double p2 = SurvivalProbabilityImpl(t2);

        return (p1 - p2) / (t2 - t1);
    }
}
