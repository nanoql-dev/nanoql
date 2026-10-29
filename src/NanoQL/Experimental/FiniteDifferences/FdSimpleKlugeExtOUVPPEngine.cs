// SPDX-FileCopyrightText: Copyright (C) 2011, 2012 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdsimpleklugeextouvppengine.hpp ql/experimental/finitedifferences/fdsimpleklugeextouvppengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// The four-dimensional finite-difference engine of a <see cref="VanillaVPPOption"/> on a <see cref="KlugeExtOUProcess"/>:
/// Kluge's power e^(f_p(t) + X + Y) and the fuel price e^(f_g(t) + G), X's and G's meshers from their extended
/// Ornstein-Uhlenbeck processes, Y's an exponential jump mesher, and the plant's states in the fourth direction, run by
/// its step condition at the exercise dates.
/// </summary>
/// <remarks>
/// The exercise times are read on the discount curve's day counter from its reference date, as QuantLib's; the maturity
/// is the last. The value is the best of the plant's states at today's spot, each read 1e-8 inside the state mesher's
/// ends. A time past a seasonal shape's last is refused.
/// </remarks>
[Experimental("NQL0001")]
public static class FdSimpleKlugeExtOUVPPEngine
{
    /// <summary>Prices a plant.</summary>
    /// <param name="option">The plant.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The process.</param>
    /// <param name="rTS">The discount curve.</param>
    /// <param name="fuelShape">The fuel price's seasonal shape, or <see langword="null"/> for none.</param>
    /// <param name="powerShape">The power price's seasonal shape, or <see langword="null"/> for none.</param>
    /// <param name="fuelCostAddon">The cost added to each unit of fuel.</param>
    /// <param name="tGrid">The time steps.</param>
    /// <param name="xGrid">X's mesher points.</param>
    /// <param name="yGrid">Y's mesher points.</param>
    /// <param name="gGrid">The fuel price's mesher points.</param>
    /// <param name="schemeDesc">The scheme, or <see langword="null"/> for Hundsdorfer's.</param>
    /// <returns>The results: the value alone.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size is negative.</exception>
    /// <exception cref="NanoQLException">The plant is refused, or a time is past a shape's last.</exception>
    public static MultiAssetOptionResults Calculate(
        VanillaVPPOption option,
        EvaluationContext context,
        KlugeExtOUProcess process,
        YieldTermStructure rTS,
        IReadOnlyList<(double Time, double Value)>? fuelShape,
        IReadOnlyList<(double Time, double Value)>? powerShape,
        double fuelCostAddon,
        int tGrid = 1,
        int xGrid = 50,
        int yGrid = 10,
        int gGrid = 20,
        FdmSchemeDesc? schemeDesc = null)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(rTS);
        ArgumentOutOfRangeException.ThrowIfNegative(tGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(xGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(yGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(gGrid);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();

        // QuantLib's "Swing exercise supported only" can't fail: the plant's exercise is a swing one
        var stepConditionFactory = new FdmVPPStepConditionFactory(option);

        // 1. Exercise definition
        ImmutableArray<double> exerciseTimes = option.SwingExercise.ExerciseTimes(rTS.DayCounter, rTS.ReferenceDate);

        // 2. Mesher
        double maturity = exerciseTimes[^1];
        ExtOUWithJumpsProcess klugeProcess = process.KlugeProcess;
        var xMesher = new FdmSimpleProcess1dMesher(xGrid, klugeProcess.ExtendedOrnsteinUhlenbeckProcess, maturity);
        var yMesher = new ExponentialJump1dMesher(yGrid, klugeProcess.Beta, klugeProcess.JumpIntensity, klugeProcess.Eta, 1e-3);
        var gMesher = new FdmSimpleProcess1dMesher(gGrid, process.ExtOUProcess, maturity);
        Fdm1dMesher exerciseMesher = stepConditionFactory.StateMesher();
        var mesher = new FdmMesherComposite(xMesher, yMesher, gMesher, exerciseMesher);

        // 3. Calculator
        var zeroInnerValue = new FdmZeroInnerValue();
        var zeroStrikeCall = new PlainVanillaPayoff(OptionType.Call, 0.0);
        var fuelPrice = new FdmExpExtOUInnerValueCalculator(zeroStrikeCall, mesher, fuelShape, 2);
        var powerPrice = new FdmExtOUJumpModelInnerValue(zeroStrikeCall, mesher, powerShape);
        var sparkSpread = new FdmSpreadPayoffInnerValue((BasketPayoff)option.Payoff!, powerPrice, fuelPrice);

        // 4. Step conditions: the plant's, at the exercise times
        var mesh = new FdmVPPStepConditionMesher(3, mesher);
        FdmVPPStepCondition stepCondition = stepConditionFactory.Build(mesh, fuelCostAddon, fuelPrice, sparkSpread);
        var conditions = new FdmStepConditionComposite([exerciseTimes], [stepCondition]);

        // 5. Boundary conditions
        ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries = [];

        // 6. Solver
        var solverDesc = new FdmSolverDesc(mesher, boundaries, conditions, zeroInnerValue, maturity, tGrid, 0);
        var solver = new FdmKlugeExtOUSolver(process, rTS, solverDesc, schemeDesc ?? FdmSchemeDesc.Hundsdorfer());
        Numerics.Vector x0 = process.InitialValues();
        double[] x = [x0[0], x0[1], x0[2], 0.0];
        const double tol = 1e-8;
        double maxExerciseValue = exerciseMesher.Location(exerciseMesher.Size - 1);
        double minExerciseValue = exerciseMesher.Location(0);
        var results = new double[exerciseMesher.Size];
        for (int i = 0; i < results.Length; ++i)
        {
            x[3] = Math.Max(minExerciseValue + tol, Math.Min(exerciseMesher.Location(i), maxExerciseValue - tol));
            results[i] = solver.ValueAt(x);
        }

        return option.FetchResults(new MultiAssetOptionResults { NPV = stepCondition.MaxValue(results) });
    }
}
