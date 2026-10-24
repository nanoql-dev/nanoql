// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/floatfloatswap.hpp ql/instruments/floatfloatswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Instruments;

/// <summary>
/// A swap exchanging capped or floored Ibor or CMS coupons with quite general specification.
/// </summary>
/// <remarks>
/// <para>
/// If no payment convention is given, the respective leg schedule's convention is used. The interest-rate indexes
/// should be linked to valid forwarding curves, and, for swap indexes, discounting curves.
/// </para>
/// <para>
/// The constructors build the legs, as QuantLib's do: an Ibor leg with the index's default pricer, which reads the
/// context's at-par switch, so they take the context; a CMS leg without a pricer, as QuantLib's, on which
/// <see cref="CouponPricerFunctions.SetCouponPricer{T}(T, int, FloatingRateCouponPricer)"/> sets a
/// <see cref="CmsCouponPricer"/>. A zero gearing becomes QuantLib's machine epsilon, so that the legs are built of
/// floating coupons.
/// The first leg is paid by a payer swap. An engine's results are a <see cref="FloatFloatSwapResults"/>, whose fair
/// spreads <see cref="FetchResults"/> derives from the legs' BPS when the engine gives none. QuantLib's
/// <c>setupArguments</c> isn't ported: nanoql's engines read the legs.
/// </para>
/// </remarks>
public sealed class FloatFloatSwap : Swap
{
    private const double BasisPoint = 1.0e-4;

