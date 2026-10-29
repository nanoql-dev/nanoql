// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/termstructures/overnightindexedfundingratehelper.hpp ql/experimental/termstructures/overnightindexedfundingratehelper.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A rate helper quoting the funding margin over a compounded overnight rate: QuantLib's
/// <c>OvernightIndexedFundingRateHelper</c>.
/// </summary>
/// <remarks>
/// <para>
/// The instrument exchanges a principal of 100 at its start and end and pays the overnight rate compounded over each
/// payment period plus the margin; every payment, the principal exchanges included, takes the payment lag. The overnight
/// rate is projected from the index's own forwarding curve, which it must have; the curve being bootstrapped discounts
/// every flow, flows on its reference date included. The implied margin is the swap's NPV over its overnight leg's BPS.
/// </para>
/// <para>
/// A helper built on a tenor follows the evaluation date: re-dated for another context, it rebuilds its swap (S5R1).
/// QuantLib's margin quote is a <see cref="double"/>.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class OvernightIndexedFundingRateHelper : RelativeDateRateHelper
{
    private readonly Period tenor;
    private readonly int? settlementDays;
    private readonly Date? startDate;
    private readonly Date? endDate;
    private readonly Calendar calendar;
    private readonly BusinessDayConvention convention;
    private readonly bool endOfMonth;
    private readonly OvernightIndex overnightIndex;
    private readonly Period paymentTenor;
    private readonly DayCounter paymentDayCounter;
    private readonly int paymentLag;
    private readonly bool telescopicValueDates;
    private readonly DateGenerationRule rule;
    private readonly PillarChoice pillarChoice;
    private readonly Date? customPillarDate;

    // Written only by the constructor and on a fresh copy (InitializeDates).
    private Swap swap = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="OvernightIndexedFundingRateHelper"/> class on a tenor from the
    /// settlement date, which follows the evaluation date.
    /// </summary>
    /// <param name="margin">The quoted margin.</param>
    /// <param name="tenor">The tenor.</param>
    /// <param name="settlementDays">The business days from the evaluation date to the start.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="convention">The schedule's convention.</param>
    /// <param name="endOfMonth">Whether the schedule follows the end-of-month rule.</param>
    /// <param name="overnightIndex">The overnight index, with its forwarding curve.</param>
    /// <param name="paymentTenor">The payment period.</param>
    /// <param name="paymentDayCounter">The payment day counter.</param>
    /// <param name="context">The evaluation context the helper is dated in.</param>
    /// <param name="paymentLag">The payment lag in business days.</param>
    /// <param name="telescopicValueDates">Whether the overnight coupons use telescopic value dates.</param>
    /// <param name="rule">The schedule's rule.</param>
    /// <param name="pillar">The pillar choice.</param>
    /// <param name="customPillarDate">The pillar date, for a custom pillar.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or an enum argument
    /// isn't a member of its enum.</exception>
    /// <exception cref="NanoQLException">The index has no forwarding curve, the payment tenor isn't positive, the lag
    /// is negative, the start isn't before the end, or the custom pillar falls outside the helper's dates, with
    /// QuantLib's texts.</exception>
    public OvernightIndexedFundingRateHelper(
        double margin,
        Period tenor,
        int settlementDays,
        Calendar calendar,
        BusinessDayConvention convention,
        bool endOfMonth,
        OvernightIndex overnightIndex,
        Period paymentTenor,
        DayCounter paymentDayCounter,
        EvaluationContext context,
        int paymentLag = 0,
        bool telescopicValueDates = false,
        DateGenerationRule rule = DateGenerationRule.Backward,
        PillarChoice pillar = PillarChoice.LastRelevantDate,
        Date? customPillarDate = null)
        : base(margin, context ?? throw new ArgumentNullException(nameof(context)), true)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        this.tenor = tenor;
        this.settlementDays = settlementDays;
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.convention = convention;
        this.endOfMonth = endOfMonth;
        this.paymentTenor = paymentTenor;
        this.paymentDayCounter = paymentDayCounter ?? throw new ArgumentNullException(nameof(paymentDayCounter));
        this.paymentLag = paymentLag;
        this.telescopicValueDates = telescopicValueDates;
        this.rule = rule;
        pillarChoice = pillar;
        this.customPillarDate = customPillarDate;
        this.overnightIndex = Initialize(overnightIndex);
        InitializeDates(context);
    }

    /// <summary>Initializes a new instance of the <see cref="OvernightIndexedFundingRateHelper"/> class on given dates.</summary>
    /// <param name="margin">The quoted margin.</param>
    /// <param name="startDate">The start.</param>
    /// <param name="endDate">The end.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="convention">The schedule's convention.</param>
    /// <param name="endOfMonth">Whether the schedule follows the end-of-month rule.</param>
    /// <param name="overnightIndex">The overnight index, with its forwarding curve.</param>
    /// <param name="paymentTenor">The payment period.</param>
    /// <param name="paymentDayCounter">The payment day counter.</param>
    /// <param name="context">The evaluation context the helper is built in.</param>
    /// <param name="paymentLag">The payment lag in business days.</param>
    /// <param name="telescopicValueDates">Whether the overnight coupons use telescopic value dates.</param>
    /// <param name="rule">The schedule's rule.</param>
    /// <param name="pillar">The pillar choice.</param>
    /// <param name="customPillarDate">The pillar date, for a custom pillar.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its enum.</exception>
    /// <exception cref="NanoQLException">As the tenor form.</exception>
    public OvernightIndexedFundingRateHelper(
        double margin,
        Date startDate,
        Date endDate,
        Calendar calendar,
        BusinessDayConvention convention,
        bool endOfMonth,
        OvernightIndex overnightIndex,
        Period paymentTenor,
        DayCounter paymentDayCounter,
        EvaluationContext context,
        int paymentLag = 0,
        bool telescopicValueDates = false,
        DateGenerationRule rule = DateGenerationRule.Backward,
        PillarChoice pillar = PillarChoice.LastRelevantDate,
        Date? customPillarDate = null)
        : base(margin, null, false)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(startDate, nameof(startDate));
        Date.ThrowIfDefault(endDate, nameof(endDate));
        this.startDate = startDate;
        this.endDate = endDate;
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.convention = convention;
        this.endOfMonth = endOfMonth;
        this.paymentTenor = paymentTenor;
        this.paymentDayCounter = paymentDayCounter ?? throw new ArgumentNullException(nameof(paymentDayCounter));
        this.paymentLag = paymentLag;
        this.telescopicValueDates = telescopicValueDates;
        this.rule = rule;
        pillarChoice = pillar;
        this.customPillarDate = customPillarDate;
        this.overnightIndex = Initialize(overnightIndex);
        InitializeDates(context);
    }

    /// <summary>Gets the funding swap: the overnight leg with the margin, and the principal exchanges.</summary>
    public Swap Swap => swap;

    /// <inheritdoc/>
    /// <remarks>Minus the swap's NPV over its overnight leg's BPS, on the bound curve.</remarks>
    /// <exception cref="NanoQLException">The helper isn't bound to a curve, or the swap's results aren't
    /// available.</exception>
    public override double ImpliedQuote(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireTermStructure();
        SwapResults results = DiscountingSwapEngine.Calculate(swap, context, TermStructure!, includeSettlementDateFlows: true);
        double npv = results.NPV ?? throw new NanoQLException("result not available");
        double bps = (results.LegBPS.IsDefaultOrEmpty ? null : results.LegBPS[0])
            ?? throw new NanoQLException("result not available");
        return -(npv / bps) * 1.0e-4;
    }

    /// <inheritdoc/>
    protected override void InitializeDates(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date effectiveDate;
        Date terminationDate;
        if (startDate is Date s)
        {
            effectiveDate = s;
            terminationDate = endDate!.Value;
        }
        else
        {
            Date today = context.EvaluationDate;
            effectiveDate = calendar.Advance(today, settlementDays!.Value, TimeUnit.Days, BusinessDayConvention.Following);
            terminationDate = effectiveDate + tenor;
        }

        if (!(effectiveDate < terminationDate))
        {
            throw new NanoQLException("start date must be before end date");
        }

        Schedule schedule = new MakeSchedule()
            .From(effectiveDate)
            .To(terminationDate)
            .WithTenor(paymentTenor)
            .WithCalendar(calendar)
            .WithConvention(convention)
            .WithTerminationDateConvention(convention)
            .EndOfMonth(endOfMonth)
            .WithRule(rule)
            .ToSchedule();
        var overnightLeg = new OvernightLeg(schedule, overnightIndex)
            .WithNotionals(100.0)
            .WithPaymentDayCounter(paymentDayCounter)
            .WithPaymentCalendar(calendar)
            .WithPaymentAdjustment(BusinessDayConvention.Following)
            .WithPaymentLag(paymentLag)
            .WithTelescopicValueDates(telescopicValueDates)
            .WithAveragingMethod(RateAveragingType.Compound)
            .ToLeg();
        Date initialPaymentDate = calendar.Advance(
            schedule.StartDate(), paymentLag, TimeUnit.Days, BusinessDayConvention.Following);
        Date finalPaymentDate = overnightLeg[^1].Date;
        System.Collections.Immutable.ImmutableArray<CashFlow> exchangeLeg =
            [new SimpleCashFlow(100.0, initialPaymentDate), new SimpleCashFlow(-100.0, finalPaymentDate)];

        earliestDate = schedule.StartDate();
        maturityDate = schedule.EndDate();
        var lastCoupon = overnightLeg[^1] as OvernightIndexedCoupon
            ?? throw new NanoQLException("expected an overnight indexed coupon");
        Date lastFixingEndDate = overnightIndex.MaturityDate(overnightIndex.ValueDate(lastCoupon.FixingDate()));
        latestRelevantDate = finalPaymentDate < lastFixingEndDate ? lastFixingEndDate : finalPaymentDate;
        switch (pillarChoice)
        {
            case PillarChoice.MaturityDate:
                pillarDate = maturityDate;
                break;
            case PillarChoice.LastRelevantDate:
                pillarDate = latestRelevantDate;
                break;
            default:
                // QuantLib's default custom pillar, Date(), is before every start
                pillarDate = customPillarDate;
                if (!(pillarDate is Date custom && custom >= earliestDate))
                {
                    throw new NanoQLException("pillar date must not be before the start date");
                }

                if (!(pillarDate <= latestRelevantDate))
                {
                    throw new NanoQLException("pillar date must not be after the latest relevant date");
                }

                break;
        }

        latestDate = pillarDate;
        swap = new Swap(overnightLeg, exchangeLeg);
    }

    // QuantLib's initialize: the index's checks, then the payment tenor's and the lag's.
    private OvernightIndex Initialize(OvernightIndex overnightIndex)
    {
        ArgumentNullException.ThrowIfNull(overnightIndex, nameof(overnightIndex));
        if (overnightIndex.ForwardingTermStructure is null)
        {
            throw new NanoQLException("the overnight index must have a forwarding curve");
        }

        if (!(paymentTenor.Length > 0))
        {
            throw new NanoQLException("non-positive payment tenor");
        }

        if (!(paymentLag >= 0))
        {
            throw new NanoQLException("negative payment lag");
        }

        EnumArguments.Check(convention, nameof(convention));
        EnumArguments.Check(rule, nameof(rule));
        EnumArguments.Check(pillarChoice, nameof(pillarChoice));
        return overnightIndex;
    }
}
