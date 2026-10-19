// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2006, 2014 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/capfloor.hpp ql/instruments/capfloor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Instruments;

/// <summary>The base class of cap-like instruments: caps, floors and collars on a floating leg.</summary>
/// <remarks>
/// <para>
/// The correctness of the returned value is tested by checking that the price of a cap (resp. floor) decreases (resp.
/// increases) with the strike rate; the relationship between the values of caps, floors and the resulting collars is
/// checked, and so is the put-call parity between the values of caps, floors and swaps; the returned implied
/// volatility is tested by reproducing the target value with it, and the returned value against a known good value.
/// </para>
/// <para>
/// Unsealed, as in QuantLib, where <see cref="Cap"/>, <see cref="Floor"/> and <see cref="Collar"/> derive from it and
/// engines take it. It holds its leg and its rates; an engine computes QuantLib's arguments from them with the
/// context (<see cref="SetupArguments"/>) and returns the instrument's results, QuantLib's plain
/// <c>Instrument::results</c> with the engine's additional results. A cash flow of the leg that isn't a floating-rate
/// coupon is refused when the instrument is priced, as QuantLib refuses it, not when it is built. Equality is by
/// reference.
/// </para>
/// </remarks>
public class CapFloor : Instrument
{
    /// <summary>Initializes a new instance of the <see cref="CapFloor"/> class with cap and floor rates.</summary>
    /// <param name="type">The type.</param>
    /// <param name="floatingLeg">The floating leg.</param>
    /// <param name="capRates">The cap rates, extended to the leg's length with the last one; read for a cap or a
    /// collar, and kept as given for a floor.</param>
    /// <param name="floorRates">The floor rates, extended to the leg's length with the last one; read for a floor or a
    /// collar, and kept as given for a cap.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">
    /// The leg is <c>default</c> or holds a null cash flow, or the rates the type reads are empty.
    /// </exception>
    /// <exception cref="ArgumentNullException">A rate sequence is <see langword="null"/>.</exception>
    public CapFloor(
        CapFloorType type,
        ImmutableArray<CashFlow> floatingLeg,
        IEnumerable<double> capRates,
        IEnumerable<double> floorRates)
        : this(type, floatingLeg, capRates, floorRates, nameof(capRates), nameof(floorRates))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CapFloor"/> class with strikes: a cap or a floor.</summary>
    /// <param name="type">The type: <see cref="CapFloorType.Cap"/> or <see cref="CapFloorType.Floor"/>.</param>
    /// <param name="floatingLeg">The floating leg.</param>
    /// <param name="strikes">The strikes, extended to the leg's length with the last one: the cap rates of a cap, the
    /// floor rates of a floor.</param>
    /// <exception cref="ArgumentException">
    /// The leg is <c>default</c> or holds a null cash flow, or there are no strikes.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="strikes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is <see cref="CapFloorType.Collar"/> or
    /// isn't a member of its type.</exception>
    public CapFloor(CapFloorType type, ImmutableArray<CashFlow> floatingLeg, IEnumerable<double> strikes)
    {
        FloatingLeg = CheckLeg(floatingLeg, nameof(floatingLeg));
        ArgumentNullException.ThrowIfNull(strikes);
        ImmutableArray<double> given = [.. strikes];
        if (given.IsEmpty)
        {
            throw new ArgumentException("no strikes given", nameof(strikes));
        }

        if (type == CapFloorType.Cap)
        {
            CapRates = Extended(given, FloatingLeg.Length);
            FloorRates = [];
        }
        else if (type == CapFloorType.Floor)
        {
            CapRates = [];
            FloorRates = Extended(given, FloatingLeg.Length);
        }
        else
        {
            // A member QuantLib's constructor never handles, or a value outside the enum: refused as QuantLib refuses it.
            throw new ArgumentOutOfRangeException(nameof(type), type, "only Cap/Floor types allowed in this constructor");
        }

        Type = type;
    }

    // The four-argument constructor, its refusals of the rates naming the caller's parameters for them (Cap's and
    // Floor's exerciseRates).
    private protected CapFloor(
        CapFloorType type,
        ImmutableArray<CashFlow> floatingLeg,
        IEnumerable<double> capRates,
        IEnumerable<double> floorRates,
        string capRatesName,
        string floorRatesName)
    {
        CheckType(type, nameof(type));
        FloatingLeg = CheckLeg(floatingLeg, nameof(floatingLeg));
        ArgumentNullException.ThrowIfNull(capRates, capRatesName);
        ArgumentNullException.ThrowIfNull(floorRates, floorRatesName);
        Type = type;
        ImmutableArray<double> caps = [.. capRates], floors = [.. floorRates];
        if (type is CapFloorType.Cap or CapFloorType.Collar)
        {
            if (caps.IsEmpty)
            {
                throw new ArgumentException("no cap rates given", capRatesName);
            }

            caps = Extended(caps, FloatingLeg.Length);
        }

        if (type is CapFloorType.Floor or CapFloorType.Collar)
        {
            if (floors.IsEmpty)
            {
                throw new ArgumentException("no floor rates given", floorRatesName);
            }

            floors = Extended(floors, FloatingLeg.Length);
        }

        CapRates = caps;
        FloorRates = floors;
    }

