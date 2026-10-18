// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructure.hpp ql/termstructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: the constructors taking a day counter only, for structures that manage their own reference date, which no pilot curve uses

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL;

/// <summary>Basic term-structure functionality: a reference date, a day counter and range checks.</summary>
/// <remarks>
/// Every term structure has a fixed reference date. QuantLib's structures whose reference date moves with the
/// evaluation date are built by factories that take the evaluation context and record the settlement days.
/// </remarks>
public abstract class TermStructure : Extrapolator
{
    private readonly DayCounter dayCounter;

    /// <summary>Initializes a new instance of the <see cref="TermStructure"/> class with a fixed reference date.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="calendar">The calendar used for reference and option date calculation, if any.</param>
    /// <param name="dc">The day counter used for date/time conversion.</param>
    /// <param name="settlementDays">The settlement days the reference date was calculated with, if any.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    protected TermStructure(Date referenceDate, Calendar? calendar, DayCounter dc, int? settlementDays = null)
    {
        ArgumentNullException.ThrowIfNull(dc);
        if (settlementDays is int days)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(days, nameof(settlementDays));
        }

        if (referenceDate.SerialNumber == 0)
        {
            throw new ArgumentException("null reference date", nameof(referenceDate));
        }

        ReferenceDate = referenceDate;
        Calendar = calendar;
        dayCounter = dc;
        SettlementDays = settlementDays;
    }

    /// <summary>Gets the date at which discount = 1.0 and/or variance = 0.0.</summary>
    public Date ReferenceDate { get; }

    /// <summary>Gets the day counter used for date/time conversion.</summary>
    public virtual DayCounter DayCounter => dayCounter;

    /// <summary>Gets the calendar used for reference and/or option date calculation, if any.</summary>
    public virtual Calendar? Calendar { get; }

    /// <summary>
    /// Gets the settlement days used for the reference date calculation; <see langword="null"/> for a structure built
    /// from its reference date, where QuantLib throws "settlement days not provided".
    /// </summary>
    public virtual int? SettlementDays { get; }

    /// <summary>Gets the latest date for which the curve can return values.</summary>
    public abstract Date MaxDate { get; }

    /// <summary>Gets the latest time for which the curve can return values.</summary>
    public virtual double MaxTime => TimeFromReference(MaxDate);

    /// <summary>Date/time conversion: the year fraction from the reference date to a date.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The time.</returns>
    public double TimeFromReference(Date date)
    {
        Date.ThrowIfDefault(date, nameof(date));
        return DayCounter.YearFraction(ReferenceDate, date);
    }

    /// <summary>Checks that a date is within the curve's range, or that extrapolation is allowed.</summary>
    /// <param name="d">The date.</param>
    /// <param name="extrapolate">Whether this call extrapolates.</param>
    /// <exception cref="NanoQLException">The date is out of range.</exception>
    protected void CheckRange(Date d, bool extrapolate)
    {
        if (!(d >= ReferenceDate))
        {
            throw new NanoQLException($"date ({d}) before reference date ({ReferenceDate})");
        }

        if (!(extrapolate || AllowsExtrapolation || d <= MaxDate))
        {
            throw new NanoQLException($"date ({d}) is past max curve date ({MaxDate})");
        }
    }

    /// <summary>Checks that a time is within the curve's range, or that extrapolation is allowed.</summary>
    /// <param name="t">The time.</param>
    /// <param name="extrapolate">Whether this call extrapolates.</param>
    /// <exception cref="NanoQLException">The time is out of range.</exception>
    protected void CheckRange(double t, bool extrapolate)
    {
        if (!(t >= 0.0))
        {
            throw new NanoQLException($"negative time ({Show(t)}) given");
        }

        if (!(extrapolate || AllowsExtrapolation || t <= MaxTime || ComparisonFunctions.CloseEnough(t, MaxTime)))
        {
            throw new NanoQLException($"time ({Show(t)}) is past max curve time ({Show(MaxTime)})");
        }
    }

    private static string Show(double t) => t.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