    /// <summary>Initializes a new instance of the <see cref="FloatFloatSwap"/> class with constant terms.</summary>
    /// <param name="type">A payer swap pays the first leg; a receiver swap receives it.</param>
    /// <param name="nominal1">The first leg's nominal.</param>
    /// <param name="nominal2">The second leg's nominal.</param>
    /// <param name="schedule1">The first leg's schedule.</param>
    /// <param name="index1">The first leg's index: an Ibor, a swap or a swap spread index.</param>
    /// <param name="dayCount1">The first leg's day counter; the index's when <see langword="null"/>, QuantLib's empty
    /// day counter.</param>
    /// <param name="schedule2">The second leg's schedule.</param>
    /// <param name="index2">The second leg's index: an Ibor, a swap or a swap spread index.</param>
    /// <param name="dayCount2">The second leg's day counter; the index's when <see langword="null"/>.</param>
    /// <param name="context">The evaluation context, whose at-par switch the Ibor legs read.</param>
    /// <param name="intermediateCapitalExchange">Whether the nominals' changes are exchanged.</param>
    /// <param name="finalCapitalExchange">Whether the final nominals are exchanged.</param>
    /// <param name="gearing1">The first leg's gearing.</param>
    /// <param name="spread1">The first leg's spread.</param>
    /// <param name="cappedRate1">The first leg's cap, or <see langword="null"/> for none.</param>
    /// <param name="flooredRate1">The first leg's floor, or <see langword="null"/> for none.</param>
    /// <param name="gearing2">The second leg's gearing.</param>
    /// <param name="spread2">The second leg's spread.</param>
    /// <param name="cappedRate2">The second leg's cap, or <see langword="null"/> for none.</param>
    /// <param name="flooredRate2">The second leg's floor, or <see langword="null"/> for none.</param>
    /// <param name="paymentConvention1">The first leg's payment convention; its schedule's when
    /// <see langword="null"/>.</param>
    /// <param name="paymentConvention2">The second leg's payment convention; its schedule's when
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">A schedule, an index or the context is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The type or a payment convention isn't a member of its
    /// enum.</exception>
    /// <exception cref="ArgumentException">A schedule is empty or has a single date; or an index is neither an Ibor, a
    /// swap nor a swap spread index.</exception>
    public FloatFloatSwap(
        SwapType type,
        double nominal1,
        double nominal2,
        Schedule schedule1,
        InterestRateIndex index1,
        DayCounter? dayCount1,
        Schedule schedule2,
        InterestRateIndex index2,
        DayCounter? dayCount2,
        EvaluationContext context,
        bool intermediateCapitalExchange = false,
        bool finalCapitalExchange = false,
        double gearing1 = 1.0,
        double spread1 = 0.0,
        double? cappedRate1 = null,
        double? flooredRate1 = null,
        double gearing2 = 1.0,
        double spread2 = 0.0,
        double? cappedRate2 = null,
        double? flooredRate2 = null,
        BusinessDayConvention? paymentConvention1 = null,
        BusinessDayConvention? paymentConvention2 = null)
        : this(Terms.OfConstants(
            type,
            nominal1,
            nominal2,
            schedule1,
            index1,
            dayCount1,
            schedule2,
            index2,
            dayCount2,
            context,
            intermediateCapitalExchange,
            finalCapitalExchange,
            gearing1,
            spread1,
            cappedRate1,
            flooredRate1,
            gearing2,
            spread2,
            cappedRate2,
            flooredRate2,
            paymentConvention1,
            paymentConvention2))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FloatFloatSwap"/> class with terms per coupon.</summary>
    /// <param name="type">A payer swap pays the first leg; a receiver swap receives it.</param>
    /// <param name="nominal1">The first leg's nominals, one per coupon.</param>
    /// <param name="nominal2">The second leg's nominals, one per coupon.</param>
    /// <param name="schedule1">The first leg's schedule.</param>
    /// <param name="index1">The first leg's index: an Ibor, a swap or a swap spread index.</param>
    /// <param name="dayCount1">The first leg's day counter; the index's when <see langword="null"/>, QuantLib's empty
    /// day counter.</param>
    /// <param name="schedule2">The second leg's schedule.</param>
    /// <param name="index2">The second leg's index: an Ibor, a swap or a swap spread index.</param>
    /// <param name="dayCount2">The second leg's day counter; the index's when <see langword="null"/>.</param>
    /// <param name="context">The evaluation context, whose at-par switch the Ibor legs read.</param>
    /// <param name="intermediateCapitalExchange">Whether the nominals' changes are exchanged.</param>
    /// <param name="finalCapitalExchange">Whether the final nominals are exchanged.</param>
    /// <param name="gearing1">The first leg's gearings, one per coupon; 1 each when <see langword="null"/> or
    /// empty.</param>
    /// <param name="spread1">The first leg's spreads, one per coupon; 0 each when <see langword="null"/> or
    /// empty.</param>
    /// <param name="cappedRate1">The first leg's caps, one per coupon, all or none of them <see langword="null"/>;
    /// none when <see langword="null"/> or empty.</param>
    /// <param name="flooredRate1">The first leg's floors, likewise.</param>
    /// <param name="gearing2">The second leg's gearings, likewise.</param>
    /// <param name="spread2">The second leg's spreads, likewise.</param>
    /// <param name="cappedRate2">The second leg's caps, likewise.</param>
    /// <param name="flooredRate2">The second leg's floors, likewise.</param>
    /// <param name="paymentConvention1">The first leg's payment convention; its schedule's when
    /// <see langword="null"/>.</param>
    /// <param name="paymentConvention2">The second leg's payment convention; its schedule's when
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">A nominal sequence, a schedule, an index or the context is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The type or a payment convention isn't a member of its
    /// enum.</exception>
    /// <exception cref="ArgumentException">A sequence's size doesn't match its leg's coupons; a schedule has a single
    /// date; a cap or floor sequence mixes null and given values; or an index is neither an Ibor, a swap nor a swap
    /// spread index.</exception>
    public FloatFloatSwap(
        SwapType type,
        IEnumerable<double> nominal1,
        IEnumerable<double> nominal2,
        Schedule schedule1,
        InterestRateIndex index1,
        DayCounter? dayCount1,
        Schedule schedule2,
        InterestRateIndex index2,
        DayCounter? dayCount2,
        EvaluationContext context,
        bool intermediateCapitalExchange = false,
        bool finalCapitalExchange = false,
        IEnumerable<double>? gearing1 = null,
        IEnumerable<double>? spread1 = null,
        IEnumerable<double?>? cappedRate1 = null,
        IEnumerable<double?>? flooredRate1 = null,
        IEnumerable<double>? gearing2 = null,
        IEnumerable<double>? spread2 = null,
        IEnumerable<double?>? cappedRate2 = null,
        IEnumerable<double?>? flooredRate2 = null,
        BusinessDayConvention? paymentConvention1 = null,
        BusinessDayConvention? paymentConvention2 = null)
        : this(Terms.OfSequences(
            type,
            nominal1,
            nominal2,
            schedule1,
            index1,
            dayCount1,
            schedule2,
            index2,
            dayCount2,
            context,
            intermediateCapitalExchange,
            finalCapitalExchange,
            gearing1,
            spread1,
            cappedRate1,
            flooredRate1,
            gearing2,
            spread2,
            cappedRate2,
            flooredRate2,
            paymentConvention1,
            paymentConvention2))
    {
    }

