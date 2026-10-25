// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009, 2014 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009, 2015 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/fdhestonvanillaengine.hpp ql/pricingengines/vanilla/fdhestonvanillaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.PricingEngines;

/// <summary>
/// The finite-difference Heston pricing engine for vanilla options, European, American or Bermudan, with cash
/// dividends, a quanto adjustment and a leverage function optional: <see cref="FdmHestonSolver"/> on
/// <see cref="FdmBlackScholesMesher"/>'s grid of the log spot, sized by the variance mesher's volatility estimate, by
/// <see cref="FdmHestonLocalVolatilityVarianceMesher"/>'s grid of the variance.
/// </summary>
/// <remarks>
/// <para>
/// The engine reports the value, delta, gamma and theta, the theta <see langword="null"/> when the problem's first
/// stopping time is time 0. Its finite-difference working types are built inside each calculation and dropped after it
/// (the S-eq plan's SEQR1). Its times run on <see cref="FdmHestonOp.Clock"/>, from the earliest reference date of the
/// structures it reads, each accruing nothing before its own date, where QuantLib's run from the risk-free curve's;
/// where none is dated before the risk-free curve, they are QuantLib's (the S-hes plan's HR7 (ii),
/// <c>Divergences/FdHestonMismatchedTimesTests.cs</c>). The variance mesher's skew factor is bounded (HR9) and its
/// fallbacks are master's (HR8).
/// </para>
/// <para>
/// QuantLib sizes the equity mesher on a helper process whose curves its call binds in each other's places, so the
/// mesher walks the forward's mirror about the spot; where the carry is large against the volatility the grid's far
/// edge falls inside the distribution and the price stops converging (a five-year call at 1.25 times the forward,
/// carry 0.30 and volatility 0.1, priced 0 where the analytic engine gives 1.5732), and where cash dividends take the
/// mirrored walk below zero its bound isn't a number and the engine refuses. Here the mesher is QuantLib's wherever its
/// range holds the true forwards' band, the walk's ends on the helper's curves in place widened by the volatility's
/// quantile, half the mesher's own margin, and QuantLib's mesher widened to the band through its own constraints where
/// it doesn't, a bound that isn't a number falling short. The multi-strike solve, whose mesher takes no constraints,
/// solves each strike alone where its grid doesn't hold the band at the points the solve is read at, S·K/Kᵢ, their
/// forwards quanto-adjusted where a quanto helper is given; the two paths run on different grids, so a strike's value
/// steps where the switch moves, by up to about 0.4% at carry 0.06 (B1-1, B2-1, B2-2 and B3-1 of the S-hes plan's C1
/// review; <c>Divergences/FdHestonHighCarryTests.cs</c>).
/// </para>
/// <para>
/// QuantLib's <c>enableMultipleStrikesCaching</c> solves once on a mesher spanning several strikes and serves later
/// options at those strikes from the solve, each V(S·d)/d with d = K/Kᵢ; the strike a later call is served depends on
/// which option solved first, which a cache under the rule on caches may not do (the master plan's Revision 79). Here
/// <see cref="CalculateStrikes(VanillaOption, IEnumerable{double}, EvaluationContext, HestonModel, int, int, int, int,
/// FdmSchemeDesc?, LocalVolTermStructure?, double)"/> returns every strike's results from one solve on the given option,
/// each QuantLib's cached value bit for bit (the S-hes plan's HR4 (ii)). Where the scaling doesn't hold, QuantLib's
/// cache serves values far from the strike's own price: with a leverage function, which a spot-dependent leverage
/// breaks (at 125, 0.830 cached where the direct price is 0.433), and after a reference whose payoff isn't a plain
/// vanilla (6.819 served for a plain call at 80 after a cash-or-nothing reference, its direct price 22.945); there
/// each strike's plain vanilla is solved alone, QuantLib's engine priced for that strike (HR7 (i), (xxiv);
/// <c>Divergences/FdHestonMultipleStrikesLeverageTests.cs</c>, <c>Divergences/FdHestonStrikesReferenceTests.cs</c>).
/// </para>
/// </remarks>
public static class FdHestonVanillaEngine
{
    /// <summary>Prices a vanilla option.</summary>
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
    /// <exception cref="NanoQLException">The payoff isn't striked, or a step or the spline fails.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        HestonModel model,
        int tGrid = 100,
        int xGrid = 100,
        int vGrid = 50,
        int dampingSteps = 0,
        FdmSchemeDesc? schemeDesc = null,
        LocalVolTermStructure? leverageFct = null,
        double mixingFactor = 1.0) =>
        Calculate(option, context, model, [], null, tGrid, xGrid, vGrid, dampingSteps, schemeDesc, leverageFct, mixingFactor);

    /// <summary>Prices a vanilla option with cash dividends.</summary>
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
        HestonModel model,
        ImmutableArray<Dividend> dividends,
        int tGrid = 100,
        int xGrid = 100,
        int vGrid = 50,
        int dampingSteps = 0,
        FdmSchemeDesc? schemeDesc = null,
        LocalVolTermStructure? leverageFct = null,
        double mixingFactor = 1.0) =>
        Calculate(option, context, model, dividends, null, tGrid, xGrid, vGrid, dampingSteps, schemeDesc, leverageFct, mixingFactor);

    /// <summary>Prices a quanto vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Heston model.</param>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/> for none.</param>
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
    /// <exception cref="NanoQLException">The payoff isn't striked, or a step or the spline fails.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        HestonModel model,
        FdmQuantoHelper? quantoHelper,
        int tGrid = 100,
        int xGrid = 100,
        int vGrid = 50,
        int dampingSteps = 0,
        FdmSchemeDesc? schemeDesc = null,
        LocalVolTermStructure? leverageFct = null,
        double mixingFactor = 1.0) =>
        Calculate(option, context, model, [], quantoHelper, tGrid, xGrid, vGrid, dampingSteps, schemeDesc, leverageFct, mixingFactor);

    /// <summary>Prices a quanto vanilla option with cash dividends.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Heston model.</param>
    /// <param name="dividends">The cash dividends, by the spot model.</param>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/> for none.</param>
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
    /// <exception cref="ArgumentException">
    /// The schedule is the default array or holds <see langword="null"/>, a grid is too small, or the rollback's
    /// arguments are refused.
    /// </exception>
    /// <exception cref="NanoQLException">The payoff isn't striked, or a step or the spline fails.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        HestonModel model,
        ImmutableArray<Dividend> dividends,
        FdmQuantoHelper? quantoHelper,
        int tGrid = 100,
        int xGrid = 100,
        int vGrid = 50,
        int dampingSteps = 0,
        FdmSchemeDesc? schemeDesc = null,
        LocalVolTermStructure? leverageFct = null,
        double mixingFactor = 1.0)
    {
        CheckArguments(option, context, model, dividends, tGrid, xGrid, vGrid, dampingSteps);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(Solve(
            option, context, model, dividends, quantoHelper, tGrid, xGrid, vGrid, dampingSteps, schemeDesc, leverageFct, mixingFactor));
    }

    /// <summary>
    /// The results of the option at each of several strikes from one solve on a mesher spanning them: QuantLib's
    /// multiple-strikes caching, as a call, each strike's value V(S·d)/d, delta, gamma·d and theta/d at d = K/Kᵢ, K the
    /// option's strike.
    /// </summary>
    /// <param name="option">The reference option, whose exercise the strikes share and whose option type their plain
    /// vanillas take.</param>
    /// <param name="strikes">The strikes, at least one.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Heston model.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of the log spot's points.</param>
    /// <param name="vGrid">The number of the variance's points.</param>
    /// <param name="dampingSteps">The number of implicit damping steps.</param>
    /// <param name="schemeDesc">The scheme; <see langword="null"/> for Hundsdorfer's, QuantLib's default.</param>
    /// <param name="leverageFct">The leverage function, or <see langword="null"/> for none; with one, each strike is
    /// solved alone (HR7 (i)).</param>
    /// <param name="mixingFactor">The factor scaling the variance's volatility σ.</param>
    /// <returns>The plain vanilla's results at each strike, in the strikes' order; the expired results for each when
    /// the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size or the damping steps is negative.</exception>
    /// <exception cref="ArgumentException">No strike is given, a grid is too small, or the rollback's arguments are
    /// refused.</exception>
    /// <exception cref="NanoQLException">The payoff isn't striked, or a step or the spline fails.</exception>
    public static ImmutableArray<OneAssetOptionResults> CalculateStrikes(
        VanillaOption option,
        IEnumerable<double> strikes,
        EvaluationContext context,
        HestonModel model,
        int tGrid = 100,
        int xGrid = 100,
        int vGrid = 50,
        int dampingSteps = 0,
        FdmSchemeDesc? schemeDesc = null,
        LocalVolTermStructure? leverageFct = null,
        double mixingFactor = 1.0) =>
        CalculateStrikes(
            option, strikes, context, model, [], null, tGrid, xGrid, vGrid, dampingSteps, schemeDesc, leverageFct, mixingFactor);

    /// <summary>
    /// The results of the option at each of several strikes from one solve on a mesher spanning them, with QuantLib's
    /// refusal of cash dividends and a quanto helper optional.
    /// </summary>
    /// <param name="option">The reference option, whose exercise the strikes share and whose option type their plain
    /// vanillas take.</param>
    /// <param name="strikes">The strikes, at least one.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Heston model.</param>
    /// <param name="dividends">The cash dividends: QuantLib's multiple-strikes engine refuses any.</param>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/> for none.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of the log spot's points.</param>
    /// <param name="vGrid">The number of the variance's points.</param>
    /// <param name="dampingSteps">The number of implicit damping steps.</param>
    /// <param name="schemeDesc">The scheme; <see langword="null"/> for Hundsdorfer's, QuantLib's default.</param>
    /// <param name="leverageFct">The leverage function, or <see langword="null"/> for none; with one, each strike is
    /// solved alone (HR7 (i)).</param>
    /// <param name="mixingFactor">The factor scaling the variance's volatility σ.</param>
    /// <returns>The plain vanilla's results at each strike, in the strikes' order; the expired results for each when
    /// the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size or the damping steps is negative.</exception>
    /// <exception cref="ArgumentException">
    /// No strike is given, dividends are given ("multiple strikes engine does not work with discrete dividends"), a grid
    /// is too small, or the rollback's arguments are refused.
    /// </exception>
    /// <exception cref="NanoQLException">The payoff isn't striked, or a step or the spline fails.</exception>
    public static ImmutableArray<OneAssetOptionResults> CalculateStrikes(
        VanillaOption option,
        IEnumerable<double> strikes,
        EvaluationContext context,
        HestonModel model,
        ImmutableArray<Dividend> dividends,
        FdmQuantoHelper? quantoHelper,
        int tGrid = 100,
        int xGrid = 100,
        int vGrid = 50,
        int dampingSteps = 0,
        FdmSchemeDesc? schemeDesc = null,
        LocalVolTermStructure? leverageFct = null,
        double mixingFactor = 1.0)
    {
        ArgumentNullException.ThrowIfNull(strikes);
        double[] k = [.. strikes];
        CheckArguments(option, context, model, dividends, tGrid, xGrid, vGrid, dampingSteps);
        if (k.Length == 0)
        {
            throw new ArgumentException("no strikes given", nameof(strikes));
        }

        if (!dividends.IsEmpty)
        {
            throw new ArgumentException("multiple strikes engine does not work with discrete dividends", nameof(dividends));
        }

        if (option.IsExpired(context))
        {
            OneAssetOptionResults expired = option.SetupExpired();
            return [.. k.Select(_ => expired)];
        }

        option.Validate();
        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        var builder = ImmutableArray.CreateBuilder<OneAssetOptionResults>(k.Length);
        if (leverageFct is not null
            || option.Payoff is not PlainVanillaPayoff
            || !MultiStrikeGridHolds(option, model, k, quantoHelper, tGrid, vGrid, mixingFactor, FdmHestonOp.Clock(model.Process, quantoHelper)))
        {
            // HR7 (i), (xxiv), and a multi-strike grid short of the true forwards' band (B1-1 of the S-hes plan's C1
            // review): each strike's plain vanilla solved alone, QuantLib's engine priced for that strike
            foreach (double strike in k)
            {
                var single = new VanillaOption(new PlainVanillaPayoff(payoff.OptionType, strike), option.Exercise);
                builder.Add(single.FetchResults(Solve(
                    single, context, model, dividends, quantoHelper, tGrid, xGrid, vGrid, dampingSteps, schemeDesc,
                    leverageFct, mixingFactor, null)));
            }

            return builder.MoveToImmutable();
        }

        HestonProcess process = model.Process;
        FdmHestonSolver solver = BuildSolver(
            option, context, model, dividends, quantoHelper, tGrid, xGrid, vGrid, dampingSteps, schemeDesc, leverageFct,
            mixingFactor, k);

        double v0 = process.V0;
        double spot = process.S0;
        foreach (double strike in k)
        {
            double d = payoff.Strike / strike;

            builder.Add(new OneAssetOptionResults
            {
                NPV = solver.ValueAt(spot * d, v0) / d,
                Delta = solver.DeltaAt(spot * d, v0),
                Gamma = solver.GammaAt(spot * d, v0) * d,
                Theta = solver.ThetaAt(spot * d, v0) / d,
            });
        }

        return builder.MoveToImmutable();
    }

    /// <summary>
    /// The engine's problem for an option: its grid, inner value, step conditions and time steps, on
    /// <see cref="FdmHestonOp.Clock"/>'s times; QuantLib's <c>getSolverDesc</c>, the helper of the Heston-like engines.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Heston model.</param>
    /// <param name="dividends">The cash dividends, by the spot model.</param>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/> for none.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of the log spot's points.</param>
    /// <param name="vGrid">The number of the variance's points.</param>
    /// <param name="dampingSteps">The number of implicit damping steps.</param>
    /// <param name="leverageFct">The leverage function, or <see langword="null"/> for none.</param>
    /// <param name="mixingFactor">The factor scaling the variance's volatility σ.</param>
    /// <returns>The problem, for <see cref="FdmHestonSolver"/> and its kin.</returns>
    /// <remarks>QuantLib's scale-factor argument, which it doesn't read, is left out.</remarks>
    /// <exception cref="ArgumentNullException">The option, the context or the model is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size or the damping steps is negative.</exception>
    /// <exception cref="ArgumentException">
    /// The schedule is the default array or holds <see langword="null"/>, or a grid is too small.
    /// </exception>
    /// <exception cref="NanoQLException">The payoff isn't striked.</exception>
    public static FdmSolverDesc GetSolverDesc(
        VanillaOption option,
        EvaluationContext context,
        HestonModel model,
        ImmutableArray<Dividend> dividends,
        FdmQuantoHelper? quantoHelper,
        int tGrid = 100,
        int xGrid = 100,
        int vGrid = 50,
        int dampingSteps = 0,
        LocalVolTermStructure? leverageFct = null,
        double mixingFactor = 1.0)
    {
        CheckArguments(option, context, model, dividends, tGrid, xGrid, vGrid, dampingSteps);
        return GetSolverDesc(
            option, context, model, dividends, quantoHelper, tGrid, xGrid, vGrid, dampingSteps, leverageFct, mixingFactor,
            null, FdmHestonOp.Clock(model.Process, quantoHelper, leverageFct));
    }

    // QuantLib's getSolverDesc for the Heston-like engines (FdBatesVanillaEngine) and the multiple strikes: the strikes
    // the multiple-strikes caching's, null for none; the times the clock's (FdmHestonOp.Clock)
    internal static FdmSolverDesc GetSolverDesc(
        VanillaOption option,
        EvaluationContext context,
        HestonModel model,
        ImmutableArray<Dividend> dividends,
        FdmQuantoHelper? quantoHelper,
        int tGrid,
        int xGrid,
        int vGrid,
        int dampingSteps,
        LocalVolTermStructure? leverageFct,
        double mixingFactor,
        IReadOnlyList<double>? strikes,
        TermStructure clock)
    {
        // 1. Mesher
        HestonProcess process = model.Process;
        double maturity = clock.TimeFromReference(option.Exercise.LastDate());

        // 1.1 The variance mesher
        const int TGridMin = 5;
        int tGridAvgSteps = Math.Max(TGridMin, tGrid / 50);
        var vMesher = new FdmHestonLocalVolatilityVarianceMesher(
            vGrid, process, leverageFct, maturity, tGridAvgSteps, 0.0001, mixingFactor, clock);

        double avgVolaEstimate = vMesher.VolaEstimate;

        // 1.2 The equity mesher
        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        (GeneralizedBlackScholesProcess helperProcess, GeneralizedBlackScholesProcess trueProcess) = HelperProcesses(process, avgVolaEstimate, clock);

        Fdm1dMesher equityMesher;
        if (strikes is null || strikes.Count == 0)
        {
            // QuantLib's mesher on its helper, which walks the forward's mirror about the spot; where its range doesn't
            // hold the true forwards' band, the walk's ends on the helper's curves in place widened by the volatility's
            // quantile, half the mesher's own margin, QuantLib's mesher widened to the band through its own constraints
            // (B1-1 of the S-hes plan's C1 review): QuantLib's grid wherever it holds the distribution
            (double xMin, double xMax) = FdmBlackScholesMesher.Bounds(
                helperProcess, maturity, payoff.Strike, 0.0001, 2.0, dividends, quantoHelper, 0.0, context, clock);
            (double bandMin, double bandMax) = FdmBlackScholesMesher.Bounds(
                trueProcess, maturity, payoff.Strike, 0.0001, 1.0, dividends, quantoHelper, 0.0, context, clock);
            // a bound of QuantLib's that isn't a number, its swapped walk below zero, is short of a band that is (B2-2
            // of the review)
            bool lowShort = double.IsNaN(xMin) ? !double.IsNaN(bandMin) : bandMin < xMin;
            bool highShort = double.IsNaN(xMax) ? !double.IsNaN(bandMax) : bandMax > xMax;
            bool widened = lowShort || highShort;
            equityMesher = new FdmBlackScholesMesher(
                xGrid,
                helperProcess,
                maturity,
                payoff.Strike,
                widened ? (lowShort ? bandMin : xMin) : null,
                widened ? (highShort ? bandMax : xMax) : null,
                0.0001,
                2.0,
                (payoff.Strike, 0.1),
                dividends,
                quantoHelper,
                0.0,
                context,
                clock);
        }
        else
        {
            if (!dividends.IsEmpty)
            {
                throw new ArgumentException("multiple strikes engine does not work with discrete dividends", nameof(dividends));
            }

            equityMesher = new FdmBlackScholesMultiStrikeMesher(
                xGrid, helperProcess, maturity, strikes, 0.0001, 1.5, (payoff.Strike, 0.075), clock);
        }

        var mesher = new FdmMesherComposite(equityMesher, vMesher);

        // 2. Calculator
        var calculator = new FdmLogInnerValue(option.Payoff, mesher, 0);

        // 3. Step conditions
        FdmStepConditionComposite conditions = FdmStepConditionComposite.VanillaComposite(
            dividends, option.Exercise, mesher, calculator, clock.ReferenceDate, clock.DayCounter, context);

        // 4. Boundary conditions
        ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries = [];

        // 5. Solver
        return new FdmSolverDesc(mesher, boundaries, conditions, calculator, maturity, tGrid, dampingSteps);
    }

    // Whether QuantLib's multi-strike mesher holds the true forwards' band at the points the solve is read at, S·K/Kᵢ
    // for the option's strike K (B1-1 and B2-1 of the S-hes plan's C1 review): from each read point to its forward on
    // the curves in place, the dividend curve quanto-adjusted as the operator's drift is (B3-1), widened there by half
    // the mesher's margin, with its -σ²T/2, and without QuantLib's floor terms, which don't scale with the spot; where
    // it doesn't, each strike is solved alone
    internal static bool MultiStrikeGridHolds(
        VanillaOption option,
        HestonModel model,
        IReadOnlyList<double> strikes,
        FdmQuantoHelper? quantoHelper,
        int tGrid,
        int vGrid,
        double mixingFactor,
        TermStructure clock)
    {
        HestonProcess process = model.Process;
        double maturity = clock.TimeFromReference(option.Exercise.LastDate());
        int tGridAvgSteps = Math.Max(5, tGrid / 50);
        var vMesher = new FdmHestonLocalVolatilityVarianceMesher(
            vGrid, process, null, maturity, tGridAvgSteps, 0.0001, mixingFactor, clock);
        double vol = vMesher.VolaEstimate;
        (GeneralizedBlackScholesProcess helperProcess, GeneralizedBlackScholesProcess trueProcess) = HelperProcesses(process, vol, clock);
        (double xMin, double xMax) = FdmBlackScholesMultiStrikeMesher.Bounds(helperProcess, maturity, [.. strikes], 0.0001, 1.5, clock);

        double minStrike = strikes[0], maxStrike = strikes[0];
        foreach (double strike in strikes)
        {
            minStrike = strike < minStrike ? strike : minStrike;
            maxStrike = maxStrike < strike ? strike : maxStrike;
        }

        // the volatility is per unit of the clock's time, so σ√T on the clock
        double spot = process.S0;
        double referenceStrike = ((StrikedTypePayoff)option.Payoff!).Strike;
        YieldTermStructure dividendCurve = FdmBlackScholesMesher.DividendCurve(trueProcess, referenceStrike, quantoHelper);
        double growth = dividendCurve.DiscountAt(clock, maturity) / trueProcess.RiskFreeRate.DiscountAt(clock, maturity);
        double margin = new InverseCumulativeNormal().Evaluate(1 - 0.0001) * vol * Math.Sqrt(maturity);
        double drift = vol * vol * maturity / 2.0;
        double readMin = Math.Log(spot * referenceStrike / maxStrike), readMax = Math.Log(spot * referenceStrike / minStrike);
        double bandMin = Min(readMin, readMin + Math.Log(growth) - (0.75 * margin) - drift);
        double bandMax = Max(readMax, readMax + Math.Log(growth) + (0.75 * margin) - drift);
        return !(bandMin < xMin || bandMax > xMax);
    }

    // QuantLib's processHelper(s0, dividendYield, riskFreeRate, vol), whose parameters are (s0, rTS, qTS, vol): the
    // helper's risk-free curve the dividend curve and its dividend curve the risk-free curve, as QuantLib's call binds
    // them, so the equity mesher is QuantLib's; and the helper with its curves in place, whose forwards are the
    // process's. The volatility, an estimate per unit of the engine's time, on the clock, and the meshers on the
    // engine's clock, not the helpers'.
    private static (GeneralizedBlackScholesProcess Helper, GeneralizedBlackScholesProcess True) HelperProcesses(
        HestonProcess process, double vol, TermStructure clock)
    {
        var volatility = new BlackConstantVol(clock.ReferenceDate, null, vol, clock.DayCounter);
        return (new GeneralizedBlackScholesProcess(process.S0, process.RiskFreeRate, process.DividendYield, volatility),
                new GeneralizedBlackScholesProcess(process.S0, process.DividendYield, process.RiskFreeRate, volatility));
    }

    // std::min and std::max, which return their first argument where neither is less than the other
    private static double Min(double a, double b) => b < a ? b : a;

    private static double Max(double a, double b) => a < b ? b : a;

    // the argument checks every member makes first
    internal static void CheckArguments(
        VanillaOption option,
        EvaluationContext context,
        HestonModel model,
        ImmutableArray<Dividend> dividends,
        int tGrid,
        int xGrid,
        int vGrid,
        int dampingSteps)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);
        if (dividends.IsDefault)
        {
            throw new ArgumentException("default(ImmutableArray) is not a dividend schedule; an empty one is []", nameof(dividends));
        }

        if (dividends.Any(d => d is null))
        {
            throw new ArgumentException("null dividend in schedule", nameof(dividends));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(tGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(xGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(vGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(dampingSteps);
    }

    // QuantLib's calculate after the cache lookup: the solve, read at the spot and v0
    private static OneAssetOptionResults Solve(
        VanillaOption option,
        EvaluationContext context,
        HestonModel model,
        ImmutableArray<Dividend> dividends,
        FdmQuantoHelper? quantoHelper,
        int tGrid,
        int xGrid,
        int vGrid,
        int dampingSteps,
        FdmSchemeDesc? schemeDesc,
        LocalVolTermStructure? leverageFct,
        double mixingFactor,
        IReadOnlyList<double>? strikes = null)
    {
        HestonProcess process = model.Process;
        FdmHestonSolver solver = BuildSolver(
            option, context, model, dividends, quantoHelper, tGrid, xGrid, vGrid, dampingSteps, schemeDesc, leverageFct,
            mixingFactor, strikes);

        double v0 = process.V0;
        double spot = process.S0;

        return new OneAssetOptionResults
        {
            NPV = solver.ValueAt(spot, v0),
            Delta = solver.DeltaAt(spot, v0),
            Gamma = solver.GammaAt(spot, v0),
            Theta = solver.ThetaAt(spot, v0),
        };
    }

    private static FdmHestonSolver BuildSolver(
        VanillaOption option,
        EvaluationContext context,
        HestonModel model,
        ImmutableArray<Dividend> dividends,
        FdmQuantoHelper? quantoHelper,
        int tGrid,
        int xGrid,
        int vGrid,
        int dampingSteps,
        FdmSchemeDesc? schemeDesc,
        LocalVolTermStructure? leverageFct,
        double mixingFactor,
        IReadOnlyList<double>? strikes)
    {
        HestonProcess process = model.Process;
        TermStructure clock = FdmHestonOp.Clock(process, quantoHelper, leverageFct);
        return new FdmHestonSolver(
            process,
            GetSolverDesc(
                option, context, model, dividends, quantoHelper, tGrid, xGrid, vGrid, dampingSteps, leverageFct,
                mixingFactor, strikes, clock),
            schemeDesc ?? FdmSchemeDesc.Hundsdorfer(),
            quantoHelper,
            leverageFct,
            mixingFactor);
    }
}
