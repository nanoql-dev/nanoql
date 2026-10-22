// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/barrier/fdblackscholesrebateengine.hpp ql/pricingengines/barrier/fdblackscholesrebateengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.PricingEngines;

/// <summary>
/// The finite-difference engine for a barrier option's rebate: the value of the rebate paid at the hit, on a grid
/// bounded by the barrier, with the rebate as its Dirichlet boundary value and a zero payoff at expiry.
/// </summary>
/// <remarks>
/// QuantLib's engine, its times read on the engine's clock (<see cref="FdmBlackScholesOp.Clock"/>), the earliest
/// reference date of the structures it reads, as S-eq's finite-difference vanilla engine reads them: the maturity, and
/// the dividends' and exercise's step conditions' reference date and day counter, where QuantLib takes the maturity
/// from the process and the step conditions' from the risk-free curve. Where every structure shares the risk-free
/// curve's date and day counter, QuantLib's times stand.
/// </remarks>
public static class FdBlackScholesRebateEngine
{
    /// <summary>Prices a barrier option's rebate.</summary>
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
    /// <returns>The rebate's results: its value, delta, gamma and theta; the expired results when the option has
    /// expired.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="option"/>, <paramref name="context"/> or
    /// <paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size or the damping steps are negative.</exception>
    /// <exception cref="ArgumentException">The dividends hold a null one.</exception>
    /// <exception cref="NanoQLException">The exercise isn't European, or the grid or solver fails.</exception>
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
        ImmutableArray<Dividend> schedule = FdBlackScholesBarrierEngine.Dividends(dividends, nameof(dividends));
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
        var rebatePayoff = new CashOrNothingPayoff(OptionType.Call, 0.0, option.Rebate);
        var calculator = new FdmLogInnerValue(rebatePayoff, mesher, 0);

        // 3. Step conditions
        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("only european style option are supported");
        }

        FdmStepConditionComposite conditions = FdmStepConditionComposite.VanillaComposite(
            dividends, option.Exercise, mesher, calculator, clock.ReferenceDate, clock.DayCounter, context);

        // 4. Boundary conditions
        ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries = FdBlackScholesBarrierEngine.Boundaries(
            option, mesher);

        // 5. Solver
        var solverDesc = new FdmSolverDesc(mesher, boundaries, conditions, calculator, maturity, tGrid, dampingSteps);

        var solver = new FdmBlackScholesSolver(
            process, payoff.Strike, solverDesc, schemeDesc, localVol, illegalLocalVolOverwrite, null);

        double spot = process.X0;
        return new OneAssetOptionResults
        {
            NPV = solver.ValueAt(spot),
            Delta = solver.DeltaAt(spot),
            Gamma = solver.GammaAt(spot),
            Theta = solver.ThetaAt(spot),
        };
    }
}