    private FloatFloatSwap(Terms terms)
        : base([terms.Leg1, terms.Leg2], [terms.Type == SwapType.Payer, terms.Type == SwapType.Receiver])
    {
        Type = terms.Type;
        Nominal1 = terms.Nominal1;
        Nominal2 = terms.Nominal2;
        Schedule1 = terms.Schedule1;
        Schedule2 = terms.Schedule2;
        Index1 = terms.Index1;
        Index2 = terms.Index2;
        Gearing1 = terms.Gearing1;
        Gearing2 = terms.Gearing2;
        Spread1 = terms.Spread1;
        Spread2 = terms.Spread2;
        CappedRate1 = terms.CappedRate1;
        FlooredRate1 = terms.FlooredRate1;
        CappedRate2 = terms.CappedRate2;
        FlooredRate2 = terms.FlooredRate2;
        DayCount1 = terms.DayCount1;
        DayCount2 = terms.DayCount2;
        PaymentConvention1 = terms.PaymentConvention1;
        PaymentConvention2 = terms.PaymentConvention2;
    }

    /// <summary>Gets whether the first leg is paid (payer) or received (receiver).</summary>
    public SwapType Type { get; }

    /// <summary>Gets the first leg's nominals: one per coupon, and one more for each capital exchange, as QuantLib's
    /// inspector gives them.</summary>
    public ImmutableArray<double> Nominal1 { get; }

    /// <summary>Gets the second leg's nominals, likewise.</summary>
    public ImmutableArray<double> Nominal2 { get; }

    /// <summary>Gets the first leg's schedule.</summary>
    public Schedule Schedule1 { get; }

    /// <summary>Gets the second leg's schedule.</summary>
    public Schedule Schedule2 { get; }

    /// <summary>Gets the first leg's index.</summary>
    public InterestRateIndex Index1 { get; }

    /// <summary>Gets the second leg's index.</summary>
    public InterestRateIndex Index2 { get; }

    /// <summary>Gets the first leg's spreads, one per coupon.</summary>
    public ImmutableArray<double> Spread1 { get; }

    /// <summary>Gets the second leg's spreads, one per coupon.</summary>
    public ImmutableArray<double> Spread2 { get; }

    /// <summary>Gets the first leg's gearings, one per coupon, a zero one as QuantLib's machine epsilon.</summary>
    public ImmutableArray<double> Gearing1 { get; }

    /// <summary>Gets the second leg's gearings, likewise.</summary>
    public ImmutableArray<double> Gearing2 { get; }

    /// <summary>Gets the first leg's caps, one per coupon, all <see langword="null"/> for none.</summary>
    public ImmutableArray<double?> CappedRate1 { get; }

    /// <summary>Gets the first leg's floors, one per coupon, all <see langword="null"/> for none.</summary>
    public ImmutableArray<double?> FlooredRate1 { get; }

    /// <summary>Gets the second leg's caps, likewise.</summary>
    public ImmutableArray<double?> CappedRate2 { get; }

    /// <summary>Gets the second leg's floors, likewise.</summary>
    public ImmutableArray<double?> FlooredRate2 { get; }

    /// <summary>Gets the first leg's day counter as given, or <see langword="null"/>.</summary>
    public DayCounter? DayCount1 { get; }

    /// <summary>Gets the second leg's day counter as given, or <see langword="null"/>.</summary>
    public DayCounter? DayCount2 { get; }

    /// <summary>Gets the first leg's payment convention: its schedule's when none was given.</summary>
    public BusinessDayConvention PaymentConvention1 { get; }

    /// <summary>Gets the second leg's payment convention: its schedule's when none was given.</summary>
    public BusinessDayConvention PaymentConvention2 { get; }

    /// <summary>Gets the first leg.</summary>
    public ImmutableArray<CashFlow> Leg1 => Legs[0];

    /// <summary>Gets the second leg.</summary>
    public ImmutableArray<CashFlow> Leg2 => Legs[1];

    /// <inheritdoc/>
    /// <remarks>An expired swap has zero leg BPS, and no fair spreads.</remarks>
    public override FloatFloatSwapResults SetupExpired() => new(base.SetupExpired())
    {
        FairSpread1 = null,
        FairSpread2 = null,
    };

