// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/flatforward.hpp ql/termstructures/yield/flatforward.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A flat forward rate term structure.</summary>
/// <remarks>
/// QuantLib's constructors taking a quote and a rate become one, since a quote is a <see cref="double"/>. The forms
/// whose reference date moves with the evaluation date become <see cref="Create"/>, which fixes it.
/// </remarks>
public sealed class FlatForward : YieldTermStructure
{
    private readonly InterestRate rate;

    /// <summary>Initializes a new instance of the <see cref="FlatForward"/> class.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="forward">The forward rate.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="compounding">The compounding rule of the forward rate.</param>
    /// <param name="frequency">The compounding frequency of the forward rate.</param>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    public FlatForward(
        Date referenceDate,
        double forward,
        DayCounter dayCounter,
        Compounding compounding = Compounding.Continuous,
        Frequency frequency = Frequency.Annual)
        : this(referenceDate, null, null, forward, dayCounter, compounding, frequency)
    {
    }

    private FlatForward(
        Date referenceDate,
        Calendar? calendar,
        int? settlementDays,
        double forward,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency)
        : base(referenceDate, calendar, dayCounter, settlementDays)
    {
        EnumArguments.Check(compounding, nameof(compounding));
        EnumArguments.Check(frequency, nameof(frequency));
        Compounding = compounding;
        CompoundingFrequency = frequency;
        rate = new InterestRate(forward, DayCounter, compounding, frequency);
    }

    /// <summary>Gets the compounding rule of the forward rate.</summary>
    public Compounding Compounding { get; }

    /// <summary>Gets the compounding frequency of the forward rate.</summary>
    public Frequency CompoundingFrequency { get; }

    /// <inheritdoc/>
    public override Date MaxDate => Date.MaxDate;

    /// <summary>
    /// A flat forward curve whose reference date is the given number of business days after the evaluation date.
    /// </summary>
    /// <param name="settlementDays">The number of business days from the evaluation date to the reference date.</param>
    /// <param name="calendar">The calendar the business days are counted on.</param>
    /// <param name="forward">The forward rate.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="compounding">The compounding rule of the forward rate.</param>
    /// <param name="frequency">The compounding frequency of the forward rate.</param>
    /// <returns>The curve, with a fixed reference date.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or the compounding
    /// or the frequency isn't a member of its enum.</exception>
    public static FlatForward Create(
        int settlementDays,
        Calendar calendar,
        double forward,
        DayCounter dayCounter,
        EvaluationContext context,
        Compounding compounding = Compounding.Continuous,
        Frequency frequency = Frequency.Annual)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        ArgumentNullException.ThrowIfNull(calendar);
        Date referenceDate = calendar.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
        return new FlatForward(referenceDate, calendar, settlementDays, forward, dayCounter, compounding, frequency);
    }

    /// <inheritdoc/>
    protected override double DiscountImpl(double t) => rate.DiscountFactor(t);
}
