// SPDX-FileCopyrightText: Copyright (C) 2010, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bonds/btp.hpp ql/instruments/bonds/btp.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// An Italian CCTEU (Certificato di Credito del Tesoro): a floating-rate bond on six-month Euribor plus a spread, with
/// two settlement days, a face of 100 and a semiannual schedule ending on its maturity date.
/// </summary>
/// <remarks>
/// Its accrued amount is rounded to five decimals, as QuantLib's is; the bond's clean price and yield read it.
/// </remarks>
public sealed class CCTEU : FloatingRateBond
{
    /// <summary>Initializes a new instance of the <see cref="CCTEU"/> class.</summary>
    /// <param name="maturityDate">The maturity date.</param>
    /// <param name="spread">The spread over six-month Euribor.</param>
    /// <param name="context">
    /// The evaluation context: the schedule starts from its evaluation date when no start date is given, and the
    /// coupons read its at-par switch.
    /// </param>
    /// <param name="fwdCurve">
    /// The curve six-month Euribor is forecast on, or <see langword="null"/>, QuantLib's empty handle.
    /// </param>
    /// <param name="startDate">
    /// The schedule's start date, or <see langword="null"/>: whole years back from the maturity date, past the
    /// evaluation date.
    /// </param>
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A date is <c>default(Date)</c>, or no start date is given and the maturity date isn't after the evaluation date
    /// (QuantLib's "null effective date").
    /// </exception>
    public CCTEU(
        Date maturityDate,
        double spread,
        EvaluationContext context,
        YieldTermStructure? fwdCurve = null,
        Date? startDate = null,
        Date? issueDate = null)
        : base(
            2,
            100.0,
            new Schedule(
                startDate,
                maturityDate,
                new Period(6, TimeUnit.Months),
                new NullCalendar(),
                BusinessDayConvention.Unadjusted,
                BusinessDayConvention.Unadjusted,
                DateGenerationRule.Backward,
                true,
                context),
            new Euribor6M(fwdCurve),
            new Actual360(),
            context,
            BusinessDayConvention.Following,
            new Euribor6M().FixingDays,
            [1.0],
            [spread],
            [],
            [],
            false,
            100.0,
            issueDate)
    {
    }

    /// <summary>The accrued amount, per 100 of notional, at a date, rounded to five decimals.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="d">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The accrued amount, rounded to the closest fifth decimal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">A coupon's rate can't be computed.</exception>
    public override double AccruedAmount(EvaluationContext context, Date? d = null)
    {
        double result = base.AccruedAmount(context, d);
        return new ClosestRounding(5).Evaluate(result);
    }
}
