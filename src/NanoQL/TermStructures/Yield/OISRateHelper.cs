// SPDX-FileCopyrightText: Copyright (C) 2009, 2012 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009, 2012 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/oisratehelper.hpp ql/termstructures/yield/oisratehelper.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A rate helper for bootstrapping over overnight indexed swap rates: the fixed rate of the overnight indexed swap,
/// built at construction, that the curve prices at par.
/// </summary>
/// <remarks>
/// <para>
/// The helper builds its swap when it is constructed, through <see cref="MakeOIS"/>, at a zero fixed rate and without
/// the spread, which the implied quote adds. An empty calendar, day counter, end-of-month flag or fixed-leg frequency
/// leaves the builder's default; a pricer given is set on the swap's overnight leg. The overnight coupons build every
/// daily value date, telescopic or not (playbook §2's telescopic row).
/// </para>
/// <para>
/// The latest relevant date is the latest of the maturity, the last payment and the end of the last fixing's deposit,
/// and the latest date stays that date whatever the pillar, where QuantLib's FRA and swap helpers reset theirs to the
/// pillar date. Bound to a curve, its copy holds a swap whose overnight coupons are copies on a clone of the index on
/// that curve, each keeping its pricer: QuantLib relinks the index's handle under the built swap. The swap is
/// discounted on the curve, or on the discounting curve given. Its dates follow the evaluation date unless explicit
/// start and end dates are given.
/// </para>
/// </remarks>
public sealed class OISRateHelper : RelativeDateRateHelper
{
    // weak implementation... to be improved
    private const double BasisPoint = 1.0e-4;

    private readonly int? settlementDays;
    private readonly Period tenor;
    private readonly Date? startDate;
    private readonly Date? endDate;
    private readonly YieldTermStructure? discountHandle;
    private readonly bool telescopicValueDates;
    private readonly int paymentLag;
    private readonly BusinessDayConvention paymentConvention;
    private readonly Frequency paymentFrequency;
    private readonly Calendar? paymentCalendar;
    private readonly Period forwardStart;
    private readonly double overnightSpread;
    private readonly PillarChoice pillarChoice;
    private readonly Date? customPillarDate;
    private readonly RateAveragingType averagingMethod;
    private readonly bool? endOfMonth;
    private readonly Frequency? fixedPaymentFrequency;
    private readonly Calendar? fixedCalendar;
    private readonly Calendar? overnightCalendar;
    private readonly BusinessDayConvention convention;
    private readonly DayCounter? fixedDayCount;
    private readonly int? lookbackDays;
    private readonly int lockoutDays;
    private readonly bool applyObservationShift;
    private readonly FloatingRateCouponPricer? pricer;
    private readonly DateGenerationRule rule;

    // Written by the constructor, and on a fresh copy only: the index cloned on the bound curve, the swap rebuilt when
    // the helper is re-dated and its coupons copied on the bound curve.
    private OvernightIndex overnightIndex;
    private OvernightIndexedSwap swap;

