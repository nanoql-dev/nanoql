// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/barrier/fdhestonbarrierengine.hpp ql/pricingengines/barrier/fdhestonbarrierengine.cpp @ v1.44
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
/// The finite-difference Heston pricing engine for barrier options: <see cref="FdmHestonSolver"/> on a log-spot grid
/// bounded at the barrier, the rebate its Dirichlet value, by <see cref="FdmHestonLocalVolatilityVarianceMesher"/>'s
/// grid of the variance; a knock-in by in-out parity on the vanilla and the rebate engines.
/// </summary>
/// <remarks>
/// <para>
/// European exercise only, as QuantLib's. The engine reports the value, delta, gamma and theta. Its working types are
/// built inside each calculation, its grids are <see cref="FdHestonVanillaEngine"/>'s, and its times run on
/// <see cref="FdmHestonOp.Clock"/>, from the earliest reference date of the structures it reads, where QuantLib's read
/// the process's time (<c>Divergences/FdHestonBarrierMismatchedTimesTests.cs</c>).
/// </para>
/// <para>
/// Documented divergences. A knock-in's vanilla and rebate legs take the knock-out leg's leverage function and mixing
/// factor, where QuantLib prices them on pure Heston, so a stochastic-local knock-in would mix models
/// (<c>Divergences/FdHestonKnockInLeverageTests.cs</c>). And a spot beyond the barrier is refused before the solve
/// ("barrier touched"), as the family's other engines refuse it, where QuantLib's grid interpolation fails outside its
/// range (<c>Conventions/FdHestonBarrierTouchedTests.cs</c>).
/// </para>
/// </remarks>
public static class FdHestonBarrierEngine
{
    /// <summary>Prices a barrier option.</summary>
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

    /// <summary>Prices a barrier option with cash dividends.</summary>
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
        CheckArguments(option, context, model, dividends, tGrid, xGrid, vGrid, dampingSteps);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        FdmSchemeDesc scheme = schemeDesc ?? FdmSchemeDesc.Hundsdorfer();
        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        // a spot beyond the barrier is refused before the solve
        if (option.Triggered(model.Process.S0))
        {
            throw new NanoQLException("barrier touched");
        }

        bool down = option.BarrierType is BarrierType.DownIn or BarrierType.DownOut;
        OneAssetOptionResults results = Solve(
            option,
            context,
            model,
            dividends,
            payoff,
            payoff,
            down ? Math.Log(option.Barrier) : null,
            down ? null : Math.Log(option.Barrier),
            option.Rebate,
            down,
            !down,
            false,
            tGrid,
            xGrid,
            vGrid,
            dampingSteps,
            scheme,
            leverageFct,
            mixingFactor);

        // 6. Calculate vanilla option and rebate for in-barriers
        if (option.BarrierType is BarrierType.DownIn or BarrierType.UpIn)
        {
            // Calculate the vanilla option, with the knock-out leg's leverage function and mixing factor
            var vanillaOption = new VanillaOption(payoff, option.Exercise);
            OneAssetOptionResults vanilla = FdHestonVanillaEngine.Calculate(
                vanillaOption, context, model, dividends, tGrid, xGrid, vGrid, dampingSteps, scheme, leverageFct, mixingFactor);

            // Calculate the rebate value, likewise
            const int xGridMin = 20;
            const int vGridMin = 10;
            int rebateDampingSteps = (dampingSteps > 0) ? Math.Min(1, dampingSteps / 2) : 0;
            OneAssetOptionResults rebate = FdHestonRebateEngine.Calculate(
                option,
                context,
                model,
                dividends,
                tGrid,
                Math.Max(xGridMin, xGrid / 4),
                Math.Max(vGridMin, vGrid / 4),
                rebateDampingSteps,
                scheme,
                leverageFct,
                mixingFactor);

            results = new OneAssetOptionResults
            {
                NPV = vanilla.NPV + rebate.NPV - results.NPV,
                Delta = vanilla.Delta + rebate.Delta - results.Delta,
                Gamma = vanilla.Gamma + rebate.Gamma - results.Gamma,
                Theta = vanilla.Theta + rebate.Theta - results.Theta,
            };
        }

