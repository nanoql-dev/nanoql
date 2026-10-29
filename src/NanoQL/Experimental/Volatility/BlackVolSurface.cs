// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/blackvolsurface.hpp ql/experimental/volatility/blackvolsurface.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The base of Black volatility (smile) surfaces: a smile section at each time, the at-the-money volatility and
/// variance read off it. Volatilities are on an annual basis.
/// </summary>
/// <remarks>An extension point, as in QuantLib. QuantLib's acyclic visitor isn't ported.</remarks>
[Experimental("NQL0001")]
public abstract class BlackVolSurface : BlackAtmVolCurve
{
    /// <summary>Initializes a new instance of the <see cref="BlackVolSurface"/> class at a reference date.</summary>
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
    protected BlackVolSurface(
        Date referenceDate, Calendar? calendar, BusinessDayConvention bdc, DayCounter dayCounter, int? settlementDays = null)
        : base(referenceDate, calendar, bdc, dayCounter, settlementDays)
    {
    }

    /// <summary>The smile section at an option tenor.</summary>
    /// <param name="optionTenor">The option tenor.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The smile section.</returns>
    /// <exception cref="NanoQLException">The option date is out of range, or can't be computed.</exception>
    public SmileSection SmileSection(Period optionTenor, bool extrapolate) =>
        SmileSection(OptionDate(optionTenor), extrapolate);

    /// <summary>The smile section at a date.</summary>
    /// <param name="date">The date.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The smile section.</returns>
    /// <exception cref="ArgumentException"><paramref name="date"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The date is out of range.</exception>
    public SmileSection SmileSection(Date date, bool extrapolate)
    {
        Date.ThrowIfDefault(date, nameof(date));
        return SmileSection(TimeFromReference(date), extrapolate);
    }

    /// <summary>The smile section at a time.</summary>
    /// <param name="t">The time.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The smile section.</returns>
    /// <exception cref="NanoQLException">The time is out of range.</exception>
    public SmileSection SmileSection(double t, bool extrapolate)
    {
        CheckRange(t, extrapolate);
        return SmileSectionImpl(t);
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The smile section has no at-the-money level.</exception>
    protected override double AtmVarianceImpl(double t)
    {
        SmileSection s = SmileSectionImpl(t);
        return s.Variance(AtmLevel(s));
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The smile section has no at-the-money level.</exception>
    protected override double AtmVolImpl(double t)
    {
        SmileSection s = SmileSectionImpl(t);
        return s.Volatility(AtmLevel(s));
    }

    /// <summary>The smile section at a time already range-checked.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The smile section.</returns>
    protected abstract SmileSection SmileSectionImpl(double t);

    // QuantLib reads atmLevel() and passes it on; nanoql's section returns null where QuantLib's returns Null<Real>()
    private static double AtmLevel(SmileSection s) =>
        s.AtmLevel() ?? throw new NanoQLException("the smile section has no at-the-money level");
}
