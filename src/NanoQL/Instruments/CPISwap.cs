// SPDX-FileCopyrightText: Copyright (C) 2007, 2009, 2011 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/cpiswap.hpp ql/instruments/cpiswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A zero-inflation-indexed swap: a CPI leg, fixed rates inflated by a zero-inflation index with an inflated notional
/// at the end, against a floating leg paying an Ibor rate plus a spread, with a notional flow where it is needed.
/// </summary>
/// <remarks>
/// <para>
/// The type, payer or receiver, refers to the floating leg. The CPI leg is the first leg, <see cref="CPILeg"/>'s; the
/// floating leg is the second, Ibor coupons on the floating schedule when it has more than one date, then a notional
/// flow when the schedule has a single date, when the inflation nominal isn't subtracted, or when it differs from the
/// nominal: the nominal less the inflation nominal where it is subtracted, else the nominal, paid with the last coupon
/// or on the single date. Accrual adjustments and calendars are in the schedules.
/// </para>
/// <para>
/// The constructor builds the Ibor leg with the index's default pricer, which reads the context's at-par switch, so it
/// takes the context; a null floating index is accepted where the schedule has a single date, since QuantLib never
/// reads it then. A type that isn't a member of <see cref="SwapType"/> is refused, where QuantLib takes it as a
/// receiver. An engine's results are a <see cref="CPISwapResults"/>: its fair rate is the swap engine's (see its
/// remarks), and its fair spread is derived from the floating leg's BPS. QuantLib's own arguments, results and engine
/// classes aren't ported.
/// </para>
/// </remarks>
public sealed class CPISwap : Swap
{
    private const double BasisPoint = 1.0e-4;

    // The CPI leg's builder, kept for the engine's unit-rate coupons.
    private readonly CPILeg cpiLegBuilder;

    /// <summary>Initializes a new instance of the <see cref="CPISwap"/> class.</summary>
    /// <param name="type">Whether the floating leg is paid or received.</param>
    /// <param name="nominal">The floating leg's nominal.</param>
    /// <param name="subtractInflationNominal">Whether the CPI leg's notional flow pays only the inflated growth, and
    /// the floating leg's notional flow the difference of the two nominals.</param>
    /// <param name="spread">The spread over the floating leg.</param>
    /// <param name="floatDayCount">The floating leg's day counter; the index's when <see langword="null"/>, QuantLib's
    /// empty day counter.</param>
    /// <param name="floatSchedule">The floating leg's schedule.</param>
    /// <param name="floatPaymentRoll">The floating leg's payment convention.</param>
    /// <param name="fixingDays">The floating coupons' fixing days.</param>
    /// <param name="floatIndex">The floating leg's index, or <see langword="null"/> where the floating schedule has a
    /// single date.</param>
    /// <param name="fixedRate">The CPI leg's fixed rate.</param>
    /// <param name="baseCPI">The CPI leg's base CPI, or <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>,
    /// for one observed at the start of the fixed schedule.</param>
    /// <param name="fixedDayCount">The CPI leg's day counter, or <see langword="null"/>, QuantLib's empty one, which a
    /// CPI leg with no coupon never reads.</param>
    /// <param name="fixedSchedule">The CPI leg's schedule.</param>
    /// <param name="fixedPaymentRoll">The CPI leg's payment convention.</param>
    /// <param name="observationLag">The observation lag.</param>
    /// <param name="fixedIndex">The zero-inflation index.</param>
    /// <param name="context">The evaluation context, whose at-par switch the floating leg reads.</param>
    /// <param name="observationInterpolation">How the index is observed.</param>
    /// <param name="inflationNominal">The CPI leg's nominal, or <see langword="null"/>, QuantLib's
    /// <c>Null&lt;Real&gt;()</c>, for the nominal.</param>
    /// <exception cref="ArgumentNullException">A schedule, <paramref name="fixedIndex"/> or <paramref name="context"/>
    /// is null; or <paramref name="floatIndex"/> is null where the floating schedule has more than one
    /// date.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/>, a convention or
    /// <paramref name="observationInterpolation"/> isn't a member of its type, or <paramref name="fixingDays"/> is
    /// negative.</exception>
    /// <exception cref="ArgumentException">A schedule is empty.</exception>
    /// <exception cref="NanoQLException">A leg can't be built from its arguments.</exception>
    public CPISwap(
        SwapType type,
        double nominal,
        bool subtractInflationNominal,
        double spread,
        DayCounter? floatDayCount,
        Schedule floatSchedule,
        BusinessDayConvention floatPaymentRoll,
        int fixingDays,
        IborIndex? floatIndex,
        double fixedRate,
        double? baseCPI,
        DayCounter? fixedDayCount,
        Schedule fixedSchedule,
        BusinessDayConvention fixedPaymentRoll,
        Period observationLag,
        ZeroInflationIndex fixedIndex,
        EvaluationContext context,
        CPIInterpolationType observationInterpolation = CPIInterpolationType.Flat,
        double? inflationNominal = null)
        : this(
            type,
            nominal,
            subtractInflationNominal,
            spread,
            floatDayCount,
            floatSchedule,
            floatPaymentRoll,
            fixingDays,
            floatIndex,
            fixedRate,
            baseCPI,
            fixedDayCount,
            fixedSchedule,
            fixedPaymentRoll,
            observationLag,
            fixedIndex,
            observationInterpolation,
            inflationNominal ?? nominal,
            BuildLegs(
                type,
                nominal,
                subtractInflationNominal,
                spread,
                floatDayCount,
                floatSchedule,
                floatPaymentRoll,
                fixingDays,
                floatIndex,
                fixedRate,
                baseCPI,
                fixedDayCount,
                fixedSchedule,
                fixedPaymentRoll,
                observationLag,
                fixedIndex,
                context,
                observationInterpolation,
                inflationNominal ?? nominal))
    {
    }

