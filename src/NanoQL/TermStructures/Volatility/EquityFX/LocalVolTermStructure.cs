// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/localvoltermstructure.hpp ql/termstructures/volatility/equityfx/localvoltermstructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: the constructor for a structure that manages its own reference date, (bdc, dc) (not ported: its QuantLib users follow another structure, and pass its reference date to the reference-date constructor instead, as LocalVolCurve and LocalVolSurface do)

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The local-volatility term structure: the base of the structures that give the local volatility at a time and an
/// underlying level. Volatilities are on an annual basis.
/// </summary>
/// <remarks>An extension point, as in QuantLib: the local-volatility structures derive from it.</remarks>
public abstract class LocalVolTermStructure : VolatilityTermStructure
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalVolTermStructure"/> class at a reference date; with
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
    protected LocalVolTermStructure(
        Date referenceDate,
        Calendar? calendar,
        BusinessDayConvention bdc,
        DayCounter dayCounter,
        int? settlementDays = null)
        : base(referenceDate, calendar, bdc, dayCounter, settlementDays)
    {
    }

    /// <summary>The local volatility at a date and underlying level.</summary>
    /// <param name="d">The date.</param>
    /// <param name="underlyingLevel">The underlying level.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The local volatility.</returns>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The date or level is out of range, or the structure can't compute
    /// it.</exception>
    public double LocalVol(Date d, double underlyingLevel, bool extrapolate = false)
    {
        Date.ThrowIfDefault(d, nameof(d));
        CheckRange(d, extrapolate);
        CheckStrike(underlyingLevel, extrapolate);
        double t = TimeFromReference(d);
        return LocalVolImpl(t, underlyingLevel);
    }

    /// <summary>The local volatility at a time and underlying level.</summary>
    /// <param name="t">The time.</param>
    /// <param name="underlyingLevel">The underlying level.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The local volatility.</returns>
    /// <exception cref="NanoQLException">The time or level is out of range, or the structure can't compute
    /// it.</exception>
    public double LocalVol(double t, double underlyingLevel, bool extrapolate = false)
    {
        CheckRange(t, extrapolate);
        CheckStrike(underlyingLevel, extrapolate);
        return LocalVolImpl(t, underlyingLevel);
    }

    /// <summary>The local volatility at a time and level, the range already checked; it must extrapolate.</summary>
    /// <param name="t">The time.</param>
    /// <param name="strike">The underlying level.</param>
    /// <returns>The local volatility.</returns>
    protected abstract double LocalVolImpl(double t, double strike);
}
