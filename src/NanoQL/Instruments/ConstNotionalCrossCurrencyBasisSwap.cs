// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: Copyright (C) 2025 Paolo D'Elia
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/constnotionalcrosscurrencybasisswap.hpp ql/instruments/constnotionalcrosscurrencybasisswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A cross-currency basis swap with constant notionals, exchanged at inception and maturity.</summary>
/// <remarks>
/// <para>
/// The first leg holds the pay currency cash flows and the second leg holds the receive currency cash flows. Each leg
/// is an overnight leg on an overnight index and an Ibor leg otherwise.
/// </para>
/// <para>
/// Payment lags apply only to coupon payments. Notional exchanges remain on the effective and maturity dates, unless
/// <c>paymentLagOnNotionalExchanges</c> is set, in which case each leg's exchanges are lagged by that leg's coupon
/// payment lag: the final exchange settles together with the final coupon and the initial exchange falls on the
/// lagged settlement date.
/// </para>
/// <para>
/// The constructor builds the legs, as QuantLib's does: an Ibor leg reads the context's at-par switch, so it takes the
/// context. An engine's results are a <see cref="ConstNotionalCrossCurrencyBasisSwapResults"/>, whose fair spreads
/// <see cref="FetchResults"/> derives from the legs' BPS when the engine gives none.
/// </para>
/// </remarks>
public sealed class ConstNotionalCrossCurrencyBasisSwap : ConstNotionalCrossCurrencySwap
{
    private const double BasisPoint = 1.0e-4;

    /// <summary>Initializes a new instance of the <see cref="ConstNotionalCrossCurrencyBasisSwap"/> class.</summary>
    /// <param name="payNominal">The pay leg's notional.</param>
    /// <param name="payCurrency">The pay leg's currency.</param>
    /// <param name="paySchedule">The pay leg's schedule.</param>
    /// <param name="payIndex">The pay leg's index.</param>
    /// <param name="paySpread">The spread over the pay leg's index.</param>
    /// <param name="payGearing">The gearing of the pay leg's index.</param>
    /// <param name="recNominal">The receive leg's notional.</param>
    /// <param name="recCurrency">The receive leg's currency.</param>
    /// <param name="recSchedule">The receive leg's schedule.</param>
    /// <param name="recIndex">The receive leg's index.</param>
    /// <param name="recSpread">The spread over the receive leg's index.</param>
    /// <param name="recGearing">The gearing of the receive leg's index.</param>
    /// <param name="context">The evaluation context, whose at-par switch an Ibor leg reads.</param>
    /// <param name="payPaymentLag">The pay leg's coupon payment lag, in days.</param>
    /// <param name="recPaymentLag">The receive leg's coupon payment lag, in days.</param>
    /// <param name="payCompoundSpread">Whether an overnight pay leg compounds its spread daily.</param>
    /// <param name="payLookbackDays">An overnight pay leg's lookback days; the index's fixing days when
    /// <see langword="null"/>, QuantLib's <c>Null&lt;Natural&gt;()</c>.</param>
    /// <param name="payObservationShift">Whether an overnight pay leg applies the observation shift.</param>
    /// <param name="payLockoutDays">An overnight pay leg's lockout days.</param>
    /// <param name="payAveragingMethod">An overnight pay leg's averaging method.</param>
    /// <param name="recCompoundSpread">Whether an overnight receive leg compounds its spread daily.</param>
    /// <param name="recLookbackDays">An overnight receive leg's lookback days; the index's fixing days when
    /// <see langword="null"/>.</param>
    /// <param name="recObservationShift">Whether an overnight receive leg applies the observation shift.</param>
    /// <param name="recLockoutDays">An overnight receive leg's lockout days.</param>
    /// <param name="recAveragingMethod">An overnight receive leg's averaging method.</param>
    /// <param name="telescopicValueDates">Whether overnight legs use telescopic value dates.</param>
    /// <param name="useIndexedCoupons">Whether Ibor legs' coupons are indexed or at par; the context's at-par switch
    /// when <see langword="null"/>.</param>
    /// <param name="paymentLagOnNotionalExchanges">Whether the notional exchanges are lagged by each leg's coupon
    /// payment lag.</param>
    /// <param name="payStubIndexSelection">The index selection for an Ibor pay leg's irregular coupons; none when
    /// <see langword="null"/>.</param>
    /// <param name="recStubIndexSelection">The index selection for an Ibor receive leg's irregular coupons; none when
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">A schedule, an index or the context is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">An averaging method isn't a member of its enum, or lookback or
    /// lockout days are negative.</exception>
    /// <exception cref="ArgumentException">A leg can't be built from the arguments.</exception>
    public ConstNotionalCrossCurrencyBasisSwap(
        double payNominal,
        Currency? payCurrency,
        Schedule paySchedule,
        IborIndex payIndex,
        double paySpread,
        double payGearing,
        double recNominal,
        Currency? recCurrency,
        Schedule recSchedule,
        IborIndex recIndex,
        double recSpread,
        double recGearing,
        EvaluationContext context,
        int payPaymentLag = 0,
        int recPaymentLag = 0,
        bool payCompoundSpread = false,
        int? payLookbackDays = null,
        bool payObservationShift = false,
        int payLockoutDays = 0,
        RateAveragingType payAveragingMethod = RateAveragingType.Compound,
        bool recCompoundSpread = false,
        int? recLookbackDays = null,
        bool recObservationShift = false,
        int recLockoutDays = 0,
        RateAveragingType recAveragingMethod = RateAveragingType.Compound,
        bool telescopicValueDates = false,
        bool? useIndexedCoupons = null,
        bool paymentLagOnNotionalExchanges = false,
        StubIndexSelection? payStubIndexSelection = null,
        StubIndexSelection? recStubIndexSelection = null)
        : base(
            BuildLegs(
                payNominal,
                paySchedule,
                payIndex,
                paySpread,
                payGearing,
                recNominal,
                recSchedule,
                recIndex,
                recSpread,
                recGearing,
                context,
                payPaymentLag,
                recPaymentLag,
                payCompoundSpread,
                payLookbackDays,
                payObservationShift,
                payLockoutDays,
                payAveragingMethod,
                recCompoundSpread,
                recLookbackDays,
                recObservationShift,
                recLockoutDays,
                recAveragingMethod,
                telescopicValueDates,
                useIndexedCoupons,
                paymentLagOnNotionalExchanges,
                payStubIndexSelection ?? new StubIndexSelection(),
                recStubIndexSelection ?? new StubIndexSelection()),
            [true, false],
            [payCurrency, recCurrency])
    {
        PayNominal = payNominal;
        PayCurrency = payCurrency;
        PaySchedule = paySchedule;
        PayIndex = payIndex;
        PaySpread = paySpread;
        PayGearing = payGearing;
        RecNominal = recNominal;
        RecCurrency = recCurrency;
        RecSchedule = recSchedule;
        RecIndex = recIndex;
        RecSpread = recSpread;
        RecGearing = recGearing;
        PayStubIndexSelection = payStubIndexSelection ?? new StubIndexSelection();
        RecStubIndexSelection = recStubIndexSelection ?? new StubIndexSelection();
    }

