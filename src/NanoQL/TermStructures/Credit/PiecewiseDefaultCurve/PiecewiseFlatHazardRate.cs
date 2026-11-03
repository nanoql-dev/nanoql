// SPDX-FileCopyrightText: Copyright (C) 2008, 2016 Jose Aparicio
// SPDX-FileCopyrightText: Copyright (C) 2008 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/credit/piecewisedefaultcurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The piecewise hazard-rate curve with backward-flat interpolation: QuantLib-Python's <c>PiecewiseFlatHazardRate</c>,
/// QuantLib's <c>PiecewiseDefaultCurve&lt;HazardRate, BackwardFlat&gt;</c>.
/// </summary>
public sealed class PiecewiseFlatHazardRate : PiecewiseDefaultCurve<HazardRate, BackwardFlat, IterativeBootstrap>
{
    /// <summary>Initializes a new instance of the <see cref="PiecewiseFlatHazardRate"/> class with a fixed reference
    /// date, and bootstraps it.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="instruments">The helpers the curve is bootstrapped on.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="context">The evaluation context the helpers are dated and priced in.</param>
    /// <param name="jumps">The survival-probability jumps, or <see langword="null"/> for none.</param>
    /// <param name="jumpDates">The jumps' dates, or <see langword="null"/> for 31 December of successive years.</param>
    /// <param name="i">The interpolator.</param>
    /// <param name="bootstrap">The bootstrap.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/>, <paramref name="instruments"/> or
    /// <paramref name="dayCounter"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A helper is <see langword="null"/>, the reference date is
    /// <c>default(Date)</c>, or the jumps and their dates differ in number.</exception>
    /// <exception cref="NanoQLException">The bootstrap fails.</exception>
    public PiecewiseFlatHazardRate(
        Date referenceDate,
        IEnumerable<BootstrapHelper<DefaultProbabilityTermStructure>> instruments,
        DayCounter dayCounter,
        EvaluationContext context,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null,
        BackwardFlat i = default,
        IterativeBootstrap bootstrap = default)
        : base(referenceDate, instruments, dayCounter, context, jumps, jumpDates, i, bootstrap)
    {
    }

    private PiecewiseFlatHazardRate(
        Date referenceDate,
        Calendar calendar,
        int settlementDays,
        IEnumerable<BootstrapHelper<DefaultProbabilityTermStructure>> instruments,
        DayCounter dayCounter,
        EvaluationContext context,
        IEnumerable<double>? jumps,
        IEnumerable<Date>? jumpDates,
        BackwardFlat i,
        IterativeBootstrap bootstrap)
        : base(referenceDate, calendar, settlementDays, instruments, dayCounter, context, jumps, jumpDates, i, bootstrap)
    {
    }

    /// <summary>
    /// A curve whose reference date is the given number of business days after the context's evaluation date:
    /// QuantLib's settlement-days constructors.
    /// </summary>
    /// <param name="settlementDays">The business days from the evaluation date to the reference date.</param>
    /// <param name="calendar">The calendar the days are counted on.</param>
    /// <param name="instruments">The helpers the curve is bootstrapped on.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="jumps">The survival-probability jumps, or <see langword="null"/> for none.</param>
    /// <param name="jumpDates">The jumps' dates, or <see langword="null"/>.</param>
    /// <param name="i">The interpolator.</param>
    /// <param name="bootstrap">The bootstrap.</param>
    /// <returns>The curve.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="calendar"/>, <paramref name="context"/>,
    /// <paramref name="instruments"/> or <paramref name="dayCounter"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    /// <exception cref="ArgumentException">A helper is <see langword="null"/>, or the jumps and their dates differ in
    /// number.</exception>
    /// <exception cref="NanoQLException">The bootstrap fails.</exception>
    public static new PiecewiseFlatHazardRate Create(
        int settlementDays,
        Calendar calendar,
        IEnumerable<BootstrapHelper<DefaultProbabilityTermStructure>> instruments,
        DayCounter dayCounter,
        EvaluationContext context,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null,
        BackwardFlat i = default,
        IterativeBootstrap bootstrap = default)
    {
        Date referenceDate = SettlementDate(settlementDays, calendar, context);
        return new PiecewiseFlatHazardRate(
            referenceDate, calendar, settlementDays, instruments, dayCounter, context, jumps, jumpDates, i, bootstrap);
    }
}
