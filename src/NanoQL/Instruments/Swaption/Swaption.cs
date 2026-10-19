// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2014 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2016, 2018 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/swaption.hpp ql/instruments/swaption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A swaption: an option to enter a fixed-versus-floating swap.</summary>
/// <remarks>
/// <para>
/// The underlying may be a <see cref="VanillaSwap"/> or an <see cref="OvernightIndexedSwap"/>; most engines would treat
/// an overnight-indexed swap as a vanilla one, which is at best a decent proxy, and <see cref="BlackSwaptionEngine"/>
/// fully supports it. A swaption has no payoff, and its results are an engine's <see cref="InstrumentResults"/>.
/// </para>
/// <para>
/// The settlement type and method are checked against each other when the swaption is priced, as QuantLib's arguments
/// check them, not when it is built.
/// </para>
/// </remarks>
public sealed class Swaption : Option
{
    /// <summary>Initializes a new instance of the <see cref="Swaption"/> class.</summary>
    /// <param name="swap">The underlying swap.</param>
    /// <param name="exercise">The exercise.</param>
    /// <param name="delivery">The settlement type.</param>
    /// <param name="settlementMethod">The settlement method.</param>
    /// <exception cref="ArgumentNullException"><paramref name="swap"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>, which QuantLib dereferences before its arguments refuse it.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delivery"/> or
    /// <paramref name="settlementMethod"/> isn't a member of its type.</exception>
    public Swaption(
        FixedVsFloatingSwap swap,
        Exercise exercise,
        SettlementType delivery = SettlementType.Physical,
        SettlementMethod settlementMethod = SettlementMethod.PhysicalOTC)
        : base(null, ExerciseOf(swap, exercise))
    {
        EnumArguments.Check(delivery, nameof(delivery));
        EnumArguments.Check(settlementMethod, nameof(settlementMethod));
        Underlying = swap;
        SettlementType = delivery;
        SettlementMethod = settlementMethod;
    }

    /// <summary>Gets the settlement type.</summary>
    public SettlementType SettlementType { get; }

    /// <summary>Gets the settlement method.</summary>
    public SettlementMethod SettlementMethod { get; }

    /// <summary>Gets the underlying swap's type: payer or receiver.</summary>
    public SwapType Type => Underlying.Type;

    /// <summary>Gets the underlying swap.</summary>
    public FixedVsFloatingSwap Underlying { get; }

    /// <summary>Whether the swaption's last exercise date has occurred.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns><see langword="true"/> when the swaption has expired.</returns>
    /// <exception cref="NanoQLException">The exercise has no dates, where QuantLib reads past them.</exception>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimpleEvent(Exercise.LastDate()).HasOccurred(context);
    }

    /// <summary>
    /// Checks the swaption's terms before an engine prices it: the underlying swap's, then its settlement type and
    /// method against each other.
    /// </summary>
    /// <exception cref="NanoQLException">The swap's terms are inconsistent.</exception>
    /// <exception cref="ArgumentException">The settlement method doesn't go with the settlement type
    /// (<see cref="Settlement.CheckTypeAndMethodConsistency"/>).</exception>
    public override void Validate()
    {
        // QuantLib's "swap not set" and "exercise not set" can't fail: both are refused at construction when null.
        Underlying.Validate();
        Settlement.CheckTypeAndMethodConsistency(SettlementType, SettlementMethod);
    }

    /// <summary>
    /// The volatility at which an engine on a constant volatility prices the swaption at the given price: a Black
    /// engine for a shifted lognormal volatility, a Bachelier engine for a normal one, both with the
    /// <see cref="CashAnnuityModel.DiscountCurve"/> model and an Actual/365 (Fixed) volatility, solved by
    /// <see cref="NewtonSafe"/>.
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
    /// <param name="priceType">Whether <paramref name="price"/> is a spot price or a forward one, to the first exercise
    /// date.</param>
    /// <returns>The implied volatility.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="discountCurve"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is negative, or
    /// <paramref name="type"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">
    /// The accuracy isn't positive, the bracket is empty, or the guess is outside it.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The swaption has expired or isn't European, the price can't be bracketed, the solver exceeds its evaluations, or
    /// the engine fails.
    /// </exception>
    /// <remarks>
    /// QuantLib's helper runs the engine's body alone, without the swaption's checks: a physical swaption with a cash
    /// method prices as a physical one, and a cash swaption with a physical method fails in the engine. Each evaluation
    /// prices afresh, where QuantLib's helper reuses its last result while the volatility is unchanged.
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
        double displacement = 0.0,
        SwaptionPriceType priceType = SwaptionPriceType.Spot)
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

        if (!(Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not a European option");
        }

        double targetValue = price;
        if (priceType == SwaptionPriceType.Forward)
        {
            // convert to spot
            targetValue *= discountCurve.Discount(Exercise.Date(0));
        }

        var f = new ImpliedSwaptionVolHelper(this, discountCurve, targetValue, displacement, type, context);
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

    // The swap is refused before the exercise, in the parameters' order; the base refuses a null exercise.
    private static Exercise ExerciseOf(FixedVsFloatingSwap swap, Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(swap);
        return exercise;
    }
}

/// <summary>
/// The function whose zero is the implied volatility: the engine's price at a volatility less the target, and its
/// vega. QuantLib's <c>ImpliedSwaptionVolHelper</c>, which prices at each call here.
/// </summary>
file readonly struct ImpliedSwaptionVolHelper : IDerivativeSolver1DFunction
{
    private readonly Swaption swaption;
    private readonly YieldTermStructure discountCurve;
    private readonly double targetValue;
    private readonly double displacement;
    private readonly VolatilityType type;
    private readonly EvaluationContext context;

    public ImpliedSwaptionVolHelper(
        Swaption swaption,
        YieldTermStructure discountCurve,
        double targetValue,
        double displacement,
        VolatilityType type,
        EvaluationContext context)
    {
        this.swaption = swaption;
        this.discountCurve = discountCurve;
        this.targetValue = targetValue;
        this.displacement = displacement;
        this.type = type;
        this.context = context;
    }

    public double Evaluate(double x) => Price(x).NPV!.Value - targetValue;

    public double Derivative(double x) => Price(x).Result<double>("vega");

    // The engine's body on the constant volatility x: QuantLib's BlackSwaptionEngine(discountCurve, h,
    // Actual365Fixed(), displacement), or BachelierSwaptionEngine(discountCurve, h, Actual365Fixed()), whose
    // displacement is its default model's integer (BachelierSwaptionEngine.Calculate).
    private InstrumentResults Price(double x)
    {
        var dc = new Actual365Fixed();
        if (type == VolatilityType.ShiftedLognormal)
        {
            ConstantSwaptionVolatility vol =
                BlackStyleSwaptionEngine<Black76Spec>.ConstantVolatility(x, dc, displacement, context);
            return BlackStyleSwaptionEngine<Black76Spec>.Body(
                swaption, context, discountCurve, vol, CashAnnuityModel.DiscountCurve);
        }
        else
        {
            ConstantSwaptionVolatility vol = BlackStyleSwaptionEngine<BachelierSpec>.ConstantVolatility(
                x, dc, (double)CashAnnuityModel.DiscountCurve, context);
            return BlackStyleSwaptionEngine<BachelierSpec>.Body(
                swaption, context, discountCurve, vol, CashAnnuityModel.DiscountCurve);
        }
    }
}
