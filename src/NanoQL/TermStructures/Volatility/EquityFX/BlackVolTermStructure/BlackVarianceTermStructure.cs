// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2026 Yassine Idyiahia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/blackvoltermstructure.hpp ql/termstructures/volatility/equityfx/blackvoltermstructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: the constructor for a structure that manages its own reference date, (bdc, dc) (not ported: its QuantLib users follow another structure, and pass its reference date to the reference-date constructor instead)

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The adapter that lets a Black-volatility structure implement only
/// <see cref="BlackVolTermStructure.BlackVarianceImpl"/>: its volatility is the square root of the variance over the
/// time, at a time of 0.00001 where the time is 0.
/// </summary>
/// <remarks>An extension point, as in QuantLib: the structures given by their variances derive from it.</remarks>
public abstract class BlackVarianceTermStructure : BlackVolTermStructure
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlackVarianceTermStructure"/> class at a reference date; with
    /// settlement days, for a structure a factory dates by the context.
    /// </summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="calendar">The calendar, or <see langword="null"/> for QuantLib's empty one.</param>
    /// <param name="bdc">The business-day convention of the option dates; QuantLib's default is
    /// <c>Following</c>.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="settlementDays">The settlement days the reference date was moved by, or
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dayCounter"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The reference date is <c>default(Date)</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bdc"/> isn't a member of its type.</exception>
    protected BlackVarianceTermStructure(
        Date referenceDate,
        Calendar? calendar,
        BusinessDayConvention bdc,
        DayCounter dayCounter,
        int? settlementDays = null)
        : base(referenceDate, calendar, bdc, dayCounter, settlementDays)
    {
    }

    /// <summary>The volatility from the variance: √(w(t, K) / t), with t = 0.00001 at t = 0.</summary>
    /// <param name="t">The time.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    protected override double BlackVolImpl(double t, double strike)
    {
        double nonZeroMaturity = t == 0.0 ? 0.00001 : t;
        double var = BlackVarianceImpl(nonZeroMaturity, strike);
        return Math.Sqrt(var / nonZeroMaturity);
    }
}
