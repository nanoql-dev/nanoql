// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/vanillaoption.hpp ql/instruments/vanillaoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;

namespace NanoQL.Instruments;

/// <summary>A vanilla option on a single asset: a striked payoff and an exercise.</summary>
/// <remarks>
/// An extension point, as in QuantLib: the European option derives from it. Dividends are an engine argument, as in
/// QuantLib 1.44.
/// </remarks>
public class VanillaOption : OneAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="VanillaOption"/> class.</summary>
    /// <param name="payoff">The striked payoff.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    public VanillaOption(StrikedTypePayoff payoff, Exercise exercise)
        : base(payoff, exercise)
    {
    }

    /// <summary>
    /// The volatility at which the option's value is the target value, with no dividends: QuantLib's analytic engine
    /// for a European option, and its finite-difference engine for an American or Bermudan one.
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
    /// The option has expired, the value can't be bracketed, the solver exceeds its evaluations, or the engine fails.
    /// </exception>
    /// <remarks>
    /// QuantLib's warnings: the result is consistent only with prices from the same engines; an option whose gamma
    /// changes sign (a binary option) has a value that isn't monotonic in the volatility, so the solve can fail or mean
    /// little; and a target the option can't reach (below an American option's intrinsic value) fails.
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
    /// dividend engine for a European option, and its finite-difference engine for an American or Bermudan one.
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
    /// The option has expired, the value can't be bracketed, the solver exceeds its evaluations, or the engine fails.
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
                    body = p => AnalyticEuropeanEngine.Body(this, p, p.RiskFreeRate);
                }
                else
                {
                    body = p => AnalyticDividendEuropeanEngine.Body(this, context, p, dividends);
                }

                break;
            case ExerciseType.American:
            case ExerciseType.Bermudan:
                // QuantLib's FdBlackScholesVanillaEngine on its defaults, with the dividends
                body = p => FdBlackScholesVanillaEngine.Body(
                    this,
                    context,
                    p,
                    dividends,
                    null,
                    100,
                    100,
                    0,
                    FdmSchemeDesc.Douglas(),
                    false,
                    null,
                    FdBlackScholesVanillaEngineCashDividendModel.Spot);
                break;
            default:
                throw new NanoQLException("unknown exercise type");
        }

        return ImpliedVolatilityHelper.Calculate(
            this, process, body, targetValue, accuracy, maxEvaluations, minVol, maxVol);
    }
}
