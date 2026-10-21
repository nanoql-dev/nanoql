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

/// <summary>A rate helper for bootstrapping over overnight-IBOR basis swaps.</summary>
/// <remarks>
/// <para>
/// The swap is assumed to pay overnight plus the basis and receive IBOR. As a default, the helper is used to bootstrap
/// the forecast curve for the IBOR index; the overnight index will need an existing forecast curve. If
/// <c>bootstrapBaseCurve</c> is set to <see langword="true"/>, instead, the helper will be used to bootstrap the
/// forecast curve for the overnight index; in this case, the IBOR index will need to be given a forecast curve.
/// </para>
/// <para>
/// An exogenous discount curve can be passed; if not, the curve being bootstrapped is also used for discounting. A
/// payment lag can also be passed; it is applied to both legs.
/// </para>
/// <para>
/// The payment frequency of the overnight leg can be overridden. It defaults to the tenor of the IBOR index. The IBOR
/// leg always pays at the tenor of its own index. Passing <see cref="Frequency.NoFrequency"/> creates a single
/// overnight coupon spanning the full swap tenor.
/// </para>
/// <para>
/// The averaging method and use of telescopic value dates can be configured for the overnight leg. Telescopic value
/// dates are only applied to compounded coupons.
/// </para>
/// <para>
/// The basis is quoted on the overnight (base) leg by default. Setting <c>basisOnIborLeg</c> solves for a margin quoted
/// on the IBOR leg instead.
/// </para>
/// <para>
/// A stub-index configuration can be passed for the IBOR leg; it is applied to that leg's irregular coupons (see
/// <see cref="StubIndexSelection"/>). Since the candidate indexes keep their own forwarding curves, this is only
/// allowed when <c>bootstrapBaseCurve</c> is <see langword="true"/>, i.e. when the IBOR index has an exogenous forecast
/// curve; otherwise the candidates could not track the curve under construction.
/// </para>
/// <para>
/// The helper builds its swap, on a nominal of 100, when it is constructed and when it is re-dated, so it reads the
/// context's evaluation date and its at-par switch then. Bound to the curve being built, its bootstrapped leg is
/// rebuilt on the index cloned onto that curve (the overnight leg) or its coupons copied on it (the IBOR leg), what
/// QuantLib's relinked handle gives them.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class OvernightIborBasisSwapRateHelper : RelativeDateRateHelper
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
    private readonly Frequency? overnightPaymentFrequency;
    private readonly bool? useIndexedCoupons;
    private readonly DateGenerationRule rule;
    private readonly RateAveragingType averagingMethod;
    private readonly bool telescopicValueDates;
    private readonly bool basisOnIborLeg;
    private readonly StubIndexSelection iborStubIndexSelection;

    // Written by the constructor, and on a fresh copy only: the bootstrapped index cloned onto the bound curve, and the
    // swap and the overnight leg's schedule rebuilt when the helper is re-dated, or its bootstrapped leg on that clone.
    private OvernightIndex baseIndex;
    private IborIndex otherIndex;
    private Schedule overnightSchedule;
    private Swap swap;

    /// <summary>Initializes a new instance of the <see cref="OvernightIborBasisSwapRateHelper"/> class.</summary>
    /// <param name="basis">The quoted basis: a spread on the overnight leg, or on the IBOR leg when
    /// <paramref name="basisOnIborLeg"/> is set.</param>
    /// <param name="tenor">The swap's maturity, from the settlement date.</param>
    /// <param name="settlementDays">The settlement days, counted from the evaluation date on the calendar.</param>
    /// <param name="calendar">The calendar of the settlement and the schedules.</param>
    /// <param name="convention">The schedules' business-day convention.</param>
    /// <param name="endOfMonth">Whether the schedules follow the end-of-month rule.</param>
    /// <param name="baseIndex">The overnight index.</param>
    /// <param name="otherIndex">The IBOR index.</param>
    /// <param name="context">The evaluation context, read when the swap is built.</param>
    /// <param name="discountHandle">The exogenous discount curve; the curve being bootstrapped when
    /// <see langword="null"/>.</param>
    /// <param name="bootstrapBaseCurve">Whether the overnight index's forecast curve is bootstrapped, rather than the
    /// IBOR index's.</param>
    /// <param name="paymentLag">The payment lag of both legs, in business days.</param>
    /// <param name="overnightPaymentFrequency">The overnight leg's payment frequency; the IBOR index's tenor when
    /// <see langword="null"/>.</param>
    /// <param name="useIndexedCoupons">Whether the IBOR leg's coupons are indexed or at par; the context's at-par
    /// switch when <see langword="null"/>.</param>
    /// <param name="rule">The schedules' date-generation rule.</param>
    /// <param name="averagingMethod">The overnight leg's averaging method.</param>
    /// <param name="telescopicValueDates">Whether the overnight leg's compounded coupons use telescopic value
    /// dates.</param>
    /// <param name="basisOnIborLeg">Whether the basis is quoted on the IBOR leg.</param>
    /// <param name="iborStubIndexSelection">The IBOR leg's stub-index selection; none when <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">An index, the calendar or the context is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or an enum
    /// argument isn't a member of its enum or a frequency a period has.</exception>
    /// <exception cref="ArgumentException">A stub-index selection is given while the IBOR curve is bootstrapped, or the
    /// swap can't be built from the arguments.</exception>
    public OvernightIborBasisSwapRateHelper(
        double basis,
        Period tenor,
        int settlementDays,
        Calendar calendar,
        BusinessDayConvention convention,
        bool endOfMonth,
        OvernightIndex baseIndex,
        IborIndex otherIndex,
        EvaluationContext context,
        YieldTermStructure? discountHandle = null,
        bool bootstrapBaseCurve = false,
        int paymentLag = 0,
        Frequency? overnightPaymentFrequency = null,
        bool? useIndexedCoupons = null,
        DateGenerationRule rule = DateGenerationRule.Backward,
        RateAveragingType averagingMethod = RateAveragingType.Compound,
        bool telescopicValueDates = false,
        bool basisOnIborLeg = false,
        StubIndexSelection? iborStubIndexSelection = null)
        : base(basis, context)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);

        // QuantLib uses the calendar unchecked, and every use of its empty one fails.
        ArgumentNullException.ThrowIfNull(calendar);
        EnumArguments.Check(convention, nameof(convention));
        if (overnightPaymentFrequency is Frequency f)
        {
            _ = Period.FromFrequency(f, nameof(overnightPaymentFrequency));
        }

        EnumArguments.Check(rule, nameof(rule));
        EnumArguments.Check(averagingMethod, nameof(averagingMethod));
        if (baseIndex is null)
        {
            throw new ArgumentNullException(nameof(baseIndex), "null base overnight index");
        }

        if (otherIndex is null)
        {
            throw new ArgumentNullException(nameof(otherIndex), "null other ibor index");
        }

        this.iborStubIndexSelection = iborStubIndexSelection ?? new StubIndexSelection();

        // stub candidates keep their own forwarding curves, so they cannot track the ibor curve if that is the one
        // being bootstrapped
        if (!(bootstrapBaseCurve || this.iborStubIndexSelection.Empty))
        {
            throw new ArgumentException(
                "stub index conventions are not supported on the leg whose forecast curve is being bootstrapped",
                nameof(iborStubIndexSelection));
        }

        this.tenor = tenor;
        this.settlementDays = settlementDays;
        this.calendar = calendar;
        this.convention = convention;
        this.endOfMonth = endOfMonth;
        this.discountHandle = discountHandle;
        this.bootstrapBaseCurve = bootstrapBaseCurve;
        this.paymentLag = paymentLag;
        this.overnightPaymentFrequency = overnightPaymentFrequency;
        this.useIndexedCoupons = useIndexedCoupons;
        this.rule = rule;
        this.averagingMethod = averagingMethod;
        this.telescopicValueDates = telescopicValueDates;
        this.basisOnIborLeg = basisOnIborLeg;

        // we need to clone the index whose forecast curve we want to bootstrap and copy the other one
        this.baseIndex = bootstrapBaseCurve ? baseIndex.Clone(null) : baseIndex;
        this.otherIndex = bootstrapBaseCurve ? otherIndex : otherIndex.Clone(null);
        (overnightSchedule, swap) = BuildSwap(context);
    }

    /// <summary>Gets the swap the helper prices: QuantLib's <c>swap()</c>, the overnight leg first.</summary>
    public Swap Swap => swap;

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The swap's results aren't available.</exception>
    public override double ImpliedQuote(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireTermStructure();
        SwapResults results = DiscountingSwapEngine.Calculate(swap, context, discountHandle ?? TermStructure!);
        int leg = basisOnIborLeg ? 1 : 0;
        return -(IborIborBasisSwapRateHelper.Npv(results) / IborIborBasisSwapRateHelper.LegBps(results, leg))
            * BasisPoint;
    }

    /// <inheritdoc/>
    protected internal override BootstrapHelper<YieldTermStructure> WithTermStructure(YieldTermStructure t)
    {
        var copy = (OvernightIborBasisSwapRateHelper)base.WithTermStructure(t);
        if (bootstrapBaseCurve)
        {
            copy.baseIndex = baseIndex.Clone(t);
            copy.swap = swap.WithLeg(0, OvernightLegOn(overnightSchedule, copy.baseIndex));
        }
        else
        {
            copy.otherIndex = otherIndex.Clone(t);
            copy.swap = IborIborBasisSwapRateHelper.WithIborIndex(swap, 1, copy.otherIndex);
        }

        return copy;
    }

    /// <inheritdoc/>
    protected override void InitializeDates(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        (overnightSchedule, swap) = BuildSwap(context);
    }

    // The overnight leg on a schedule and an index.
    private ImmutableArray<CashFlow> OvernightLegOn(Schedule schedule, OvernightIndex index) =>
        new OvernightLeg(schedule, index)
            .WithNotionals(100.0)
            .WithPaymentLag(paymentLag)
            .WithTelescopicValueDates(telescopicValueDates && averagingMethod == RateAveragingType.Compound)
            .WithAveragingMethod(averagingMethod)
            .ToLeg();

    // initializeDates' swap and dates, with the overnight leg's schedule.
    private (Schedule OvernightSchedule, Swap Swap) BuildSwap(EvaluationContext context)
    {
        Date earliest = calendar.Advance(
            context.EvaluationDate, settlementDays, TimeUnit.Days, BusinessDayConvention.Following);
        earliestDate = earliest;

        // unadjusted, to avoid a spurious stub
        Date terminationDate = earliest + tenor;

        Period overnightTenor = overnightPaymentFrequency is Frequency f ? new Period(f) : otherIndex.Tenor;
        Schedule onSchedule = new MakeSchedule()
            .From(earliest)
            .To(terminationDate)
            .WithTenor(overnightTenor)
            .WithCalendar(calendar)
            .WithConvention(convention)
            .EndOfMonth(endOfMonth)
            .WithRule(rule)
            .ToSchedule();

        ImmutableArray<CashFlow> baseLeg = OvernightLegOn(onSchedule, baseIndex);
        var lastBaseCoupon = (OvernightIndexedCoupon)baseLeg[^1];

        // an ibor leg pays one coupon per fixing, so its payment frequency is the tenor of its own index
        Schedule iborSchedule = new MakeSchedule()
            .From(earliest)
            .To(terminationDate)
            .WithTenor(otherIndex.Tenor)
            .WithCalendar(calendar)
            .WithConvention(convention)
            .EndOfMonth(endOfMonth)
            .WithRule(rule)
            .ToSchedule();

        ImmutableArray<CashFlow> otherLeg = new IborLeg(iborSchedule, otherIndex)
            .WithNotionals(100.0)
            .WithPaymentLag(paymentLag)
            .WithIndexedCoupons(useIndexedCoupons)
            .WithStubIndexSelection(iborStubIndexSelection)
            .ToLeg(context);
        var lastOtherCoupon = (IborCoupon)otherLeg[^1];

        Date maturity = IborIborBasisSwapRateHelper.Later(onSchedule.EndDate(), iborSchedule.EndDate());
        maturityDate = maturity;

        // the payment lag can push the last payment past the maturity date, in which case the discount curve is
        // needed up to that date
        Date lastPaymentDate = IborIborBasisSwapRateHelper.Later(baseLeg[^1].Date, otherLeg[^1].Date);
        Date lastBaseFixingEndDate = baseIndex.MaturityDate(baseIndex.ValueDate(lastBaseCoupon.FixingDate()));

        Date latestRelevant = IborIborBasisSwapRateHelper.Later(
            IborIborBasisSwapRateHelper.Later(maturity, lastPaymentDate),
            IborIborBasisSwapRateHelper.Later(lastBaseFixingEndDate, lastOtherCoupon.FixingEndDate()));
        latestRelevantDate = latestRelevant;
        pillarDate = latestRelevant;

        return (onSchedule, new Swap(baseLeg, otherLeg));
    }
}
