// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/interestratevolsurface.hpp ql/experimental/volatility/interestratevolsurface.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The base of interest-rate volatility (smile) surfaces on an index: an option tenor's date is the index's fixing date
/// for a start that tenor after the index's value date. Volatilities are on an annual basis.
/// </summary>
/// <remarks>
/// <para>An extension point, as in QuantLib. QuantLib's acyclic visitor isn't ported.</para>
/// <para>
/// Documented divergence (<c>Divergences/InterestRateVolSurfaceOptionDateTests.cs</c>): every option tenor's date on the
/// surface is the index's, the at-the-money volatility and variance and the smile section at a tenor included, where
/// QuantLib's <c>optionDateFromTenor</c> hides its base's non-virtual member, so those members, defined on the bases,
/// date the option on the calendar instead (the S-xeq plan's XER5 (xxv)). <see cref="OptionDateFromTenor"/> hides the
/// base's member as QuantLib's does; called through a <see cref="TermStructures.VolatilityTermStructure"/> reference,
/// the base's date it is.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public abstract class InterestRateVolSurface : BlackVolSurface
{
    /// <summary>Initializes a new instance of the <see cref="InterestRateVolSurface"/> class at a reference date.</summary>
    /// <param name="index">The interest-rate index.</param>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="calendar">The calendar, or <see langword="null"/> for QuantLib's empty one.</param>
    /// <param name="bdc">The business-day convention of the option dates; QuantLib's default is
    /// <c>Following</c>.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="settlementDays">The settlement days the reference date was moved by, or
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> or <paramref name="dayCounter"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The reference date is <c>default(Date)</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bdc"/> isn't a member of its type.</exception>
    protected InterestRateVolSurface(
        InterestRateIndex index,
        Date referenceDate,
        Calendar? calendar,
        BusinessDayConvention bdc,
        DayCounter dayCounter,
        int? settlementDays = null)
        : base(referenceDate, calendar, bdc, dayCounter, settlementDays)
    {
        ArgumentNullException.ThrowIfNull(index);
        Index = index;
    }

    /// <summary>Gets the interest-rate index.</summary>
    public InterestRateIndex Index { get; }

    /// <summary>
    /// The option date of a tenor: the index's fixing date for a start that tenor after the value date of the reference
    /// date adjusted on the fixing calendar.
    /// </summary>
    /// <param name="p">The tenor.</param>
    /// <returns>The option date.</returns>
    public new Date OptionDateFromTenor(Period p)
    {
        InterestRateIndex i = Index;
        Date refDate = i.FixingCalendar.Adjust(ReferenceDate, BusinessDayConvention.Following);
        Date settlement = i.ValueDate(refDate);
        Date start = settlement + p;
        return i.FixingDate(start);
    }

    /// <summary>The option date of a tenor: the index's (XER5 (xxv)).</summary>
    /// <param name="optionTenor">The option tenor.</param>
    /// <returns>The option date.</returns>
    protected override Date OptionDate(Period optionTenor) => OptionDateFromTenor(optionTenor);
}
