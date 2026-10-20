// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/yearonyearinflationswap.hpp ql/instruments/yearonyearinflationswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A year-on-year inflation-indexed swap, quoted as a fixed rate <c>K</c>: each period exchanges <c>N K</c> against the
/// index's year-on-year growth, <c>N [I(t_i)/I(t_i - 1) - 1]</c>, plus a spread.
/// </summary>
/// <remarks>
/// <para>
/// The type, payer or receiver, refers to the fixed leg. The fixed leg is the first leg, fixed-rate coupons on the
/// fixed schedule; the year-on-year leg is the second, <see cref="YoYInflationLeg"/>'s coupons with its default pricer,
/// paying on the payment calendar, since an inflation index has none. Both legs pay on the payment convention.
/// </para>
/// <para>
/// The constructor reads no evaluation date. An engine's results are a <see cref="YearOnYearInflationSwapResults"/>,
/// whose fair rate and spread <see cref="FetchResults"/> derives from the legs' BPS, as QuantLib's swap derives them
/// from a swap engine's results; QuantLib's own arguments and engine classes aren't ported, since no engine uses them.
/// A type that isn't a member of <see cref="SwapType"/> is refused, where QuantLib takes it as a receiver.
/// </para>
/// </remarks>
public sealed class YearOnYearInflationSwap : Swap
{
    private const double BasisPoint = 1.0e-4;

    /// <summary>Initializes a new instance of the <see cref="YearOnYearInflationSwap"/> class.</summary>
    /// <param name="type">Whether the fixed leg is paid or received.</param>
    /// <param name="nominal">The nominal of both legs.</param>
    /// <param name="fixedSchedule">The fixed leg's schedule.</param>
    /// <param name="fixedRate">The fixed rate.</param>
    /// <param name="fixedDayCount">The fixed leg's day counter.</param>
    /// <param name="yoySchedule">The year-on-year leg's schedule.</param>
    /// <param name="yoyIndex">The year-on-year inflation index.</param>
    /// <param name="observationLag">The observation lag.</param>
    /// <param name="interpolation">How the index is observed.</param>
    /// <param name="spread">The spread over the year-on-year leg.</param>
    /// <param name="yoyDayCount">The year-on-year leg's day counter, or <see langword="null"/>, QuantLib's empty one,
    /// which the leg refuses.</param>
    /// <param name="paymentCalendar">The year-on-year leg's payment calendar, or <see langword="null"/> for QuantLib's
    /// empty calendar, used unchecked as QuantLib uses it.</param>
    /// <param name="paymentConvention">Both legs' payment convention.</param>
    /// <exception cref="ArgumentNullException">A schedule, <paramref name="fixedDayCount"/> or
    /// <paramref name="yoyIndex"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/>, <paramref name="interpolation"/> or
    /// <paramref name="paymentConvention"/> isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">A leg can't be built from its arguments.</exception>
    public YearOnYearInflationSwap(
        SwapType type,
        double nominal,
        Schedule fixedSchedule,
        double fixedRate,
        DayCounter fixedDayCount,
        Schedule yoySchedule,
        YoYInflationIndex yoyIndex,
        Period observationLag,
        CPIInterpolationType interpolation,
        double spread,
        DayCounter? yoyDayCount,
        Calendar? paymentCalendar,
        BusinessDayConvention paymentConvention = BusinessDayConvention.ModifiedFollowing)
        : base(
            BuildLegs(
                type,
                nominal,
                fixedSchedule,
                fixedRate,
                fixedDayCount,
                yoySchedule,
                yoyIndex,
                observationLag,
                interpolation,
                spread,
                yoyDayCount,
                paymentCalendar,
                paymentConvention),
            [type == SwapType.Payer, type != SwapType.Payer])
    {
        Type = type;
        Nominal = nominal;
        FixedSchedule = fixedSchedule;
        FixedRate = fixedRate;
        FixedDayCount = fixedDayCount;
        YoYSchedule = yoySchedule;
        YoYInflationIndex = yoyIndex;
        ObservationLag = observationLag;
        Spread = spread;
        YoYDayCount = yoyDayCount;
        PaymentCalendar = paymentCalendar;
        PaymentConvention = paymentConvention;
    }

    /// <summary>Gets whether the fixed leg is paid or received.</summary>
    public SwapType Type { get; }

    /// <summary>Gets the nominal of both legs.</summary>
    public double Nominal { get; }

