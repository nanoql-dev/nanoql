// SPDX-FileCopyrightText: Copyright (C) 2025 William Day
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/softbarrieroption.hpp ql/instruments/softbarrieroption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.PricingEngines;
using NanoQL.Processes;

namespace NanoQL.Instruments;

/// <summary>
/// A soft barrier option: knocked in or out in proportion over a range of barriers, [L, U], instead of in full at one
/// hard barrier; the average of the single-barrier options with barriers in the range.
/// </summary>
/// <remarks>
/// Immutable and sealed: QuantLib derives nothing from it. European exercise only, as in QuantLib; it has no checks of
/// its own beyond the option's, its engine checking the barriers.
/// </remarks>
public sealed class SoftBarrierOption : OneAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="SoftBarrierOption"/> class.</summary>
    /// <param name="barrierType">The barrier's type.</param>
    /// <param name="barrierLo">The range's lower barrier, L.</param>
    /// <param name="barrierHi">The range's upper barrier, U.</param>
    /// <param name="payoff">The striked payoff.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="barrierType"/> isn't a member of its
    /// type.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    public SoftBarrierOption(
        BarrierType barrierType, double barrierLo, double barrierHi, StrikedTypePayoff payoff, Exercise exercise)
        : base(payoff, exercise)
    {
        EnumArguments.Check(barrierType, nameof(barrierType));
        BarrierType = barrierType;
        BarrierLo = barrierLo;
        BarrierHi = barrierHi;
    }

    /// <summary>Gets the barrier's type.</summary>
    public BarrierType BarrierType { get; }

    /// <summary>Gets the range's lower barrier, L.</summary>
    public double BarrierLo { get; }

    /// <summary>Gets the range's upper barrier, U.</summary>
    public double BarrierHi { get; }

    /// <summary>The volatility at which the option's value is the target value: QuantLib's analytic soft-barrier
    /// engine.</summary>
    /// <param name="targetValue">The target value.</param>
    /// <param name="process">The process, whose Black volatility is replaced by a flat one at each trial.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxEvaluations">The solver's maximum number of evaluations.</param>
    /// <param name="minVol">The lower end of the bracket; QuantLib's 1e-6, "0 vol values can make the soft barrier
    /// formula produce NaN results".</param>
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
    /// The solve prices through the analytic engine as nanoql fixes it (the band average where Haug's form doesn't
    /// hold, each rate on the volatility's clock). QuantLib's warning: the result is consistent only with prices from
    /// the same engine.
    /// </remarks>
    public double ImpliedVolatility(
        double targetValue,
        GeneralizedBlackScholesProcess process,
        EvaluationContext context,
        double accuracy = 1.0e-4,
        int maxEvaluations = 100,
        double minVol = 1e-6,
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
            ExerciseType.European => p => AnalyticSoftBarrierEngine.Body(this, p),
            ExerciseType.American or ExerciseType.Bermudan =>
                throw new NanoQLException("engine not available for non-European soft barrier option"),
            _ => throw new NanoQLException("unknown exercise type"),
        };

        return ImpliedVolatilityHelper.Calculate(
            this, process, body, targetValue, accuracy, maxEvaluations, minVol, maxVol);
    }
}