        return option.FetchResults(results);
    }

    // The family's solve (QuantLib's steps 1 to 5 and the reads at the spot and v0): the variance mesher, the equity
    // mesher on QuantLib's helper process, bounded at the barriers given, the calculator on the payoff, the dividend
    // condition or the vanilla composite, and the Dirichlet boundaries at the rebate, on FdmHestonOp.Clock's times
    internal static OneAssetOptionResults Solve(
        Option option,
        EvaluationContext context,
        HestonModel model,
        ImmutableArray<Dividend> dividends,
        StrikedTypePayoff meshPayoff,
        Payoff innerPayoff,
        double? xMin,
        double? xMax,
        double rebate,
        bool lowerBoundary,
        bool upperBoundary,
        bool vanillaComposite,
        int tGrid,
        int xGrid,
        int vGrid,
        int dampingSteps,
        FdmSchemeDesc scheme,
        LocalVolTermStructure? leverageFct,
        double mixingFactor)
    {
        // 1. Mesher
        HestonProcess process = model.Process;
        TermStructure clock = FdmHestonOp.Clock(process, null, leverageFct);
        double maturity = clock.TimeFromReference(option.Exercise.LastDate());

        // 1.1 The variance mesher
        const int TGridMin = 5;
        int tGridAvgSteps = Math.Max(TGridMin, tGrid / 50);
        var vMesher = new FdmHestonLocalVolatilityVarianceMesher(
            vGrid, process, leverageFct, maturity, tGridAvgSteps, 0.0001, mixingFactor, clock);

        // 1.2 The equity mesher, on QuantLib's helper process, whose curves its call binds in each other's places
        GeneralizedBlackScholesProcess helper = FdHestonVanillaEngine.HelperProcesses(process, vMesher.VolaEstimate, clock).Helper;
        var equityMesher = new FdmBlackScholesMesher(
            xGrid, helper, maturity, meshPayoff.Strike, xMin, xMax, 0.0001, 1.5, (null, null), dividends, null, 0.0, context,
            clock);

        var mesher = new FdmMesherComposite(equityMesher, vMesher);

        // 2. Calculator
        var calculator = new FdmLogInnerValue(innerPayoff, mesher, 0);

        // 3. Step conditions
        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("only european style option are supported");
        }

        FdmStepConditionComposite conditions;
        if (vanillaComposite)
        {
            conditions = FdmStepConditionComposite.VanillaComposite(
                dividends, option.Exercise, mesher, calculator, clock.ReferenceDate, clock.DayCounter, context);
        }
        else
        {
            var stepConditions = new List<StepCondition>();
            var stoppingTimes = new List<IEnumerable<double>>();

            // 3.1 Step condition if discrete dividends
            var dividendCondition = new FdmDividendHandler(
                dividends, mesher, clock.ReferenceDate, clock.DayCounter, 0, context);
            if (!dividends.IsEmpty)
            {
                stepConditions.Add(dividendCondition);
                stoppingTimes.Add([.. dividendCondition.DividendTimes.Select(t => Math.Min(maturity, t))]);
            }

            conditions = new FdmStepConditionComposite(stoppingTimes, stepConditions);
        }

        // 4. Boundary conditions
        ImmutableArray<BoundaryCondition<FdmLinearOp>>.Builder boundaries =
            ImmutableArray.CreateBuilder<BoundaryCondition<FdmLinearOp>>();
        if (lowerBoundary)
        {
            boundaries.Add(new FdmDirichletBoundary(mesher, rebate, 0, BoundaryConditionSide.Lower));
        }

        if (upperBoundary)
        {
            boundaries.Add(new FdmDirichletBoundary(mesher, rebate, 0, BoundaryConditionSide.Upper));
        }

        // 5. Solver
        var solverDesc = new FdmSolverDesc(
            mesher, boundaries.ToImmutable(), conditions, calculator, maturity, tGrid, dampingSteps);
        var solver = new FdmHestonSolver(process, solverDesc, scheme, null, leverageFct, mixingFactor);

        double spot = process.S0;
        double v0 = process.V0;
        return new OneAssetOptionResults
        {
            NPV = solver.ValueAt(spot, v0),
            Delta = solver.DeltaAt(spot, v0),
            Gamma = solver.GammaAt(spot, v0),
            Theta = solver.ThetaAt(spot, v0),
        };
    }

    // the argument checks every engine of the family makes first
    internal static void CheckArguments(
        Option option,
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
}