    /// <summary>Gets the pay leg's notional.</summary>
    public double PayNominal { get; }

    /// <summary>Gets the pay leg's currency.</summary>
    public Currency? PayCurrency { get; }

    /// <summary>Gets the pay leg's schedule.</summary>
    public Schedule PaySchedule { get; }

    /// <summary>Gets the pay leg's index.</summary>
    public IborIndex PayIndex { get; }

    /// <summary>Gets the spread over the pay leg's index.</summary>
    public double PaySpread { get; }

    /// <summary>Gets the gearing of the pay leg's index.</summary>
    public double PayGearing { get; }

    /// <summary>Gets the receive leg's notional.</summary>
    public double RecNominal { get; }

    /// <summary>Gets the receive leg's currency.</summary>
    public Currency? RecCurrency { get; }

    /// <summary>Gets the receive leg's schedule.</summary>
    public Schedule RecSchedule { get; }

    /// <summary>Gets the receive leg's index.</summary>
    public IborIndex RecIndex { get; }

    /// <summary>Gets the spread over the receive leg's index.</summary>
    public double RecSpread { get; }

    /// <summary>Gets the gearing of the receive leg's index.</summary>
    public double RecGearing { get; }

    /// <summary>Gets the index selection for an Ibor pay leg's irregular coupons.</summary>
    public StubIndexSelection PayStubIndexSelection { get; }

    /// <summary>Gets the index selection for an Ibor receive leg's irregular coupons.</summary>
    public StubIndexSelection RecStubIndexSelection { get; }

    /// <inheritdoc/>
    public override ConstNotionalCrossCurrencyBasisSwapResults SetupExpired() => new(base.SetupExpired())
    {
        FairPaySpread = null,
        FairRecSpread = null,
    };

    /// <inheritdoc/>
    /// <remarks>
    /// The fair spreads are the engine's when it gives them; otherwise each is derived from the NPV and its leg's BPS,
    /// and is <see langword="null"/> when that BPS is unknown or zero, where QuantLib divides by it and gives an
    /// infinite spread (QuantLib's float-float and mark-to-market swaps test the zero).
    /// </remarks>
    public override ConstNotionalCrossCurrencyBasisSwapResults FetchResults(InstrumentResults results)
    {
        CrossCurrencySwapResults r = base.FetchResults(results);
        var fetched = r as ConstNotionalCrossCurrencyBasisSwapResults;
        double? fairPaySpread = fetched?.FairPaySpread;
        double? fairRecSpread = fetched?.FairRecSpread;

        // Calculate the fair pay and receive spreads if they are null
        if (fairPaySpread is null && r.LegBPS[0] is double payBps && payBps != 0.0)
        {
            fairPaySpread = PaySpread - (r.NPV / (payBps / BasisPoint));
        }

        if (fairRecSpread is null && r.LegBPS[1] is double recBps && recBps != 0.0)
        {
            fairRecSpread = RecSpread - (r.NPV / (recBps / BasisPoint));
        }

        return fetched is not null
            ? fetched with { FairPaySpread = fairPaySpread, FairRecSpread = fairRecSpread }
            : new ConstNotionalCrossCurrencyBasisSwapResults(r)
            {
                FairPaySpread = fairPaySpread,
                FairRecSpread = fairRecSpread,
            };
    }

