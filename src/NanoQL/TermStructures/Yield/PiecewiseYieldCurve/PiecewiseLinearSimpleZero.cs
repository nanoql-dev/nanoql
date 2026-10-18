// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/piecewiseyieldcurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A yield term structure bootstrapped on rate helpers, on linearly interpolated simply compounded zero rates.
/// </summary>
/// <remarks>
/// QuantLib-Python's <c>%template</c> name for <see cref="PiecewiseYieldCurve{TTraits,TInterpolator,TBootstrap}"/> on
/// <see cref="TermStructures.SimpleZeroYield"/>, <see cref="Linear"/> and <see cref="IterativeBootstrap"/>: it adds
/// constructors only.
/// </remarks>
public sealed class PiecewiseLinearSimpleZero : PiecewiseYieldCurve<SimpleZeroYield, Linear, IterativeBootstrap>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PiecewiseLinearSimpleZero"/> class with a fixed reference date, and bootstraps it.
    /// </summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="instruments">The helpers the curve is bootstrapped on.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="context">The evaluation context the helpers are dated and priced in.</param>
    /// <param name="jumps">The discount-factor jumps, or <see langword="null"/> for none.</param>
    /// <param name="jumpDates">The jumps' dates, or <see langword="null"/> for 31 December of successive years.</param>
    /// <param name="i">The interpolator.</param>
    /// <param name="bootstrap">The bootstrap.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/>, <paramref name="instruments"/> or <paramref name="dayCounter"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A helper is <see langword="null"/>, the reference date is <c>default(Date)</c>, or the jumps and their dates
    /// differ in number.
    /// </exception>
    /// <exception cref="NanoQLException">The bootstrap fails.</exception>
    public PiecewiseLinearSimpleZero(
        Date referenceDate,
        IEnumerable<BootstrapHelper<YieldTermStructure>> instruments,
        DayCounter dayCounter,
        EvaluationContext context,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null,
        Linear i = default,
        IterativeBootstrap bootstrap = default)
        : base(referenceDate, instruments, dayCounter, context, jumps, jumpDates, i, bootstrap)
    {
    }

    private PiecewiseLinearSimpleZero(
        Date referenceDate,
        Calendar calendar,
        int settlementDays,
        IEnumerable<BootstrapHelper<YieldTermStructure>> instruments,
        DayCounter dayCounter,
        EvaluationContext context,
        IEnumerable<double>? jumps,
        IEnumerable<Date>? jumpDates,
        Linear i,
        IterativeBootstrap bootstrap)
        : base(referenceDate, calendar, settlementDays, instruments, dayCounter, context, jumps, jumpDates, i, bootstrap)
    {
    }

    /// <summary>
    /// A curve with a reference date that is the evaluation date advanced by settlement days on a calendar: QuantLib's
    /// constructors taking settlement days, whose reference date moves with the evaluation date. The curve is built for
    /// the context's evaluation date.
    /// </summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="calendar">The calendar the settlement days are counted on.</param>
    /// <param name="instruments">The helpers the curve is bootstrapped on.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="jumps">The discount-factor jumps, or <see langword="null"/> for none.</param>
    /// <param name="jumpDates">The jumps' dates, or <see langword="null"/> for 31 December of successive years.</param>
    /// <param name="i">The interpolator.</param>
    /// <param name="bootstrap">The bootstrap.</param>
    /// <returns>The bootstrapped curve.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/>, <paramref name="calendar"/>, <paramref name="instruments"/> or
    /// <paramref name="dayCounter"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// A helper is <see langword="null"/>, or the jumps and their dates differ in number.
    /// </exception>
    /// <exception cref="NanoQLException">The bootstrap fails.</exception>
    public static new PiecewiseLinearSimpleZero Create(
        int settlementDays,
        Calendar calendar,
        IEnumerable<BootstrapHelper<YieldTermStructure>> instruments,
        DayCounter dayCounter,
        EvaluationContext context,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null,
        Linear i = default,
        IterativeBootstrap bootstrap = default)
    {
        Date referenceDate = SettlementDate(settlementDays, calendar, context);
        return new PiecewiseLinearSimpleZero(
            referenceDate, calendar, settlementDays, instruments, dayCounter, context, jumps, jumpDates, i, bootstrap);
    }
}
