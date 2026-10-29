// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdklugeextouspreadengine.hpp ql/experimental/finitedifferences/fdklugeextouspreadengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// The three-dimensional finite-difference engine of a spread option between Kluge's power e^(f_p(t) + X + Y) and the
/// gas price e^(f_g(t) + U) of a <see cref="KlugeExtOUProcess"/>: X's and U's meshers from their extended
/// Ornstein-Uhlenbeck processes, Y's an exponential jump mesher, the basket payoff on power and gas, and early exercise
/// by the vanilla step conditions.
/// </summary>
/// <remarks>
/// QuantLib's engine takes the vanilla option's arguments and casts its payoff to a basket payoff; its case prices a
/// basket option, which is the instrument here. The maturity and the step conditions' times are read on the discount
/// curve's day counter from its reference date, as QuantLib's. A time past a seasonal shape's last is refused.
/// </remarks>
[Experimental("NQL0001")]
public static class FdKlugeExtOUSpreadEngine
{
    /// <summary>Prices a spread option.</summary>
    /// <param name="option">The option, its payoff a basket payoff on power and gas.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="klugeOUProcess">The process.</param>
    /// <param name="rTS">The discount curve.</param>
    /// <param name="tGrid">The time steps.</param>
    /// <param name="xGrid">X's mesher points.</param>
    /// <param name="yGrid">Y's mesher points.</param>
    /// <param name="uGrid">The gas price's mesher points.</param>
    /// <param name="gasShape">The gas price's seasonal shape, or <see langword="null"/> for none.</param>
    /// <param name="powerShape">The power price's seasonal shape, or <see langword="null"/> for none.</param>
    /// <param name="schemeDesc">The scheme, or <see langword="null"/> for Hundsdorfer's.</param>
    /// <returns>The results: the value alone.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size is negative.</exception>
    /// <exception cref="NanoQLException">The option is refused, or a time is past a shape's last.</exception>
    public static MultiAssetOptionResults Calculate(
        BasketOption option,
        EvaluationContext context,
        KlugeExtOUProcess klugeOUProcess,
        YieldTermStructure rTS,
        int tGrid = 25,
        int xGrid = 50,
        int yGrid = 10,
        int uGrid = 25,
        IReadOnlyList<(double Time, double Value)>? gasShape = null,
        IReadOnlyList<(double Time, double Value)>? powerShape = null,
        FdmSchemeDesc? schemeDesc = null)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(klugeOUProcess);
        ArgumentNullException.ThrowIfNull(rTS);
        ArgumentOutOfRangeException.ThrowIfNegative(tGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(xGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(yGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(uGrid);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();

        // 1. Mesher
        double maturity = rTS.DayCounter.YearFraction(rTS.ReferenceDate, option.Exercise.LastDate());
        ExtOUWithJumpsProcess klugeProcess = klugeOUProcess.KlugeProcess;
        var xMesher = new FdmSimpleProcess1dMesher(xGrid, klugeProcess.ExtendedOrnsteinUhlenbeckProcess, maturity);
        var yMesher = new ExponentialJump1dMesher(yGrid, klugeProcess.Beta, klugeProcess.JumpIntensity, klugeProcess.Eta);
        var uMesher = new FdmSimpleProcess1dMesher(uGrid, klugeOUProcess.ExtOUProcess, maturity);
        var mesher = new FdmMesherComposite(xMesher, yMesher, uMesher);

        // 2. Calculator
        if (option.Payoff is not BasketPayoff basketPayoff)
        {
            throw new NanoQLException(" basket payoff expected");
        }

        var zeroStrikeCall = new PlainVanillaPayoff(OptionType.Call, 0.0);
        var gasPrice = new FdmExpExtOUInnerValueCalculator(zeroStrikeCall, mesher, gasShape, 2);
        var powerPrice = new FdmExtOUJumpModelInnerValue(zeroStrikeCall, mesher, powerShape);
        var calculator = new FdmSpreadPayoffInnerValue(basketPayoff, powerPrice, gasPrice);

        // 3. Step conditions
        FdmStepConditionComposite conditions = FdmStepConditionComposite.VanillaComposite(
            [], option.Exercise, mesher, calculator, rTS.ReferenceDate, rTS.DayCounter, context);

        // 4. Boundary conditions
        ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries = [];

        // 5. Solver
        var solverDesc = new FdmSolverDesc(mesher, boundaries, conditions, calculator, maturity, tGrid, 0);
        var solver = new FdmKlugeExtOUSolver(klugeOUProcess, rTS, solverDesc, schemeDesc ?? FdmSchemeDesc.Hundsdorfer());
        Numerics.Vector x0 = klugeOUProcess.InitialValues();
        return option.FetchResults(new MultiAssetOptionResults { NPV = solver.ValueAt([x0[0], x0[1], x0[2]]) });
    }
}
