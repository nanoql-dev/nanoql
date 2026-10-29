// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/equityfxvolsurface.hpp ql/experimental/volatility/equityfxvolsurface.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The base of equity and FX volatility (smile) surfaces, with the at-the-money forward volatility and variance between
/// two times. Volatilities are on an annual basis; only without a smile does the at-the-money forward volatility make
/// sense.
/// </summary>
/// <remarks>An extension point, as in QuantLib. QuantLib's acyclic visitor isn't ported.</remarks>
[Experimental("NQL0001")]
public abstract class EquityFXVolSurface : BlackVolSurface
{
    /// <summary>Initializes a new instance of the <see cref="EquityFXVolSurface"/> class at a reference date.</summary>
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
    protected EquityFXVolSurface(
        Date referenceDate, Calendar? calendar, BusinessDayConvention bdc, DayCounter dayCounter, int? settlementDays = null)
        : base(referenceDate, calendar, bdc, dayCounter, settlementDays)
    {
    }

    /// <summary>The at-the-money forward volatility between two dates.</summary>
    /// <param name="date1">The first date.</param>
    /// <param name="date2">The second date, after the first.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The forward volatility.</returns>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The dates aren't increasing ("wrong dates"), a date is out of range, or the
    /// variances aren't increasing.</exception>
    public double AtmForwardVol(Date date1, Date date2, bool extrapolate = false)
    {
        (double t1, double t2) = Times(date1, date2);
        return AtmForwardVol(t1, t2, extrapolate);
    }

    /// <summary>The at-the-money forward volatility between two times.</summary>
    /// <param name="time1">The first time.</param>
    /// <param name="time2">The second time, after the first.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The forward volatility.</returns>
    /// <exception cref="NanoQLException">The times aren't increasing ("wrong times"), a time is out of range, or the
    /// variances aren't increasing ("non-increasing variances").</exception>
    public double AtmForwardVol(double time1, double time2, bool extrapolate = false)
    {
        double fwdVariance = AtmForwardVariance(time1, time2, extrapolate);
        double t = time2 - time1;
        return Math.Sqrt(fwdVariance / t);
    }

    /// <summary>The at-the-money forward variance between two dates.</summary>
    /// <param name="date1">The first date.</param>
    /// <param name="date2">The second date, after the first.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The forward variance.</returns>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The dates aren't increasing ("wrong dates"), a date is out of range, or the
    /// variances aren't increasing.</exception>
    public double AtmForwardVariance(Date date1, Date date2, bool extrapolate = false)
    {
        (double t1, double t2) = Times(date1, date2);
        return AtmForwardVariance(t1, t2, extrapolate);
    }

    /// <summary>The at-the-money forward variance between two times.</summary>
    /// <param name="time1">The first time.</param>
    /// <param name="time2">The second time, after the first.</param>
    /// <param name="extrapolate">Whether to allow extrapolation.</param>
    /// <returns>The forward variance.</returns>
    /// <exception cref="NanoQLException">The times aren't increasing ("wrong times"), a time is out of range, or the
    /// variances aren't increasing ("non-increasing variances").</exception>
    public double AtmForwardVariance(double time1, double time2, bool extrapolate = false)
    {
        if (!(time1 < time2))
        {
            throw new NanoQLException("wrong times");
        }

        double var1 = AtmVariance(time1, extrapolate);
        double var2 = AtmVariance(time2, extrapolate);
        if (!(var1 < var2))
        {
            throw new NanoQLException("non-increasing variances");
        }

        return var2 - var1;
    }

    private (double T1, double T2) Times(Date date1, Date date2)
    {
        Date.ThrowIfDefault(date1, nameof(date1));
        Date.ThrowIfDefault(date2, nameof(date2));
        if (!(date1 < date2))
        {
            throw new NanoQLException("wrong dates");
        }

        return (TimeFromReference(date1), TimeFromReference(date2));
    }
}
