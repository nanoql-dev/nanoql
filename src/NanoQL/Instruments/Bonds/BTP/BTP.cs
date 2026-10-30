// SPDX-FileCopyrightText: Copyright (C) 2010, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bonds/btp.hpp ql/instruments/bonds/btp.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// An Italian BTP (Buono del Tesoro Poliennale): a fixed-rate bond with two settlement days, a face of 100, a
/// semiannual schedule ending on its maturity date, coupons on Actual/Actual (ISMA) paid on TARGET business days.
/// </summary>
/// <remarks>
/// Its accrued amount is rounded to five decimals, as QuantLib's is; the bond's clean price and yield read it. Its
/// conventions are read through <see cref="ConventionProvider.Current"/> when it is built
/// (<see cref="ConventionProvider.ResolveBond"/>): QuantLib's with nothing configured.
/// </remarks>
public sealed class BTP : FixedRateBond
{
    /// <summary>Initializes a new instance of the <see cref="BTP"/> class, redeemed at par.</summary>
    /// <param name="maturityDate">The maturity date.</param>
    /// <param name="fixedRate">The coupon rate.</param>
    /// <param name="context">
    /// The evaluation context: the schedule starts from its evaluation date when no start date is given.
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
    public BTP(
        Date maturityDate,
        double fixedRate,
        EvaluationContext context,
        Date? startDate = null,
        Date? issueDate = null)
        : this(maturityDate, fixedRate, 100.0, context, startDate, issueDate)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BTP"/> class with a redemption other than par, as a legacy BTP
    /// has.
    /// </summary>
    /// <param name="maturityDate">The maturity date.</param>
    /// <param name="fixedRate">The coupon rate.</param>
    /// <param name="redemption">The redemption price, in base 100.</param>
    /// <param name="context">
    /// The evaluation context: the schedule starts from its evaluation date when no start date is given.
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
    public BTP(
        Date maturityDate,
        double fixedRate,
        double redemption,
        EvaluationContext context,
        Date? startDate = null,
        Date? issueDate = null)
        : this(ConventionProvider.ReadBond(nameof(BTP), Reference), maturityDate, fixedRate, redemption, context, startDate, issueDate)
    {
    }

    private BTP(
        BondConventions conventions,
        Date maturityDate,
        double fixedRate,
        double redemption,
        EvaluationContext context,
        Date? startDate,
        Date? issueDate)
        : base(
            conventions.SettlementDays,
            100.0,
            new Schedule(
                startDate,
                maturityDate,
                conventions.Tenor,
                conventions.Calendar,
                conventions.Convention,
                conventions.TerminationDateConvention,
                conventions.Rule,
                conventions.EndOfMonth,
                context),
            [fixedRate],
            conventions.AccrualDayCounter,
            conventions.PaymentConvention,
            redemption,
            issueDate,
            conventions.PaymentCalendar,
            conventions.ExCouponPeriod,
            conventions.ExCouponCalendar,
            conventions.ExCouponConvention,
            conventions.ExCouponEndOfMonth)
    {
    }

    // QuantLib's conventions: two settlement days, a semiannual schedule back from the maturity date on NullCalendar,
    // unadjusted, at month end, Actual/Actual (ISMA) coupons paid Modified Following on TARGET, and no ex-coupon period.
    private static BondConventions Reference() =>
        new(
            2,
            new Period(6, TimeUnit.Months),
            new NullCalendar(),
            BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Backward,
            true,
            new ActualActual(ActualActualConvention.ISMA),
            BusinessDayConvention.ModifiedFollowing,
            new TARGET(),
            default,
            null,
            BusinessDayConvention.Unadjusted,
            false);

    /// <summary>The accrued amount, per 100 of notional, at a date, rounded to five decimals.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="d">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The accrued amount, rounded to the closest fifth decimal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    public override double AccruedAmount(EvaluationContext context, Date? d = null)
    {
        double result = base.AccruedAmount(context, d);
        return new ClosestRounding(5).Evaluate(result);
    }

    /// <summary>
    /// The yield of a clean price, on the BTP's conventions: Actual/Actual (ISMA), compounded annually.
    /// </summary>
    /// <param name="cleanPrice">The clean price, per 100 of notional.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxEvaluations">The solver's maximum number of evaluations.</param>
    /// <returns>The yield; 0 when the notional at the settlement date is 0.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="settlementDate"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The bond isn't tradable at the settlement date, or the solver
    /// fails.</exception>
    public double Yield(
        double cleanPrice,
        EvaluationContext context,
        Date? settlementDate = null,
        double accuracy = 1.0e-8,
        int maxEvaluations = 100) =>
        Yield(
            new BondPrice(cleanPrice, BondPriceType.Clean),
            new ActualActual(ActualActualConvention.ISMA),
            Compounding.Compounded,
            Frequency.Annual,
            context,
            settlementDate,
            accuracy,
            maxEvaluations);
}
