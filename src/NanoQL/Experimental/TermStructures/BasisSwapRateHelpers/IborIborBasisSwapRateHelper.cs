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

/// <summary>A rate helper for bootstrapping over IBOR-IBOR basis swaps.</summary>
/// <remarks>
/// <para>
/// The swap is assumed to pay the base index plus the basis and receive the other index. The helper can be used to
/// bootstrap the forecast curve for the base index (in which case you'll have to pass <c>bootstrapBaseCurve</c> =
/// <see langword="true"/> and provide the other index with a forecast curve) or the forecast curve for the other index
/// (in which case <c>bootstrapBaseCurve</c> = <see langword="false"/> and the base index will need a forecast curve).
/// In both cases, an exogenous discount curve is required. A payment lag can also be passed; it is applied to both
/// legs.
/// </para>
/// <para>
/// Limitation: the forecasting curve can't be bootstrapped with stubs computed off itself for now, so a stub-index
/// selection is refused on the leg whose curve is bootstrapped.
/// </para>
/// <para>
/// The helper builds its swap, on a nominal of 100, when it is constructed and when it is re-dated, so it reads the
/// context's evaluation date and its at-par switch then. Bound to the curve being built, its bootstrapped leg's coupons
/// are copied on the index cloned onto that curve, what QuantLib's relinked handle gives them.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class IborIborBasisSwapRateHelper : RelativeDateRateHelper
{
    private const double BasisPoint = 1.0e-4;

    private readonly Period tenor;
    private readonly int settlementDays;
    private readonly Calendar calendar;
    private readonly BusinessDayConvention convention;
    private readonly bool endOfMonth;
    private readonly YieldTermStructure? discountHandle;
    private readonly bool bootstrapBaseCurve;
    private readonly bool? useIndexedCoupons;
    private readonly DateGenerationRule rule;
    private readonly int paymentLag;
    private readonly StubIndexSelection baseStubIndexSelection;
    private readonly StubIndexSelection otherStubIndexSelection;

    // Written by the constructor, and on a fresh copy only: the bootstrapped index cloned onto the bound curve, and the
    // swap rebuilt when the helper is re-dated or its bootstrapped leg copied on that clone.
    private IborIndex baseIndex;
    private IborIndex otherIndex;
    private Swap swap;

    /// <summary>Initializes a new instance of the <see cref="IborIborBasisSwapRateHelper"/> class.</summary>
    /// <param name="basis">The quoted basis, added to the base index.</param>
    /// <param name="tenor">The swap's maturity, from the settlement date.</param>
    /// <param name="settlementDays">The settlement days, counted from the evaluation date on the calendar.</param>
    /// <param name="calendar">The calendar of the settlement and the schedules.</param>
    /// <param name="convention">The schedules' business-day convention.</param>
    /// <param name="endOfMonth">Whether the schedules follow the end-of-month rule.</param>
    /// <param name="baseIndex">The index of the leg that pays the basis.</param>
    /// <param name="otherIndex">The index of the other leg.</param>
    /// <param name="discountHandle">The exogenous discount curve, or <see langword="null"/>, QuantLib's empty handle,
    /// with which the implied quote refuses.</param>
    /// <param name="bootstrapBaseCurve">Whether the base index's forecast curve is bootstrapped, rather than the other
    /// index's.</param>
    /// <param name="context">The evaluation context, read when the swap is built.</param>
    /// <param name="useIndexedCoupons">Whether the legs' coupons are indexed or at par; the context's at-par switch
    /// when <see langword="null"/>.</param>
    /// <param name="rule">The schedules' date-generation rule.</param>
    /// <param name="paymentLag">The payment lag of both legs, in business days.</param>
    /// <param name="baseStubIndexSelection">The base leg's stub-index selection; none when
    /// <see langword="null"/>.</param>
    /// <param name="otherStubIndexSelection">The other leg's stub-index selection; none when
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">An index, the calendar or the context is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or the convention
    /// or the rule isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">A stub-index selection is given for the leg whose curve is bootstrapped, or
    /// the swap can't be built from the arguments.</exception>
    public IborIborBasisSwapRateHelper(
        double basis,
        Period tenor,
        int settlementDays,
        Calendar calendar,
        BusinessDayConvention convention,
        bool endOfMonth,
        IborIndex baseIndex,
        IborIndex otherIndex,
        YieldTermStructure? discountHandle,
        bool bootstrapBaseCurve,
        EvaluationContext context,
        bool? useIndexedCoupons = null,
        DateGenerationRule rule = DateGenerationRule.Backward,
        int paymentLag = 0,
        StubIndexSelection? baseStubIndexSelection = null,
        StubIndexSelection? otherStubIndexSelection = null)
        : base(basis, context)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);

        // QuantLib uses the calendar unchecked, and every use of its empty one fails.
        ArgumentNullException.ThrowIfNull(calendar);
        EnumArguments.Check(convention, nameof(convention));
        EnumArguments.Check(rule, nameof(rule));
        if (baseIndex is null)
        {
            throw new ArgumentNullException(nameof(baseIndex), "null base ibor index");
        }

        if (otherIndex is null)
        {
            throw new ArgumentNullException(nameof(otherIndex), "null other ibor index");
        }

        this.baseStubIndexSelection = baseStubIndexSelection ?? new StubIndexSelection();
        this.otherStubIndexSelection = otherStubIndexSelection ?? new StubIndexSelection();

        // stub candidates keep their own forwarding curves, so they cannot track the curve being bootstrapped
        if (!(bootstrapBaseCurve ? this.baseStubIndexSelection.Empty : this.otherStubIndexSelection.Empty))
        {
            throw new ArgumentException(
                "stub index conventions are not supported on the leg whose forecast curve is being bootstrapped",
                bootstrapBaseCurve ? nameof(baseStubIndexSelection) : nameof(otherStubIndexSelection));
        }

        this.tenor = tenor;
        this.settlementDays = settlementDays;
        this.calendar = calendar;
        this.convention = convention;
        this.endOfMonth = endOfMonth;
        this.discountHandle = discountHandle;
        this.bootstrapBaseCurve = bootstrapBaseCurve;
        this.useIndexedCoupons = useIndexedCoupons;
        this.rule = rule;
        this.paymentLag = paymentLag;

        // we need to clone the index whose forecast curve we want to bootstrap and copy the other one
        this.baseIndex = bootstrapBaseCurve ? baseIndex.Clone(null) : baseIndex;
        this.otherIndex = bootstrapBaseCurve ? otherIndex : otherIndex.Clone(null);
        swap = BuildSwap(context);
    }

    /// <summary>Gets the swap the helper prices: QuantLib's <c>swap()</c>, the base leg first.</summary>
    public Swap Swap => swap;

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">No discount curve was given, or the swap's results aren't available.
    /// </exception>
    public override double ImpliedQuote(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireTermStructure();
        SwapResults results = discountHandle is null
            ? DiscountingSwapEngine.CalculateWithoutCurve(swap, context)
            : DiscountingSwapEngine.Calculate(swap, context, discountHandle);
        return -(Npv(results) / LegBps(results, 0)) * BasisPoint;
    }

    /// <inheritdoc/>
    protected internal override BootstrapHelper<YieldTermStructure> WithTermStructure(YieldTermStructure t)
    {
        var copy = (IborIborBasisSwapRateHelper)base.WithTermStructure(t);
        if (bootstrapBaseCurve)
        {
            copy.baseIndex = baseIndex.Clone(t);
            copy.swap = WithIborIndex(swap, 0, copy.baseIndex);
        }
        else
        {
            copy.otherIndex = otherIndex.Clone(t);
            copy.swap = WithIborIndex(swap, 1, copy.otherIndex);
        }

        return copy;
    }

    /// <inheritdoc/>
    protected override void InitializeDates(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        swap = BuildSwap(context);
    }

    // QuantLib's NPV() and legBPS(j), with their refusals of a missing result.
    internal static double Npv(SwapResults results) =>
        results.NPV ?? throw new NanoQLException("NPV not available");

    internal static double LegBps(SwapResults results, int j) =>
        results.LegBPS[j] ?? throw new NanoQLException("result not available");

    // The swap with an Ibor leg's coupons copied on another index, each keeping its pricer: what QuantLib's relinked
    // handle gives the coupons of the swap it built. The coupons' dates read the index's calendar and fixing days,
    // which its clone shares.
    internal static Swap WithIborIndex(Swap swap, int j, IborIndex index)
    {
        ImmutableArray<CashFlow> ibor = swap.Leg(j);
        var leg = ImmutableArray.CreateBuilder<CashFlow>(ibor.Length);
        foreach (CashFlow cf in ibor)
        {
            var c = (IborCoupon)cf;
            leg.Add(new IborCoupon(
                c.Date,
                c.ContractNominal,
                c.AccrualStartDate,
                c.AccrualEndDate,
                c.FixingDays,
                index,
                c.Gearing,
                c.Spread,
                c.ReferencePeriodStart,
                c.ReferencePeriodEnd,
                c.DayCounter,
                c.IsInArrears,
                c.ExCouponDate,
                c.FixingConvention,
                c.Pricer));
        }

        return swap.WithLeg(j, leg.MoveToImmutable());
    }

    // std::max of two dates.
    internal static Date Later(Date a, Date b) => a < b ? b : a;

    // initializeDates' swap and dates.
    private Swap BuildSwap(EvaluationContext context)
    {
        Date earliest = calendar.Advance(
            context.EvaluationDate, settlementDays, TimeUnit.Days, BusinessDayConvention.Following);
        earliestDate = earliest;

        // unadjusted, to avoid a spurious stub
        Date terminationDate = earliest + tenor;

        Schedule baseSchedule = new MakeSchedule()
            .From(earliest)
            .To(terminationDate)
            .WithTenor(baseIndex.Tenor)
            .WithCalendar(calendar)
            .WithConvention(convention)
            .EndOfMonth(endOfMonth)
            .WithRule(rule)
            .ToSchedule();
        ImmutableArray<CashFlow> baseLeg = new IborLeg(baseSchedule, baseIndex)
            .WithNotionals(100.0)
            .WithPaymentLag(paymentLag)
            .WithIndexedCoupons(useIndexedCoupons)
            .WithStubIndexSelection(baseStubIndexSelection)
            .ToLeg(context);
        var lastBaseCoupon = (IborCoupon)baseLeg[^1];

        Schedule otherSchedule = new MakeSchedule()
            .From(earliest)
            .To(terminationDate)
            .WithTenor(otherIndex.Tenor)
            .WithCalendar(calendar)
            .WithConvention(convention)
            .EndOfMonth(endOfMonth)
            .WithRule(rule)
            .ToSchedule();
        ImmutableArray<CashFlow> otherLeg = new IborLeg(otherSchedule, otherIndex)
            .WithNotionals(100.0)
            .WithPaymentLag(paymentLag)
            .WithIndexedCoupons(useIndexedCoupons)
            .WithStubIndexSelection(otherStubIndexSelection)
            .ToLeg(context);
        var lastOtherCoupon = (IborCoupon)otherLeg[^1];

        Date maturity = Later(baseSchedule.EndDate(), otherSchedule.EndDate());
        maturityDate = maturity;

        Date lastPaymentDate = Later(baseLeg[^1].Date, otherLeg[^1].Date);
        Date latestRelevant = Later(
            Later(maturity, lastPaymentDate), Later(lastBaseCoupon.FixingEndDate(), lastOtherCoupon.FixingEndDate()));
        latestRelevantDate = latestRelevant;
        pillarDate = latestRelevant;

        return new Swap(baseLeg, otherLeg);
    }
}