    private CPISwap(
        SwapType type,
        double nominal,
        bool subtractInflationNominal,
        double spread,
        DayCounter? floatDayCount,
        Schedule floatSchedule,
        BusinessDayConvention floatPaymentRoll,
        int fixingDays,
        IborIndex? floatIndex,
        double fixedRate,
        double? baseCPI,
        DayCounter? fixedDayCount,
        Schedule fixedSchedule,
        BusinessDayConvention fixedPaymentRoll,
        Period observationLag,
        ZeroInflationIndex fixedIndex,
        CPIInterpolationType observationInterpolation,
        double inflationNominal,
        (ImmutableArray<CashFlow>[] Legs, CPILeg CpiLeg) built)
        : base(built.Legs, [type != SwapType.Payer, type == SwapType.Payer])
    {
        Type = type;
        Nominal = nominal;
        SubtractInflationNominal = subtractInflationNominal;
        Spread = spread;
        FloatDayCount = floatDayCount;
        FloatSchedule = floatSchedule;
        FloatPaymentRoll = floatPaymentRoll;
        FixingDays = fixingDays;
        FloatIndex = floatIndex;
        FixedRate = fixedRate;
        BaseCPI = baseCPI;
        FixedDayCount = fixedDayCount;
        FixedSchedule = fixedSchedule;
        FixedPaymentRoll = fixedPaymentRoll;
        ObservationLag = observationLag;
        FixedIndex = fixedIndex;
        ObservationInterpolation = observationInterpolation;
        InflationNominal = inflationNominal;
        cpiLegBuilder = built.CpiLeg;
    }

    /// <summary>Gets whether the floating leg is paid or received.</summary>
    public SwapType Type { get; }

    /// <summary>Gets the floating leg's nominal.</summary>
    public double Nominal { get; }

    /// <summary>Gets a value indicating whether the inflation nominal is subtracted.</summary>
    public bool SubtractInflationNominal { get; }

    /// <summary>Gets the spread over the floating leg.</summary>
    public double Spread { get; }

    /// <summary>Gets the floating leg's day counter, or <see langword="null"/> for the index's.</summary>
    public DayCounter? FloatDayCount { get; }

    /// <summary>Gets the floating leg's schedule.</summary>
    public Schedule FloatSchedule { get; }

