// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2003 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/barrieroption.hpp ql/instruments/barrieroption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;

namespace NanoQL.Instruments;

/// <summary>A barrier option on a single asset: a striked payoff and an exercise, knocked in or out at a
/// barrier.</summary>
/// <remarks>
/// An extension point, as in QuantLib: the quanto barrier option derives from it. A knock-out pays its rebate when the
/// barrier is hit, a knock-in at expiry when it never was. Dividends are an engine argument, as in QuantLib 1.44.
/// </remarks>
public class BarrierOption : OneAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="BarrierOption"/> class.</summary>
    /// <param name="barrierType">The barrier's type.</param>
    /// <param name="barrier">The barrier.</param>
    /// <param name="rebate">The rebate.</param>
    /// <param name="payoff">The striked payoff.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="barrierType"/> isn't a member of its
    /// type.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    public BarrierOption(
        BarrierType barrierType, double barrier, double rebate, StrikedTypePayoff payoff, Exercise exercise)
        : base(payoff, exercise)
    {
        EnumArguments.Check(barrierType, nameof(barrierType));
        BarrierType = barrierType;
        Barrier = barrier;
        Rebate = rebate;
    }

    /// <summary>Gets the barrier's type.</summary>
    public BarrierType BarrierType { get; }

    /// <summary>Gets the barrier.</summary>
    public double Barrier { get; }

    /// <summary>Gets the rebate.</summary>
    public double Rebate { get; }

    /// <summary>
    /// Whether an underlying value lies beyond the barrier: below a down barrier, above an up one. QuantLib's barrier
    /// engines' <c>triggered</c>, which each of them checks the spot against.
    /// </summary>
    /// <param name="underlying">The underlying value.</param>
    /// <returns><see langword="true"/> when the value lies strictly beyond the barrier.</returns>
    public bool Triggered(double underlying) => BarrierType switch
    {
        BarrierType.DownIn or BarrierType.DownOut => underlying < Barrier,
        _ => underlying > Barrier,
    };

    /// <summary>
    /// The volatility at which the option's value is the target value, with no dividends: QuantLib's analytic barrier
    /// engine.
    /// </summary>
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
    /// The solve prices through the analytic engine as nanoql fixes it (a negative rebate priced, each rate on the
    /// volatility's clock, a knock-out's rebate under negative rates integrated), so it moves where those fixes move
    /// the price. QuantLib's warning: the result is consistent only with prices from the same engine.
    /// </remarks>
    public double ImpliedVolatility(
        double targetValue,
        GeneralizedBlackScholesProcess process,
        EvaluationContext context,
        double accuracy = 1.0e-4,
        int maxEvaluations = 100,
        double minVol = 1.0e-7,
        double maxVol = 4.0) =>
        ImpliedVolatility(targetValue, process, [], context, accuracy, maxEvaluations, minVol, maxVol);

    /// <summary>
    /// The volatility at which the option's value is the target value, with discrete dividends: QuantLib's analytic
    /// barrier engine without them, its finite-difference barrier engine on its defaults with them.
    /// </summary>
    /// <param name="targetValue">The target value.</param>
    /// <param name="process">The process, whose Black volatility is replaced by a flat one at each trial.</param>
    /// <param name="dividends">The dividends.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxEvaluations">The solver's maximum number of evaluations.</param>
    /// <param name="minVol">The lower end of the bracket.</param>
    /// <param name="maxVol">The upper end of the bracket.</param>
    /// <returns>The implied volatility.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> or <paramref name="context"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// The dividends are <c>default(ImmutableArray)</c> or hold a null one, the accuracy isn't positive, or the bracket
    /// is empty.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The option has expired or isn't European, the value can't be bracketed, the solver exceeds its evaluations, or
    /// the engine fails.
    /// </exception>
    /// <remarks>
    /// As QuantLib's helper does, the solver prices through the engine's body directly, past the expiry check its
    /// pricing would make; the option's own expiry is checked first.
    /// </remarks>
    public double ImpliedVolatility(
        double targetValue,
        GeneralizedBlackScholesProcess process,
        ImmutableArray<Dividend> dividends,
        EvaluationContext context,
        double accuracy = 1.0e-4,
        int maxEvaluations = 100,
        double minVol = 1.0e-7,
        double maxVol = 4.0)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(context);
        AnalyticDividendEuropeanEngine.CheckDividends(dividends, nameof(dividends));
        ArgumentOutOfRangeException.ThrowIfNegative(maxEvaluations);
        if (IsExpired(context))
        {
            throw new NanoQLException("option expired");
        }

        Func<GeneralizedBlackScholesProcess, OneAssetOptionResults> body;
        switch (Exercise.Type)
        {
            case ExerciseType.European:
                if (dividends.IsEmpty)
                {
                    body = p => AnalyticBarrierEngine.Body(this, p);
                }
                else
                {
                    // QuantLib's FdBlackScholesBarrierEngine on its defaults, with the dividends
                    body = p => FdBlackScholesBarrierEngine.Body(
                        this, context, p, dividends, 100, 100, 0, FdmSchemeDesc.Douglas(), false, null);
                }

                break;
            case ExerciseType.American:
            case ExerciseType.Bermudan:
                throw new NanoQLException("engine not available for non-European barrier option");
            default:
                throw new NanoQLException("unknown exercise type");
        }

        return ImpliedVolatilityHelper.Calculate(
            this, process, body, targetValue, accuracy, maxEvaluations, minVol, maxVol);
    }
}
