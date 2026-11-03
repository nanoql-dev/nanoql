// SPDX-FileCopyrightText: Copyright (C) 2010 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/fdbatesvanillaengine.hpp ql/pricingengines/vanilla/fdbatesvanillaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.PricingEngines;

/// <summary>
/// The partial-integro finite-difference Bates pricing engine for vanilla options, European, American or Bermudan,
/// with cash dividends: <see cref="FdmBatesSolver"/> on the finite-difference Heston engine's grid.
/// </summary>
/// <remarks>
/// The engine reports the value, delta, gamma and theta, the theta <see langword="null"/> when the problem's first
/// stopping time is time 0. Its grid is <see cref="FdHestonVanillaEngine"/>'s, QuantLib's helper engine, on its clock
/// (<see cref="FdmHestonOp.Clock"/>), with its bounded skew factor and its equity grid widened where QuantLib's falls
/// short of the true forwards' band or isn't a number.
/// </remarks>
public static class FdBatesVanillaEngine
{
    /// <summary>Prices a vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Bates model.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of the log spot's points.</param>
    /// <param name="vGrid">The number of the variance's points.</param>
    /// <param name="dampingSteps">The number of implicit damping steps.</param>
    /// <param name="schemeDesc">The scheme; <see langword="null"/> for Hundsdorfer's, QuantLib's default.</param>
    /// <returns>The option's results; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the model is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size or the damping steps is negative.</exception>
    /// <exception cref="ArgumentException">A grid is too small, or the rollback's arguments are refused.</exception>
    /// <exception cref="NanoQLException">The payoff isn't striked, or a step or the spline fails.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        BatesModel model,
        int tGrid = 100,
        int xGrid = 100,
        int vGrid = 50,
        int dampingSteps = 0,
        FdmSchemeDesc? schemeDesc = null) =>
        Calculate(option, context, model, [], tGrid, xGrid, vGrid, dampingSteps, schemeDesc);

    /// <summary>Prices a vanilla option with cash dividends.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Bates model.</param>
    /// <param name="dividends">The cash dividends, by the spot model.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of the log spot's points.</param>
    /// <param name="vGrid">The number of the variance's points.</param>
    /// <param name="dampingSteps">The number of implicit damping steps.</param>
    /// <param name="schemeDesc">The scheme; <see langword="null"/> for Hundsdorfer's, QuantLib's default.</param>
    /// <returns>The option's results; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the model is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size or the damping steps is negative.</exception>
    /// <exception cref="ArgumentException">
    /// The schedule is the default array or holds <see langword="null"/>, a grid is too small, or the rollback's
    /// arguments are refused.
    /// </exception>
    /// <exception cref="NanoQLException">The payoff isn't striked, or a step or the spline fails.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        BatesModel model,
        ImmutableArray<Dividend> dividends,
        int tGrid = 100,
        int xGrid = 100,
        int vGrid = 50,
        int dampingSteps = 0,
        FdmSchemeDesc? schemeDesc = null)
    {
        FdHestonVanillaEngine.CheckArguments(option, context, model, dividends, tGrid, xGrid, vGrid, dampingSteps);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();

        var process = (BatesProcess)model.Process;
        TermStructure clock = FdmHestonOp.Clock(process);
        FdmSolverDesc solverDesc = FdHestonVanillaEngine.GetSolverDesc(
            option, context, model, dividends, null, tGrid, xGrid, vGrid, dampingSteps, null, 1.0, null, clock);

        var solver = new FdmBatesSolver(process, solverDesc, schemeDesc ?? FdmSchemeDesc.Hundsdorfer());

        double v0 = process.V0;
        double spot = process.S0;

        return option.FetchResults(new OneAssetOptionResults
        {
            NPV = solver.ValueAt(spot, v0),
            Delta = solver.DeltaAt(spot, v0),
            Gamma = solver.GammaAt(spot, v0),
            Theta = solver.ThetaAt(spot, v0),
        });
    }
}
