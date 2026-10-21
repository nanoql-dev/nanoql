// SPDX-FileCopyrightText: Copyright (C) 2018 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: Copyright (C) 2025 Paolo D'Elia
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/constnotionalcrosscurrencyfixedvsfloatingswap.hpp ql/instruments/constnotionalcrosscurrencyfixedvsfloatingswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A cross-currency swap exchanging a fixed-rate leg in one currency against a floating-rate leg in another, with
/// constant notionals exchanged at inception and maturity.
/// </summary>
/// <remarks>
/// <para>
/// Payment lags apply only to coupon payments. Notional exchanges remain on the effective and maturity dates, adjusted
/// by the corresponding leg's payment convention. A payer swap pays the fixed leg, which is then the first leg; a
/// receiver swap pays the floating leg. The floating leg is an overnight leg on an overnight index and an Ibor leg
/// otherwise.
/// </para>
/// <para>
/// The constructor builds the legs, as QuantLib's does: an Ibor leg reads the context's at-par switch, so it takes the
/// context. An engine's results are a <see cref="ConstNotionalCrossCurrencyFixedVsFloatingSwapResults"/>, whose fair
/// rate and spread <see cref="FetchResults"/> derives from the legs' BPS when the engine gives none.
/// </para>
/// </remarks>
public sealed class ConstNotionalCrossCurrencyFixedVsFloatingSwap : ConstNotionalCrossCurrencySwap
{
    private const double BasisPoint = 1.0e-4;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConstNotionalCrossCurrencyFixedVsFloatingSwap"/> class.
    /// </summary>
    /// <param name="type">A payer swap pays the fixed leg; a receiver swap receives it.</param>
    /// <param name="fixedNominal">The fixed leg's notional.</param>
    /// <param name="fixedCurrency">The fixed leg's currency.</param>
    /// <param name="fixedSchedule">The fixed leg's schedule.</param>
    /// <param name="fixedRate">The fixed rate.</param>
    /// <param name="fixedDayCount">The fixed leg's day counter.</param>
    /// <param name="fixedPaymentBdc">The fixed leg's payment convention.</param>
    /// <param name="fixedPaymentLag">The fixed leg's coupon payment lag, in days.</param>
    /// <param name="fixedPaymentCalendar">The fixed leg's payment calendar; the schedule's when
    /// <see langword="null"/>, QuantLib's empty calendar, for the coupons.</param>
    /// <param name="floatNominal">The floating leg's notional.</param>
    /// <param name="floatCurrency">The floating leg's currency.</param>
    /// <param name="floatSchedule">The floating leg's schedule.</param>
    /// <param name="floatIndex">The floating leg's index.</param>
    /// <param name="floatSpread">The spread over the floating leg's index.</param>
    /// <param name="floatPaymentBdc">The floating leg's payment convention.</param>
    /// <param name="floatPaymentLag">The floating leg's coupon payment lag, in days.</param>
    /// <param name="floatPaymentCalendar">The floating leg's payment calendar; the schedule's when
    /// <see langword="null"/>, for the coupons.</param>
    /// <param name="context">The evaluation context, whose at-par switch an Ibor leg reads.</param>
    /// <param name="telescopicValueDates">Whether an overnight leg uses telescopic value dates.</param>
    /// <param name="floatCompoundSpread">Whether an overnight leg compounds its spread daily.</param>
    /// <param name="floatLookbackDays">An overnight leg's lookback days; the index's fixing days when
    /// <see langword="null"/>, QuantLib's <c>Null&lt;Natural&gt;()</c>.</param>
    /// <param name="floatObservationShift">Whether an overnight leg applies the observation shift.</param>
    /// <param name="floatLockoutDays">An overnight leg's lockout days.</param>
    /// <param name="floatAveragingMethod">An overnight leg's averaging method.</param>
    /// <param name="useIndexedCoupons">Whether an Ibor leg's coupons are indexed or at par; the context's at-par
    /// switch when <see langword="null"/>.</param>
    /// <param name="floatStubIndexSelection">The index selection for an Ibor leg's irregular coupons; none when
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">A schedule, the index, the fixed day counter or the context is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The type, a convention or the averaging method isn't a member of
    /// its enum, or a payment lag, lookback or lockout days are negative.</exception>
    /// <exception cref="ArgumentException">A leg can't be built from the arguments.</exception>
    public ConstNotionalCrossCurrencyFixedVsFloatingSwap(
        SwapType type,
        double fixedNominal,
        Currency? fixedCurrency,
        Schedule fixedSchedule,
        double fixedRate,
        DayCounter fixedDayCount,
        BusinessDayConvention fixedPaymentBdc,
        int fixedPaymentLag,
        Calendar? fixedPaymentCalendar,
        double floatNominal,
        Currency? floatCurrency,
        Schedule floatSchedule,
        IborIndex floatIndex,
        double floatSpread,
        BusinessDayConvention floatPaymentBdc,
        int floatPaymentLag,
        Calendar? floatPaymentCalendar,
        EvaluationContext context,
        bool telescopicValueDates = false,
        bool floatCompoundSpread = false,
        int? floatLookbackDays = null,
        bool floatObservationShift = false,
        int floatLockoutDays = 0,
        RateAveragingType floatAveragingMethod = RateAveragingType.Compound,
        bool? useIndexedCoupons = null,
        StubIndexSelection? floatStubIndexSelection = null)
        : base(
            BuildLegs(
                type,
                fixedNominal,
                fixedSchedule,
                fixedRate,
                fixedDayCount,
                fixedPaymentBdc,
                fixedPaymentLag,
                fixedPaymentCalendar,
                floatNominal,
                floatSchedule,
                floatIndex,
                floatSpread,
                floatPaymentBdc,
                floatPaymentLag,
                floatPaymentCalendar,
                context,
                telescopicValueDates,
                floatCompoundSpread,
                floatLookbackDays,
                floatObservationShift,
                floatLockoutDays,
                floatAveragingMethod,
                useIndexedCoupons,
                floatStubIndexSelection ?? new StubIndexSelection()),
            [true, false],
            type == SwapType.Payer ? new[] { fixedCurrency, floatCurrency } : new[] { floatCurrency, fixedCurrency })
    {
        Type = type;
        FixedNominal = fixedNominal;
        FixedCurrency = fixedCurrency;
        FixedSchedule = fixedSchedule;
        FixedRate = fixedRate;
        FixedDayCount = fixedDayCount;
        FixedPaymentBdc = fixedPaymentBdc;
        FixedPaymentLag = fixedPaymentLag;
        FixedPaymentCalendar = fixedPaymentCalendar;
        FloatNominal = floatNominal;
        FloatCurrency = floatCurrency;
        FloatSchedule = floatSchedule;
        FloatIndex = floatIndex;
        FloatSpread = floatSpread;
        FloatPaymentBdc = floatPaymentBdc;
        FloatPaymentLag = floatPaymentLag;
        FloatPaymentCalendar = floatPaymentCalendar;
        FloatCompoundSpread = floatCompoundSpread;
        FloatLookbackDays = floatLookbackDays;
        FloatLockoutDays = floatLockoutDays;
        FloatAveragingMethod = floatAveragingMethod;
        FloatStubIndexSelection = floatStubIndexSelection ?? new StubIndexSelection();
    }