    /// <summary>
    /// Initializes a new instance of the <see cref="OISRateHelper"/> class for a swap of the given tenor, starting
    /// after the settlement days from the evaluation date.
    /// </summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="fixedRate">The swap rate.</param>
    /// <param name="overnightIndex">The overnight index.</param>
    /// <param name="context">The evaluation context, whose evaluation date the swap's start is counted from.</param>
    /// <param name="discountingCurve">An exogenous discounting curve, or <see langword="null"/> to discount on the curve.</param>
    /// <param name="telescopicValueDates">QuantLib's telescopic value dates (the swap builder's setter).</param>
    /// <param name="paymentLag">The payment lag, in business days.</param>
    /// <param name="paymentConvention">The payment dates' business-day convention.</param>
    /// <param name="paymentFrequency">Both legs' payment frequency.</param>
    /// <param name="paymentCalendar">
    /// The payment calendar, or <see langword="null"/>, QuantLib's empty calendar, for each leg's schedule calendar.
    /// </param>
    /// <param name="forwardStart">The forward start from the spot date.</param>
    /// <param name="overnightSpread">The spread over the overnight leg, added by the implied quote.</param>
    /// <param name="pillar">The pillar choice.</param>
    /// <param name="customPillarDate">The pillar date for <see cref="PillarChoice.CustomDate"/>.</param>
    /// <param name="averagingMethod">Whether the overnight rates are compounded or averaged.</param>
    /// <param name="endOfMonth">Both legs' end-of-month flag, or <see langword="null"/> for the builder's default.</param>
    /// <param name="fixedPaymentFrequency">
    /// The fixed leg's payment frequency, or <see langword="null"/> for <paramref name="paymentFrequency"/>.
    /// </param>
    /// <param name="fixedCalendar">The fixed leg's calendar, or <see langword="null"/> for the index's.</param>
    /// <param name="lookbackDays">The lookback days, or <see langword="null"/> for the index's fixing days.</param>
    /// <param name="lockoutDays">The lockout days.</param>
    /// <param name="applyObservationShift">Whether the observation shift applies.</param>
    /// <param name="pricer">A pricer for the overnight coupons, or <see langword="null"/> for the default.</param>
    /// <param name="rule">Both legs' date-generation rule.</param>
    /// <param name="overnightCalendar">The overnight leg's calendar, or <see langword="null"/> for the index's.</param>
    /// <param name="convention">Both legs' business-day convention and termination-date convention.</param>
    /// <param name="fixedDayCount">The fixed leg's day counter, or <see langword="null"/> for the index's.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="overnightIndex"/> or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/>, <paramref name="lookbackDays"/> or <paramref name="lockoutDays"/> is
    /// negative, or an enum argument isn't a member of its type; or the start date computed from the settlement days
    /// leaves the date range.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="customPillarDate"/> is <c>default(Date)</c>, or, for a custom pillar, outside the swap's earliest
    /// and latest relevant dates; or a coupon refuses its terms or the pricer.
    /// </exception>
    /// <exception cref="NanoQLException">The swap can't be built.</exception>
    public OISRateHelper(
        int settlementDays,
        Period tenor,
        double fixedRate,
        OvernightIndex overnightIndex,
        EvaluationContext context,
        YieldTermStructure? discountingCurve = null,
        bool telescopicValueDates = false,
        int paymentLag = 0,
        BusinessDayConvention paymentConvention = BusinessDayConvention.Following,
        Frequency paymentFrequency = Frequency.Annual,
        Calendar? paymentCalendar = null,
        Period forwardStart = default,
        double overnightSpread = 0.0,
        PillarChoice pillar = PillarChoice.LastRelevantDate,
        Date? customPillarDate = null,
        RateAveragingType averagingMethod = RateAveragingType.Compound,
        bool? endOfMonth = null,
        Frequency? fixedPaymentFrequency = null,
        Calendar? fixedCalendar = null,
        int? lookbackDays = null,
        int lockoutDays = 0,
        bool applyObservationShift = false,
        FloatingRateCouponPricer? pricer = null,
        DateGenerationRule rule = DateGenerationRule.Backward,
        Calendar? overnightCalendar = null,
        BusinessDayConvention convention = BusinessDayConvention.ModifiedFollowing,
        DayCounter? fixedDayCount = null)
        : base(fixedRate, context)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        ArgumentNullException.ThrowIfNull(overnightIndex);
        CheckArguments(
            paymentConvention,
            paymentFrequency,
            pillar,
            customPillarDate,
            averagingMethod,
            fixedPaymentFrequency,
            lookbackDays,
            lockoutDays,
            rule,
            convention);
        this.settlementDays = settlementDays;
        this.tenor = tenor;
        discountHandle = discountingCurve;
        this.telescopicValueDates = telescopicValueDates;
        this.paymentLag = paymentLag;
        this.paymentConvention = paymentConvention;
        this.paymentFrequency = paymentFrequency;
        this.paymentCalendar = paymentCalendar;
        this.forwardStart = forwardStart;
        this.overnightSpread = overnightSpread;
        pillarChoice = pillar;
        this.customPillarDate = customPillarDate;
        this.averagingMethod = averagingMethod;
        this.endOfMonth = endOfMonth;
        this.fixedPaymentFrequency = fixedPaymentFrequency;
        this.fixedCalendar = fixedCalendar;
        this.overnightCalendar = overnightCalendar;
        this.convention = convention;
        this.fixedDayCount = fixedDayCount;
        this.lookbackDays = lookbackDays;
        this.lockoutDays = lockoutDays;
        this.applyObservationShift = applyObservationShift;
        this.pricer = pricer;
        this.rule = rule;

