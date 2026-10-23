// SPDX-FileCopyrightText: Copyright (C) 2013, 2018 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/floatfloatswaption.hpp ql/instruments/floatfloatswaption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;

namespace NanoQL.Instruments;

/// <summary>An option to enter a float-float swap.</summary>
/// <remarks>
/// A swaption has no payoff, and its results are an engine's <see cref="InstrumentResults"/>. The settlement type and
/// method are checked against each other when the swaption is priced, as QuantLib's arguments check them.
/// <see cref="CalibrationBasket"/> takes the basket-generating engine, which QuantLib's instrument holds and nanoql's
/// doesn't (the S-g1d plan's G1D5).
/// </remarks>
public sealed class FloatFloatSwaption : Option
{
    /// <summary>Initializes a new instance of the <see cref="FloatFloatSwaption"/> class.</summary>
    /// <param name="swap">The underlying swap.</param>
    /// <param name="exercise">The exercise.</param>
    /// <param name="delivery">The settlement type.</param>
    /// <param name="settlementMethod">The settlement method.</param>
    /// <exception cref="ArgumentNullException"><paramref name="swap"/> or <paramref name="exercise"/> is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delivery"/> or
    /// <paramref name="settlementMethod"/> isn't a member of its type.</exception>
    public FloatFloatSwaption(
        FloatFloatSwap swap,
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

    /// <summary>Gets the underlying swap's type.</summary>
    public SwapType Type => Underlying.Type;

    /// <summary>Gets the underlying swap: QuantLib's <c>underlyingSwap()</c>.</summary>
    public FloatFloatSwap Underlying { get; }

    /// <inheritdoc/>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimpleEvent(Exercise.LastDate()).HasOccurred(context);
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        // QuantLib's "underlying cms swap not set" and "exercise not set" can't fail: both are refused at
        // construction when null; its swap arguments' sizes are the builder's, equal.
        Settlement.CheckTypeAndMethodConsistency(SettlementType, SettlementMethod);
    }

    /// <summary>
    /// The calibration basket of the swaption's live exercise dates: per date, a standard swaption matching the
    /// underlying's value, delta and gamma in the model's state, or the at-the-money coterminal one.
    /// </summary>
    /// <param name="engine">The basket-generating engine: a <see cref="Gaussian1dFloatFloatSwaptionEngine"/>.</param>
    /// <param name="standardSwapBase">The standard swaps' index, with its forwarding and, if exogenous, discounting
    /// curves.</param>
    /// <param name="swaptionVolatility">The standard swaptions' volatilities.</param>
    /// <param name="context">The evaluation context: the exercise dates after its evaluation date are live.</param>
    /// <param name="basketType">The basket's type.</param>
    /// <returns>The helpers, without pricing engines.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="NanoQLException">The engine prices another instrument, the swaption's settlement is
    /// inconsistent, a curve is missing, or a match fails.</exception>
    public ImmutableArray<SwaptionHelper> CalibrationBasket(
        BasketGeneratingEngine engine,
        SwapIndex standardSwapBase,
        SwaptionVolatilityStructure swaptionVolatility,
        EvaluationContext context,
        BasketGeneratingEngineCalibrationBasketType basketType = BasketGeneratingEngineCalibrationBasketType.MaturityStrikeByDeltaGamma)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(context);
        Validate();
        return engine.CalibrationBasket(this, Exercise, standardSwapBase, swaptionVolatility, basketType, context);
    }

    private static Exercise ExerciseOf(FloatFloatSwap swap, Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(swap);
        ArgumentNullException.ThrowIfNull(exercise);
        return exercise;
    }
}