    /// <inheritdoc/>
    /// <remarks>
    /// The fair spreads are the engine's when it gives them; otherwise each is derived from the NPV and its leg's BPS,
    /// and is <see langword="null"/> when that BPS is zero or unknown. The BPS route is exact for a leg with no cap or
    /// floor; <see cref="PricingEngines.DiscountingSwapEngine"/> solves a capped or floored leg's fair spread instead.
    /// </remarks>
    public override FloatFloatSwapResults FetchResults(InstrumentResults results)
    {
        SwapResults r = base.FetchResults(results);
        var fetched = r as FloatFloatSwapResults;
        double? fairSpread1 = fetched?.FairSpread1;
        double? fairSpread2 = fetched?.FairSpread2;

        if (fairSpread1 is null && r.LegBPS[0] is double bps1 && bps1 != 0.0)
        {
            fairSpread1 = Spread1[0] - (r.NPV / (bps1 / BasisPoint));
        }

        if (fairSpread2 is null && r.LegBPS[1] is double bps2 && bps2 != 0.0)
        {
            fairSpread2 = Spread2[0] - (r.NPV / (bps2 / BasisPoint));
        }

        return fetched is not null
            ? fetched with { FairSpread1 = fairSpread1, FairSpread2 = fairSpread2 }
            : new FloatFloatSwapResults(r) { FairSpread1 = fairSpread1, FairSpread2 = fairSpread2 };
    }

    /// <summary>Whether a leg holds a capped or floored coupon, whose amount doesn't move one for one with its
    /// spread.</summary>
    /// <param name="j">The leg's index.</param>
    /// <returns><see langword="true"/> when the leg holds one.</returns>
    internal bool IsOptional(int j) => Legs[j].Any(cf => cf is CappedFlooredCoupon);

    /// <summary>
    /// A copy of the swap with every coupon of a leg at its spread plus a shift, each keeping its pricer, cap and
    /// floor: the swap QuantLib's constructor would build at the shifted spreads, priced as the coupons are.
    /// </summary>
    /// <param name="j">The leg's index.</param>
    /// <param name="shift">The shift added to each coupon's spread.</param>
    /// <returns>The copy; the receiver is unchanged.</returns>
    internal FloatFloatSwap WithLegSpreadShift(int j, double shift) =>
        (FloatFloatSwap)WithLeg(j, [.. Legs[j].Select(cf => cf is FloatingRateCoupon c ? Shifted(c, shift) : cf)]);

    // A coupon at its spread plus a shift, of the kinds the constructors build, with its pricer.
    private static FloatingRateCoupon Shifted(FloatingRateCoupon c, double shift) => c switch
    {
        CappedFlooredCoupon capped => new CappedFlooredCoupon(Shifted(capped.Underlying, shift), capped.Cap, capped.Floor)
            .SetPricer(capped.Pricer),
        IborCoupon ibor => new IborCoupon(
            ibor.Date,
            ibor.ContractNominal,
            ibor.AccrualStartDate,
            ibor.AccrualEndDate,
            ibor.FixingDays,
            ibor.IborIndex,
            ibor.Gearing,
            ibor.Spread + shift,
            ibor.ReferencePeriodStart,
            ibor.ReferencePeriodEnd,
            ibor.DayCounter,
            ibor.IsInArrears,
            ibor.ExCouponDate,
            ibor.FixingConvention,
            ibor.Pricer),
        CmsCoupon cms => new CmsCoupon(
            cms.Date,
            cms.ContractNominal,
            cms.AccrualStartDate,
            cms.AccrualEndDate,
            cms.FixingDays,
            cms.SwapIndex,
            cms.Gearing,
            cms.Spread + shift,
            cms.ReferencePeriodStart,
            cms.ReferencePeriodEnd,
            cms.DayCounter,
            cms.IsInArrears,
            cms.ExCouponDate,
            cms.FixingConvention).SetPricer(cms.Pricer),
        CmsSpreadCoupon spread => new CmsSpreadCoupon(
            spread.Date,
            spread.ContractNominal,
            spread.AccrualStartDate,
            spread.AccrualEndDate,
            spread.FixingDays,
            spread.SwapSpreadIndex,
            spread.Gearing,
            spread.Spread + shift,
            spread.ReferencePeriodStart,
            spread.ReferencePeriodEnd,
            spread.DayCounter,
            spread.IsInArrears,
            spread.ExCouponDate,
            spread.FixingConvention).SetPricer(spread.Pricer),
        _ => throw new NanoQLException("unexpected coupon on a float-float swap's leg"),
    };

    // QuantLib's constructors and init, run before the swap's own constructor takes the legs: the terms checked,
    // completed and the legs built, in QuantLib's order.
    private sealed class Terms
    {
        public required SwapType Type { get; init; }

        public required ImmutableArray<double> Nominal1 { get; init; }

        public required ImmutableArray<double> Nominal2 { get; init; }

        public required Schedule Schedule1 { get; init; }

        public required Schedule Schedule2 { get; init; }

        public required InterestRateIndex Index1 { get; init; }

        public required InterestRateIndex Index2 { get; init; }

        public required ImmutableArray<double> Gearing1 { get; init; }