    /// <summary>Gets the type.</summary>
    public CapFloorType Type { get; }

    /// <summary>Gets the cap rates, extended to the leg's length for a cap or a collar.</summary>
    public ImmutableArray<double> CapRates { get; }

    /// <summary>Gets the floor rates, extended to the leg's length for a floor or a collar.</summary>
    public ImmutableArray<double> FloorRates { get; }

    /// <summary>Gets the floating leg.</summary>
    public ImmutableArray<CashFlow> FloatingLeg { get; }

    /// <summary>Whether every cash flow of the leg has occurred, an empty leg among them.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns><see langword="true"/> when the instrument has expired.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        for (int i = FloatingLeg.Length; i > 0; --i)
        {
            if (!FloatingLeg[i - 1].HasOccurred(context))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The earliest accrual start date of the leg's coupons.</summary>
    /// <returns>The start date.</returns>
    /// <exception cref="ArgumentException">The leg is empty.</exception>
    public Date StartDate() => CashFlows.StartDate(FloatingLeg);

    /// <summary>The latest accrual end date of the leg's coupons.</summary>
    /// <returns>The maturity date.</returns>
    /// <exception cref="ArgumentException">The leg is empty.</exception>
    public Date MaturityDate() => CashFlows.MaturityDate(FloatingLeg);

    /// <summary>The leg's last cash flow as a floating-rate coupon.</summary>
    /// <returns>The coupon, or <see langword="null"/> when the last cash flow isn't a floating-rate coupon.</returns>
    /// <exception cref="NanoQLException">The leg is empty, where QuantLib reads past its end.</exception>
    public FloatingRateCoupon? LastFloatingRateCoupon()
    {
        if (FloatingLeg.IsEmpty)
        {
            throw new NanoQLException("empty leg");
        }

        return FloatingLeg[^1] as FloatingRateCoupon;
    }

    /// <summary>
    /// The <paramref name="i"/>-th optionlet, a new <see cref="CapFloor"/> of the same type on that cash flow alone, with
    /// its cap and floor rates.
    /// </summary>
    /// <param name="i">The optionlet's index, from 0.</param>
    /// <returns>The optionlet.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is negative.</exception>
    /// <exception cref="ArgumentException">The leg has no cash flow at <paramref name="i"/>.</exception>
    public CapFloor Optionlet(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        if (!(i < FloatingLeg.Length))
        {
            throw new ArgumentException(
                $"{DataFormattersFunctions.Ordinal(i + 1)} optionlet does not exist, only {FloatingLeg.Length}",
                nameof(i));
        }

        ImmutableArray<CashFlow> cf = [FloatingLeg[i]];

        var cap = new List<double>();
        var floor = new List<double>();
        if (Type is CapFloorType.Cap or CapFloorType.Collar)
        {
            cap.Add(CapRates[i]);
        }

        if (Type is CapFloorType.Floor or CapFloorType.Collar)
        {
            floor.Add(FloorRates[i]);
        }

        return new CapFloor(Type, cf, cap, floor);
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
    public double AtmRate(YieldTermStructure discountCurve, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        const bool includeSettlementDateFlows = false;
        Date settlementDate = discountCurve.ReferenceDate;
        return CashFlows.AtmRate(FloatingLeg, discountCurve, context, includeSettlementDateFlows, settlementDate);
    }

    /// <summary>
    /// The implied term volatility: the volatility at which an engine on a constant volatility prices the instrument
    /// at the given price, a Black engine for a shifted lognormal volatility and a Bachelier engine for a normal one,
    /// each with an Actual/365 (Fixed) volatility, solved by <see cref="NewtonSafe"/>.
    /// </summary>
    /// <param name="price">The price.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="guess">The solver's initial guess.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxEvaluations">The solver's maximum number of evaluations.</param>
    /// <param name="minVol">The lower end of the bracket.</param>
    /// <param name="maxVol">The upper end of the bracket.</param>
    /// <param name="type">The volatility type.</param>
    /// <param name="displacement">The shift of a shifted lognormal volatility; a normal one has none.</param>
    /// <returns>The implied volatility.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="discountCurve"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is negative, or
    /// <paramref name="type"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">
    /// The accuracy isn't positive, the bracket is empty, or the guess is outside it.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The instrument has expired, a cash flow isn't a floating-rate coupon, the price can't be bracketed, the solver
    /// exceeds its evaluations, or the engine fails.
    /// </exception>
    /// <remarks>
    /// As QuantLib's helper does, the arguments are computed once and the engine's body runs alone at each evaluation,
    /// without the instrument's checks. Each evaluation prices afresh, where QuantLib's helper reuses its last result
    /// while the volatility is unchanged; the volatility's reference date is the context's evaluation date.
    /// </remarks>
    public double ImpliedVolatility(
        double price,
        YieldTermStructure discountCurve,
        double guess,
        EvaluationContext context,
        double accuracy = 1.0e-4,
        int maxEvaluations = 100,
        double minVol = 1.0e-7,
        double maxVol = 4.0,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double displacement = 0.0)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        ArgumentOutOfRangeException.ThrowIfNegative(maxEvaluations);
        if (type is not (VolatilityType.ShiftedLognormal or VolatilityType.Normal))
        {
            // QuantLib's text nests the volatility type's own stream text.
            throw new ArgumentOutOfRangeException(
                nameof(type), type, $"unknown VolatilityType (Unknown volatility type ({(int)type}))");
        }

        if (IsExpired(context))
        {
            throw new NanoQLException("instrument expired");
        }

        var f = new ImpliedCapVolHelper(SetupArguments(context), discountCurve, price, displacement, type, context);
        NewtonSafe solver = new NewtonSafe().SetMaxEvaluations(maxEvaluations);
        try
        {
            return solver.Solve(f, accuracy, guess, minVol, maxVol);
        }
        catch (ArgumentException e) when (e.ParamName == "xMin")
        {
            // The solver's empty range is this member's own argument: it names its own parameter.
            throw new ArgumentException(DiscountingSwapEngine.QuantLibText(e), nameof(minVol), e);
        }
    }

    /// <summary>
    /// QuantLib's <c>setupArguments</c>: the type and, per coupon, what the engines read, the forwards only for the
    /// coupons paid on or after the evaluation date.
    /// </summary>
    /// <param name="context">The evaluation context: its evaluation date and the coupons' fixings and forecasts.</param>
    /// <returns>The arguments.</returns>
    /// <exception cref="NanoQLException">A cash flow isn't a floating-rate coupon, or a forward can't be computed.</exception>
    internal CapFloorArguments SetupArguments(EvaluationContext context) =>
        CapFloorArguments.Of(Type, FloatingLeg, CapRates, FloorRates, context);

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

    private static void CheckType(CapFloorType type, string paramName)
    {
        if (type is not (CapFloorType.Cap or CapFloorType.Floor or CapFloorType.Collar))
        {
            throw new ArgumentOutOfRangeException(paramName, type, $"unknown CapFloor::Type ({(int)type})");
        }
    }
}

/// <summary>
/// The function whose zero is the implied volatility: the engine's price at a volatility less the target, and its
/// vega. QuantLib's <c>ImpliedCapVolHelper</c>, on arguments computed once, which prices at each call here.
/// </summary>
file readonly struct ImpliedCapVolHelper : IDerivativeSolver1DFunction
{
    private readonly CapFloorArguments arguments;
    private readonly YieldTermStructure discountCurve;
    private readonly double targetValue;
    private readonly double displacement;
    private readonly VolatilityType type;
    private readonly EvaluationContext context;

    public ImpliedCapVolHelper(
        CapFloorArguments arguments,
        YieldTermStructure discountCurve,
        double targetValue,
        double displacement,
        VolatilityType type,
        EvaluationContext context)
    {
        this.arguments = arguments;
        this.discountCurve = discountCurve;
        this.targetValue = targetValue;
        this.displacement = displacement;
        this.type = type;
        this.context = context;
    }

    public double Evaluate(double x) => Price(x).NPV!.Value - targetValue;

    public double Derivative(double x) => Price(x).Result<double>("vega");

    // The engine's body on the constant volatility x: QuantLib's BlackCapFloorEngine(discountCurve, h,
    // Actual365Fixed(), displacement), or BachelierCapFloorEngine(discountCurve, h, Actual365Fixed()), whose
    // displacement the helper ignores.
    private InstrumentResults Price(double x)
    {
        OptionletVolatilityStructure vol = ConstantOptionletVolatility.Create(
            0, new NullCalendar(), BusinessDayConvention.Following, x, new Actual365Fixed(), context);
        return type == VolatilityType.ShiftedLognormal
            ? BlackCapFloorEngine.Body(arguments, discountCurve, vol, displacement)
            : BachelierCapFloorEngine.Body(arguments, discountCurve, vol);
    }
}
