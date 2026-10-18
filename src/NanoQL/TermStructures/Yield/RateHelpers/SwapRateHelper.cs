// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2015 Maddalena Zanzi
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: Copyright (C) 2018 Matthias Lungwitz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/ratehelpers.hpp ql/termstructures/yield/ratehelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: the constructor from a swap index, which waits for S5's fourth commit with SwapIndex

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A rate helper for bootstrapping over swap rates: the fixed rate of the vanilla swap, built at construction, that
/// the curve prices at par.
/// </summary>
/// <remarks>
/// <para>
/// The helper builds its swap when it is constructed, through <see cref="MakeVanillaSwap"/>, so it reads the context's
/// at-par switch then, which also moves the default pillar, the last relevant date, through the last coupon's fixing end
/// date. A coupon pricer given is set on the swap's floating leg.
/// </para>
/// <para>
/// Bound to a curve, its copy holds a swap whose floating coupons are copies on a clone of the index on that curve,
/// each keeping its pricer and so its at-par choice: QuantLib relinks the index's handle under the built swap. The swap
/// is discounted on the curve, or on the discounting curve given. Its dates follow the evaluation date unless explicit
/// start and end dates are given.
/// </para>
/// </remarks>
public sealed class SwapRateHelper : RelativeDateRateHelper
{
    // weak implementation... to be improved
    private const double BasisPoint = 1.0e-4;

    private readonly int? settlementDays;
    private readonly Period tenor;
    private readonly Date? startDate;
    private readonly Date? endDate;
    private readonly PillarChoice pillarChoice;
    private readonly Date? customPillarDate;
    private readonly Calendar? calendar;
    private readonly BusinessDayConvention fixedConvention;
    private readonly Frequency fixedFrequency;
    private readonly DayCounter? fixedDayCount;
    private readonly bool endOfMonth;
    private readonly YieldTermStructure? discountHandle;
    private readonly bool? useIndexedCoupons;
    private readonly BusinessDayConvention? floatConvention;
    private readonly FloatingRateCouponPricer? couponPricer;

    // Written by the constructor, and on a fresh copy only: the index cloned on the bound curve, the swap rebuilt when
    // the helper is re-dated and its coupons copied on the bound curve.
    private IborIndex iborIndex;
    private VanillaSwap swap;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwapRateHelper"/> class for a swap of the given tenor, starting
    /// at the spot date (or after the settlement days) from the evaluation date.
    /// </summary>
    /// <param name="rate">The swap rate.</param>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="calendar">
    /// The calendar of both legs, or <see langword="null"/>, QuantLib's empty calendar, which builds with unadjusted
    /// conventions only.
    /// </param>
    /// <param name="fixedFrequency">The fixed leg's frequency; <see cref="Frequency.Once"/> pays once at maturity.</param>
    /// <param name="fixedConvention">The fixed leg's business-day convention.</param>
    /// <param name="fixedDayCount">
    /// The fixed leg's day counter, or <see langword="null"/> for the currency's default, as the swap builder reads it.
    /// </param>
    /// <param name="iborIndex">The floating leg's index.</param>
    /// <param name="context">The evaluation context, read when the swap is built.</param>
    /// <param name="spread">The spread over the floating leg.</param>
    /// <param name="fwdStart">The forward start from the spot date.</param>
    /// <param name="discountingCurve">An exogenous discounting curve, or <see langword="null"/> to discount on the curve.</param>
    /// <param name="settlementDays">The settlement days, or <see langword="null"/> for the index's spot date.</param>
    /// <param name="pillar">The pillar choice.</param>
    /// <param name="customPillarDate">The pillar date for <see cref="PillarChoice.CustomDate"/>.</param>
    /// <param name="endOfMonth">Whether the legs follow the end-of-month rule.</param>
    /// <param name="useIndexedCoupons">
    /// Whether the floating coupons are indexed, or <see langword="null"/> for the context's at-par switch.
    /// </param>
    /// <param name="floatConvention">The floating leg's convention, or <see langword="null"/> for the index's.</param>
    /// <param name="couponPricer">A pricer for the floating coupons, or <see langword="null"/> for the default.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="iborIndex"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> is negative, or an enum argument isn't a member of its type, or
    /// <paramref name="fixedFrequency"/> is <see cref="Frequency.OtherFrequency"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="customPillarDate"/> is <c>default(Date)</c>, or, for a custom pillar, outside the swap's earliest
    /// and latest relevant dates.
    /// </exception>
    /// <exception cref="NanoQLException">The swap can't be built.</exception>
    public SwapRateHelper(
        double rate,
        Period tenor,
        Calendar? calendar,
        Frequency fixedFrequency,
        BusinessDayConvention fixedConvention,
        DayCounter? fixedDayCount,
        IborIndex iborIndex,
        EvaluationContext context,
        double spread = 0.0,
        Period fwdStart = default,
        YieldTermStructure? discountingCurve = null,
        int? settlementDays = null,
        PillarChoice pillar = PillarChoice.LastRelevantDate,
        Date? customPillarDate = null,
        bool endOfMonth = false,
        bool? useIndexedCoupons = null,
        BusinessDayConvention? floatConvention = null,
        FloatingRateCouponPricer? couponPricer = null)
        : base(rate, context)
    {
        ArgumentNullException.ThrowIfNull(iborIndex);
        if (settlementDays is int days)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(days, nameof(settlementDays));
        }