        public required ImmutableArray<double> Gearing2 { get; init; }

        public required ImmutableArray<double> Spread1 { get; init; }

        public required ImmutableArray<double> Spread2 { get; init; }

        public required ImmutableArray<double?> CappedRate1 { get; init; }

        public required ImmutableArray<double?> FlooredRate1 { get; init; }

        public required ImmutableArray<double?> CappedRate2 { get; init; }

        public required ImmutableArray<double?> FlooredRate2 { get; init; }

        public required DayCounter? DayCount1 { get; init; }

        public required DayCounter? DayCount2 { get; init; }

        public required BusinessDayConvention PaymentConvention1 { get; init; }

        public required BusinessDayConvention PaymentConvention2 { get; init; }

        public required ImmutableArray<CashFlow> Leg1 { get; init; }

        public required ImmutableArray<CashFlow> Leg2 { get; init; }

        // The constant-terms constructor fills a vector per term with the schedule's coupon count, schedule.size() - 1,
        // which an empty schedule wraps to SIZE_MAX, and std::vector's constructor throws std::length_error
        // (floatfloatswap.cpp:58-69): refused there with "empty schedule".
        public static Terms OfConstants(
            SwapType type,
            double nominal1,
            double nominal2,
            Schedule schedule1,
            InterestRateIndex index1,
            DayCounter? dayCount1,
            Schedule schedule2,
            InterestRateIndex index2,
            DayCounter? dayCount2,
            EvaluationContext context,
            bool intermediateCapitalExchange,
            bool finalCapitalExchange,
            double gearing1,
            double spread1,
            double? cappedRate1,
            double? flooredRate1,
            double gearing2,
            double spread2,
            double? cappedRate2,
            double? flooredRate2,
            BusinessDayConvention? paymentConvention1,
            BusinessDayConvention? paymentConvention2)
        {
            CheckEntry(type, context);
            int n1 = CouponCount(schedule1, nameof(schedule1));
            int n2 = CouponCount(schedule2, nameof(schedule2));
            return Of(
                type,
                [.. Enumerable.Repeat(nominal1, n1)],
                [.. Enumerable.Repeat(nominal2, n2)],
                schedule1,
                index1,
                dayCount1,
                schedule2,
                index2,
                dayCount2,
                context,
                intermediateCapitalExchange,
                finalCapitalExchange,
                [.. Enumerable.Repeat(gearing1, n1)],
                [.. Enumerable.Repeat(spread1, n1)],
                [.. Enumerable.Repeat(cappedRate1, n1)],
                [.. Enumerable.Repeat(flooredRate1, n1)],
                [.. Enumerable.Repeat(gearing2, n2)],
                [.. Enumerable.Repeat(spread2, n2)],
                [.. Enumerable.Repeat(cappedRate2, n2)],
                [.. Enumerable.Repeat(flooredRate2, n2)],
                paymentConvention1,
                paymentConvention2);
        }

        public static Terms OfSequences(
            SwapType type,
            IEnumerable<double> nominal1,
            IEnumerable<double> nominal2,
            Schedule schedule1,
            InterestRateIndex index1,
            DayCounter? dayCount1,
            Schedule schedule2,
            InterestRateIndex index2,
            DayCounter? dayCount2,
            EvaluationContext context,
            bool intermediateCapitalExchange,
            bool finalCapitalExchange,
            IEnumerable<double>? gearing1,
            IEnumerable<double>? spread1,
            IEnumerable<double?>? cappedRate1,
            IEnumerable<double?>? flooredRate1,
            IEnumerable<double>? gearing2,
            IEnumerable<double>? spread2,
            IEnumerable<double?>? cappedRate2,
            IEnumerable<double?>? flooredRate2,
            BusinessDayConvention? paymentConvention1,
            BusinessDayConvention? paymentConvention2)
        {
            CheckEntry(type, context);
            ArgumentNullException.ThrowIfNull(nominal1);
            ArgumentNullException.ThrowIfNull(nominal2);
            ArgumentNullException.ThrowIfNull(schedule1);
            ArgumentNullException.ThrowIfNull(schedule2);
            return Of(
                type,
                [.. nominal1],
                [.. nominal2],
                schedule1,
                index1,
                dayCount1,
                schedule2,
                index2,
                dayCount2,
                context,
                intermediateCapitalExchange,
                finalCapitalExchange,
                gearing1 is null ? [] : [.. gearing1],
                spread1 is null ? [] : [.. spread1],
                cappedRate1 is null ? [] : [.. cappedRate1],
                flooredRate1 is null ? [] : [.. flooredRate1],
                gearing2 is null ? [] : [.. gearing2],
                spread2 is null ? [] : [.. spread2],
                cappedRate2 is null ? [] : [.. cappedRate2],
                flooredRate2 is null ? [] : [.. flooredRate2],
                paymentConvention1,
                paymentConvention2);
        }