    /// <summary>Gets the swap's type: a payer swap pays the fixed leg.</summary>
    public SwapType Type { get; }

    /// <summary>Gets the fixed leg's notional.</summary>
    public double FixedNominal { get; }

    /// <summary>Gets the fixed leg's currency.</summary>
    public Currency? FixedCurrency { get; }

    /// <summary>Gets the fixed leg's schedule.</summary>
    public Schedule FixedSchedule { get; }

    /// <summary>Gets the fixed rate.</summary>
    public double FixedRate { get; }

    /// <summary>Gets the fixed leg's day counter.</summary>
    public DayCounter FixedDayCount { get; }

    /// <summary>Gets the fixed leg's payment convention.</summary>
    public BusinessDayConvention FixedPaymentBdc { get; }

    /// <summary>Gets the fixed leg's coupon payment lag, in days.</summary>
    public int FixedPaymentLag { get; }

    /// <summary>Gets the fixed leg's payment calendar, <see langword="null"/> for QuantLib's empty one.</summary>
    public Calendar? FixedPaymentCalendar { get; }

    /// <summary>Gets the floating leg's notional.</summary>
    public double FloatNominal { get; }

    /// <summary>Gets the floating leg's currency.</summary>
    public Currency? FloatCurrency { get; }

    /// <summary>Gets the floating leg's schedule.</summary>
    public Schedule FloatSchedule { get; }

