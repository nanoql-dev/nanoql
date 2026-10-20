// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/voltermstructure.hpp ql/termstructures/voltermstructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: the constructor for a structure that manages its own reference date, (bdc, dc), which waits for its remaining users (S-eq's Black volatility structures)

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>The base class of volatility term structures.</summary>
/// <remarks>
/// A structure whose reference date moves with the evaluation date is built by a factory taking the context, as
/// <see cref="FlatForward.Create"/> is; every built structure has a fixed reference date.
/// </remarks>
public abstract class VolatilityTermStructure : TermStructure
{
    private readonly BusinessDayConvention bdc;

    /// <summary>Initializes a new instance of the <see cref="VolatilityTermStructure"/> class.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="calendar">The calendar of the option dates, or <see langword="null"/>, QuantLib's empty calendar,
    /// with which option dates can't be computed from tenors.</param>
    /// <param name="bdc">The convention adjusting option dates computed from tenors.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="settlementDays">The settlement days the reference date was calculated with, if any.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bdc"/> isn't a member of its type, or
    /// <paramref name="settlementDays"/> is negative.</exception>
    protected VolatilityTermStructure(
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

    /// <summary>Gets the convention adjusting option dates computed from tenors.</summary>
    public virtual BusinessDayConvention BusinessDayConvention => bdc;

    /// <summary>The option date of a tenor: the reference date advanced by it, on the structure's calendar.</summary>
    /// <param name="p">The tenor.</param>
    /// <returns>The option date.</returns>
    /// <exception cref="NanoQLException">The structure has no calendar.</exception>
    public Date OptionDateFromTenor(Period p)
    {
        Calendar calendar = Calendar ?? throw new NanoQLException("no calendar implementation provided");
        return calendar.Advance(ReferenceDate, p, BusinessDayConvention);
    }

    /// <summary>The minimum strike for which the structure returns values.</summary>
    /// <returns>The strike.</returns>
    public abstract double MinStrike();

    /// <summary>The maximum strike for which the structure returns values.</summary>
    /// <returns>The strike.</returns>
    public abstract double MaxStrike();

    /// <summary>Checks that a strike is in the structure's domain, or that extrapolation is allowed.</summary>
    /// <param name="strike">The strike.</param>
    /// <param name="extrapolate">Whether this call extrapolates.</param>
    /// <exception cref="NanoQLException">The strike is outside the domain.</exception>
    protected void CheckStrike(double strike, bool extrapolate)
    {
        if (!(extrapolate || AllowsExtrapolation || (strike >= MinStrike() && strike <= MaxStrike())))
        {
            throw new NanoQLException(
                $"strike ({Show(strike)}) is outside the curve domain [{Show(MinStrike())},{Show(MaxStrike())}]");
        }
    }

    private static string Show(double x) => x.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