        CheckArguments(fixedFrequency, fixedConvention, pillar, customPillarDate, floatConvention);
        this.settlementDays = settlementDays;
        this.tenor = tenor;
        pillarChoice = pillar;
        this.customPillarDate = customPillarDate;
        this.calendar = calendar;
        this.fixedConvention = fixedConvention;
        this.fixedFrequency = fixedFrequency;
        this.fixedDayCount = fixedDayCount;
        Spread = spread;
        this.endOfMonth = endOfMonth;
        ForwardStart = fwdStart;
        discountHandle = discountingCurve;
        this.useIndexedCoupons = useIndexedCoupons;
        this.floatConvention = floatConvention;
        this.couponPricer = couponPricer;

        // take fixing into account
        this.iborIndex = iborIndex.Clone(null);
        swap = BuildSwap(context);
        SetDates();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwapRateHelper"/> class for a swap between explicit start and end
    /// dates; its dates don't follow the evaluation date.
    /// </summary>
    /// <param name="rate">The swap rate.</param>
    /// <param name="startDate">The swap's start date.</param>
    /// <param name="endDate">The swap's end date.</param>
    /// <param name="calendar">
    /// The calendar of both legs, or <see langword="null"/>, QuantLib's empty calendar, which builds with unadjusted
    /// conventions only.
    /// </param>
    /// <param name="fixedFrequency">The fixed leg's frequency; <see cref="Frequency.Once"/> isn't supported here.</param>
    /// <param name="fixedConvention">The fixed leg's business-day convention.</param>
    /// <param name="fixedDayCount">
    /// The fixed leg's day counter, or <see langword="null"/> for the currency's default, as the swap builder reads it.
    /// </param>
    /// <param name="iborIndex">The floating leg's index.</param>
    /// <param name="context">The evaluation context, read when the swap is built.</param>
    /// <param name="spread">The spread over the floating leg.</param>
    /// <param name="discountingCurve">An exogenous discounting curve, or <see langword="null"/> to discount on the curve.</param>
    /// <param name="pillar">The pillar choice.</param>
    /// <param name="customPillarDate">The pillar date for <see cref="PillarChoice.CustomDate"/>.</param>
    /// <param name="endOfMonth">Whether the legs follow the end-of-month rule.</param>
    /// <param name="useIndexedCoupons">
    /// Whether the floating coupons are indexed, or <see langword="null"/> for the context's at-par switch.
    /// </param>
    /// <param name="floatConvention">The floating leg's convention, or <see langword="null"/> for the index's.</param>
    /// <param name="couponPricer">A pricer for the floating coupons, or <see langword="null"/> for the default.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="iborIndex"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An enum argument isn't a member of its type, or <paramref name="fixedFrequency"/> is
    /// <see cref="Frequency.OtherFrequency"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="fixedFrequency"/> is <see cref="Frequency.Once"/>; a date is <c>default(Date)</c>; or, for a
    /// custom pillar, the pillar date is outside the swap's earliest and latest relevant dates.
    /// </exception>
    /// <exception cref="NanoQLException">The swap can't be built.</exception>
    public SwapRateHelper(
        double rate,
        Date startDate,
        Date endDate,
        Calendar? calendar,
        Frequency fixedFrequency,
        BusinessDayConvention fixedConvention,
        DayCounter? fixedDayCount,
        IborIndex iborIndex,
        EvaluationContext context,
        double spread = 0.0,
        YieldTermStructure? discountingCurve = null,
        PillarChoice pillar = PillarChoice.LastRelevantDate,
        Date? customPillarDate = null,
        bool endOfMonth = false,
        bool? useIndexedCoupons = null,
        BusinessDayConvention? floatConvention = null,
        FloatingRateCouponPricer? couponPricer = null)
        : base(rate, null, false)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (fixedFrequency == Frequency.Once)
        {
            throw new ArgumentException(
                "fixedFrequency == Once is not supported when passing explicit startDate and endDate",
                nameof(fixedFrequency));
        }