    /// <summary>Gets the index selection for an Ibor floating leg's irregular coupons.</summary>
    public StubIndexSelection FloatStubIndexSelection { get; }

    /// <summary>Gets the floating leg's index.</summary>
    public IborIndex FloatIndex { get; }

    /// <summary>Gets the spread over the floating leg's index.</summary>
    public double FloatSpread { get; }

    /// <summary>Gets the floating leg's payment convention.</summary>
    public BusinessDayConvention FloatPaymentBdc { get; }

    /// <summary>Gets the floating leg's coupon payment lag, in days.</summary>
    public int FloatPaymentLag { get; }

    /// <summary>Gets the floating leg's payment calendar, <see langword="null"/> for QuantLib's empty one.</summary>
    public Calendar? FloatPaymentCalendar { get; }

    /// <summary>Gets a value indicating whether an overnight floating leg compounds its spread daily.</summary>
    public bool FloatCompoundSpread { get; }

    /// <summary>Gets an overnight floating leg's lookback days, <see langword="null"/> for the index's.</summary>
    public int? FloatLookbackDays { get; }

    /// <summary>Gets an overnight floating leg's lockout days.</summary>
    public int FloatLockoutDays { get; }

    /// <summary>Gets an overnight floating leg's averaging method.</summary>
    public RateAveragingType FloatAveragingMethod { get; }

    /// <inheritdoc/>
    public override ConstNotionalCrossCurrencyFixedVsFloatingSwapResults SetupExpired() => new(base.SetupExpired())
    {
        FairFixedRate = null,
        FairSpread = null,
    };

    /// <inheritdoc/>
    /// <remarks>
    /// The fair rate and spread are the engine's when it gives them; otherwise each is derived from the NPV and its
    /// leg's BPS, and is <see langword="null"/> when that BPS is unknown or zero, where QuantLib divides by it and
    /// gives an infinite rate (the S-rx plan's SRXR3 (xviii); QuantLib's fixed-vs-floating swap tests the zero).
    /// </remarks>
    public override ConstNotionalCrossCurrencyFixedVsFloatingSwapResults FetchResults(InstrumentResults results)
    {
        CrossCurrencySwapResults r = base.FetchResults(results);
        var fetched = r as ConstNotionalCrossCurrencyFixedVsFloatingSwapResults;
        double? fairFixedRate = fetched?.FairFixedRate;
        double? fairSpread = fetched?.FairSpread;

        // Calculate fair rate and spread if they are still null here
        int idxFixed = Type == SwapType.Payer ? 0 : 1;
        if (fairFixedRate is null && r.LegBPS[idxFixed] is double fixedBps && fixedBps != 0.0)
        {
            fairFixedRate = FixedRate - (r.NPV / (fixedBps / BasisPoint));
        }

        int idxFloat = Type == SwapType.Payer ? 1 : 0;
        if (fairSpread is null && r.LegBPS[idxFloat] is double floatBps && floatBps != 0.0)
        {
            fairSpread = FloatSpread - (r.NPV / (floatBps / BasisPoint));
        }

        return fetched is not null
            ? fetched with { FairFixedRate = fairFixedRate, FairSpread = fairSpread }
            : new ConstNotionalCrossCurrencyFixedVsFloatingSwapResults(r)
            {
                FairFixedRate = fairFixedRate,
                FairSpread = fairSpread,
            };
    }

