// SPDX-FileCopyrightText: Copyright (C) 2015 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/doublebarrieroption.hpp ql/instruments/doublebarrieroption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.PricingEngines;
using NanoQL.Processes;

namespace NanoQL.Instruments;

/// <summary>
/// A double barrier option on a single asset: a striked payoff and an exercise, knocked in or out at a lower and an
/// upper barrier.
/// </summary>
/// <remarks>
/// An extension point, as in QuantLib: the quanto double-barrier option derives from it, and the experimental
/// double-barrier engines price it. Immutable. A knock-out's rebate is paid at the first touch of either barrier, a
/// knock-in's at expiry if neither was touched (as the analytic and binary engines price it; the binary engine refuses
/// a KIKO's or KOKI's); QuantLib's analytic and binary engines read no rebate.
/// </remarks>
public class DoubleBarrierOption : OneAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="DoubleBarrierOption"/> class.</summary>
    /// <param name="barrierType">The barrier's type.</param>
    /// <param name="barrierLo">The lower barrier.</param>
    /// <param name="barrierHi">The upper barrier.</param>
    /// <param name="rebate">The rebate.</param>
    /// <param name="payoff">The striked payoff.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="barrierType"/> isn't a member of its type, with
    /// QuantLib's "Invalid barrier type".</exception>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    public DoubleBarrierOption(
        DoubleBarrierType barrierType,
        double barrierLo,
        double barrierHi,
        double rebate,
        StrikedTypePayoff payoff,
        Exercise exercise)
        : base(payoff, exercise)
    {
        EnumArguments.Check(barrierType, nameof(barrierType));
        BarrierType = barrierType;
        BarrierLo = barrierLo;
        BarrierHi = barrierHi;
        Rebate = rebate;
    }

    /// <summary>Gets the barrier's type.</summary>
    public DoubleBarrierType BarrierType { get; }

    /// <summary>Gets the lower barrier.</summary>
    public double BarrierLo { get; }

    /// <summary>Gets the upper barrier.</summary>
    public double BarrierHi { get; }

    /// <summary>Gets the rebate.</summary>
    public double Rebate { get; }

    /// <summary>
    /// Whether an underlying value lies on or beyond either barrier: QuantLib's double-barrier engines'
    /// <c>triggered</c>.
    /// </summary>
    /// <param name="underlying">The underlying value.</param>
    /// <returns><see langword="true"/> when the value is at or below the lower barrier or at or above the upper
    /// one.</returns>
    public bool Triggered(double underlying) => underlying <= BarrierLo || underlying >= BarrierHi;

    /// <summary>The volatility at which the option's value is the target value: QuantLib's analytic double-barrier
    /// engine.</summary>
    /// <param name="targetValue">The target value.</param>
    /// <param name="process">The process, whose Black volatility is replaced by a flat one at each trial.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxEvaluations">The solver's maximum number of evaluations.</param>
    /// <param name="minVol">The lower end of the bracket.</param>
    /// <param name="maxVol">The upper end of the bracket.</param>
    /// <returns>The implied volatility.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> or <paramref name="context"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is negative.</exception>
    /// <exception cref="ArgumentException">The accuracy isn't positive, or the bracket is empty.</exception>
    /// <exception cref="NanoQLException">
    /// The option has expired or isn't European, the value can't be bracketed, the solver exceeds its evaluations, or
    /// the engine fails.
    /// </exception>
    /// <remarks>
    /// The solve prices through the analytic engine as nanoql fixes it (its rebate, its terms beyond five, each rate on
    /// the volatility's clock), so it moves where those fixes move the price. QuantLib's warning: the result is
    /// consistent only with prices from the same engine.
    /// </remarks>
    public double ImpliedVolatility(
        double targetValue,
        GeneralizedBlackScholesProcess process,
        EvaluationContext context,
        double accuracy = 1.0e-4,
        int maxEvaluations = 100,
        double minVol = 1.0e-7,
        double maxVol = 4.0)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(maxEvaluations);
        if (IsExpired(context))
        {
            throw new NanoQLException("option expired");
        }

        Func<GeneralizedBlackScholesProcess, OneAssetOptionResults> body = Exercise.Type switch
        {
            ExerciseType.European => p => AnalyticDoubleBarrierEngine.Body(this, p, 5),
            ExerciseType.American or ExerciseType.Bermudan =>
                throw new NanoQLException("engine not available for non-European barrier option"),
            _ => throw new NanoQLException("unknown exercise type"),
        };

        return ImpliedVolatilityHelper.Calculate(
            this, process, body, targetValue, accuracy, maxEvaluations, minVol, maxVol);
    }
}
