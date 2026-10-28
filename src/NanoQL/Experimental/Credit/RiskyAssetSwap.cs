// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/riskyassetswap.hpp ql/experimental/credit/riskyassetswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A risky asset swap: a risky fixed-rate bond bought at par and swapped into the floating rate plus a spread, the
/// bond's value from the default-probability curve with its recovery integrated over the default density.
/// </summary>
/// <remarks>
/// <para>
/// An instrument QuantLib prices without an engine: <see cref="Calculate"/> computes it. Its NPV is the package's
/// value, the risky bond's price less the fixed leg plus the floating leg and the spread, so at the fair spread it is
/// the par nominal, QuantLib's (the S-xcr plan's XR8 (xxxix)). Without a coupon the par coupon is used on every
/// calculation, where QuantLib's first calculation stores it in place of the coupon for later ones.
/// </para>
/// <para>
/// <b>The annuities</b> (XR8 (xii)): the fixed annuity runs over the fixed schedule, where QuantLib's runs over the
/// float schedule's dates with the fixed day counter, and the fair spread's denominator is the float annuity, the leg
/// the spread is paid on, where QuantLib's is that fixed annuity: an annual-fixed, quarterly-float swap's fair spread
/// reads 0.0118084 in QuantLib for 0.0115616 on equal schedules, and its package 99.58 on 100 at the fair spread. Where
/// the schedules and the day counters coincide the values are QuantLib's. <b>The recovery value</b> (XR8 (xxi)): the
/// daily Euler sum runs to each period's end, where QuantLib's stops a day short, −2.74e-3 relative on annual periods.
/// Reads between the evaluation date and a curve's later reference date are a discount and a survival of 1, where
/// QuantLib's refuses them (XR8 (x)); before the evaluation date, a seasoned swap's past periods, they are QuantLib's,
/// which refuses a date before the curve's (the S-xcr code review's C4 O1-1); <c>Divergences/RiskyAssetSwapTests.cs</c>.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class RiskyAssetSwap : Instrument
{
    private readonly Schedule fixedSchedule;
    private readonly Schedule floatSchedule;
    private readonly DayCounter fixedDayCounter;
    private readonly DayCounter floatDayCounter;
    private readonly double recoveryRate;
    private readonly YieldTermStructure yieldTS;
    private readonly DefaultProbabilityTermStructure defaultTS;
    private readonly double? coupon;

    /// <summary>Initializes a new instance of the <see cref="RiskyAssetSwap"/> class.</summary>
    /// <param name="fixedPayer">Whether the fixed leg is paid, the bond's coupons swapped away.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="fixedSchedule">The bond's and the fixed leg's schedule.</param>
    /// <param name="floatSchedule">The floating leg's schedule.</param>
    /// <param name="fixedDayCounter">The fixed leg's day counter.</param>
    /// <param name="floatDayCounter">The floating leg's day counter.</param>
    /// <param name="spread">The spread over the floating rate.</param>
    /// <param name="recoveryRate">The bond's recovery rate.</param>
    /// <param name="yieldTS">The discount curve.</param>
    /// <param name="defaultTS">The bond issuer's default-probability curve.</param>
    /// <param name="coupon">The bond's coupon, or <see langword="null"/>, QuantLib's <c>Null&lt;Rate&gt;()</c>, for
    /// the par coupon.</param>
    /// <exception cref="ArgumentNullException">An argument but <paramref name="coupon"/> is
    /// <see langword="null"/>.</exception>
    public RiskyAssetSwap(
        bool fixedPayer,
        double nominal,
        Schedule fixedSchedule,
        Schedule floatSchedule,
        DayCounter fixedDayCounter,
        DayCounter floatDayCounter,
        double spread,
        double recoveryRate,
        YieldTermStructure yieldTS,
        DefaultProbabilityTermStructure defaultTS,
        double? coupon = null)
    {
        ArgumentNullException.ThrowIfNull(fixedSchedule);
        ArgumentNullException.ThrowIfNull(floatSchedule);
        ArgumentNullException.ThrowIfNull(fixedDayCounter);
        ArgumentNullException.ThrowIfNull(floatDayCounter);
        ArgumentNullException.ThrowIfNull(yieldTS);
        ArgumentNullException.ThrowIfNull(defaultTS);
        FixedPayer = fixedPayer;
        Nominal = nominal;
        this.fixedSchedule = fixedSchedule;
        this.floatSchedule = floatSchedule;
        this.fixedDayCounter = fixedDayCounter;
        this.floatDayCounter = floatDayCounter;
        Spread = spread;
        this.recoveryRate = recoveryRate;
        this.yieldTS = yieldTS;
        this.defaultTS = defaultTS;
        this.coupon = coupon;
    }

    /// <summary>Gets the nominal.</summary>
    public double Nominal { get; }

    /// <summary>Gets the spread over the floating rate.</summary>
    public double Spread { get; }

    /// <summary>Gets a value indicating whether the fixed leg is paid.</summary>
    public bool FixedPayer { get; }

    /// <inheritdoc/>
    /// <remarks>The swap expires with its fixed schedule's last date, on the discount curve's reference date.</remarks>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimpleEvent(fixedSchedule.Dates[^1]).HasOccurred(context, yieldTS.ReferenceDate);
    }

    /// <summary>The results of an expired swap: its value 0, and no fair spread, which QuantLib computes from
    /// members its expired calculation leaves unset.</summary>
    /// <returns>The results.</returns>
    public override InstrumentResults SetupExpired() => new RiskyAssetSwapResults
    {
        NPV = 0.0,
        ErrorEstimate = 0.0,
        ValuationDate = null,
    };

    /// <summary>Prices the swap: the package's value and the fair spread.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The results; an expired swap's NPV is 0.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">A curve refuses a read before the evaluation date (a seasoned swap's past
    /// period) or past its maximum date.</exception>
    public RiskyAssetSwapResults Calculate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (IsExpired(context))
        {
            return (RiskyAssetSwapResults)SetupExpired();
        }

        // order of calls is essential
        Date today = context.EvaluationDate;
        double floatAnnuity = FloatAnnuity(context);
        double fixedAnnuity = FixedAnnuity(today);
        double parCoupon = ParCoupon(fixedAnnuity, today);
        double c = coupon ?? parCoupon;
        double recoveryValue = RecoveryValue(today);
        double riskyBondPrice = RiskyBondPrice(c, recoveryValue, today);
        double npv = riskyBondPrice
            - (c * fixedAnnuity)
            + Discount(today, fixedSchedule.Dates[0])
            - Discount(today, fixedSchedule.Dates[^1])
            + (Spread * floatAnnuity);
        npv *= Nominal;
        if (!FixedPayer)
        {
            npv *= -1;
        }

        return new RiskyAssetSwapResults { NPV = npv, FairSpread = FairSpread(c, recoveryValue, floatAnnuity, today) };
    }

    /// <summary>The floating leg's annuity: its periods' accruals discounted at their ends.</summary>
    /// <param name="context">The evaluation context, whose date the reads before the curve's are dated from.</param>
    /// <returns>The annuity, per unit of nominal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The discount curve refuses a read before the evaluation date or past its maximum
    /// date.</exception>
    public double FloatAnnuity(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date today = context.EvaluationDate;
        double annuity = 0;
        for (int i = 1; i < floatSchedule.Dates.Length; i++)
        {
            double dcf = floatDayCounter.YearFraction(floatSchedule.Dates[i - 1], floatSchedule.Dates[i]);
            annuity += dcf * Discount(today, floatSchedule.Dates[i]);
        }

        return annuity;
    }

    // The fixed leg's annuity, over the fixed schedule, where QuantLib's runs over the float schedule's dates (XR8
    // (xii)).
    private double FixedAnnuity(Date today)
    {
        double annuity = 0;
        for (int i = 1; i < fixedSchedule.Dates.Length; i++)
        {
            double dcf = fixedDayCounter.YearFraction(fixedSchedule.Dates[i - 1], fixedSchedule.Dates[i]);
            annuity += dcf * Discount(today, fixedSchedule.Dates[i]);
        }

        return annuity;
    }

    private double ParCoupon(double fixedAnnuity, Date today) =>
        (Discount(today, fixedSchedule.Dates[0]) - Discount(today, fixedSchedule.Dates[^1])) / fixedAnnuity;

    private double RecoveryValue(Date today)
    {
        double recoveryValue = 0;

        // simple Euler integral to evaluate the recovery value, to each period's end (XR8 (xxi))
        for (int i = 1; i < fixedSchedule.Dates.Length; i++)
        {
            Date d = fixedSchedule.Dates[i - 1] >= defaultTS.ReferenceDate ? fixedSchedule.Dates[i - 1] : defaultTS.ReferenceDate;
            Date d0 = d;
            do
            {
                double disc = Discount(today, d);
                double dd = defaultTS.DefaultDensity(d, true);
                double dcf = defaultTS.DayCounter.YearFraction(d0, d);
                recoveryValue += disc * dd * dcf;
                d0 = d;

                // QuantLib's NullCalendar().advance(d0, 1, Days, Unadjusted)
                d = d0 + 1;
            }
            while (d <= fixedSchedule.Dates[i]);
        }

        recoveryValue *= recoveryRate;
        return recoveryValue;
    }

    private double RiskyBondPrice(double c, double recoveryValue, Date today)
    {
        double value = 0;
        for (int i = 1; i < fixedSchedule.Dates.Length; i++)
        {
            double dcf = fixedDayCounter.YearFraction(fixedSchedule.Dates[i - 1], fixedSchedule.Dates[i]);
            value += dcf * Discount(today, fixedSchedule.Dates[i]) * Survival(today, fixedSchedule.Dates[i]);
        }

        value *= c;
        value += Discount(today, fixedSchedule.Dates[^1]) * Survival(today, fixedSchedule.Dates[^1]);
        return value + recoveryValue;
    }

    // The spread at which the package is worth par, over the float annuity, where QuantLib's divides by its fixed
    // annuity (XR8 (xii)).
    private double FairSpread(double c, double recoveryValue, double floatAnnuity, Date today)
    {
        double value = 0;
        for (int i = 1; i < fixedSchedule.Dates.Length; i++)
        {
            double dcf = fixedDayCounter.YearFraction(fixedSchedule.Dates[i - 1], fixedSchedule.Dates[i]);
            value += dcf * Discount(today, fixedSchedule.Dates[i]) * (1.0 - Survival(today, fixedSchedule.Dates[i]));
        }

        value *= c;
        value += Discount(today, fixedSchedule.Dates[^1]) * (1.0 - Survival(today, fixedSchedule.Dates[^1]));
        double initialDiscount = Discount(today, fixedSchedule.Dates[0]);
        return (1.0 - initialDiscount + value - recoveryValue) / floatAnnuity;
    }

    // A discount: 1 between the evaluation date and the curve's reference date, where QuantLib's read throws (XR8 (x));
    // before the evaluation date QuantLib's read, which refuses a date before the curve's (the S-xcr code review's C4
    // O1-1: a seasoned swap's past periods would otherwise enter at par).
    private double Discount(Date today, Date d) => d < today ? yieldTS.Discount(d) : yieldTS.AccruedDiscount(d);

    // A survival probability, extrapolated as QuantLib's: 1 between the evaluation date and the curve's reference date
    // (XR8 (x)); before the evaluation date QuantLib's read.
    private double Survival(Date today, Date d) =>
        d >= today && d < defaultTS.ReferenceDate ? 1.0 : defaultTS.SurvivalProbability(d, true);
}
