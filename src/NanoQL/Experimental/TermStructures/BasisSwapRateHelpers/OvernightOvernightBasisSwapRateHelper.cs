// SPDX-FileCopyrightText: Copyright (C) 2021 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/termstructures/basisswapratehelpers.hpp ql/experimental/termstructures/basisswapratehelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A rate helper for bootstrapping over overnight-overnight basis swaps.</summary>
/// <remarks>
/// <para>
/// The swap is assumed to pay the base index plus the basis and receive the other index. The helper can be used to
/// bootstrap the forecast curve for either index; the other index must have an existing forecast curve.
/// </para>
/// <para>
/// An exogenous discount curve can be passed. If none is passed, the curve being bootstrapped is also used for
/// discounting.
/// </para>
/// <para>
/// Both legs share the same schedule and payment lag, but their averaging methods can be configured independently.
/// This allows, for instance, an arithmetically averaged Fed Funds leg to be matched against a compounded SOFR leg.
/// Telescopic value dates are only applied to compounded legs. Arithmetically averaged legs retain their full
/// value-date schedule so that they are priced exactly.
/// </para>
/// <para>
/// Passing <see cref="Frequency.NoFrequency"/> as the payment frequency creates one coupon on each leg spanning the
/// full swap tenor.
/// </para>
/// <para>
/// The helper builds its swap, on a nominal of 100, when it is constructed and when it is re-dated, so it reads the
/// context's evaluation date then. Bound to the curve being built, its bootstrapped leg is rebuilt on the index cloned
/// onto that curve, what QuantLib's relinked handle gives its coupons.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class OvernightOvernightBasisSwapRateHelper : RelativeDateRateHelper
{
    private const double BasisPoint = 1.0e-4;

    private readonly Period tenor;
    private readonly int settlementDays;
    private readonly Calendar calendar;
    private readonly BusinessDayConvention convention;
    private readonly bool endOfMonth;
    private readonly YieldTermStructure? discountHandle;
    private readonly bool bootstrapBaseCurve;
    private readonly int paymentLag;
    private readonly Frequency paymentFrequency;
    private readonly RateAveragingType baseAveragingMethod;
    private readonly RateAveragingType otherAveragingMethod;
    private readonly bool telescopicValueDates;
    private readonly DateGenerationRule rule;

    // Written by the constructor, and on a fresh copy only: the bootstrapped index cloned onto the bound curve, and the
    // swap and its schedule rebuilt when the helper is re-dated, or its bootstrapped leg on that clone.
    private OvernightIndex baseIndex;
    private OvernightIndex otherIndex;
    private Schedule schedule;
    private Swap swap;

    /// <summary>Initializes a new instance of the <see cref="OvernightOvernightBasisSwapRateHelper"/> class.</summary>
    /// <param name="basis">The quoted basis, added to the base index.</param>
    /// <param name="tenor">The swap's maturity, from the settlement date.</param>
    /// <param name="settlementDays">The settlement days, counted from the evaluation date on the calendar.</param>
    /// <param name="calendar">The calendar of the settlement and the schedule.</param>
    /// <param name="convention">The schedule's business-day convention.</param>
    /// <param name="endOfMonth">Whether the schedule follows the end-of-month rule.</param>
    /// <param name="baseIndex">The index of the leg that pays the basis.</param>
    /// <param name="otherIndex">The index of the other leg.</param>
    /// <param name="context">The evaluation context, read when the swap is built.</param>
    /// <param name="discountHandle">The exogenous discount curve; the curve being bootstrapped when
    /// <see langword="null"/>.</param>
    /// <param name="bootstrapBaseCurve">Whether the base index's forecast curve is bootstrapped, rather than the other
    /// index's.</param>
    /// <param name="paymentLag">The payment lag of both legs, in business days.</param>
    /// <param name="paymentFrequency">The legs' payment frequency.</param>
    /// <param name="baseAveragingMethod">The base leg's averaging method.</param>
    /// <param name="otherAveragingMethod">The other leg's averaging method.</param>
    /// <param name="telescopicValueDates">Whether the compounded legs use telescopic value dates.</param>
    /// <param name="rule">The schedule's date-generation rule.</param>
    /// <exception cref="ArgumentNullException">An index, the calendar or the context is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or an enum
    /// argument isn't a member of its enum or a frequency a period has.</exception>
    /// <exception cref="ArgumentException">The swap can't be built from the arguments.</exception>
    public OvernightOvernightBasisSwapRateHelper(
        double basis,
        Period tenor,
        int settlementDays,
        Calendar calendar,
        BusinessDayConvention convention,
        bool endOfMonth,
        OvernightIndex baseIndex,
        OvernightIndex otherIndex,
        EvaluationContext context,
        YieldTermStructure? discountHandle = null,
        bool bootstrapBaseCurve = false,
        int paymentLag = 0,
        Frequency paymentFrequency = Frequency.Annual,
        RateAveragingType baseAveragingMethod = RateAveragingType.Compound,
        RateAveragingType otherAveragingMethod = RateAveragingType.Compound,
        bool telescopicValueDates = false,
        DateGenerationRule rule = DateGenerationRule.Backward)
        : base(basis, context)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);

        // QuantLib uses the calendar unchecked, and every use of its empty one fails.
        ArgumentNullException.ThrowIfNull(calendar);
        EnumArguments.Check(convention, nameof(convention));
        _ = Period.FromFrequency(paymentFrequency, nameof(paymentFrequency));
        EnumArguments.Check(baseAveragingMethod, nameof(baseAveragingMethod));
        EnumArguments.Check(otherAveragingMethod, nameof(otherAveragingMethod));
        EnumArguments.Check(rule, nameof(rule));
        if (baseIndex is null)
        {
            throw new ArgumentNullException(nameof(baseIndex), "null base overnight index");
        }

        if (otherIndex is null)
        {
            throw new ArgumentNullException(nameof(otherIndex), "null other overnight index");
        }

        this.tenor = tenor;
        this.settlementDays = settlementDays;
        this.calendar = calendar;
        this.convention = convention;
        this.endOfMonth = endOfMonth;
        this.discountHandle = discountHandle;
        this.bootstrapBaseCurve = bootstrapBaseCurve;
        this.paymentLag = paymentLag;
        this.paymentFrequency = paymentFrequency;
        this.baseAveragingMethod = baseAveragingMethod;
        this.otherAveragingMethod = otherAveragingMethod;
        this.telescopicValueDates = telescopicValueDates;
        this.rule = rule;

        // We need to clone the index whose forecast curve we want to bootstrap and copy the other one.
        this.baseIndex = bootstrapBaseCurve ? baseIndex.Clone(null) : baseIndex;
        this.otherIndex = bootstrapBaseCurve ? otherIndex : otherIndex.Clone(null);
        (schedule, swap) = BuildSwap(context);
    }

    /// <summary>Gets the swap the helper prices: QuantLib's <c>swap()</c>, the base leg first.</summary>
    public Swap Swap => swap;

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The swap's results aren't available.</exception>
    public override double ImpliedQuote(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireTermStructure();
        SwapResults results = DiscountingSwapEngine.Calculate(swap, context, discountHandle ?? TermStructure!);
        return -(IborIborBasisSwapRateHelper.Npv(results) / IborIborBasisSwapRateHelper.LegBps(results, 0))
            * BasisPoint;
    }

    /// <inheritdoc/>
    protected internal override BootstrapHelper<YieldTermStructure> WithTermStructure(YieldTermStructure t)
    {
        var copy = (OvernightOvernightBasisSwapRateHelper)base.WithTermStructure(t);
        if (bootstrapBaseCurve)
        {
            copy.baseIndex = baseIndex.Clone(t);
            copy.swap = swap.WithLeg(0, LegOn(copy.baseIndex, baseAveragingMethod));
        }
        else
        {
            copy.otherIndex = otherIndex.Clone(t);
            copy.swap = swap.WithLeg(1, LegOn(copy.otherIndex, otherAveragingMethod));
        }

        return copy;
    }

    /// <inheritdoc/>
    protected override void InitializeDates(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        (schedule, swap) = BuildSwap(context);
    }

    // A leg on the schedule, an index and an averaging method.
    private ImmutableArray<CashFlow> LegOn(OvernightIndex index, RateAveragingType averaging, Schedule? on = null) =>
        new OvernightLeg(on ?? schedule, index)
            .WithNotionals(100.0)
            .WithPaymentLag(paymentLag)
            .WithTelescopicValueDates(telescopicValueDates && averaging == RateAveragingType.Compound)
            .WithAveragingMethod(averaging)
            .ToLeg();

    // initializeDates' swap and dates, with its schedule.
    private (Schedule Schedule, Swap Swap) BuildSwap(EvaluationContext context)
    {
        Date earliest = calendar.Advance(
            context.EvaluationDate, settlementDays, TimeUnit.Days, BusinessDayConvention.Following);
        earliestDate = earliest;

        // unadjusted, to avoid a spurious stub
        Date terminationDate = earliest + tenor;

        Schedule built = new MakeSchedule()
            .From(earliest)
            .To(terminationDate)
            .WithFrequency(paymentFrequency)
            .WithCalendar(calendar)
            .WithConvention(convention)
            .EndOfMonth(endOfMonth)
            .WithRule(rule)
            .ToSchedule();

        Date maturity = built.EndDate();
        maturityDate = maturity;

        ImmutableArray<CashFlow> baseLeg = LegOn(baseIndex, baseAveragingMethod, built);
        ImmutableArray<CashFlow> otherLeg = LegOn(otherIndex, otherAveragingMethod, built);

        var lastBaseCoupon = (OvernightIndexedCoupon)baseLeg[^1];
        var lastOtherCoupon = (OvernightIndexedCoupon)otherLeg[^1];

        Date lastPaymentDate = IborIborBasisSwapRateHelper.Later(baseLeg[^1].Date, otherLeg[^1].Date);
        Date lastBaseFixingEndDate = baseIndex.MaturityDate(baseIndex.ValueDate(lastBaseCoupon.FixingDate()));
        Date lastOtherFixingEndDate = otherIndex.MaturityDate(otherIndex.ValueDate(lastOtherCoupon.FixingDate()));

        Date latestRelevant = IborIborBasisSwapRateHelper.Later(
            IborIborBasisSwapRateHelper.Later(maturity, lastPaymentDate),
            IborIborBasisSwapRateHelper.Later(lastBaseFixingEndDate, lastOtherFixingEndDate));
        latestRelevantDate = latestRelevant;
        pillarDate = latestRelevant;

        return (built, new Swap(baseLeg, otherLeg));
    }
}
