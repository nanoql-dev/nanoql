// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Stamm
// SPDX-FileCopyrightText: Copyright (C) 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/cdsoption.hpp ql/experimental/credit/cdsoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// An option on a credit default swap. The side of the option is set by choosing the side of the swap: a receiver
/// option is a right to buy an underlying swap selling protection and receiving a coupon, a payer option a right to buy
/// one buying protection and paying a coupon.
/// </summary>
/// <remarks>
/// QuantLib's engine and its at-the-money rate read the swap's attached engine; here each takes the swap's engine as a
/// delegate. The implied volatility is an instance method running the Black engine's body on each trial.
/// </remarks>
[Experimental("NQL0001")]
public sealed class CdsOption : Option
{
    /// <summary>Initializes a new instance of the <see cref="CdsOption"/> class.</summary>
    /// <param name="swap">The underlying swap, running-spread only.</param>
    /// <param name="exercise">The exercise.</param>
    /// <param name="knocksOut">Whether the option knocks out on a default before its exercise; a receiver option
    /// must.</param>
    /// <exception cref="ArgumentNullException"><paramref name="swap"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">A receiver option doesn't knock out ("receiver CDS options must knock out"),
    /// or the swap has an upfront ("underlying must be running-spread only").</exception>
    public CdsOption(CreditDefaultSwap swap, Exercise exercise, bool knocksOut = true)
        : base(new NullPayoff(), exercise)
    {
        ArgumentNullException.ThrowIfNull(swap);
        if (!(swap.Side == ProtectionSide.Buyer || knocksOut))
        {
            throw new NanoQLException("receiver CDS options must knock out");
        }

        if (!(swap.Upfront is null))
        {
            throw new NanoQLException("underlying must be running-spread only");
        }

        UnderlyingSwap = swap;
        KnocksOut = knocksOut;
    }

    /// <summary>Gets the underlying swap.</summary>
    public CreditDefaultSwap UnderlyingSwap { get; }

    /// <summary>Gets a value indicating whether the option knocks out on a default before its exercise.</summary>
    public bool KnocksOut { get; }

    /// <summary>Whether the option's last exercise date has occurred.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns><see langword="true"/> when the option has expired.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimpleEvent(Exercise.LastDate()).HasOccurred(context);
    }

    /// <summary>The results of an expired option: its value and its risky annuity 0.</summary>
    /// <returns>The results.</returns>
    public override InstrumentResults SetupExpired() => new CdsOptionResults
    {
        NPV = 0.0,
        ErrorEstimate = 0.0,
        ValuationDate = null,
        RiskyAnnuity = 0.0,
    };

    /// <summary>Checks the option's terms before an engine prices it: the swap's, then the option's. QuantLib's "CDS
    /// not set" and "exercise not set" hold by construction.</summary>
    /// <exception cref="NanoQLException">The swap's terms fail its checks.</exception>
    public override void Validate()
    {
        UnderlyingSwap.Validate();
        base.Validate();
    }

    /// <summary>The at-the-money rate: the underlying swap's fair spread.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="swapEngine">The swap's engine.</param>
    /// <returns>The rate.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The engine gives no fair spread ("fair spread not available").</exception>
    public double AtmRate(EvaluationContext context, Func<CreditDefaultSwap, EvaluationContext, CreditDefaultSwapResults> swapEngine)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(swapEngine);
        return BlackCdsOptionEngine.FairSpread(swapEngine(UnderlyingSwap, context));
    }

    /// <summary>The Black volatility that gives the option a target value.</summary>
    /// <param name="price">The target value.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="termStructure">The discount curve.</param>
    /// <param name="probability">The default-probability curve, or <see langword="null"/>, QuantLib's empty handle,
    /// which a knock-out option doesn't read.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="swapEngine">The swap's engine.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxEvaluations">The solver's evaluations at most.</param>
    /// <param name="minVol">The volatility's lower bound.</param>
    /// <param name="maxVol">The volatility's upper bound.</param>
    /// <returns>The volatility.</returns>
    /// <remarks>QuantLib prices the option once through its attached engine first, a calculation whose results it
    /// discards; here the Black engine's body runs on each trial, from QuantLib's guess of 0.10.</remarks>
    /// <exception cref="ArgumentNullException">An argument but <paramref name="probability"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is negative.</exception>
    /// <exception cref="NanoQLException">The option has expired ("instrument expired"); the engine refuses the option;
    /// or the solver fails.</exception>
    public double ImpliedVolatility(
        double price,
        EvaluationContext context,
        YieldTermStructure termStructure,
        DefaultProbabilityTermStructure? probability,
        double recoveryRate,
        Func<CreditDefaultSwap, EvaluationContext, CreditDefaultSwapResults> swapEngine,
        double accuracy = 1.0e-4,
        int maxEvaluations = 100,
        double minVol = 1.0e-7,
        double maxVol = 4.0)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(termStructure);
        ArgumentNullException.ThrowIfNull(swapEngine);
        ArgumentOutOfRangeException.ThrowIfNegative(maxEvaluations);
        if (IsExpired(context))
        {
            throw new NanoQLException("instrument expired");
        }

        double guess = 0.10;
        double F(double x) => BlackCdsOptionEngine.Body(this, context, probability, recoveryRate, termStructure, x, swapEngine).NPV!.Value - price;
        Brent solver = new Brent().SetMaxEvaluations(maxEvaluations);
        return solver.Solve(F, accuracy, guess, minVol, maxVol);
    }
}
