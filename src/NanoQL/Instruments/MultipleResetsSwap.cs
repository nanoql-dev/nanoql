// SPDX-FileCopyrightText: Copyright (C) 2026 Zain Mughal
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/multipleresetsswap.hpp ql/instruments/multipleresetsswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A swap of a fixed leg against a multiple-resets floating leg, each floating coupon compounding or averaging
/// <c>resetsPerCoupon</c> consecutive fixings of its index.
/// </summary>
/// <remarks>
/// <para>
/// The spread is added to each fixing, the multiple-resets leg's rate spread. The floating leg is built by
/// <see cref="FixedVsFloatingSwap"/>'s protected constructor's builder, where QuantLib replaces the base's leg after
/// construction; the base's floating schedule is the coupons' dates.
/// </para>
/// <para>
/// QuantLib's fair spread is linear in the floating leg's BPS, exact on an averaged leg but not on a compounded one,
/// whose spread compounds, so at it the swap misses par. On a compounded leg
/// <see cref="PricingEngines.DiscountingSwapEngine"/>'s <see cref="MultipleResetsSwap"/> overload solves for the spread
/// that zeroes the NPV, and <see cref="FetchResults"/> leaves the fair spread <see langword="null"/>, QuantLib's
/// "result not available", for another engine that gives none; an averaged leg keeps QuantLib's formula.
/// </para>
/// </remarks>
public sealed class MultipleResetsSwap : FixedVsFloatingSwap
{
    private readonly double nominal;
    private readonly BusinessDayConvention? givenPaymentConvention;
    private readonly Calendar? givenPaymentCalendar;

    /// <summary>Initializes a new instance of the <see cref="MultipleResetsSwap"/> class.</summary>
    /// <param name="type">Whether the fixed rate is paid (payer) or received.</param>
    /// <param name="nominal">The nominal of both legs.</param>
    /// <param name="fixedSchedule">The fixed leg's schedule.</param>
    /// <param name="fixedRate">The fixed rate.</param>
    /// <param name="fixedDayCount">The fixed leg's day counter.</param>
    /// <param name="fullResetSchedule">The schedule of every floating coupon's resets.</param>
    /// <param name="iborIndex">The index whose fixings the floating coupons combine.</param>
    /// <param name="resetsPerCoupon">The number of resets per floating coupon.</param>
    /// <param name="spread">The spread added to each fixing.</param>
    /// <param name="averagingMethod">How the fixings combine: compounded, the default, or averaged.</param>
    /// <param name="paymentConvention">The payment convention; the coupons' schedule's, unadjusted, when
    /// <see langword="null"/>.</param>
    /// <param name="paymentLag">The payment lag, in business days.</param>
    /// <param name="paymentCalendar">The payment calendar; for the floating leg the reset schedule's when
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">A schedule or the index is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="resetsPerCoupon"/> isn't positive, where QuantLib
    /// divides by it first; or an enum argument isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">The reset periods aren't a multiple of <paramref name="resetsPerCoupon"/>,
    /// with QuantLib's text.</exception>
    /// <exception cref="NanoQLException">The fixed leg can't be built from the arguments.</exception>
    public MultipleResetsSwap(
        SwapType type,
        double nominal,
        Schedule fixedSchedule,
        double fixedRate,
        DayCounter fixedDayCount,
        Schedule fullResetSchedule,
        IborIndex iborIndex,
        int resetsPerCoupon,
        double spread = 0.0,
        RateAveragingType averagingMethod = RateAveragingType.Compound,
        BusinessDayConvention? paymentConvention = null,
        int paymentLag = 0,
        Calendar? paymentCalendar = null)
        : base(
            type,
            Repeat(nominal, NotNull(fixedSchedule, nameof(fixedSchedule)).Size - 1),
            fixedSchedule,
            fixedRate,
            fixedDayCount,
            Repeat(nominal, CouponCount(fullResetSchedule, iborIndex, resetsPerCoupon)),
            CouponSchedule(fullResetSchedule, resetsPerCoupon),
            iborIndex,
            spread,
            iborIndex.DayCounter,
            paymentConvention,
            paymentLag,
            paymentCalendar,
            () => FloatingLegOf(
                nominal,
                fullResetSchedule,
                iborIndex,
                resetsPerCoupon,
                spread,
                averagingMethod,
                paymentConvention,
                paymentLag,
                paymentCalendar),
            nameof(fixedSchedule),
            nameof(paymentConvention))
    {
        this.nominal = nominal;
        FullResetSchedule = fullResetSchedule;
        ResetsPerCoupon = resetsPerCoupon;
        AveragingMethod = averagingMethod;
        givenPaymentConvention = paymentConvention;
        givenPaymentCalendar = paymentCalendar;
    }

