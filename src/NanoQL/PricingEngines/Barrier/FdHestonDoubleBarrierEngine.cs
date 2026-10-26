// SPDX-FileCopyrightText: Copyright (C) 2016 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/barrier/fdhestondoublebarrierengine.hpp ql/pricingengines/barrier/fdhestondoublebarrierengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.TermStructures;

namespace NanoQL.PricingEngines;

/// <summary>
/// The finite-difference Heston pricing engine for double barrier options: <see cref="FdmHestonSolver"/> on a log-spot
/// grid bounded at both barriers, the rebate their Dirichlet value, paid at the first touch.
/// </summary>
/// <remarks>
/// <para>
/// European exercise only, as QuantLib's. The engine reports the value, delta, gamma and theta, on
/// <see cref="FdHestonBarrierEngine"/>'s grids and <see cref="FdmHestonOp.Clock"/>'s times (the S-bar plan's BR2 (v), BR5
/// (iv)).
/// </para>
/// <para>
/// Documented divergences. A knock-in, which QuantLib refuses, is priced by in-out parity, the method QuantLib's
/// single-barrier engine applies: the vanilla less the knock-out, both legs with the knock-out's leverage function and
/// mixing factor, and a knock-in's rebate, paid at expiry where neither barrier is touched, as the rebate times the
/// knock-out of a unit cash-or-nothing at strike 0 (the S-bar plan's BR8 (b), BR5 (xlvi)'s convention;
/// <c>Divergences/FdHestonDoubleKnockInTests.cs</c>). KIKO and KOKI options stay refused. And a spot on or beyond a
/// barrier is refused before the solve ("barrier(s) already touched"), as the analytic engine refuses it, where QuantLib's
/// grid interpolation fails outside its range (BR5 (xliv)).
/// </para>
/// </remarks>
public static class FdHestonDoubleBarrierEngine
{
    /// <summary>Prices a double barrier option.</summary>
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
    /// <returns>The option's results; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the model is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size or the damping steps is negative.</exception>
    /// <exception cref="ArgumentException">A grid is too small, or the rollback's arguments are refused.</exception>
    /// <exception cref="NanoQLException">
    /// The type is KIKO or KOKI, the payoff isn't striked, the exercise isn't European, the spot lies on or beyond a
    /// barrier ("barrier(s) already touched"), or a step or the spline fails.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        DoubleBarrierOption option,
        EvaluationContext context,
        HestonModel model,
        int tGrid = 100,
        int xGrid = 100,
        int vGrid = 50,
        int dampingSteps = 0,
        FdmSchemeDesc? schemeDesc = null,
        LocalVolTermStructure? leverageFct = null,
        double mixingFactor = 1.0)
    {
        FdHestonBarrierEngine.CheckArguments(option, context, model, [], tGrid, xGrid, vGrid, dampingSteps);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        if (option.BarrierType is not (DoubleBarrierType.KnockOut or DoubleBarrierType.KnockIn))
        {
            throw new NanoQLException("only Knock-Out and Knock-In double barrier options are supported");
        }

        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        // a spot on or beyond a barrier is refused before the solve (BR5 (xliv))
        if (option.Triggered(model.Process.S0))
        {
            throw new NanoQLException("barrier(s) already touched");
        }

        FdmSchemeDesc scheme = schemeDesc ?? FdmSchemeDesc.Hundsdorfer();
        double xMin = Math.Log(option.BarrierLo);
        double xMax = Math.Log(option.BarrierHi);
        if (option.BarrierType == DoubleBarrierType.KnockOut)
        {
            return option.FetchResults(FdHestonBarrierEngine.Solve(
                option, context, model, [], payoff, payoff, xMin, xMax, option.Rebate, true, true, false, tGrid, xGrid,
                vGrid, dampingSteps, scheme, leverageFct, mixingFactor));
        }

        // the knock-in by in-out parity (BR8 (b)): the vanilla less the knock-out without a rebate, the rebate paid at
        // expiry where no barrier is touched, R × the unit cash-or-nothing knock-out at strike 0 (BR5 (xlvi)'s
        // convention); every leg with the leverage function and mixing factor
        OneAssetOptionResults vanilla = FdHestonVanillaEngine.Calculate(
            new VanillaOption(payoff, option.Exercise), context, model, tGrid, xGrid, vGrid, dampingSteps, scheme,
            leverageFct, mixingFactor);
        OneAssetOptionResults knockOut = FdHestonBarrierEngine.Solve(
            option, context, model, [], payoff, payoff, xMin, xMax, 0.0, true, true, false, tGrid, xGrid, vGrid,
            dampingSteps, scheme, leverageFct, mixingFactor);
        double? value = vanilla.NPV - knockOut.NPV;
        double? delta = vanilla.Delta - knockOut.Delta;
        double? gamma = vanilla.Gamma - knockOut.Gamma;
        double? theta = vanilla.Theta - knockOut.Theta;
        if (option.Rebate != 0.0)
        {
            OneAssetOptionResults noTouch = FdHestonBarrierEngine.Solve(
                option, context, model, [], payoff, new CashOrNothingPayoff(OptionType.Call, 0.0, 1.0), xMin, xMax, 0.0,
                true, true, false, tGrid, xGrid, vGrid, dampingSteps, scheme, leverageFct, mixingFactor);
            value += option.Rebate * noTouch.NPV;
            delta += option.Rebate * noTouch.Delta;
            gamma += option.Rebate * noTouch.Gamma;
            theta += option.Rebate * noTouch.Theta;
        }

        return option.FetchResults(new OneAssetOptionResults { NPV = value, Delta = delta, Gamma = gamma, Theta = theta });
    }
}
