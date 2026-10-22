// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ralph Schreyer
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/barrier/fdblackscholesbarrierengine.hpp ql/pricingengines/barrier/fdblackscholesbarrierengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.PricingEngines;

/// <summary>
/// The finite-difference engine for European barrier options: a knock-out on a grid bounded by the barrier, its rebate
/// the Dirichlet boundary value; a knock-in as the vanilla option, plus the rebate, less the knock-out.
/// </summary>
/// <remarks>
/// QuantLib's engine, its times read on the engine's clock (<see cref="FdmBlackScholesOp.Clock"/>), the earliest
/// reference date of the structures it reads, as S-eq's finite-difference vanilla engine reads them: the maturity and
/// the dividend handler's reference date and day counter, where QuantLib takes the maturity from the process and the
/// handler's from the risk-free curve, so that a knock-in's three parts price on one clock. Where every structure
/// shares the risk-free curve's date and day counter, QuantLib's times stand.
/// </remarks>
public static class FdBlackScholesBarrierEngine
{
    /// <summary>Prices a barrier option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context, which the dividends' amounts take.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="dividends">The cash dividends; none when <c>default</c>.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of points of the grid.</param>
    /// <param name="dampingSteps">The number of damping steps.</param>
    /// <param name="schemeDesc">The scheme; QuantLib's Douglas scheme when <see langword="null"/>.</param>
    /// <param name="localVol">Whether to use the local volatility.</param>
    /// <param name="illegalLocalVolOverwrite">The volatility where the local volatility can't be computed.</param>
    /// <returns>The option's results: its value, delta, gamma and theta; the expired results when the option has
    /// expired.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="option"/>, <paramref name="context"/> or
    /// <paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size or the damping steps are negative.</exception>
    /// <exception cref="ArgumentException">The dividends hold a null one.</exception>
    /// <exception cref="NanoQLException">
    /// The strike isn't positive, the exercise isn't European, the spot isn't positive or lies beyond the barrier
    /// ("barrier touched"), or the grid or solver fails.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        BarrierOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        ImmutableArray<Dividend> dividends = default,
        int tGrid = 100,
        int xGrid = 100,
        int dampingSteps = 0,
        FdmSchemeDesc? schemeDesc = null,
        bool localVol = false,
        double? illegalLocalVolOverwrite = null)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        ImmutableArray<Dividend> schedule = Dividends(dividends, nameof(dividends));
        ArgumentOutOfRangeException.ThrowIfNegative(tGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(xGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(dampingSteps);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        OneAssetOptionResults results = Body(
            option,
            context,
            process,
            schedule,
            tGrid,
            xGrid,
            dampingSteps,
            schemeDesc ?? FdmSchemeDesc.Douglas(),
            localVol,
            illegalLocalVolOverwrite);
        return option.FetchResults(results);
    }

    internal static OneAssetOptionResults Body(
        BarrierOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        ImmutableArray<Dividend> dividends,
        int tGrid,
        int xGrid,
        int dampingSteps,
        FdmSchemeDesc schemeDesc,
        bool localVol,
        double? illegalLocalVolOverwrite)
    {
        // 1. Mesher; the times the engine's clock's (FdmBlackScholesOp.Clock)
        var payoff = (StrikedTypePayoff)option.Payoff!;
        if (!(payoff.Strike > 0.0))
        {
            throw new NanoQLException("strike must be positive");
        }

        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("only european style option are supported");
        }

        double spot = process.X0;
        if (!(spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        if (option.Triggered(spot))
        {
            throw new NanoQLException("barrier touched");
        }

        TermStructure clock = FdmBlackScholesOp.Clock(process);
        double maturity = clock.TimeFromReference(option.Exercise.LastDate());
        double? xMin = null;
        double? xMax = null;
        if (option.BarrierType is BarrierType.DownIn or BarrierType.DownOut)
        {
            xMin = Math.Log(option.Barrier);
        }

        if (option.BarrierType is BarrierType.UpIn or BarrierType.UpOut)
        {
            xMax = Math.Log(option.Barrier);
        }

        var equityMesher = new FdmBlackScholesMesher(
            xGrid, process, maturity, payoff.Strike, xMin, xMax, 0.0001, 1.5, (
                null, null), dividends, null, 0.0, context);

        var mesher = new FdmMesherComposite(equityMesher);

        // 2. Calculator
        var calculator = new FdmLogInnerValue(payoff, mesher, 0);

        // 3. Step conditions
        var stepConditions = new List<StepCondition>();
        var stoppingTimes = new List<IEnumerable<double>>();

        // 3.1 Step condition if discrete dividends
        var dividendCondition = new FdmDividendHandler(
            dividends, mesher, clock.ReferenceDate, clock.DayCounter, 0, context);

        if (!dividends.IsEmpty)
        {
            stepConditions.Add(dividendCondition);

            // this effectively excludes times after maturity
            stoppingTimes.Add([.. dividendCondition.DividendTimes.Select(t => Math.Min(maturity, t))]);
        }

        var conditions = new FdmStepConditionComposite(stoppingTimes, stepConditions);

        // 4. Boundary conditions
        ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries = Boundaries(option, mesher);

        // 5. Solver
        var solverDesc = new FdmSolverDesc(mesher, boundaries, conditions, calculator, maturity, tGrid, dampingSteps);

        var solver = new FdmBlackScholesSolver(
            process, payoff.Strike, solverDesc, schemeDesc, localVol, illegalLocalVolOverwrite, null);

        double value = solver.ValueAt(spot);
        double delta = solver.DeltaAt(spot);
        double gamma = solver.GammaAt(spot);
        double? theta = solver.ThetaAt(spot);

        // 6. Calculate vanilla option and rebate for in-barriers
        if (option.BarrierType is BarrierType.DownIn or BarrierType.UpIn)
        {
            // Calculate the vanilla option
            var vanillaOption = new VanillaOption(payoff, option.Exercise);
            OneAssetOptionResults vanilla = FdBlackScholesVanillaEngine.Body(
                vanillaOption,
                context,
                process,
                dividends,
                null,
                tGrid,
                xGrid,
                0, // dampingSteps
                schemeDesc,
                localVol,
                illegalLocalVolOverwrite,
                FdBlackScholesVanillaEngineCashDividendModel.Spot);

            // Calculate the rebate value
            const int minGridSize = 50;
            int rebateDampingSteps = (dampingSteps > 0) ? Math.Min(1, dampingSteps / 2) : 0;
            OneAssetOptionResults rebate = FdBlackScholesRebateEngine.Body(
                option,
                context,
                process,
                dividends,
                tGrid,
                Math.Max(minGridSize, xGrid / 5),
                rebateDampingSteps,
                schemeDesc,
                localVol,
                illegalLocalVolOverwrite);

            value = vanilla.NPV!.Value + rebate.NPV!.Value - value;
            delta = vanilla.Delta!.Value + rebate.Delta!.Value - delta;
            gamma = vanilla.Gamma!.Value + rebate.Gamma!.Value - gamma;
            theta = vanilla.Theta + rebate.Theta - theta;
        }

        return new OneAssetOptionResults { NPV = value, Delta = delta, Gamma = gamma, Theta = theta };
    }

    // The Dirichlet boundary at the barrier, the rebate its value: QuantLib's step 4, shared by the rebate engine.
    internal static ImmutableArray<BoundaryCondition<FdmLinearOp>> Boundaries(BarrierOption option, FdmMesher mesher)
    {
        ImmutableArray<BoundaryCondition<FdmLinearOp>>.Builder boundaries =
            ImmutableArray.CreateBuilder<BoundaryCondition<FdmLinearOp>>();
        if (option.BarrierType is BarrierType.DownIn or BarrierType.DownOut)
        {
            boundaries.Add(new FdmDirichletBoundary(mesher, option.Rebate, 0, BoundaryConditionSide.Lower));
        }

        if (option.BarrierType is BarrierType.UpIn or BarrierType.UpOut)
        {
            boundaries.Add(new FdmDirichletBoundary(mesher, option.Rebate, 0, BoundaryConditionSide.Upper));
        }

        return boundaries.ToImmutable();
    }

    // The schedule given: none for default, a null dividend refused.
    internal static ImmutableArray<Dividend> Dividends(ImmutableArray<Dividend> dividends, string paramName)
    {
        ImmutableArray<Dividend> schedule = dividends.IsDefault ? [] : dividends;
        if (schedule.Any(d => d is null))
        {
            throw new ArgumentException("null dividend in schedule", paramName);
        }

        return schedule;
    }
}