        // QuantLib refuses an unknown type after building the legs (floatfloatswap.cpp:377-388); a value outside the
        // enum is refused on entry, as the enum row has it.
        private static void CheckEntry(SwapType type, EvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (type is not (SwapType.Payer or SwapType.Receiver))
            {
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown float float - swap type");
            }
        }

        private static int CouponCount(Schedule schedule, string paramName)
        {
            ArgumentNullException.ThrowIfNull(schedule, paramName);
            if (schedule.Empty)
            {
                throw new ArgumentException("empty schedule", paramName);
            }

            return schedule.Size - 1;
        }

        // QuantLib's init (floatfloatswap.cpp:111-389).
        private static Terms Of(
            SwapType type,
            List<double> nominal1,
            List<double> nominal2,
            Schedule schedule1,
            InterestRateIndex index1,
            DayCounter? dayCount1,
            Schedule schedule2,
            InterestRateIndex index2,
            DayCounter? dayCount2,
            EvaluationContext context,
            bool intermediateCapitalExchange,
            bool finalCapitalExchange,
            List<double> gearing1,
            List<double> spread1,
            List<double?> cappedRate1,
            List<double?> flooredRate1,
            List<double> gearing2,
            List<double> spread2,
            List<double?> cappedRate2,
            List<double?> flooredRate2,
            BusinessDayConvention? paymentConvention1,
            BusinessDayConvention? paymentConvention2)
        {
            // An empty schedule given here is refused by the nominal check, its count wrapping as QuantLib's does.
            if (!(schedule1.Size > 0 && nominal1.Count == schedule1.Size - 1))
            {
                throw new ArgumentException(
                    $"nominal1 size ({nominal1.Count}) does not match schedule1 size ({schedule1.Size})",
                    nameof(nominal1));
            }

            // QuantLib prints nominal2's size in schedule2's place (floatfloatswap.cpp:119-122); the schedule's is
            // printed here.
            if (!(schedule2.Size > 0 && nominal2.Count == schedule2.Size - 1))
            {
                throw new ArgumentException(
                    $"nominal2 size ({nominal2.Count}) does not match schedule2 size ({schedule2.Size})",
                    nameof(nominal2));
            }

            CheckSize(gearing1.Count, nominal1.Count, "nominal1", "gearing1");
            CheckSize(gearing2.Count, nominal2.Count, "nominal2", "gearing2");
            CheckSize(cappedRate1.Count, nominal1.Count, "nominal1", "cappedRate1");
            CheckSize(cappedRate2.Count, nominal2.Count, "nominal2", "cappedRate2");
            CheckSize(flooredRate1.Count, nominal1.Count, "nominal1", "flooredRate1");
            CheckSize(flooredRate2.Count, nominal2.Count, "nominal2", "flooredRate2");

            BusinessDayConvention convention1 =
                PaymentConvention(paymentConvention1, schedule1, nameof(paymentConvention1));
            BusinessDayConvention convention2 =
                PaymentConvention(paymentConvention2, schedule2, nameof(paymentConvention2));

            Fill(gearing1, nominal1.Count, 1.0);
            Fill(gearing2, nominal2.Count, 1.0);
            Fill(spread1, nominal1.Count, 0.0);
            Fill(spread2, nominal2.Count, 0.0);
            Fill(cappedRate1, nominal1.Count, null);
            Fill(cappedRate2, nominal2.Count, null);
            Fill(flooredRate1, nominal1.Count, null);
            Fill(flooredRate2, nominal2.Count, null);

            // QuantLib reads each vector's first element unchecked (floatfloatswap.cpp:170, :182, :194, :206), which a
            // schedule of one date leaves empty: refused before the first read of its leg.
            CheckCoupons(cappedRate1, nameof(schedule1));
            CheckNullForAllOrNone(cappedRate1, "cappedRate1", "cappedRate 1", nameof(cappedRate1));
            CheckCoupons(cappedRate2, nameof(schedule2));
            CheckNullForAllOrNone(cappedRate2, "cappedRate2", "cappedRate2", nameof(cappedRate2));
            CheckNullForAllOrNone(flooredRate1, "flooredRate1", "flooredRate 1", nameof(flooredRate1));
            CheckNullForAllOrNone(flooredRate2, "flooredRate2", "flooredRate2", nameof(flooredRate2));

            // if the gearing is zero then the ibor / cms leg will be set up with fixed coupons which makes trouble
            // here in this context. We therefore use a dirty trick and enforce the gearing to be non zero.
            NonZero(gearing1);
            NonZero(gearing2);

            CheckIndex(index1, "index1 must be ibor or cms or cms spread", nameof(index1));
            CheckIndex(index2, "index2 must be ibor or cms", nameof(index2));

            ImmutableArray<CashFlow> leg1 = [];
            ImmutableArray<CashFlow> leg2 = [];
            if (index1 is IborIndex ibor1)
            {
                leg1 = BuildIborLeg(
                    schedule1, ibor1, nominal1, dayCount1, convention1, spread1, gearing1, cappedRate1, flooredRate1,
                    context);
            }

            if (index2 is IborIndex ibor2)
            {
                leg2 = BuildIborLeg(
                    schedule2, ibor2, nominal2, dayCount2, convention2, spread2, gearing2, cappedRate2, flooredRate2,
                    context);
            }

            if (index1 is SwapIndex cms1)
            {
                leg1 = BuildCmsLeg(
                    schedule1, cms1, nominal1, dayCount1, convention1, spread1, gearing1, cappedRate1, flooredRate1);
            }

            if (index2 is SwapIndex cms2)
            {
                leg2 = BuildCmsLeg(
                    schedule2, cms2, nominal2, dayCount2, convention2, spread2, gearing2, cappedRate2, flooredRate2);
            }

            if (index1 is SwapSpreadIndex cmsspread1)
            {
                leg1 = BuildCmsSpreadLeg(
                    schedule1, cmsspread1, nominal1, dayCount1, convention1, spread1, gearing1, cappedRate1,
                    flooredRate1);
            }

            if (index2 is SwapSpreadIndex cmsspread2)
            {
                leg2 = BuildCmsSpreadLeg(
                    schedule2, cmsspread2, nominal2, dayCount2, convention2, spread2, gearing2, cappedRate2,
                    flooredRate2);
            }

            List<CashFlow> legs1 = [.. leg1];
            List<CashFlow> legs2 = [.. leg2];
            if (intermediateCapitalExchange)
            {
                ExchangeIntermediateCapital(legs1, nominal1);
                ExchangeIntermediateCapital(legs2, nominal2);
            }

            if (finalCapitalExchange)
            {
                legs1.Add(new Redemption(nominal1[^1], legs1[^1].Date));
                nominal1.Add(nominal1[^1]);
                legs2.Add(new Redemption(nominal2[^1], legs2[^1].Date));
                nominal2.Add(nominal2[^1]);
            }

            return new Terms
            {
                Type = type,
                Nominal1 = [.. nominal1],
                Nominal2 = [.. nominal2],
                Schedule1 = schedule1,
                Schedule2 = schedule2,
                Index1 = index1,
                Index2 = index2,
                Gearing1 = [.. gearing1],
                Gearing2 = [.. gearing2],
                Spread1 = [.. spread1],
                Spread2 = [.. spread2],
                CappedRate1 = [.. cappedRate1],
                FlooredRate1 = [.. flooredRate1],
                CappedRate2 = [.. cappedRate2],
                FlooredRate2 = [.. flooredRate2],
                DayCount1 = dayCount1,
                DayCount2 = dayCount2,
                PaymentConvention1 = convention1,
                PaymentConvention2 = convention2,
                Leg1 = [.. legs1],
                Leg2 = [.. legs2],
            };
        }

