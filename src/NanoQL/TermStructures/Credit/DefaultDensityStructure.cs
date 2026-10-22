// SPDX-FileCopyrightText: Copyright (C) 2008 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/credit/defaultdensitystructure.hpp ql/termstructures/credit/defaultdensitystructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: survivalProbabilityImpl's default, a Gauss-Chebyshev integral of the default density, which waits for S-xcr's curves, its only users

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The default-density term structure: an adapter that lets a derived curve implement the default density, from which
/// the hazard rates follow.
/// </summary>
/// <remarks>
/// An extension point. QuantLib's default survival probability integrates the default density numerically, with a
/// 48-point Gauss-Chebyshev rule, floored at 0, and warns that it "might be inefficient and inaccurate": that rule
/// integrates a constant over [-1, 1] to 2.000357018144566, a relative error of 1.785e-4. It waits for S-xcr's
/// curves, its only users; here <see cref="DefaultProbabilityTermStructure.SurvivalProbabilityImpl"/> stays abstract,
/// and every curve of this slot implements it exactly.
/// </remarks>
public abstract class DefaultDensityStructure : DefaultProbabilityTermStructure
{
    /// <summary>Initializes a new instance of the <see cref="DefaultDensityStructure"/> class with a fixed reference
    /// date.</summary>
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
    protected DefaultDensityStructure(
        Date referenceDate,
        Calendar? cal,
        DayCounter dc,
        int? settlementDays = null,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null)
        : base(referenceDate, cal, dc, settlementDays, jumps, jumpDates)
    {
    }
}
