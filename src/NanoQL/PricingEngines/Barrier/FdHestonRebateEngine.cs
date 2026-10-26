// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/barrier/fdhestonrebateengine.hpp ql/pricingengines/barrier/fdhestonrebateengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.TermStructures;

namespace NanoQL.PricingEngines;

/// <summary>
/// The finite-difference Heston pricing engine for a barrier option's rebate: the rebate paid at the barrier, its
/// Dirichlet value, or at expiry where the barrier is never hit, the knock-in leg of <see cref="FdHestonBarrierEngine"/>.
/// </summary>
/// <remarks>
/// European exercise only, as QuantLib's. The engine reports the value, delta, gamma and theta, on
/// <see cref="FdHestonBarrierEngine"/>'s grids and <see cref="FdmHestonOp.Clock"/>'s times (the S-bar plan's BR2 (v), BR5
/// (iv)). A spot beyond the barrier is refused before the solve ("barrier touched"), where QuantLib's grid interpolation
/// fails outside its range (BR5 (xliv)).
/// </remarks>
public static class FdHestonRebateEngine
{
    /// <summary>Prices a barrier option's rebate.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Heston model.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of the log spot's points.</param>
    /// <param name="vGrid">The number of the variance's points.</param>
    /// <param name="dampingSteps">The number of implicit damping steps.</param>
    /// <param name="schemeDesc">The scheme; <see langword="null"/> for Hundsdorfer's, QuantLib's default.</param>
    /// <param name="leverageFct">The leverage function, or <see langword="null"/> for none.</param>
    /// <param name="mixingFactor">The factor scaling the variance's volatility σ.</param>
    /// <returns>The rebate's results; the expired results when the option has expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the model is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size or the damping steps is negative.</exception>
    /// <exception cref="ArgumentException">A grid is too small, or the rollback's arguments are refused.</exception>
    /// <exception cref="NanoQLException">
    /// The payoff isn't striked, the exercise isn't European, the spot lies beyond the barrier ("barrier touched"), or a
    /// step or the spline fails.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        BarrierOption option,
        EvaluationContext context,
        HestonModel model,
        int tGrid = 100,
        int xGrid = 100,
        int vGrid = 50,
        int dampingSteps = 0,
        FdmSchemeDesc? schemeDesc = null,
        LocalVolTermStructure? leverageFct = null,
        double mixingFactor = 1.0) =>
        Calculate(option, context, model, [], tGrid, xGrid, vGrid, dampingSteps, schemeDesc, leverageFct, mixingFactor);

    /// <summary>Prices a barrier option's rebate with cash dividends.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Heston model.</param>
    /// <param name="dividends">The cash dividends, by the spot model.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of the log spot's points.</param>
    /// <param name="vGrid">The number of the variance's points.</param>
    /// <param name="dampingSteps">The number of implicit damping steps.</param>
    /// <param name="schemeDesc">The scheme; <see langword="null"/> for Hundsdorfer's, QuantLib's default.</param>
    /// <param name="leverageFct">The leverage function, or <see langword="null"/> for none.</param>
    /// <param name="mixingFactor">The factor scaling the variance's volatility σ.</param>
    /// <returns>The rebate's results; the expired results when the option has expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the model is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size or the damping steps is negative.</exception>
    /// <exception cref="ArgumentException">
    /// The schedule is the default array or holds <see langword="null"/>, a grid is too small, or the rollback's
    /// arguments are refused.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The payoff isn't striked, the exercise isn't European, the spot lies beyond the barrier ("barrier touched"), or a
    /// step or the spline fails.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        BarrierOption option,
        EvaluationContext context,
        HestonModel model,
        ImmutableArray<Dividend> dividends,
        int tGrid = 100,
        int xGrid = 100,
        int vGrid = 50,
        int dampingSteps = 0,
        FdmSchemeDesc? schemeDesc = null,
        LocalVolTermStructure? leverageFct = null,
        double mixingFactor = 1.0)
    {
        FdHestonBarrierEngine.CheckArguments(option, context, model, dividends, tGrid, xGrid, vGrid, dampingSteps);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        // a spot beyond the barrier is refused before the solve (BR5 (xliv))
        if (option.Triggered(model.Process.S0))
        {
            throw new NanoQLException("barrier touched");
        }

        bool down = option.BarrierType is BarrierType.DownIn or BarrierType.DownOut;

        // the rebate paid at expiry, the payoff of the calculator; at the barrier, the boundaries'
        var rebatePayoff = new CashOrNothingPayoff(OptionType.Call, 0.0, option.Rebate);
        return option.FetchResults(FdHestonBarrierEngine.Solve(
            option,
            context,
            model,
            dividends,
            payoff,
            rebatePayoff,
            down ? Math.Log(option.Barrier) : null,
            down ? null : Math.Log(option.Barrier),
            option.Rebate,
            down,
            !down,
            true,
            tGrid,
            xGrid,
            vGrid,
            dampingSteps,
            schemeDesc ?? FdmSchemeDesc.Hundsdorfer(),
            leverageFct,
            mixingFactor));
    }
}
