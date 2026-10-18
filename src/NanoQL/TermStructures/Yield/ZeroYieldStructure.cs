// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/zeroyieldstructure.hpp ql/termstructures/yield/zeroyieldstructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: the constructor taking a day counter only, for structures that manage their own reference date, which no curve ported so far uses

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A zero-yield term structure: an adapter to <see cref="YieldTermStructure"/> that lets a derived class implement
/// only <see cref="ZeroYieldImpl"/>; discount factors and forward rates are calculated from the zero yields, which are
/// continuously compounded with an annual time unit.
/// </summary>
/// <remarks>
/// An extension point, as in QuantLib: the interpolated zero and forward curves derive from it. QuantLib's constructor
/// taking settlement days is <see cref="YieldTermStructure"/>'s reference-date constructor with the settlement days
/// given, the reference date computed by the derived class's <c>Create(…, context)</c> factory.
/// </remarks>
public abstract class ZeroYieldStructure : YieldTermStructure
{
    /// <summary>Initializes a new instance of the <see cref="ZeroYieldStructure"/> class with a fixed reference date.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="calendar">The calendar, or <see langword="null"/>, QuantLib's empty calendar.</param>
    /// <param name="dc">The day counter used for date/time conversion.</param>
    /// <param name="settlementDays">The settlement days the reference date was calculated with, if any.</param>
    /// <param name="jumps">The discount-factor jumps, or <see langword="null"/> for none.</param>
    /// <param name="jumpDates">The jumps' dates, or <see langword="null"/> for 31 December of successive years.</param>
    /// <exception cref="ArgumentException">
    /// Jump dates are given and their number differs from the jumps', or one of them is <c>default(Date)</c>.
    /// </exception>
    protected ZeroYieldStructure(
        Date referenceDate,
        Calendar? calendar,
        DayCounter dc,
        int? settlementDays = null,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null)
        : base(referenceDate, calendar, dc, settlementDays, jumps, jumpDates)
    {
    }

    /// <summary>
    /// The zero yield at a time, continuously compounded. Range checks have already been performed, so it must assume
    /// that extrapolation is required.
    /// </summary>
    /// <param name="t">The time.</param>
    /// <returns>The zero yield.</returns>
    protected abstract double ZeroYieldImpl(double t);

    /// <summary>The discount factor at a time, calculated from the zero yield: exp(−z·t), and 1 at time 0.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The discount factor.</returns>
    protected override double DiscountImpl(double t)
    {
        // this acts as a safe guard in cases where zeroYieldImpl(0.0) would throw.
        if (t == 0.0)
        {
            return 1.0;
        }

        double r = ZeroYieldImpl(t);
        return Math.Exp(-r * t);
    }
}
