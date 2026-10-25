// SPDX-FileCopyrightText: Copyright (C) 2010 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/fdsimplebsswingengine.hpp ql/pricingengines/vanilla/fdsimplebsswingengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.PricingEngines;

/// <summary>
/// The finite-difference pricing engine for swing options under the Black-Scholes process: the Black-Scholes operator
/// on a grid of the log spot by the rights used, the swing condition at each exercise time
/// (<see cref="FdmSimpleSwingCondition"/>).
/// </summary>
/// <remarks>
/// <para>
/// The engine reports the value, delta, gamma and theta, the delta and gamma by central differences at 1% of the spot;
/// the theta is <see langword="null"/> when the problem's first stopping time is time 0, as QuantLib's is. Its
/// finite-difference working types are built inside each calculation and dropped after it (the S-eq plan's SEQR1).
/// </para>
/// <para>
/// The exercises are timed by the swing exercise's date times (<see cref="SwingExercise.ExerciseTimes"/>), seconds
/// included, on <see cref="FdmBlackScholesOp.Clock"/>, the maturity the last of them, where QuantLib times each by its
/// date alone and the maturity by the last date, so that 60 half-daily exercises price as 30 daily ones (the S-van
/// plan's VR4 (xvi)). On whole dates and where no structure is dated before the risk-free curve, QuantLib's times.
/// </para>
/// </remarks>
public static class FdSimpleBSSwingEngine
{
    /// <summary>Prices a swing option.</summary>
    /// <param name="option">The swing option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of the underlying's points.</param>
    /// <param name="schemeDesc">The scheme; <see langword="null"/> for Douglas, QuantLib's default.</param>
    /// <returns>The option's results; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size is negative.</exception>
    /// <exception cref="ArgumentException">
    /// An exercise is before the clock's reference date, or a grid is too small (a maximum of no rights among them).
    /// </exception>
    /// <exception cref="NanoQLException">The option's checks fail, the payoff isn't striked, or a step or the spline
    /// fails.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaSwingOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int tGrid = 50,
        int xGrid = 100,
        FdmSchemeDesc? schemeDesc = null)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentOutOfRangeException.ThrowIfNegative(tGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(xGrid);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(Body(option, context, process, tGrid, xGrid, schemeDesc ?? FdmSchemeDesc.Douglas()));
    }

    private static OneAssetOptionResults Body(
        VanillaSwingOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int tGrid,
        int xGrid,
        FdmSchemeDesc schemeDesc)
    {
        // QuantLib's "Bermudan exercise supported only" can't fail: a swing exercise is Bermudan
        // 1. Mesher
        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("Strike type payoff expected");
        }

        // the exercise times, seconds included, on the engine's clock (VR4 (xvi)), the maturity the last of them
        TermStructure clock = FdmBlackScholesOp.Clock(process);
        ImmutableArray<double> exerciseTimes = option.SwingExercise.ExerciseTimes(clock.DayCounter, clock.ReferenceDate);
        double maturity = exerciseTimes[^1];

        var equityMesher = new FdmBlackScholesMesher(xGrid, process, maturity, payoff.Strike, context: context);

        var exerciseMesher = new Uniform1dMesher(0, option.MaxExerciseRights, option.MaxExerciseRights + 1);

        var mesher = new FdmMesherComposite(equityMesher, exerciseMesher);

        // 2. Calculator
        var calculator = new FdmZeroInnerValue();

        // 3. Step conditions
        // 3.1 Bermudan step conditions
        var exerciseCalculator = new FdmLogInnerValue(payoff, mesher, 0);

        var conditions = new FdmStepConditionComposite(
            [exerciseTimes],
            [new FdmSimpleSwingCondition(exerciseTimes, mesher, exerciseCalculator, 1, option.MinExerciseRights)]);

        // 4. Boundary conditions
        ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries = [];

        // 5. Solver
        var solverDesc = new FdmSolverDesc(mesher, boundaries, conditions, calculator, maturity, tGrid, 0);
        var solver = new FdmSimple2dBSSolver(process, payoff.Strike, solverDesc, schemeDesc);

        double spot = process.X0;

        return new OneAssetOptionResults
        {
            NPV = solver.ValueAt(spot, 1),
            Delta = solver.DeltaAt(spot, 1, spot * 0.01),
            Gamma = solver.GammaAt(spot, 1, spot * 0.01),
            Theta = solver.ThetaAt(spot, 1),
        };
    }
}