    // QuantLib's constructor body: the floating and fixed legs, each with its notional exchanges, in the type's order.
    private static ImmutableArray<CashFlow>[] BuildLegs(
        SwapType type,
        double fixedNominal,
        Schedule fixedSchedule,
        double fixedRate,
        DayCounter fixedDayCount,
        BusinessDayConvention fixedPaymentBdc,
        int fixedPaymentLag,
        Calendar? fixedPaymentCalendar,
        double floatNominal,
        Schedule floatSchedule,
        IborIndex floatIndex,
        double floatSpread,
        BusinessDayConvention floatPaymentBdc,
        int floatPaymentLag,
        Calendar? floatPaymentCalendar,
        EvaluationContext context,
        bool telescopicValueDates,
        bool floatCompoundSpread,
        int? floatLookbackDays,
        bool floatObservationShift,
        int floatLockoutDays,
        RateAveragingType floatAveragingMethod,
        bool? useIndexedCoupons,
        StubIndexSelection floatStubIndexSelection)
    {
        EnumArguments.Check(type, nameof(type));
        ArgumentNullException.ThrowIfNull(fixedSchedule);
        ArgumentNullException.ThrowIfNull(floatSchedule);
        ArgumentNullException.ThrowIfNull(floatIndex);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(fixedPaymentLag);
        ArgumentOutOfRangeException.ThrowIfNegative(floatPaymentLag);

        // Build the float leg
        ImmutableArray<CashFlow> floatLeg = floatIndex is OvernightIndex on
            ? new OvernightLeg(floatSchedule, on)
                .WithNotionals(floatNominal)
                .WithSpreads(floatSpread)
                .WithPaymentAdjustment(floatPaymentBdc)
                .WithPaymentLag(floatPaymentLag)
                .WithLookbackDays(floatLookbackDays)
                .CompoundingSpreadDaily(floatCompoundSpread)
                .WithObservationShift(floatObservationShift)
                .WithPaymentCalendar(floatPaymentCalendar)
                .WithLockoutDays(floatLockoutDays)
                .WithAveragingMethod(floatAveragingMethod)
                .WithTelescopicValueDates(telescopicValueDates)
                .ToLeg()
            : new IborLeg(floatSchedule, floatIndex)
                .WithNotionals(floatNominal)
                .WithSpreads(floatSpread)
                .WithPaymentAdjustment(floatPaymentBdc)
                .WithPaymentLag(floatPaymentLag)
                .WithPaymentCalendar(floatPaymentCalendar)
                .WithIndexedCoupons(useIndexedCoupons)
                .WithStubIndexSelection(floatStubIndexSelection)
                .ToLeg(context);

        // Build the fixed rate leg
        ImmutableArray<CashFlow> fixedLeg = new FixedRateLeg(fixedSchedule)
            .WithNotionals(fixedNominal)
            .WithCouponRates(fixedRate, fixedDayCount)
            .WithPaymentAdjustment(fixedPaymentBdc)
            .WithPaymentLag(fixedPaymentLag)
            .WithPaymentCalendar(fixedPaymentCalendar)
            .ToLeg();

        Date floatStart = CashFlows.StartDate(floatLeg);
        Date fixedStart = CashFlows.StartDate(fixedLeg);
        Date earliestDate = fixedStart < floatStart ? fixedStart : floatStart;
        Date floatMaturity = CashFlows.MaturityDate(floatLeg);
        Date fixedMaturity = CashFlows.MaturityDate(fixedLeg);
        Date maturityDate = floatMaturity < fixedMaturity ? fixedMaturity : floatMaturity;

        // Add notional exchanges on float Leg
        floatLeg = AddNotionalExchangesToLeg(
            floatLeg,
            floatPaymentCalendar ?? EmptyCalendar.Instance,
            earliestDate,
            maturityDate,
            floatPaymentBdc,
            floatNominal);

        // Add notional exchanges on fixed leg
        fixedLeg = AddNotionalExchangesToLeg(
            fixedLeg,
            fixedPaymentCalendar ?? EmptyCalendar.Instance,
            earliestDate,
            maturityDate,
            fixedPaymentBdc,
            fixedNominal);

        // Deriving from cross currency swap where:
        //   First leg should hold the pay flows
        //   Second leg should hold the receive flows
        return type == SwapType.Payer ? [fixedLeg, floatLeg] : [floatLeg, fixedLeg];
    }
}
