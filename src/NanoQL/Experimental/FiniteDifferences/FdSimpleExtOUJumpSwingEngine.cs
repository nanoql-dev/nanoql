// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdsimpleextoujumpswingengine.hpp ql/experimental/finitedifferences/fdsimpleextoujumpswingengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// The three-dimensional finite-difference engine of a swing option on Kluge's spot e^(f(t) + X + Y): X's mesher from
/// its extended Ornstein-Uhlenbeck process, Y's an exponential jump mesher, and the exercises taken in the third
/// direction by the simple swing condition.
/// </summary>
/// <remarks>
/// The exercise times are read on the discount curve's day counter from its reference date, as QuantLib's; the
/// maturity is the last. A time past the seasonal shape's last is refused (<see cref="FdmExtOUJumpModelInnerValue"/>).
/// </remarks>
[Experimental("NQL0001")]
public static class FdSimpleExtOUJumpSwingEngine
{
    /// <summary>Prices a swing option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The process.</param>
    /// <param name="rTS">The discount curve.</param>
    /// <param name="tGrid">The time steps.</param>
    /// <param name="xGrid">X's mesher points.</param>
    /// <param name="yGrid">Y's mesher points.</param>
    /// <param name="shape">The seasonal shape, (time, value) pairs by increasing time, or <see langword="null"/> for
    /// none.</param>
    /// <param name="schemeDesc">The scheme, or <see langword="null"/> for Hundsdorfer's.</param>
    /// <returns>The results: the value alone.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size is negative.</exception>
    /// <exception cref="NanoQLException">The option is refused, or a time is past the shape's last.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaSwingOption option,
        EvaluationContext context,
        ExtOUWithJumpsProcess process,
        YieldTermStructure rTS,
        int tGrid = 50,
        int xGrid = 200,
        int yGrid = 50,
        IReadOnlyList<(double Time, double Value)>? shape = null,
        FdmSchemeDesc? schemeDesc = null)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(rTS);
        ArgumentOutOfRangeException.ThrowIfNegative(tGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(xGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(yGrid);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();

        // 1. Exercise: QuantLib's "Swing exercise supported only" can't fail, the option's exercise being a swing one
        // 2. Mesher
        ImmutableArray<double> exerciseTimes = option.SwingExercise.ExerciseTimes(rTS.DayCounter, rTS.ReferenceDate);
        double maturity = exerciseTimes[^1];
        var xMesher = new FdmSimpleProcess1dMesher(xGrid, process.ExtendedOrnsteinUhlenbeckProcess, maturity);
        var yMesher = new ExponentialJump1dMesher(yGrid, process.Beta, process.JumpIntensity, process.Eta);
        var exerciseMesher = new Uniform1dMesher(0, option.MaxExerciseRights, option.MaxExerciseRights + 1);
        var mesher = new FdmMesherComposite(xMesher, yMesher, exerciseMesher);

        // 3. Calculator
        var calculator = new FdmZeroInnerValue();

        // 4. Step conditions: the Bermudan exercises, taken by the swing condition (the payoff striked, as validated)
        var exerciseCalculator = new FdmExtOUJumpModelInnerValue(option.StrikedPayoff!, mesher, shape);
        var conditions = new FdmStepConditionComposite(
            [exerciseTimes],
            [new FdmSimpleSwingCondition(exerciseTimes, mesher, exerciseCalculator, 2, option.MinExerciseRights)]);

        // 5. Boundary conditions
        ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries = [];

        // 6. Solver
        var solverDesc = new FdmSolverDesc(mesher, boundaries, conditions, calculator, maturity, tGrid, 0);
        var solver = new FdmSimple3dExtOUJumpSolver(process, rTS, solverDesc, schemeDesc ?? FdmSchemeDesc.Hundsdorfer());
        Numerics.Vector x0 = process.InitialValues();
        return option.FetchResults(new OneAssetOptionResults { NPV = solver.ValueAt(x0[0], x0[1], 0.0) });
    }
}