        this.overnightIndex = overnightIndex.Clone(null);
        swap = BuildSwap(context);
        SetDates();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OISRateHelper"/> class for a swap between explicit start and end
    /// dates; its dates don't follow the evaluation date.
    /// </summary>
    /// <param name="startDate">The swap's start date.</param>
    /// <param name="endDate">The swap's end date.</param>
    /// <param name="fixedRate">The swap rate.</param>
    /// <param name="overnightIndex">The overnight index.</param>
    /// <param name="context">The evaluation context, read when the swap is built.</param>
    /// <param name="discountingCurve">An exogenous discounting curve, or <see langword="null"/> to discount on the curve.</param>
    /// <param name="telescopicValueDates">QuantLib's telescopic value dates (the swap builder's setter).</param>
    /// <param name="paymentLag">The payment lag, in business days.</param>
    /// <param name="paymentConvention">The payment dates' business-day convention.</param>
    /// <param name="paymentFrequency">Both legs' payment frequency.</param>
    /// <param name="paymentCalendar">
    /// The payment calendar, or <see langword="null"/>, QuantLib's empty calendar, for each leg's schedule calendar.
    /// </param>
    /// <param name="overnightSpread">The spread over the overnight leg, added by the implied quote.</param>
    /// <param name="pillar">The pillar choice.</param>
    /// <param name="customPillarDate">The pillar date for <see cref="PillarChoice.CustomDate"/>.</param>
    /// <param name="averagingMethod">Whether the overnight rates are compounded or averaged.</param>
    /// <param name="endOfMonth">Both legs' end-of-month flag, or <see langword="null"/> for the builder's default.</param>
    /// <param name="fixedPaymentFrequency">
    /// The fixed leg's payment frequency, or <see langword="null"/> for <paramref name="paymentFrequency"/>.
    /// </param>
    /// <param name="fixedCalendar">The fixed leg's calendar, or <see langword="null"/> for the index's.</param>
    /// <param name="lookbackDays">The lookback days, or <see langword="null"/> for the index's fixing days.</param>
    /// <param name="lockoutDays">The lockout days.</param>
    /// <param name="applyObservationShift">Whether the observation shift applies.</param>
    /// <param name="pricer">A pricer for the overnight coupons, or <see langword="null"/> for the default.</param>
    /// <param name="rule">Both legs' date-generation rule.</param>
    /// <param name="overnightCalendar">The overnight leg's calendar, or <see langword="null"/> for the index's.</param>
    /// <param name="convention">Both legs' business-day convention and termination-date convention.</param>
    /// <param name="fixedDayCount">The fixed leg's day counter, or <see langword="null"/> for the index's.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="overnightIndex"/> or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="lookbackDays"/> or <paramref name="lockoutDays"/> is negative, or an enum argument isn't a member
    /// of its type.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A date is <c>default(Date)</c>, or, for a custom pillar, the pillar date is outside the swap's earliest and latest
    /// relevant dates; or a coupon refuses its terms or the pricer.
    /// </exception>
    /// <exception cref="NanoQLException">The swap can't be built.</exception>
    public OISRateHelper(
        Date startDate,
        Date endDate,
        double fixedRate,
        OvernightIndex overnightIndex,
        EvaluationContext context,
        YieldTermStructure? discountingCurve = null,
        bool telescopicValueDates = false,
        int paymentLag = 0,
        BusinessDayConvention paymentConvention = BusinessDayConvention.Following,
        Frequency paymentFrequency = Frequency.Annual,
        Calendar? paymentCalendar = null,
        double overnightSpread = 0.0,
        PillarChoice pillar = PillarChoice.LastRelevantDate,
        Date? customPillarDate = null,
        RateAveragingType averagingMethod = RateAveragingType.Compound,
        bool? endOfMonth = null,
        Frequency? fixedPaymentFrequency = null,
        Calendar? fixedCalendar = null,
        int? lookbackDays = null,
        int lockoutDays = 0,
        bool applyObservationShift = false,
        FloatingRateCouponPricer? pricer = null,
        DateGenerationRule rule = DateGenerationRule.Backward,
        Calendar? overnightCalendar = null,
        BusinessDayConvention convention = BusinessDayConvention.ModifiedFollowing,
        DayCounter? fixedDayCount = null)
        : base(fixedRate, null, false)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(startDate, nameof(startDate));
        Date.ThrowIfDefault(endDate, nameof(endDate));
        ArgumentNullException.ThrowIfNull(overnightIndex);
        CheckArguments(
            paymentConvention,
            paymentFrequency,
            pillar,
            customPillarDate,
            averagingMethod,
            fixedPaymentFrequency,
            lookbackDays,
            lockoutDays,
            rule,
            convention);
        this.startDate = startDate;
        this.endDate = endDate;
        discountHandle = discountingCurve;
        this.telescopicValueDates = telescopicValueDates;
        this.paymentLag = paymentLag;
        this.paymentConvention = paymentConvention;
        this.paymentFrequency = paymentFrequency;
        this.paymentCalendar = paymentCalendar;
        this.overnightSpread = overnightSpread;
        pillarChoice = pillar;
        this.customPillarDate = customPillarDate;
        this.averagingMethod = averagingMethod;
        this.endOfMonth = endOfMonth;
        this.fixedPaymentFrequency = fixedPaymentFrequency;
        this.fixedCalendar = fixedCalendar;
        this.overnightCalendar = overnightCalendar;
        this.convention = convention;
        this.fixedDayCount = fixedDayCount;
        this.lookbackDays = lookbackDays;
        this.lockoutDays = lockoutDays;
        this.applyObservationShift = applyObservationShift;
        this.pricer = pricer;
        this.rule = rule;