    /// <summary>Gets the floating leg's payment convention.</summary>
    public BusinessDayConvention FloatPaymentRoll { get; }

    /// <summary>Gets the floating coupons' fixing days.</summary>
    public int FixingDays { get; }

    /// <summary>Gets the floating leg's index, or <see langword="null"/>.</summary>
    public IborIndex? FloatIndex { get; }

    /// <summary>Gets the CPI leg's fixed rate.</summary>
    public double FixedRate { get; }

    /// <summary>Gets the CPI leg's base CPI, or <see langword="null"/> for one observed.</summary>
    public double? BaseCPI { get; }

    /// <summary>Gets the CPI leg's day counter, or <see langword="null"/> for none.</summary>
    public DayCounter? FixedDayCount { get; }

    /// <summary>Gets the CPI leg's schedule.</summary>
    public Schedule FixedSchedule { get; }

    /// <summary>Gets the CPI leg's payment convention.</summary>
    public BusinessDayConvention FixedPaymentRoll { get; }

    /// <summary>Gets the observation lag.</summary>
    public Period ObservationLag { get; }

    /// <summary>Gets the zero-inflation index.</summary>
    public ZeroInflationIndex FixedIndex { get; }

    /// <summary>Gets how the index is observed.</summary>
    public CPIInterpolationType ObservationInterpolation { get; }

    /// <summary>Gets the CPI leg's nominal: the given one, or the nominal.</summary>
    public double InflationNominal { get; }

    /// <summary>Gets the CPI leg.</summary>
    public ImmutableArray<CashFlow> CpiLeg => Legs[0];

    /// <summary>Gets the floating leg.</summary>
    public ImmutableArray<CashFlow> FloatLeg => Legs[1];

    /// <inheritdoc/>
    /// <remarks>An expired swap has zero leg BPS and neither a fair rate nor a fair spread.</remarks>
    public override CPISwapResults SetupExpired() => new(base.SetupExpired()) { FairRate = null, FairSpread = null };

    /// <inheritdoc/>
    /// <remarks>
    /// The fair rate is the engine's, and <see langword="null"/> when the engine gives none: QuantLib's derivation from
    /// the CPI leg's BPS isn't ported. The fair spread is the engine's when it gives one; otherwise it is derived from
    /// the NPV and the floating leg's BPS, and is <see langword="null"/> when that BPS is zero or unknown.
    /// </remarks>
    public override CPISwapResults FetchResults(InstrumentResults results)
    {
        SwapResults r = base.FetchResults(results);

        // might be a swap engine, so no error is thrown
        var fetched = r as CPISwapResults;
        double? fairRate = fetched?.FairRate;
        double? fairSpread = fetched?.FairSpread;

        if (fairSpread is null)
        {
            // calculate it from other results
            if (r.LegBPS[1] is double bps && bps != 0.0)
            {
                fairSpread = Spread - (r.NPV / (bps / BasisPoint));
            }
        }

        return fetched is not null
            ? fetched with { FairRate = fairRate, FairSpread = fairSpread }
            : new CPISwapResults(r) { FairRate = fairRate, FairSpread = fairSpread };
    }

    /// <summary>
    /// The CPI leg's coupons rebuilt at a unit fixed rate, without the notional flow, each with the pricer its coupon
    /// in the swap holds: the leg's sensitivity to its rate, which the swap engine's fair rate reads. A zero-rate
    /// period, a fixed-rate coupon in the swap, is a CPI coupon here.
    /// </summary>
    /// <returns>The coupons.</returns>
    internal ImmutableArray<CashFlow> UnitRateCpiCoupons()
    {
        ImmutableArray<CashFlow> unit = cpiLegBuilder.WithFixedRates(1.0).ToLeg();
        ImmutableArray<CashFlow> leg = Legs[0];
        var coupons = ImmutableArray.CreateBuilder<CashFlow>(unit.Length - 1);
        for (int i = 0; i < unit.Length - 1; ++i)
        {
            CashFlow cf = unit[i];
            if (cf is CPICoupon c && i < leg.Length && leg[i] is CPICoupon held && held.Pricer is { } pricer)
            {
                cf = c.SetPricer(pricer);
            }

            coupons.Add(cf);
        }

        return coupons.MoveToImmutable();
    }

