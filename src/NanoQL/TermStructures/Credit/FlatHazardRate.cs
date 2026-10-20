// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/credit/flathazardrate.hpp ql/termstructures/credit/flathazardrate.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A flat hazard-rate curve.</summary>
/// <remarks>
/// QuantLib's quote and rate constructors are one here, the quote form being the value form (PR6.8), and its
/// settlement-days form is <see cref="Create"/>, which computes the reference date from the evaluation context (D15).
/// </remarks>
public sealed class FlatHazardRate : HazardRateStructure
{
    private readonly double hazardRate;

    /// <summary>Initializes a new instance of the <see cref="FlatHazardRate"/> class with a fixed reference
    /// date.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="hazardRate">The hazard rate.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dayCounter"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="referenceDate"/> is <c>default(Date)</c>.</exception>
    public FlatHazardRate(Date referenceDate, double hazardRate, DayCounter dayCounter)
        : this(referenceDate, null, null, hazardRate, dayCounter)
    {
    }

    private FlatHazardRate(
        Date referenceDate, Calendar? calendar, int? settlementDays, double hazardRate, DayCounter dayCounter)
        : base(referenceDate, calendar, dayCounter, settlementDays)
    {
        this.hazardRate = hazardRate;
    }

    /// <inheritdoc/>
    public override Date MaxDate => Date.MaxDate;

    /// <summary>
    /// A flat hazard-rate curve whose reference date is the given number of business days after the context's
    /// evaluation date: QuantLib's settlement-days constructor (D15).
    /// </summary>
    /// <param name="settlementDays">The business days from the evaluation date to the reference date.</param>
    /// <param name="calendar">The calendar the days are counted on.</param>
    /// <param name="hazardRate">The hazard rate.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The curve.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="calendar"/>, <paramref name="dayCounter"/> or
    /// <paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    public static FlatHazardRate Create(
        int settlementDays, Calendar calendar, double hazardRate, DayCounter dayCounter, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        ArgumentNullException.ThrowIfNull(calendar);
        Date referenceDate = calendar.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
        return new FlatHazardRate(referenceDate, calendar, settlementDays, hazardRate, dayCounter);
    }

    /// <inheritdoc/>
    protected override double HazardRateImpl(double t) => hazardRate;

    /// <inheritdoc/>
    protected override double SurvivalProbabilityImpl(double t) => Math.Exp(-hazardRate * t);
}