        private static void CheckSize(int size, int nominals, string nominalName, string paramName)
        {
            if (!(size == 0 || size == nominals))
            {
                throw new ArgumentException(
                    $"{nominalName} size ({nominals}) does not match {paramName} size ({size})", paramName);
            }
        }

        private static BusinessDayConvention PaymentConvention(
            BusinessDayConvention? convention, Schedule schedule, string paramName)
        {
            if (convention is BusinessDayConvention c)
            {
                EnumArguments.Check(c, paramName);
                return c;
            }

            return schedule.BusinessDayConvention;
        }

        private static void Fill<T>(List<T> values, int count, T value)
        {
            if (values.Count == 0)
            {
                values.AddRange(Enumerable.Repeat(value, count));
            }
        }

        private static void CheckCoupons(List<double?> rates, string paramName)
        {
            if (rates.Count == 0)
            {
                throw new ArgumentException($"{paramName} has a single date, so its leg has no coupons", paramName);
            }
        }

        // QuantLib's texts, kept: each check names its vector one way when a null is given among values ("cappedRate1
        // must be null...") and, for the first leg, with a space when a value is given among nulls ("cappedRate 1").
        private static void CheckNullForAllOrNone(
            List<double?> rates, string nullName, string valueName, string paramName)
        {
            bool isNull = rates[0] is null;
            for (int i = 0; i < rates.Count; i++)
            {
                if (isNull)
                {
                    if (rates[i] is double given)
                    {
                        throw new ArgumentException(
                            $"{nullName} must be null for all or none entry ({i + 1}th is "
                            + $"{DataFormattersFunctions.General(given)})",
                            paramName);
                    }
                }
                else if (rates[i] is null)
                {
                    throw new ArgumentException(
                        $"{valueName} must be null for all or none entry (1st is "
                        + $"{DataFormattersFunctions.General(rates[0]!.Value)})",
                        paramName);
                }
            }
        }

