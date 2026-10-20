// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/inflationcapfloor.hpp ql/instruments/inflationcapfloor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Instruments;

/// <summary>The base class of year-on-year inflation cap-like instruments: caps, floors and collars on a YoY
/// leg.</summary>
/// <remarks>
/// <para>
/// The standard YoY inflation cap or floor differs from a nominal one: a nominal cap has no first optionlet, since it
/// sets in advance, while a YoY one sets (effectively) in arrears, against a lag of a few months, so its first
/// optionlet counts; the parity test needs no special definition of the instrument. The relationship between the values
/// of caps, floors and collars is checked, and so are the put-call parity with swaps and a known good value.
/// </para>
/// <para>
/// Unsealed, as in QuantLib, where <see cref="YoYInflationCap"/>, <see cref="YoYInflationFloor"/> and
/// <see cref="YoYInflationCollar"/> derive from it and engines take it. It holds its leg and its rates; an engine
/// computes QuantLib's arguments from them with the context (<see cref="SetupArguments"/>) and returns the instrument's
/// results, QuantLib's plain <c>Instrument::results</c> with the engine's additional results. A cash flow of the leg
/// that isn't a year-on-year inflation coupon is refused when the instrument is priced, as QuantLib refuses it, not
/// when it is built. Equality is by reference.
/// </para>
/// </remarks>
public class YoYInflationCapFloor : Instrument
{
    /// <summary>Initializes a new instance of the <see cref="YoYInflationCapFloor"/> class with cap and floor
    /// rates.</summary>
    /// <param name="type">The type.</param>
    /// <param name="yoyLeg">The year-on-year leg.</param>
    /// <param name="capRates">The cap rates, extended to the leg's length with the last one; read for a cap or a
    /// collar, and kept as given for a floor.</param>
    /// <param name="floorRates">The floor rates, extended to the leg's length with the last one; read for a floor or a
    /// collar, and kept as given for a cap.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">The leg is <c>default</c> or holds a null cash flow, or the rates the type
    /// reads are empty.</exception>
    /// <exception cref="ArgumentNullException">A rate sequence is <see langword="null"/>.</exception>
    public YoYInflationCapFloor(
        YoYInflationCapFloorType type,
        ImmutableArray<CashFlow> yoyLeg,
        IEnumerable<double> capRates,
        IEnumerable<double> floorRates)
        : this(type, yoyLeg, capRates, floorRates, nameof(capRates), nameof(floorRates))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YoYInflationCapFloor"/> class with strikes: a cap or a floor.
    /// </summary>
    /// <param name="type">The type: <see cref="YoYInflationCapFloorType.Cap"/> or
    /// <see cref="YoYInflationCapFloorType.Floor"/>.</param>
    /// <param name="yoyLeg">The year-on-year leg.</param>
    /// <param name="strikes">The strikes, extended to the leg's length with the last one: the cap rates of a cap, the
    /// floor rates of a floor.</param>
    /// <exception cref="ArgumentException">The leg is <c>default</c> or holds a null cash flow, or there are no
    /// strikes.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="strikes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is
    /// <see cref="YoYInflationCapFloorType.Collar"/> or isn't a member of its type.</exception>
    public YoYInflationCapFloor(
        YoYInflationCapFloorType type, ImmutableArray<CashFlow> yoyLeg, IEnumerable<double> strikes)
    {
        YoYLeg = CheckLeg(yoyLeg, nameof(yoyLeg));
        ArgumentNullException.ThrowIfNull(strikes);
        ImmutableArray<double> given = [.. strikes];
        if (given.IsEmpty)
        {
            throw new ArgumentException("no strikes given", nameof(strikes));
        }

        if (type == YoYInflationCapFloorType.Cap)
        {
            CapRates = Extended(given, YoYLeg.Length);
            FloorRates = [];
        }
        else if (type == YoYInflationCapFloorType.Floor)
        {
            CapRates = [];
            FloorRates = Extended(given, YoYLeg.Length);
        }
        else
        {
            // A member QuantLib's constructor never handles, or a value outside the enum: refused as QuantLib refuses
            // it.
            throw new ArgumentOutOfRangeException(
                nameof(type), type, "only Cap/Floor types allowed in this constructor");
        }

        Type = type;
    }

