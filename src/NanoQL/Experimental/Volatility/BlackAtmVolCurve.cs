// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/blackatmvolcurve.hpp ql/experimental/volatility/blackatmvolcurve.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The base of Black at-the-money (no-smile) volatility curves. Volatilities are on an annual basis.
/// </summary>
/// <remarks>
/// An extension point, as in QuantLib: the curve and the smile surfaces derive from it. A curve is immutable, its
/// reference date fixed at construction. An option tenor's date is <see cref="OptionDate"/>'s, the structure's own
/// option date from a tenor, which an interest-rate surface overrides with its index's (the S-xeq plan's XER5 (xxv)).
/// QuantLib's acyclic visitor isn't ported.
/// </remarks>
[Experimental("NQL0001")]
public abstract class BlackAtmVolCurve : VolatilityTermStructure
{
    /// <summary>Initializes a new instance of the <see cref="BlackAtmVolCurve"/> class at a reference date.</summary>
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
    protected BlackAtmVolCurve(
        Date referenceDate, Calendar? calendar, BusinessDayConvention bdc, DayCounter dayCounter, int? settlementDays = null)
        : base(referenceDate, calendar, bdc, dayCounter, settlementDays)
    {
    }

    /// <summary>The at-the-money volatility at an option tenor.</summary>
    /// <param name="optionTenor">The option tenor.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="NanoQLException">The option date is out of range, or can't be computed.</exception>
    public double AtmVol(Period optionTenor, bool extrapolate = false) => AtmVol(OptionDate(optionTenor), extrapolate);

    /// <summary>The at-the-money volatility at a maturity date.</summary>
    /// <param name="maturity">The maturity date.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="ArgumentException"><paramref name="maturity"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The date is out of range.</exception>
    public double AtmVol(Date maturity, bool extrapolate = false)
    {
        Date.ThrowIfDefault(maturity, nameof(maturity));
        return AtmVol(TimeFromReference(maturity), extrapolate);
    }

    /// <summary>The at-the-money volatility at a maturity time.</summary>
    /// <param name="maturity">The maturity time.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="NanoQLException">The time is out of range.</exception>
    public double AtmVol(double maturity, bool extrapolate = false)
    {
        CheckRange(maturity, extrapolate);
        return AtmVolImpl(maturity);
    }

    /// <summary>The at-the-money variance at an option tenor.</summary>
    /// <param name="optionTenor">The option tenor.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The variance.</returns>
    /// <exception cref="NanoQLException">The option date is out of range, or can't be computed.</exception>
    public double AtmVariance(Period optionTenor, bool extrapolate = false) =>
        AtmVariance(OptionDate(optionTenor), extrapolate);

    /// <summary>The at-the-money variance at a maturity date.</summary>
    /// <param name="maturity">The maturity date.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The variance.</returns>
    /// <exception cref="ArgumentException"><paramref name="maturity"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The date is out of range.</exception>
    public double AtmVariance(Date maturity, bool extrapolate = false)
    {
        Date.ThrowIfDefault(maturity, nameof(maturity));
        return AtmVariance(TimeFromReference(maturity), extrapolate);
    }

    /// <summary>The at-the-money variance at a maturity time.</summary>
    /// <param name="maturity">The maturity time.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The variance.</returns>
    /// <exception cref="NanoQLException">The time is out of range.</exception>
    public double AtmVariance(double maturity, bool extrapolate = false)
    {
        CheckRange(maturity, extrapolate);
        return AtmVarianceImpl(maturity);
    }

    /// <summary>
    /// The option date of a tenor: <see cref="VolatilityTermStructure.OptionDateFromTenor"/>'s, the reference date
    /// advanced on the calendar; an interest-rate surface's is its index's fixing date (XER5 (xxv)).
    /// </summary>
    /// <param name="optionTenor">The option tenor.</param>
    /// <returns>The option date.</returns>
    /// <exception cref="NanoQLException">The structure has no calendar.</exception>
    protected virtual Date OptionDate(Period optionTenor) => OptionDateFromTenor(optionTenor);

    /// <summary>The at-the-money variance at a time already range-checked.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The variance.</returns>
    protected abstract double AtmVarianceImpl(double t);

    /// <summary>The at-the-money volatility at a time already range-checked.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The volatility.</returns>
    protected abstract double AtmVolImpl(double t);
}
