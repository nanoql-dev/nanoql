// SPDX-FileCopyrightText: Copyright (C) 2008 Allen Kuo
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/callablebonds/callablebondconstantvol.hpp ql/experimental/callablebonds/callablebondconstantvol.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A constant callable-bond volatility, with no time, length or strike dependence.</summary>
/// <remarks>
/// QuantLib's four constructors collapse into two: its quote overloads are the <see cref="double"/> ones, and the
/// settlement-days form, whose reference date moves with the evaluation date, is <see cref="Create"/>. The date form
/// has QuantLib's empty calendar, so its option dates can be computed only from tenors in weeks, months or years, and
/// QuantLib's base convention, <see cref="BusinessDayConvention.Following"/>, which that calendar refuses for any
/// date it would adjust. The bond tenors reach 100 years, the bond lengths and the strikes have no limit, and the
/// smile sections are flat, by time, with no at-the-money level.
/// </remarks>
[Experimental("NQL0001")]
public sealed class CallableBondConstantVolatility : CallableBondVolatilityStructure
{
    private static readonly Period MaxTenor = new(100, TimeUnit.Years);

    private readonly double volatility;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallableBondConstantVolatility"/> class with a fixed reference date.
    /// </summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="volatility">The volatility.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dayCounter"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="referenceDate"/> is <c>default(Date)</c>.</exception>
    public CallableBondConstantVolatility(Date referenceDate, double volatility, DayCounter dayCounter)
        : this(referenceDate, null, volatility, dayCounter, null)
    {
    }

    private CallableBondConstantVolatility(
        Date referenceDate, Calendar? calendar, double volatility, DayCounter dayCounter, int? settlementDays)
        : base(referenceDate, calendar, dayCounter, BusinessDayConvention.Following, settlementDays)
    {
        this.volatility = volatility;
    }

    /// <inheritdoc/>
    public override Date MaxDate => Date.MaxDate;

    /// <summary>Gets the largest bond tenor for which the structure returns volatilities: 100 years.</summary>
    public override Period MaxBondTenor => MaxTenor;

    /// <summary>
    /// A constant callable-bond volatility whose reference date is the given number of business days after the
    /// evaluation date.
    /// </summary>
    /// <param name="settlementDays">The business days from the evaluation date to the reference date.</param>
    /// <param name="calendar">The calendar of the business days and of the option dates.</param>
    /// <param name="volatility">The volatility.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The structure, with a fixed reference date.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/>, <paramref name="calendar"/> or <paramref name="dayCounter"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    public static CallableBondConstantVolatility Create(
        int settlementDays, Calendar calendar, double volatility, DayCounter dayCounter, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        ArgumentNullException.ThrowIfNull(calendar);
        Date referenceDate = calendar.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
        return new CallableBondConstantVolatility(referenceDate, calendar, volatility, dayCounter, settlementDays);
    }

    /// <summary>The largest bond length for which the structure returns volatilities: QuantLib's largest real, no limit.</summary>
    /// <returns>The length.</returns>
    public override double MaxBondLength() => NumericLimits.QL_MAX_REAL;

    /// <summary>The minimum strike: QuantLib's lowest real, no limit.</summary>
    /// <returns>The strike.</returns>
    public override double MinStrike() => NumericLimits.QL_MIN_REAL;

    /// <summary>The maximum strike: QuantLib's largest real, no limit.</summary>
    /// <returns>The strike.</returns>
    public override double MaxStrike() => NumericLimits.QL_MAX_REAL;

    /// <inheritdoc/>
    protected override double VolatilityImpl(Date optionDate, Period bondTenor, double strike) => volatility;

    /// <inheritdoc/>
    protected override double VolatilityImpl(double optionTime, double bondLength, double strike) => volatility;

    /// <inheritdoc/>
    protected override SmileSection SmileSectionImpl(double optionTime, double bondLength)
    {
        double atmVol = volatility;
        return new FlatSmileSection(optionTime, atmVol, DayCounter);
    }
}
