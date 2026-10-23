// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/impliedtermstructure.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The term structure another curve implies at a date in the future, its reference date: the original's discounts
/// divided by its discount at that date.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's curve derives from <c>DerivedYieldTermStructure</c>, a class template over its base, which C# can't
/// express; it implements the template's members itself. Its day counter is the original's, read when it is built, and
/// so is its extrapolation flag; its maximum date is the original's, read when it is asked for. QuantLib's
/// constructor taking settlement days, whose reference date moves with the evaluation date, is
/// <see cref="Create"/>. The original's time and discount at the reference date are read when the curve is built, where
/// QuantLib reads them at the first discount; a curve whose original can't give them refuses to be built. An original
/// that changes is a new curve: QuantLib's curve follows its handle.
/// </para>
/// <para>
/// A time on the curve is read from the original at the time plus the reference date's time on the original's clock,
/// QuantLib's sum, which assumes year fractions add. A date is read by date where they don't: where the sum and the
/// date's own time on the original's clock differ by more than 1e-12 relative, the curve's discount at a date is the
/// original's ratio P(d)/P(ref) by date, the one QuantLib's formula states, and its zero rates and forward rates by
/// date follow it. On Thirty360 and on Actual/Actual ISMA without a schedule QuantLib's sum misses that ratio, by up to
/// 1.25e-4 and 3.7e-3 relative over four years of dates on a zero curve, and gives Thirty360's month-end dates of one
/// time one discount; on every other date, and on the day counters whose year fractions add, the curve keeps
/// QuantLib's bits. A date before the reference date, or past the maximum date unless extrapolating, is
/// refused first, as a time is. A documented divergence.
/// </para>
/// </remarks>
public sealed class ImpliedTermStructure : YieldTermStructure
{
    // the relative difference beyond which QuantLib's sum of times doesn't denote a date's time
    private const double SumTolerance = 1.0e-12;

    private readonly YieldTermStructure originalCurve;
    private readonly double refDf;
    private readonly double refTime;

    /// <summary>Initializes a new instance of the <see cref="ImpliedTermStructure"/> class.</summary>
    /// <param name="originalCurve">The curve the implied curve is read from.</param>
    /// <param name="referenceDate">The implied reference date.</param>
    /// <exception cref="ArgumentNullException"><paramref name="originalCurve"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="referenceDate"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The original can't give its discount at the reference date.</exception>
    public ImpliedTermStructure(YieldTermStructure originalCurve, Date referenceDate)
        : this(originalCurve, referenceDate, null, null)
    {
    }

    private ImpliedTermStructure(YieldTermStructure originalCurve, Date referenceDate, Calendar? calendar, int? settlementDays)
        : base(referenceDate, calendar, Original(originalCurve).DayCounter, settlementDays)
    {
        this.originalCurve = originalCurve;
        AllowsExtrapolation = originalCurve.AllowsExtrapolation;

        // t is relative to the current reference date and needs to be converted to the time relative to the reference
        // date of the original curve
        refTime = DayCounter.YearFraction(originalCurve.ReferenceDate, ReferenceDate);
        refDf = originalCurve.Discount(ReferenceDate, true);
    }

    /// <summary>Gets the original curve's maximum date, read when asked for.</summary>
    public override Date MaxDate => originalCurve.MaxDate;

    /// <summary>
    /// A curve implied at the context's evaluation date advanced by settlement days on a calendar: QuantLib's
    /// constructor taking settlement days, whose reference date moves with the evaluation date. The curve is built for
    /// the context's evaluation date.
    /// </summary>
    /// <param name="originalCurve">The curve the implied curve is read from.</param>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="calendar">The calendar the settlement days are counted on.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The implied curve.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="originalCurve"/>, <paramref name="calendar"/> or <paramref name="context"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    /// <exception cref="NanoQLException">The original can't give its discount at the reference date.</exception>
    public static ImpliedTermStructure Create(
        YieldTermStructure originalCurve, int settlementDays, Calendar calendar, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(originalCurve);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(context);
        Date referenceDate = calendar.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
        return new ImpliedTermStructure(originalCurve, referenceDate, calendar, settlementDays);
    }

    // A date's discount: the curve's range checked first, as a time's is, then QuantLib's sum where it denotes the
    // date's time on the original's clock, and the original's ratio by date where it doesn't.
    internal override double DiscountOn(Date d, bool extrapolate)
    {
        double t = TimeFromReference(d);
        CheckRange(t, extrapolate);
        double summed = t + refTime;
        double own = originalCurve.TimeFromReference(d);
        if (Math.Abs(summed - own) <= SumTolerance * Math.Abs(own))
        {
            return DiscountImpl(t);
        }

        return originalCurve.Discount(d, true) / refDf;
    }

    /// <summary>The original's discount at the time plus the reference date's time, divided by its discount there.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The discount factor.</returns>
    protected override double DiscountImpl(double t) => originalCurve.Discount(t + refTime, true) / refDf;

    // The original, refused when null before the base constructor reads it.
    private static YieldTermStructure Original(YieldTermStructure originalCurve)
    {
        ArgumentNullException.ThrowIfNull(originalCurve);
        return originalCurve;
    }
}