    // The four-argument constructor, its refusals of the rates naming the caller's parameters for them (the cap's and
    // the floor's exerciseRates).
    private protected YoYInflationCapFloor(
        YoYInflationCapFloorType type,
        ImmutableArray<CashFlow> yoyLeg,
        IEnumerable<double> capRates,
        IEnumerable<double> floorRates,
        string capRatesName,
        string floorRatesName)
    {
        if (type is not (YoYInflationCapFloorType.Cap or YoYInflationCapFloorType.Floor
            or YoYInflationCapFloorType.Collar))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, $"unknown YoYInflationCapFloor::Type ({(int)type})");
        }

        YoYLeg = CheckLeg(yoyLeg, nameof(yoyLeg));
        ArgumentNullException.ThrowIfNull(capRates, capRatesName);
        ArgumentNullException.ThrowIfNull(floorRates, floorRatesName);
        Type = type;
        ImmutableArray<double> caps = [.. capRates], floors = [.. floorRates];
        if (type is YoYInflationCapFloorType.Cap or YoYInflationCapFloorType.Collar)
        {
            if (caps.IsEmpty)
            {
                throw new ArgumentException("no cap rates given", capRatesName);
            }

            caps = Extended(caps, YoYLeg.Length);
        }

        if (type is YoYInflationCapFloorType.Floor or YoYInflationCapFloorType.Collar)
        {
            if (floors.IsEmpty)
            {
                throw new ArgumentException("no floor rates given", floorRatesName);
            }

            floors = Extended(floors, YoYLeg.Length);
        }

        CapRates = caps;
        FloorRates = floors;
    }

    /// <summary>Gets the type.</summary>
    public YoYInflationCapFloorType Type { get; }

    /// <summary>Gets the cap rates, extended to the leg's length for a cap or a collar.</summary>
    public ImmutableArray<double> CapRates { get; }

    /// <summary>Gets the floor rates, extended to the leg's length for a floor or a collar.</summary>
    public ImmutableArray<double> FloorRates { get; }

    /// <summary>Gets the year-on-year leg.</summary>
    public ImmutableArray<CashFlow> YoYLeg { get; }

    /// <summary>Whether every cash flow of the leg has occurred, an empty leg among them.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns><see langword="true"/> when the instrument has expired.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        for (int i = YoYLeg.Length; i > 0; --i)
        {
            if (!YoYLeg[i - 1].HasOccurred(context))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The earliest accrual start date of the leg's coupons.</summary>
    /// <returns>The start date.</returns>
    /// <exception cref="ArgumentException">The leg is empty.</exception>
    public Date StartDate() => CashFlows.StartDate(YoYLeg);

    /// <summary>The latest accrual end date of the leg's coupons.</summary>
    /// <returns>The maturity date.</returns>
    /// <exception cref="ArgumentException">The leg is empty.</exception>
    public Date MaturityDate() => CashFlows.MaturityDate(YoYLeg);

    /// <summary>The leg's last cash flow as a year-on-year inflation coupon.</summary>
    /// <returns>The coupon, or <see langword="null"/> when the last cash flow isn't a year-on-year inflation
    /// coupon.</returns>
    /// <exception cref="NanoQLException">The leg is empty, where QuantLib reads past its end, which is undefined: a
    /// documented divergence.</exception>
    public YoYInflationCoupon? LastYoYInflationCoupon()
    {
        if (YoYLeg.IsEmpty)
        {
            throw new NanoQLException("empty leg");
        }

        return YoYLeg[^1] as YoYInflationCoupon;
    }

    /// <summary>
    /// The <paramref name="i"/>-th optionlet, a new <see cref="YoYInflationCapFloor"/> of the same type on that cash
    /// flow alone, with its cap and floor rates.
    /// </summary>
    /// <param name="i">The optionlet's index, from 0.</param>
    /// <returns>The optionlet.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is negative.</exception>
    /// <exception cref="ArgumentException">The leg has no cash flow at <paramref name="i"/>.</exception>
    public YoYInflationCapFloor Optionlet(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        if (!(i < YoYLeg.Length))
        {
            throw new ArgumentException(
                $"{DataFormattersFunctions.Ordinal(i + 1)} optionlet does not exist, only {YoYLeg.Length}", nameof(i));
        }

        ImmutableArray<CashFlow> cf = [YoYLeg[i]];

        var cap = new List<double>();
        var floor = new List<double>();
        if (Type is YoYInflationCapFloorType.Cap or YoYInflationCapFloorType.Collar)
        {
            cap.Add(CapRates[i]);
        }

        if (Type is YoYInflationCapFloorType.Floor or YoYInflationCapFloorType.Collar)
        {
            floor.Add(FloorRates[i]);
        }

        return new YoYInflationCapFloor(Type, cf, cap, floor);
    }

    /// <summary>
    /// The at-the-money rate on a discount curve: the leg's at-the-money rate (<see cref="CashFlows.AtmRate"/>) at the
    /// curve's reference date, without the cash flows paid on it.
    /// </summary>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="context">The evaluation context: the coupons' amounts read its fixings and forecasts.</param>
    /// <returns>The rate; 0 for an empty leg.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The leg's BPS is 0, or a coupon can't be priced.</exception>
    public virtual double AtmRate(YieldTermStructure discountCurve, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        const bool includeSettlementDateFlows = false;
        Date settlementDate = discountCurve.ReferenceDate;
        return CashFlows.AtmRate(YoYLeg, discountCurve, context, includeSettlementDateFlows, settlementDate);
    }

    /// <summary>The implied volatility: not implemented, as in QuantLib.</summary>
    /// <param name="price">The price.</param>
    /// <param name="yoyCurve">The year-on-year inflation curve.</param>
    /// <param name="guess">The solver's initial guess.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxEvaluations">The solver's maximum number of evaluations.</param>
    /// <param name="minVol">The lower end of the bracket.</param>
    /// <param name="maxVol">The upper end of the bracket.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NanoQLException">Always: "not implemented yet".</exception>
    public virtual double ImpliedVolatility(
        double price,
        YoYInflationTermStructure? yoyCurve,
        double guess,
        EvaluationContext context,
        double accuracy = 1.0e-4,
        int maxEvaluations = 100,
        double minVol = 1.0e-7,
        double maxVol = 4.0) =>
        throw new NanoQLException("not implemented yet");

    /// <summary>
    /// QuantLib's <c>setupArguments</c>: the type and, per coupon, what the engines read.
    /// </summary>
    /// <param name="context">The evaluation context, which the coupons' nominals take.</param>
    /// <returns>The arguments.</returns>
    /// <exception cref="NanoQLException">A cash flow isn't a year-on-year inflation coupon.</exception>
    internal YoYInflationCapFloorArguments SetupArguments(EvaluationContext context) =>
        YoYInflationCapFloorArguments.Of(Type, YoYLeg, CapRates, FloorRates, context);

    // QuantLib's last rate repeated up to the leg's length; more rates than coupons are kept.
    private static ImmutableArray<double> Extended(ImmutableArray<double> rates, int length)
    {
        if (rates.Length >= length)
        {
            return rates;
        }

        ImmutableArray<double>.Builder extended = rates.ToBuilder();
        while (extended.Count < length)
        {
            extended.Add(rates[^1]);
        }

        return extended.ToImmutable();
    }

    private static ImmutableArray<CashFlow> CheckLeg(ImmutableArray<CashFlow> leg, string paramName)
    {
        if (leg.IsDefault)
        {
            throw new ArgumentException("default(ImmutableArray) is not a leg; an empty leg is []", paramName);
        }

        // QuantLib's isExpired dereferences each cash flow, which is undefined for a null one.
        if (leg.Any(cf => cf is null))
        {
            throw new ArgumentException("null cash flow in leg", paramName);
        }

        return leg;
    }
}