        this.overnightIndex = overnightIndex.Clone(null);
        swap = BuildSwap(context);
        SetDates();
    }

    /// <summary>Gets the swap the helper prices: QuantLib's <c>swap()</c>.</summary>
    public OvernightIndexedSwap Swap => swap;

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The swap's results aren't available, as for an expired swap.</exception>
    public override double ImpliedQuote(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireTermStructure();
        YieldTermStructure discount = discountHandle ?? TermStructure!;
        OvernightIndexedSwapResults results =
            DiscountingSwapEngine.Calculate(swap, context, discount, includeSettlementDateFlows: false);
        double floatingLegNPV = Available(results.OvernightLegNPV);
        double spread = overnightSpread;
        double spreadNPV = Available(results.OvernightLegBPS) / BasisPoint * spread;
        double totNPV = -(floatingLegNPV + spreadNPV);
        double result = totNPV / (Available(results.FixedLegBPS) / BasisPoint);
        return result;
    }

    /// <inheritdoc/>
    protected internal override BootstrapHelper<YieldTermStructure> WithTermStructure(YieldTermStructure t)
    {
        var copy = (OISRateHelper)base.WithTermStructure(t);
        copy.overnightIndex = overnightIndex.Clone(t);
        copy.swap = WithOvernightIndex(swap, copy.overnightIndex);
        return copy;
    }

    /// <inheritdoc/>
    protected override void InitializeDates(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        swap = BuildSwap(context);
        SetDates();
    }

    private static void CheckArguments(
        BusinessDayConvention paymentConvention,
        Frequency paymentFrequency,
        PillarChoice pillar,
        Date? customPillarDate,
        RateAveragingType averagingMethod,
        Frequency? fixedPaymentFrequency,
        int? lookbackDays,
        int lockoutDays,
        DateGenerationRule rule,
        BusinessDayConvention convention)
    {
        // The helper stores them for its re-dating and computes with them when it builds its swap, so it checks them
        // on entry, in parameter order.
        EnumArguments.Check(paymentConvention, nameof(paymentConvention));
        EnumArguments.Check(paymentFrequency, nameof(paymentFrequency));
        EnumArguments.Check(pillar, nameof(pillar));
        Date.ThrowIfDefault(customPillarDate, nameof(customPillarDate));
        EnumArguments.Check(averagingMethod, nameof(averagingMethod));
        if (fixedPaymentFrequency is Frequency f)
        {
            EnumArguments.Check(f, nameof(fixedPaymentFrequency));
        }

        if (lookbackDays is int lookback)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(lookback, nameof(lookbackDays));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(lockoutDays);
        EnumArguments.Check(rule, nameof(rule));
        EnumArguments.Check(convention, nameof(convention));
    }

    // A swap result QuantLib's inspectors refuse when it is null.
    private static double Available(double? result) => result ?? throw new NanoQLException("result not available");

    private static string Show(Date? d) => d is Date date ? date.ToString() : "null date";

    // The swap with its overnight coupons copied on another index, each keeping its pricer: what QuantLib's relinked
    // handle gives the coupons of the swap it built. The coupons' value and fixing dates read the index's calendar and
    // fixing days, which its clone shares.
    private static OvernightIndexedSwap WithOvernightIndex(OvernightIndexedSwap swap, OvernightIndex index)
    {
        ImmutableArray<CashFlow> overnight = swap.OvernightLeg;
        var leg = ImmutableArray.CreateBuilder<CashFlow>(overnight.Length);
        foreach (CashFlow cf in overnight)
        {
            var c = (OvernightIndexedCoupon)cf;
            leg.Add(new OvernightIndexedCoupon(
                c.Date,
                c.ContractNominal,
                c.AccrualStartDate,
                c.AccrualEndDate,
                index,
                c.Gearing,
                c.Spread,
                c.ReferencePeriodStart,
                c.ReferencePeriodEnd,
                c.DayCounter,
                swap.TelescopicValueDates,
                c.AveragingMethod,
                swap.LookbackDays,
                c.LockoutDays,
                c.ApplyObservationShift,
                c.CompoundSpreadDaily,
                c.RateComputationStartDate,
                c.RateComputationEndDate,
                c.ExCouponDate,
                swap.RoundingPrecision).SetPricer(c.Pricer));
        }

        return (OvernightIndexedSwap)swap.WithLeg(1, leg.MoveToImmutable());
    }

    // initializeDates' swap. 1. The spread isn't passed, as it might be a quote, i.e. it can dynamically change; 2. the
    // discounting curve isn't either: the swap is built with a fixed rate, so its builder prices nothing.
    private OvernightIndexedSwap BuildSwap(EvaluationContext context)
    {
        MakeOIS builder = new MakeOIS(tenor, overnightIndex)
            .WithFixedRate(0.0)
            .WithForwardStart(forwardStart)
            .WithEffectiveDate(startDate)
            .WithTerminationDate(endDate)
            .WithTelescopicValueDates(telescopicValueDates)
            .WithPaymentLag(paymentLag)
            .WithPaymentAdjustment(paymentConvention)
            .WithPaymentFrequency(paymentFrequency)
            .WithPaymentCalendar(paymentCalendar)
            .WithAveragingMethod(averagingMethod)
            .WithLookbackDays(lookbackDays)
            .WithLockoutDays(lockoutDays)
            .WithRule(rule)
            .WithConvention(convention)
            .WithTerminationDateConvention(convention)
            .WithObservationShift(applyObservationShift);
        if (endOfMonth is bool eom)
        {
            builder = builder.WithEndOfMonth(eom);
        }

        if (fixedPaymentFrequency is Frequency f)
        {
            builder = builder.WithFixedLegPaymentFrequency(f);
        }

        if (fixedCalendar is not null)
        {
            builder = builder.WithFixedLegCalendar(fixedCalendar);
        }

        if (fixedDayCount is not null)
        {
            builder = builder.WithFixedLegDayCount(fixedDayCount);
        }

        if (overnightCalendar is not null)
        {
            builder = builder.WithOvernightLegCalendar(overnightCalendar);
        }

        // only set settlementDays when no explicit start date, to avoid conflict
        if (startDate is null && settlementDays is not null)
        {
            builder = builder.WithSettlementDays(settlementDays);
        }

        OvernightIndexedSwap built = builder.ToOvernightIndexedSwap(context);
        return pricer is null ? built : CouponPricerFunctions.SetCouponPricer(built, 1, pricer);
    }

    // initializeDates' dates, from the swap built.
    private void SetDates()
    {
        Date earliest = swap.StartDate();
        earliestDate = earliest;
        Date maturity = swap.MaturityDate();
        maturityDate = maturity;

        Date lastOvernightPayment = swap.OvernightLeg[^1].Date;
        Date lastFixedPayment = swap.FixedLeg[^1].Date;
        Date lastPaymentDate = lastOvernightPayment < lastFixedPayment ? lastFixedPayment : lastOvernightPayment;
        Date lastFixingDate = ((OvernightIndexedCoupon)swap.OvernightLeg[^1]).FixingDate();
        Date fixingEndDate = overnightIndex.MaturityDate(overnightIndex.ValueDate(lastFixingDate));

        // std::max({maturity, lastPaymentDate, fixingEndDate}), the first of the largest
        Date lastRelevant = maturity;
        if (lastRelevant < lastPaymentDate)
        {
            lastRelevant = lastPaymentDate;
        }

        if (lastRelevant < fixingEndDate)
        {
            lastRelevant = fixingEndDate;
        }

        latestRelevantDate = latestDate = lastRelevant;

        switch (pillarChoice)
        {
            case PillarChoice.MaturityDate:
                pillarDate = maturity;
                break;
            case PillarChoice.LastRelevantDate:
                pillarDate = lastRelevant;
                break;
            default:
                // pillarDate_ already assigned at construction time
                pillarDate = customPillarDate;
                if (!(customPillarDate is Date custom && custom >= earliest))
                {
                    throw new ArgumentException(
                        $"pillar date ({Show(customPillarDate)}) must be later than or equal to the instrument's "
                        + $"earliest date ({earliest})",
                        nameof(customPillarDate));
                }

                if (!(custom <= lastRelevant))
                {
                    throw new ArgumentException(
                        $"pillar date ({custom}) must be before or equal to the instrument's latest relevant date "
                        + $"({lastRelevant})",
                        nameof(customPillarDate));
                }

                break;
        }
    }
}
