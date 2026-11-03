// SPDX-FileCopyrightText: Copyright (C) 2026 Zain Mughal
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/multipleresetsswaphelper.hpp ql/termstructures/yield/multipleresetsswaphelper.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A rate helper bootstrapping from the quotes of multiple-resets swaps.</summary>
/// <remarks>
/// On the rate helpers' contract: the helper's index is cloned without a curve, and the copy bound to a curve under
/// construction rebuilds the swap on the index cloned on it, where QuantLib relinks its handle; a re-dated helper
/// rebuilds the swap under the context. The implied quote is the swap's fair rate, priced on the discounting curve
/// given or else on the curve under construction, without the settlement date's flows. The latest date is the last
/// payment date of either leg or the end of the last fixing's period, whichever is later.
/// </remarks>
public sealed class MultipleResetsSwapRateHelper : RelativeDateRateHelper
{
    private readonly int settlementDays;
    private readonly Period tenor;
    private readonly int resetsPerCoupon;
    private readonly RateAveragingType averagingMethod;
    private readonly double spread;
    private readonly Frequency fixedFrequency;
    private readonly DayCounter fixedDayCount;
    private readonly BusinessDayConvention fixedConvention;
    private readonly YieldTermStructure? discountHandle;

    // Written by the constructor, and on a fresh copy only: the index cloned on the bound curve, the swap rebuilt when
    // the helper is re-dated and rebuilt on the bound curve's index.
    private IborIndex iborIndex;
    private MultipleResetsSwap swap;

    /// <summary>Initializes a new instance of the <see cref="MultipleResetsSwapRateHelper"/> class.</summary>
    /// <param name="settlementDays">The settlement days from the evaluation date to the start.</param>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="fixedRate">The quoted fixed rate.</param>
    /// <param name="iborIndex">The index whose fixings the floating coupons combine.</param>
    /// <param name="resetsPerCoupon">The number of resets per floating coupon.</param>
    /// <param name="context">The evaluation context the swap is built under.</param>
    /// <param name="discountingCurve">The discounting curve, or <see langword="null"/> for the curve under
    /// construction.</param>
    /// <param name="averagingMethod">How the floating fixings combine.</param>
    /// <param name="spread">The spread added to each floating fixing.</param>
    /// <param name="fixedFrequency">The fixed leg's frequency, the coupons' period by default.</param>
    /// <param name="fixedDayCount">The fixed leg's day counter, the index's when <see langword="null"/>.</param>
    /// <param name="fixedConvention">The fixed leg's convention.</param>
    /// <exception cref="ArgumentNullException"><paramref name="iborIndex"/> or <paramref name="context"/> is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative,
    /// <paramref name="resetsPerCoupon"/> isn't positive, or an enum argument isn't a member of its enum.</exception>
    /// <exception cref="NanoQLException">The swap can't be built.</exception>
    public MultipleResetsSwapRateHelper(
        int settlementDays,
        Period tenor,
        double fixedRate,
        IborIndex iborIndex,
        int resetsPerCoupon,
        EvaluationContext context,
        YieldTermStructure? discountingCurve = null,
        RateAveragingType averagingMethod = RateAveragingType.Compound,
        double spread = 0.0,
        Frequency fixedFrequency = Frequency.NoFrequency,
        DayCounter? fixedDayCount = null,
        BusinessDayConvention fixedConvention = BusinessDayConvention.ModifiedFollowing)
        : base(fixedRate, context)
    {
        ArgumentNullException.ThrowIfNull(iborIndex);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        EnumArguments.Check(averagingMethod, nameof(averagingMethod));
        EnumArguments.Check(fixedFrequency, nameof(fixedFrequency));
        EnumArguments.Check(fixedConvention, nameof(fixedConvention));
        this.settlementDays = settlementDays;
        this.tenor = tenor;
        this.resetsPerCoupon = resetsPerCoupon;
        this.averagingMethod = averagingMethod;
        this.spread = spread;
        this.fixedFrequency = fixedFrequency;
        this.fixedDayCount = fixedDayCount ?? iborIndex.DayCounter;
        this.fixedConvention = fixedConvention;
        discountHandle = discountingCurve;

        // Clone the index so it forwards rates from the curve under construction.
        this.iborIndex = iborIndex.Clone(null);
        swap = BuildSwap(context);
        SetDates();
    }

    /// <summary>Gets the swap the helper prices: QuantLib's <c>swap()</c>.</summary>
    public MultipleResetsSwap Swap => swap;

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">No term structure is bound, or the swap's fair rate isn't available, as for an
    /// expired swap.</exception>
    public override double ImpliedQuote(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireTermStructure();
        YieldTermStructure discount = discountHandle ?? TermStructure!;
        FixedVsFloatingSwapResults results =
            DiscountingSwapEngine.Calculate((FixedVsFloatingSwap)swap, context, discount, includeSettlementDateFlows: false);
        return results.FairRate ?? throw new NanoQLException("result not available");
    }

    /// <inheritdoc/>
    protected internal override BootstrapHelper<YieldTermStructure> WithTermStructure(YieldTermStructure t)
    {
        var copy = (MultipleResetsSwapRateHelper)base.WithTermStructure(t);
        copy.iborIndex = iborIndex.Clone(t);
        copy.swap = swap.WithIborIndex(copy.iborIndex);
        return copy;
    }

    /// <inheritdoc/>
    protected override void InitializeDates(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        swap = BuildSwap(context);
        SetDates();
    }

    private MultipleResetsSwap BuildSwap(EvaluationContext context) =>
        new MakeMultipleResetsSwap(tenor, iborIndex, resetsPerCoupon)
            .WithFixedRate(0.0)
            .WithSettlementDays(settlementDays)
            .WithFixedLegFrequency(fixedFrequency)
            .WithFixedLegDayCount(fixedDayCount)
            .WithFixedLegConvention(fixedConvention)
            .WithFloatingLegSpread(spread)
            .WithAveragingMethod(averagingMethod)
            .ToSwap(context);

    // initializeDates' dates, from the swap built.
    private void SetDates()
    {
        earliestDate = swap.StartDate();
        maturityDate = swap.MaturityDate();

        // The last fixing forecasts the index until its own maturity, which
        // can be later than the end of the swap if the last reset date was
        // adjusted; the curve must extend at least that far.
        var lastCoupon = swap.FloatingLeg[^1] as MultipleResetsCoupon
            ?? throw new NanoQLException("multiple-resets coupon expected");
        Date lastFixingEndDate = iborIndex.MaturityDate(iborIndex.ValueDate(lastCoupon.FixingDates[^1]));
        Date latest = swap.FixedLeg[^1].Date;
        if (latest < swap.FloatingLeg[^1].Date)
        {
            latest = swap.FloatingLeg[^1].Date;
        }

        if (latest < lastFixingEndDate)
        {
            latest = lastFixingEndDate;
        }

        latestRelevantDate = latestDate = latest;
    }
}