    /// <summary>Gets the schedule of every floating coupon's resets.</summary>
    public Schedule FullResetSchedule { get; }

    /// <summary>Gets the number of resets per floating coupon.</summary>
    public int ResetsPerCoupon { get; }

    /// <summary>Gets how the fixings combine.</summary>
    public RateAveragingType AveragingMethod { get; }

    /// <summary>The swap's results: the base's, with the fair spread left to the engine on a compounded leg.</summary>
    /// <param name="results">The engine's results.</param>
    /// <returns>The results.</returns>
    public override FixedVsFloatingSwapResults FetchResults(InstrumentResults results)
    {
        double? given = (results as FixedVsFloatingSwapResults)?.FairSpread;
        FixedVsFloatingSwapResults r = base.FetchResults(results);
        return AveragingMethod == RateAveragingType.Compound ? r with { FairSpread = given } : r;
    }

    /// <summary>The same swap with another spread, for the fair spread's solve.</summary>
    /// <param name="spread">The spread.</param>
    /// <returns>The swap.</returns>
    internal MultipleResetsSwap WithSpread(double spread) => new(
        Type,
        nominal,
        FixedSchedule,
        FixedRate,
        FixedDayCount,
        FullResetSchedule,
        IborIndex,
        ResetsPerCoupon,
        spread,
        AveragingMethod,
        givenPaymentConvention,
        PaymentLag,
        givenPaymentCalendar);

    /// <summary>The same swap on another index, for a rate helper's copy bound to its curve.</summary>
    /// <param name="iborIndex">The index.</param>
    /// <returns>The swap.</returns>
    internal MultipleResetsSwap WithIborIndex(IborIndex iborIndex) => new(
        Type,
        nominal,
        FixedSchedule,
        FixedRate,
        FixedDayCount,
        FullResetSchedule,
        iborIndex,
        ResetsPerCoupon,
        Spread,
        AveragingMethod,
        givenPaymentConvention,
        PaymentLag,
        givenPaymentCalendar);

    private static ImmutableArray<CashFlow> FloatingLegOf(
        double nominal,
        Schedule fullResetSchedule,
        IborIndex iborIndex,
        int resetsPerCoupon,
        double spread,
        RateAveragingType averagingMethod,
        BusinessDayConvention? paymentConvention,
        int paymentLag,
        Calendar? paymentCalendar)
    {
        if (!((fullResetSchedule.Size - 1) % resetsPerCoupon == 0))
        {
            throw new ArgumentException(
                $"number of reset periods ({fullResetSchedule.Size - 1}) is not a multiple of resetsPerCoupon "
                + $"({resetsPerCoupon})",
                nameof(resetsPerCoupon));
        }

        int n = (fullResetSchedule.Size - 1) / resetsPerCoupon;
        BusinessDayConvention convention = ResolvePaymentConvention(
            paymentConvention, CouponSchedule(fullResetSchedule, resetsPerCoupon), nameof(paymentConvention));
        return new MultipleResetsLeg(fullResetSchedule, iborIndex, resetsPerCoupon)
            .WithNotionals(Enumerable.Repeat(nominal, n))
            .WithRateSpreads(spread)
            .WithAveragingMethod(averagingMethod)
            .WithPaymentAdjustment(convention)
            .WithPaymentLag(paymentLag)
            .WithPaymentCalendar(paymentCalendar ?? fullResetSchedule.Calendar)
            .ToLeg();
    }

    // QuantLib's buildCouponSchedule: every resetsPerCoupon-th reset date.
    private static Schedule CouponSchedule(Schedule fullResetSchedule, int resetsPerCoupon)
    {
        int n = (fullResetSchedule.Size - 1) / resetsPerCoupon;
        var dates = new Date[n + 1];
        for (int i = 0; i <= n; ++i)
        {
            dates[i] = fullResetSchedule.Date(i * resetsPerCoupon);
        }

        return new Schedule(dates);
    }

    // The number of floating coupons, the schedule, the index and the resets checked first.
    private static int CouponCount(Schedule fullResetSchedule, IborIndex iborIndex, int resetsPerCoupon)
    {
        ArgumentNullException.ThrowIfNull(fullResetSchedule);
        CheckFloatingIndex(iborIndex, nameof(iborIndex));
        if (!(resetsPerCoupon > 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resetsPerCoupon), resetsPerCoupon, "a coupon needs at least one reset, so resetsPerCoupon must be positive");
        }

        return Math.Max(fullResetSchedule.Size - 1, 0) / resetsPerCoupon;
    }

    private static IEnumerable<double> Repeat(double nominal, int count) => Enumerable.Repeat(nominal, Math.Max(count, 0));

    private static Schedule NotNull(Schedule schedule, string paramName) =>
        schedule ?? throw new ArgumentNullException(paramName);
}