    // QuantLib builds the legs in its constructor's body; here they are built before the base constructor takes them,
    // in QuantLib's order, after the context and the type are checked.
    private static (ImmutableArray<CashFlow>[] Legs, CPILeg CpiLeg) BuildLegs(
        SwapType type,
        double nominal,
        bool subtractInflationNominal,
        double spread,
        DayCounter? floatDayCount,
        Schedule floatSchedule,
        BusinessDayConvention floatPaymentRoll,
        int fixingDays,
        IborIndex? floatIndex,
        double fixedRate,
        double? baseCPI,
        DayCounter? fixedDayCount,
        Schedule fixedSchedule,
        BusinessDayConvention fixedPaymentRoll,
        Period observationLag,
        ZeroInflationIndex fixedIndex,
        EvaluationContext context,
        CPIInterpolationType observationInterpolation,
        double inflationNominal)
    {
        ArgumentNullException.ThrowIfNull(context);
        EnumArguments.Check(type, nameof(type));
        ArgumentOutOfRangeException.ThrowIfNegative(fixingDays);
        ArgumentNullException.ThrowIfNull(floatSchedule);
        ArgumentNullException.ThrowIfNull(fixedSchedule);
        if (floatSchedule.Empty)
        {
            throw new ArgumentException("empty float schedule", nameof(floatSchedule));
        }

        if (fixedSchedule.Empty)
        {
            throw new ArgumentException("empty fixed schedule", nameof(fixedSchedule));
        }

        // \todo if roll!=unadjusted then need calendars ...
        EnumArguments.Check(floatPaymentRoll, nameof(floatPaymentRoll));
        ImmutableArray<CashFlow> floatingLeg = [];
        if (floatSchedule.Size > 1)
        {
            if (floatIndex is null)
            {
                throw new ArgumentNullException(nameof(floatIndex), "no index provided");
            }

            floatingLeg = new IborLeg(floatSchedule, floatIndex)
                .WithNotionals(nominal)
                .WithSpreads(spread)
                .WithPaymentDayCounter(floatDayCount)
                .WithPaymentAdjustment(floatPaymentRoll)
                .WithFixingDays(fixingDays)
                .ToLeg(context);
        }

        if (floatSchedule.Size == 1
            || !subtractInflationNominal
            || (subtractInflationNominal && Math.Abs(nominal - inflationNominal) > 0.00001))
        {
            Date payNotional;
            if (floatSchedule.Size == 1)
            {
                // no coupons; QuantLib's empty calendar, null here, is used unchecked, as QuantLib uses it
                payNotional = floatSchedule.Date(0);
                payNotional = (floatSchedule.Calendar ?? EmptyCalendar.Instance).Adjust(payNotional, floatPaymentRoll);
            }
            else
            {
                // use the pay date of the last coupon
                payNotional = floatingLeg[^1].Date;
            }

            double floatAmount = subtractInflationNominal ? nominal - inflationNominal : nominal;
            floatingLeg = floatingLeg.Add(new SimpleCashFlow(floatAmount, payNotional));
        }

        // a CPIleg know about zero legs and inclusion of base inflation notional
        if (fixedIndex is null)
        {
            throw new ArgumentNullException(nameof(fixedIndex), "no index provided");
        }

        CPILeg cpiLegBuilder = new CPILeg(fixedSchedule, fixedIndex, baseCPI, observationLag)
            .WithNotionals(inflationNominal)
            .WithFixedRates(fixedRate)
            .WithPaymentDayCounter(fixedDayCount)
            .WithPaymentAdjustment(fixedPaymentRoll)
            .WithObservationInterpolation(observationInterpolation)
            .WithSubtractInflationNominal(subtractInflationNominal);
        ImmutableArray<CashFlow> cpiLeg = cpiLegBuilder.ToLeg();

        return ([cpiLeg, floatingLeg], cpiLegBuilder);
    }
}