    // QuantLib's initialize: the two legs, an overnight leg on an overnight index and an Ibor leg otherwise, each with
    // its notional exchanges at the swap's start and maturity.
    private static ImmutableArray<CashFlow>[] BuildLegs(
        double payNominal,
        Schedule paySchedule,
        IborIndex payIndex,
        double paySpread,
        double payGearing,
        double recNominal,
        Schedule recSchedule,
        IborIndex recIndex,
        double recSpread,
        double recGearing,
        EvaluationContext context,
        int payPaymentLag,
        int recPaymentLag,
        bool payCompoundSpread,
        int? payLookbackDays,
        bool payObservationShift,
        int payLockoutDays,
        RateAveragingType payAveragingMethod,
        bool recCompoundSpread,
        int? recLookbackDays,
        bool recObservationShift,
        int recLockoutDays,
        RateAveragingType recAveragingMethod,
        bool telescopicValueDates,
        bool? useIndexedCoupons,
        bool paymentLagOnNotionalExchanges,
        StubIndexSelection payStubIndexSelection,
        StubIndexSelection recStubIndexSelection)
    {
        ArgumentNullException.ThrowIfNull(paySchedule);
        ArgumentNullException.ThrowIfNull(payIndex);
        ArgumentNullException.ThrowIfNull(recSchedule);
        ArgumentNullException.ThrowIfNull(recIndex);
        ArgumentNullException.ThrowIfNull(context);

        // Pay leg
        ImmutableArray<CashFlow> payLeg = Leg(
            paySchedule,
            payIndex,
            payNominal,
            paySpread,
            payGearing,
            payPaymentLag,
            payCompoundSpread,
            payLookbackDays,
            payObservationShift,
            payLockoutDays,
            payAveragingMethod,
            telescopicValueDates,
            useIndexedCoupons,
            payStubIndexSelection,
            context);

        // Receive leg
        ImmutableArray<CashFlow> recLeg = Leg(
            recSchedule,
            recIndex,
            recNominal,
            recSpread,
            recGearing,
            recPaymentLag,
            recCompoundSpread,
            recLookbackDays,
            recObservationShift,
            recLockoutDays,
            recAveragingMethod,
            telescopicValueDates,
            useIndexedCoupons,
            recStubIndexSelection,
            context);

        Date earliestDate = Earlier(CashFlows.StartDate(payLeg), CashFlows.StartDate(recLeg));
        Date maturityDate = Later(CashFlows.MaturityDate(payLeg), CashFlows.MaturityDate(recLeg));

        // Add notional exchanges on payLeg
        payLeg = AddNotionalExchangesToLeg(
            payLeg,
            paySchedule.Calendar ?? EmptyCalendar.Instance,
            earliestDate,
            maturityDate,
            paySchedule.BusinessDayConvention,
            payNominal,
            paymentLagOnNotionalExchanges ? payPaymentLag : 0);

        // Add notional exchanges on recLeg
        recLeg = AddNotionalExchangesToLeg(
            recLeg,
            recSchedule.Calendar ?? EmptyCalendar.Instance,
            earliestDate,
            maturityDate,
            recSchedule.BusinessDayConvention,
            recNominal,
            paymentLagOnNotionalExchanges ? recPaymentLag : 0);

        return [payLeg, recLeg];
    }

    private static ImmutableArray<CashFlow> Leg(
        Schedule schedule,
        IborIndex index,
        double nominal,
        double spread,
        double gearing,
        int paymentLag,
        bool compoundSpread,
        int? lookbackDays,
        bool observationShift,
        int lockoutDays,
        RateAveragingType averagingMethod,
        bool telescopicValueDates,
        bool? useIndexedCoupons,
        StubIndexSelection stubIndexSelection,
        EvaluationContext context) =>
        index is OvernightIndex on
            ? new OvernightLeg(schedule, on) // ON leg
                .WithNotionals(nominal)
                .WithSpreads(spread)
                .WithGearings(gearing)
                .WithPaymentLag(paymentLag)
                .CompoundingSpreadDaily(compoundSpread)
                .WithLookbackDays(lookbackDays)
                .WithObservationShift(observationShift)
                .WithLockoutDays(lockoutDays)
                .WithAveragingMethod(averagingMethod)
                .WithTelescopicValueDates(telescopicValueDates)
                .ToLeg()
            : new IborLeg(schedule, index) // Ibor leg
                .WithNotionals(nominal)
                .WithSpreads(spread)
                .WithGearings(gearing)
                .WithPaymentLag(paymentLag)
                .WithIndexedCoupons(useIndexedCoupons)
                .WithStubIndexSelection(stubIndexSelection)
                .ToLeg(context);

    private static Date Earlier(Date a, Date b) => b < a ? b : a;

    private static Date Later(Date a, Date b) => a < b ? b : a;
}
