// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/blackconstantvol.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A constant Black volatility, with no time or strike dependence.</summary>
/// <remarks>
/// QuantLib's constructors taking the volatility as a quote and as a number coincide once a quote is a number. The
/// settlement-days form is <see cref="Create"/>, whose reference date is fixed at the context's evaluation date.
/// </remarks>
public sealed class BlackConstantVol : BlackVolatilityTermStructure
{
    private readonly double volatility;

    /// <summary>Initializes a new instance of the <see cref="BlackConstantVol"/> class at a reference date.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="calendar">The calendar, or <see langword="null"/> for QuantLib's empty one.</param>
    /// <param name="volatility">The volatility.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dayCounter"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="referenceDate"/> is <c>default(Date)</c>.</exception>
    public BlackConstantVol(Date referenceDate, Calendar? calendar, double volatility, DayCounter dayCounter)
        : this(referenceDate, calendar, volatility, dayCounter, null)
    {
    }

    private BlackConstantVol(
        Date referenceDate, Calendar? calendar, double volatility, DayCounter dayCounter, int? settlementDays)
        : base(referenceDate, calendar, BusinessDayConvention.Following, dayCounter, settlementDays)
    {
        this.volatility = volatility;
    }

    /// <inheritdoc/>
    public override Date MaxDate => Date.MaxDate;

    /// <summary>
    /// A constant volatility whose reference date is the context's evaluation date moved by the settlement days on the
    /// calendar.
    /// </summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="volatility">The volatility.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The volatility structure.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/>, <paramref name="calendar"/> or <paramref name="dayCounter"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    public static BlackConstantVol Create(
        int settlementDays, Calendar calendar, double volatility, DayCounter dayCounter, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        ArgumentNullException.ThrowIfNull(calendar);
        Date referenceDate = calendar.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
        return new BlackConstantVol(referenceDate, calendar, volatility, dayCounter, settlementDays);
    }

    /// <inheritdoc/>
    public override double MinStrike() => NumericLimits.QL_MIN_REAL;

    /// <inheritdoc/>
    public override double MaxStrike() => NumericLimits.QL_MAX_REAL;

    /// <inheritdoc/>
    protected override double BlackVolImpl(double t, double strike) => volatility;
}