    /// <summary>Gets the fixed leg's schedule.</summary>
    public Schedule FixedSchedule { get; }

    /// <summary>Gets the fixed rate.</summary>
    public double FixedRate { get; }

    /// <summary>Gets the fixed leg's day counter.</summary>
    public DayCounter FixedDayCount { get; }

    /// <summary>Gets the year-on-year leg's schedule.</summary>
    public Schedule YoYSchedule { get; }

    /// <summary>Gets the year-on-year inflation index.</summary>
    public YoYInflationIndex YoYInflationIndex { get; }

    /// <summary>Gets the observation lag.</summary>
    public Period ObservationLag { get; }

    /// <summary>Gets the spread over the year-on-year leg.</summary>
    public double Spread { get; }

    /// <summary>Gets the year-on-year leg's day counter.</summary>
    public DayCounter? YoYDayCount { get; }

    /// <summary>Gets the year-on-year leg's payment calendar, or <see langword="null"/> for QuantLib's empty
    /// calendar.</summary>
    public Calendar? PaymentCalendar { get; }

    /// <summary>Gets both legs' payment convention.</summary>
    public BusinessDayConvention PaymentConvention { get; }

    /// <summary>Gets the fixed leg.</summary>
    public ImmutableArray<CashFlow> FixedLeg => Legs[0];

    /// <summary>Gets the year-on-year leg.</summary>
    public ImmutableArray<CashFlow> YoYLeg => Legs[1];

    /// <inheritdoc/>
    /// <remarks>An expired swap has zero leg BPS and neither a fair rate nor a fair spread.</remarks>
    public override YearOnYearInflationSwapResults SetupExpired() =>
        new(base.SetupExpired()) { FairRate = null, FairSpread = null };

    /// <inheritdoc/>
    /// <remarks>
    /// The fair rate and spread are the engine's when it gives them; otherwise each is derived from the NPV and its
    /// leg's BPS, and is <see langword="null"/> when that BPS is zero or unknown.
    /// </remarks>
    public override YearOnYearInflationSwapResults FetchResults(InstrumentResults results)
    {
        SwapResults r = base.FetchResults(results);

        // might be a swap engine, so no error is thrown
        var fetched = r as YearOnYearInflationSwapResults;
        double? fairRate = fetched?.FairRate;
        double? fairSpread = fetched?.FairSpread;

        if (fairRate is null)
        {
            // calculate it from other results
            if (r.LegBPS[0] is double bps && bps != 0.0)
            {
                fairRate = FixedRate - (r.NPV / (bps / BasisPoint));
            }
        }

        if (fairSpread is null)
        {
            // ditto
            if (r.LegBPS[1] is double bps && bps != 0.0)
            {
                fairSpread = Spread - (r.NPV / (bps / BasisPoint));
            }
        }

        return fetched is not null
            ? fetched with { FairRate = fairRate, FairSpread = fairSpread }
            : new YearOnYearInflationSwapResults(r) { FairRate = fairRate, FairSpread = fairSpread };
    }

    // QuantLib builds the legs in its constructor's body; here they are built before the base constructor takes them,
    // in QuantLib's order, after the type is checked.
    private static ImmutableArray<CashFlow>[] BuildLegs(
        SwapType type,
        double nominal,
        Schedule fixedSchedule,
        double fixedRate,
        DayCounter fixedDayCount,
        Schedule yoySchedule,
        YoYInflationIndex yoyIndex,
        Period observationLag,
        CPIInterpolationType interpolation,
        double spread,
        DayCounter? yoyDayCount,
        Calendar? paymentCalendar,
        BusinessDayConvention paymentConvention)
    {
        EnumArguments.Check(type, nameof(type));

        // Simple compounding by default
        ImmutableArray<CashFlow> fixedLeg = new FixedRateLeg(fixedSchedule)
            .WithNotionals(nominal)
            .WithCouponRates(fixedRate, fixedDayCount)
            .WithPaymentAdjustment(paymentConvention)
            .ToLeg();

        ImmutableArray<CashFlow> yoyLeg = new YoYInflationLeg(
                yoySchedule, paymentCalendar, yoyIndex, observationLag, interpolation)
            .WithNotionals(nominal)
            .WithPaymentDayCounter(yoyDayCount)
            .WithPaymentAdjustment(paymentConvention)
            .WithSpreads(spread)
            .ToLeg();

        return [fixedLeg, yoyLeg];
    }
}