        private static void NonZero(List<double> gearings)
        {
            for (int i = 0; i < gearings.Count; i++)
            {
                if (ComparisonFunctions.Close(gearings[i], 0.0))
                {
                    gearings[i] = NumericLimits.QL_EPSILON;
                }
            }
        }

        // QuantLib's dynamic casts give null for a null index, which its check then refuses with its text.
        private static void CheckIndex(InterestRateIndex index, string message, string paramName)
        {
            if (index is null)
            {
                throw new ArgumentNullException(paramName, message);
            }

            if (index is not (IborIndex or SwapIndex or SwapSpreadIndex))
            {
                throw new ArgumentException(message, paramName);
            }
        }

        private static ImmutableArray<CashFlow> BuildIborLeg(
            Schedule schedule,
            IborIndex index,
            List<double> nominals,
            DayCounter? dayCount,
            BusinessDayConvention convention,
            List<double> spreads,
            List<double> gearings,
            List<double?> caps,
            List<double?> floors,
            EvaluationContext context)
        {
            IborLeg leg = new IborLeg(schedule, index)
                .WithNotionals(nominals)
                .WithPaymentDayCounter(dayCount)
                .WithPaymentAdjustment(convention)
                .WithSpreads(spreads)
                .WithGearings(gearings);
            if (caps[0] is not null)
            {
                leg = leg.WithCaps(caps);
            }

            if (floors[0] is not null)
            {
                leg = leg.WithFloors(floors);
            }

            return leg.ToLeg(context);
        }

        private static ImmutableArray<CashFlow> BuildCmsLeg(
            Schedule schedule,
            SwapIndex index,
            List<double> nominals,
            DayCounter? dayCount,
            BusinessDayConvention convention,
            List<double> spreads,
            List<double> gearings,
            List<double?> caps,
            List<double?> floors)
        {
            CmsLeg leg = new CmsLeg(schedule, index)
                .WithNotionals(nominals)
                .WithPaymentDayCounter(dayCount)
                .WithPaymentAdjustment(convention)
                .WithSpreads(spreads)
                .WithGearings(gearings);
            if (caps[0] is not null)
            {
                leg = leg.WithCaps(caps);
            }

            if (floors[0] is not null)
            {
                leg = leg.WithFloors(floors);
            }

            return leg.ToLeg();
        }

        private static ImmutableArray<CashFlow> BuildCmsSpreadLeg(
            Schedule schedule,
            SwapSpreadIndex index,
            List<double> nominals,
            DayCounter? dayCount,
            BusinessDayConvention convention,
            List<double> spreads,
            List<double> gearings,
            List<double?> caps,
            List<double?> floors)
        {
            CmsSpreadLeg leg = new CmsSpreadLeg(schedule, index)
                .WithNotionals(nominals)
                .WithPaymentDayCounter(dayCount)
                .WithPaymentAdjustment(convention)
                .WithSpreads(spreads)
                .WithGearings(gearings);
            if (caps[0] is not null)
            {
                leg = leg.WithCaps(caps);
            }

            if (floors[0] is not null)
            {
                leg = leg.WithFloors(floors);
            }

            return leg.ToLeg();
        }

        // A redemption of each nominal's change after the coupon before it, its nominal repeated beside it.
        private static void ExchangeIntermediateCapital(List<CashFlow> leg, List<double> nominals)
        {
            for (int i = 0; i < leg.Count - 1; i++)
            {
                double cap = nominals[i] - nominals[i + 1];
                if (!ComparisonFunctions.Close(cap, 0.0))
                {
                    leg.Insert(i + 1, new Redemption(cap, leg[i].Date));
                    nominals.Insert(i + 1, nominals[i]);
                    i++;
                }
            }
        }
    }
}
