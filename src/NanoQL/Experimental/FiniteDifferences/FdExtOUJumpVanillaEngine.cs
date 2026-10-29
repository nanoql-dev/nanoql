// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdextoujumpvanillaengine.hpp ql/experimental/finitedifferences/fdextoujumpvanillaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// The two-dimensional finite-difference engine of a vanilla option on Kluge's spot e^(f(t) + X + Y): X's mesher from
/// its extended Ornstein-Uhlenbeck process, Y's an exponential jump mesher, and early exercise by the vanilla step
/// conditions.
/// </summary>
/// <remarks>
/// The maturity and the step conditions' times are read on the discount curve's day counter from its reference date,
/// as QuantLib's. A time past the seasonal shape's last is refused (<see cref="FdmExtOUJumpModelInnerValue"/>).
/// </remarks>
[Experimental("NQL0001")]
public static class FdExtOUJumpVanillaEngine
{
    /// <summary>Prices an option.</summary>
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
        VanillaOption option,
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

        // 1. Mesher
        double maturity = rTS.DayCounter.YearFraction(rTS.ReferenceDate, option.Exercise.LastDate());
        var xMesher = new FdmSimpleProcess1dMesher(xGrid, process.ExtendedOrnsteinUhlenbeckProcess, maturity);
        var yMesher = new ExponentialJump1dMesher(yGrid, process.Beta, process.JumpIntensity, process.Eta);
        var mesher = new FdmMesherComposite(xMesher, yMesher);

        // 2. Calculator
        var calculator = new FdmExtOUJumpModelInnerValue(option.Payoff ?? throw new NanoQLException("no payoff given"), mesher, shape);

        // 3. Step conditions
        FdmStepConditionComposite conditions = FdmStepConditionComposite.VanillaComposite(
            [], option.Exercise, mesher, calculator, rTS.ReferenceDate, rTS.DayCounter, context);

        // 4. Boundary conditions
        ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries = [];

        // 5. Solver
        var solverDesc = new FdmSolverDesc(mesher, boundaries, conditions, calculator, maturity, tGrid, 0);
        var solver = new FdmExtOUJumpSolver(process, rTS, solverDesc, schemeDesc ?? FdmSchemeDesc.Hundsdorfer());
        Numerics.Vector x0 = process.InitialValues();
        return option.FetchResults(new OneAssetOptionResults { NPV = solver.ValueAt(x0[0], x0[1]) });
    }
}
