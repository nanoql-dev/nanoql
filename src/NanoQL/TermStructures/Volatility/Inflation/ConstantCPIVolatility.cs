// SPDX-FileCopyrightText: Copyright (C) 2009, 2011 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/inflation/constantcpivolatility.hpp ql/termstructures/volatility/inflation/constantcpivolatility.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A constant CPI volatility, with no strike or time dependence.</summary>
/// <remarks>
/// QuantLib's two constructors, a quote and a volatility, collapse into one, since a quote is a <see cref="double"/>;
/// both take settlement days, so the reference date moves with the evaluation date, and the structure is built by
/// <see cref="Create"/>, which fixes it.
/// </remarks>
public sealed class ConstantCPIVolatility : CPIVolatilitySurface
{
    private readonly double volatility;

    private ConstantCPIVolatility(
        double vol,
        Date referenceDate,
        int settlementDays,
        Calendar cal,
        BusinessDayConvention bdc,
        DayCounter dc,
        Period observationLag,
        Frequency frequency,
        bool indexIsInterpolated)
        : base(referenceDate, settlementDays, cal, bdc, dc, observationLag, frequency, indexIsInterpolated)
    {
        volatility = vol;
    }

    /// <inheritdoc/>
    public override Date MaxDate => Date.MaxDate;

    /// <summary>
    /// A constant CPI volatility whose reference date is the given number of business days after the evaluation date.
    /// </summary>
    /// <param name="vol">The volatility.</param>
    /// <param name="settlementDays">The business days from the evaluation date to the reference date.</param>
    /// <param name="cal">The calendar of the business days and of the option dates.</param>
    /// <param name="bdc">The convention adjusting option dates computed from tenors.</param>
    /// <param name="dc">The day counter.</param>
    /// <param name="observationLag">The lag the structure observes the index with.</param>
    /// <param name="frequency">The index's frequency.</param>
    /// <param name="indexIsInterpolated">Whether the index is observed with linear interpolation.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The structure, with a fixed reference date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cal"/>, <paramref name="dc"/> or
    /// <paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or
    /// <paramref name="bdc"/> or <paramref name="frequency"/> isn't a member of its type.</exception>
    public static ConstantCPIVolatility Create(
        double vol,
        int settlementDays,
        Calendar cal,
        BusinessDayConvention bdc,
        DayCounter dc,
        Period observationLag,
        Frequency frequency,
        bool indexIsInterpolated,
        EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        ArgumentNullException.ThrowIfNull(cal);
        Date referenceDate = cal.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
        return new ConstantCPIVolatility(
            vol, referenceDate, settlementDays, cal, bdc, dc, observationLag, frequency, indexIsInterpolated);
    }

    /// <inheritdoc/>
    public override double MinStrike() => NumericLimits.QL_MIN_REAL;

    /// <inheritdoc/>
    public override double MaxStrike() => NumericLimits.QL_MAX_REAL;

    /// <inheritdoc/>
    protected override double VolatilityImpl(double length, double strike) => volatility;
}
