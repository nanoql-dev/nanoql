// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/correlationstructure.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: the constructor for a structure that manages its own reference date, (cal, bdc, dc), which no QuantLib 1.44 class uses and which waits for its first user

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The abstract correlation term structure, pretty much like the volatility term structure: the correlation's range is
/// known in advance and there is no strike. Derived structures might hold correlations of any dimension.
/// </summary>
/// <remarks>
/// In principle there might be several extrapolation dimensions; at this level neither their number nor their nature
/// (time, strike, …) is known, and neither is whether the correlation is a number or a matrix, which the hierarchy
/// defers to its derived structures.
/// </remarks>
[Experimental("NQL0001")]
public abstract class CorrelationTermStructure : TermStructure
{
    private readonly BusinessDayConvention bdc;

    /// <summary>Initializes a new instance of the <see cref="CorrelationTermStructure"/> class with a fixed reference
    /// date.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="calendar">The calendar of the tenors' dates, or <see langword="null"/>, QuantLib's empty calendar,
    /// with which dates can't be computed from tenors.</param>
    /// <param name="bdc">The convention adjusting dates computed from tenors.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="settlementDays">The settlement days the reference date was calculated with, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dayCounter"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bdc"/> isn't a member of its type, or
    /// <paramref name="settlementDays"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="referenceDate"/> is <c>default(Date)</c>.</exception>
    protected CorrelationTermStructure(
        Date referenceDate,
        Calendar? calendar,
        BusinessDayConvention bdc,
        DayCounter dayCounter,
        int? settlementDays = null)
        : base(referenceDate, calendar, dayCounter, settlementDays)
    {
        EnumArguments.Check(bdc, nameof(bdc));
        this.bdc = bdc;
    }

    /// <summary>Gets the convention adjusting dates computed from tenors.</summary>
    public BusinessDayConvention BusinessDayConvention => bdc;

    /// <summary>Gets the size of the squared correlation.</summary>
    public abstract int CorrelationSize { get; }

    /// <summary>The date of a tenor: the reference date advanced by it on the structure's calendar, swaption style,
    /// which still holds here.</summary>
    /// <param name="p">The tenor.</param>
    /// <returns>The date.</returns>
    /// <exception cref="NanoQLException">The structure has no calendar.</exception>
    public Date DateFromTenor(Period p)
    {
        Calendar calendar = Calendar ?? throw new NanoQLException("no calendar implementation provided");
        return calendar.Advance(ReferenceDate, p, BusinessDayConvention);
    }
}
