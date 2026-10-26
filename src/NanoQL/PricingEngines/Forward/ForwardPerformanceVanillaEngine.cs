// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/forward/forwardperformanceengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// The forward-start performance engine for vanilla options: the forward engine's inner option, its value per unit of
/// the spot and discounted on the risk-free curve to the reset date.
/// </summary>
/// <remarks>
/// Immutable, as its base, whose plain-payoff refusal, shifted vega and clock it shares (the S-exo plan's ER5 (xxiii),
/// (xxxvii) and (xxiv)); the rho's reset term is on the risk-free curve's own time.
/// </remarks>
public sealed class ForwardPerformanceVanillaEngine : ForwardVanillaEngine
{
    /// <summary>Initializes a new instance of the <see cref="ForwardPerformanceVanillaEngine"/> class.</summary>
    /// <param name="process">The underlying's process.</param>
    /// <param name="engine">The vanilla engine run on the forward process.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public ForwardPerformanceVanillaEngine(
        GeneralizedBlackScholesProcess process,
        Func<VanillaOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults> engine)
        : base(process, engine)
    {
    }

    /// <summary>Prices a forward-start performance option: QuantLib's <c>ForwardPerformanceVanillaEngine&lt;Engine&gt;</c>.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The underlying's process.</param>
    /// <param name="engine">The vanilla engine run on the forward process.</param>
    /// <returns>The option's results: the value and the inner engine's Greeks, carried to today.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The option's terms are invalid, its payoff isn't plain vanilla, the spot isn't positive, or the inner engine
    /// refuses.
    /// </exception>
    public static new OneAssetOptionResults Calculate(
        ForwardVanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        Func<VanillaOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults> engine) =>
        new ForwardPerformanceVanillaEngine(process, engine).Calculate(option, context);

    /// <inheritdoc/>
    protected override OneAssetOptionResults OriginalResults(
        ForwardVanillaOption option, OneAssetOptionResults originalResults)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(originalResults);
        TermStructure clock = FdmBlackScholesOp.Clock(Process);
        double resetTime = ResetTime(option, Process.RiskFreeRate);
        double discR = Process.RiskFreeRate.AccruedDiscount(option.ResetDate);

        // it's a performance option
        discR /= Process.X0;

        double temp = originalResults.NPV!.Value;
        double value = discR * temp;
        return new OneAssetOptionResults
        {
            NPV = value,
            Delta = 0.0,
            Gamma = 0.0,
            Theta = ResetRate(Process.RiskFreeRate, option, clock) * value,
            Vega = discR * (originalResults.Vega is double v ? ShiftedVega(option, v) : null),
            Rho = (-resetTime * value) + (discR * originalResults.Rho),
            DividendRho = discR * originalResults.DividendRho,
        };
    }
}