        Date.ThrowIfDefault(startDate, nameof(startDate));
        Date.ThrowIfDefault(endDate, nameof(endDate));
        ArgumentNullException.ThrowIfNull(iborIndex);
        CheckArguments(fixedFrequency, fixedConvention, pillar, customPillarDate, floatConvention);
        this.startDate = startDate;
        this.endDate = endDate;
        pillarChoice = pillar;
        this.customPillarDate = customPillarDate;
        this.calendar = calendar;
        this.fixedConvention = fixedConvention;
        this.fixedFrequency = fixedFrequency;
        this.fixedDayCount = fixedDayCount;
        Spread = spread;
        this.endOfMonth = endOfMonth;
        discountHandle = discountingCurve;
        this.useIndexedCoupons = useIndexedCoupons;
        this.floatConvention = floatConvention;
        this.couponPricer = couponPricer;

        // take fixing into account
        this.iborIndex = iborIndex.Clone(null);
        swap = BuildSwap(context);
        SetDates();
    }

    /// <summary>Gets the spread over the floating leg.</summary>
    public double Spread { get; }

    /// <summary>Gets the swap the helper prices: QuantLib's <c>swap()</c>.</summary>
    public VanillaSwap Swap => swap;

    /// <summary>Gets the forward start from the spot date: zero for a swap between explicit dates.</summary>
    public Period ForwardStart { get; }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The swap's results aren't available, as for an expired swap.</exception>
    public override double ImpliedQuote(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireTermStructure();
        YieldTermStructure discount = discountHandle ?? TermStructure!;
        FixedVsFloatingSwapResults results =
            DiscountingSwapEngine.Calculate(swap, context, discount, includeSettlementDateFlows: false);
        double floatingLegNPV = Available(results.FloatingLegNPV);
        double spreadNPV = Available(results.FloatingLegBPS) / BasisPoint * Spread;
        double totNPV = -(floatingLegNPV + spreadNPV);
        return totNPV / (Available(results.FixedLegBPS) / BasisPoint);
    }

    /// <inheritdoc/>
    protected internal override BootstrapHelper<YieldTermStructure> WithTermStructure(YieldTermStructure t)
    {
        var copy = (SwapRateHelper)base.WithTermStructure(t);
        copy.iborIndex = iborIndex.Clone(t);
        copy.swap = WithFloatingIndex(swap, copy.iborIndex);
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
        Frequency fixedFrequency,
        BusinessDayConvention fixedConvention,
        PillarChoice pillar,
        Date? customPillarDate,
        BusinessDayConvention? floatConvention)
    {
        // The helper stores them and computes with them when it builds its swap, so it checks them on entry.
        if (fixedFrequency != Frequency.Once)
        {
            _ = Period.FromFrequency(fixedFrequency, nameof(fixedFrequency));
        }

        EnumArguments.Check(fixedConvention, nameof(fixedConvention));
        EnumArguments.Check(pillar, nameof(pillar));
        Date.ThrowIfDefault(customPillarDate, nameof(customPillarDate));
        if (floatConvention is BusinessDayConvention convention)
        {
            EnumArguments.Check(convention, nameof(floatConvention));
        }
    }

    // A swap result QuantLib's inspectors refuse when it is null.
    private static double Available(double? result) => result ?? throw new NanoQLException("result not available");

    private static string Show(Date? d) => d is Date date ? date.ToString() : "null date";

    // The swap with its floating coupons copied on another index, each keeping its pricer: what QuantLib's relinked
    // handle gives the coupons of the swap it built.
    private static VanillaSwap WithFloatingIndex(VanillaSwap swap, IborIndex index)
    {
        ImmutableArray<CashFlow> floating = swap.FloatingLeg;
        var leg = ImmutableArray.CreateBuilder<CashFlow>(floating.Length);
        foreach (CashFlow cf in floating)
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

        return (VanillaSwap)swap.WithLeg(1, leg.MoveToImmutable());
    }

    // initializeDates' swap. The spread isn't passed, and the discounting curve isn't either: the swap is built with a
    // fixed rate, so its builder prices nothing.
    private VanillaSwap BuildSwap(EvaluationContext context)
    {
        MakeVanillaSwap builder = new MakeVanillaSwap(tenor, iborIndex)
            .WithFixedRate(0.0)
            .WithForwardStart(ForwardStart)
            .WithEffectiveDate(startDate)
            .WithTerminationDate(endDate)
            .WithFixedLegDayCount(fixedDayCount)
            .WithFixedLegTenor(fixedFrequency == Frequency.Once ? tenor : new Period(fixedFrequency))
            .WithFixedLegConvention(fixedConvention)
            .WithFixedLegTerminationDateConvention(fixedConvention)
            .WithFixedLegCalendar(calendar)
            .WithFixedLegEndOfMonth(endOfMonth)
            .WithFloatingLegCalendar(calendar)
            .WithFloatingLegEndOfMonth(endOfMonth)
            .WithIndexedCoupons(useIndexedCoupons);
        if (floatConvention is BusinessDayConvention convention)
        {
            builder = builder.WithFloatingLegConvention(convention).WithFloatingLegTerminationDateConvention(convention);
        }

        // only set settlementDays when no explicit start date, to avoid conflict
        if (startDate is null && settlementDays is not null)
        {
            builder = builder.WithSettlementDays(settlementDays);
        }

        VanillaSwap built = builder.ToVanillaSwap(context);
        return couponPricer is null ? built : CouponPricerFunctions.SetCouponPricer(built, 1, couponPricer);
    }

    // initializeDates' dates, from the swap built.
    private void SetDates()
    {
        earliestDate = swap.StartDate();
        Date maturity = swap.MaturityDate();
        maturityDate = maturity;
        Date fixingEnd = ((IborCoupon)swap.FloatingLeg[^1]).FixingEndDate();
        Date lastRelevant = maturity < fixingEnd ? fixingEnd : maturity;
        latestRelevantDate = lastRelevant;

        switch (pillarChoice)
        {
            case PillarChoice.MaturityDate:
                pillarDate = maturity;
                break;
            case PillarChoice.LastRelevantDate:
                pillarDate = lastRelevant;
                break;
            default:
                // the custom pillar date given at construction
                pillarDate = customPillarDate;
                if (!(customPillarDate is Date custom && custom >= earliestDate.Value))
                {
                    throw new ArgumentException(
                        $"pillar date ({Show(customPillarDate)}) must be later than or equal to the instrument's "
                        + $"earliest date ({earliestDate.Value})",
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

        // backward compatibility
        latestDate = pillarDate;
    }
}
