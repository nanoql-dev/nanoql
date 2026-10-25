// SPDX-FileCopyrightText: Copyright (C) 2020 Lew Wei Hao
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/fdcirvanillaengine.hpp ql/pricingengines/vanilla/fdcirvanillaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The finite-difference pricing engine for vanilla options, European, American or Bermudan, on a Black-Scholes equity
/// under a Cox-Ingersoll-Ross short rate, with cash dividends and optionally quanto: <see cref="FdmCIRSolver"/> on
/// <see cref="FdmBlackScholesMesher"/>'s grid of the log spot by a uniform grid of the short rate.
/// </summary>
/// <remarks>
/// <para>
/// The engine reports the value, delta, gamma and theta; the theta is <see langword="null"/> when the problem's first
/// stopping time is time 0, as QuantLib's is. Its finite-difference working types are built inside each calculation
/// and dropped after it (the S-eq plan's SEQR1). Its times run on <see cref="FdmBlackScholesOp.Clock"/>, from the
/// earliest reference date of the equity's structures, each accruing nothing before its own date; where none is dated
/// before the risk-free curve, they are QuantLib's.
/// </para>
/// <para>
/// QuantLib's engine prices another model: its operator's rate diffusion is σ²r where the CIR generator's is ½σ²r, and
/// its covariance 2ρσ_Sσ where it is ρσ_Sσ√r (<see cref="FdmCIROp"/>), so that its rate has twice the CIR variance
/// and it blows up at ρ ≠ 0; and its rate grid, <see cref="FdmSimpleProcess1dMesher"/>'s time-averaged quantiles, stops
/// above zero and, where the rate drifts or σ is small, leaves the rate's path (3.58 to 15.7 where the
/// deterministic-rate limit is 4.21 and 3.28). Here the CIR generator's coefficients, and the rate grid uniform on
/// [0, max(upper, r₀, θ, E[r_T])], upper the last point of QuantLib's grid, where at r = 0 the equation is its own
/// boundary condition (the S-van plan's VR4 (xv), VR8). The default scheme is Hundsdorfer's, where QuantLib's
/// modified Hundsdorfer blows up on fine grids when the rate drifts up fast (VR8); a scheme the caller names is used as
/// given. With a quanto helper the operator applies its adjustment (<see cref="FdmCIROp"/>'s remarks), where QuantLib
/// passes the helper to the mesher alone. Documented divergences: <c>Divergences/FdCirModelTests.cs</c>.
/// </para>
/// </remarks>
public static class FdCIRVanillaEngine
{
    /// <summary>Prices a vanilla option without dividends.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="cirProcess">The short rate's process.</param>
    /// <param name="bsProcess">The equity's process.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of the log spot's points.</param>
    /// <param name="rGrid">The number of the short rate's points.</param>
    /// <param name="dampingSteps">The number of implicit damping steps.</param>
    /// <param name="rho">The correlation of the two factors.</param>
    /// <param name="schemeDesc">The scheme; <see langword="null"/> for Hundsdorfer (VR8).</param>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/> for none.</param>
    /// <returns>The option's results; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or a process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size or the damping steps is negative.</exception>
    /// <exception cref="ArgumentException">A grid is too small, or the rollback's arguments are refused.</exception>
    /// <exception cref="NanoQLException">The payoff isn't striked, or a step or the spline fails.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        CoxIngersollRossProcess cirProcess,
        GeneralizedBlackScholesProcess bsProcess,
        int tGrid,
        int xGrid,
        int rGrid,
        int dampingSteps,
        double rho,
        FdmSchemeDesc? schemeDesc = null,
        FdmQuantoHelper? quantoHelper = null) =>
        Calculate(
            option, context, cirProcess, bsProcess, [], tGrid, xGrid, rGrid, dampingSteps, rho, schemeDesc, quantoHelper);

    /// <summary>Prices a vanilla option with cash dividends.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="cirProcess">The short rate's process.</param>
    /// <param name="bsProcess">The equity's process.</param>
    /// <param name="dividends">The cash dividends, by the spot model.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of the log spot's points.</param>
    /// <param name="rGrid">The number of the short rate's points.</param>
    /// <param name="dampingSteps">The number of implicit damping steps.</param>
    /// <param name="rho">The correlation of the two factors.</param>
    /// <param name="schemeDesc">The scheme; <see langword="null"/> for Hundsdorfer (VR8).</param>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/> for none.</param>
    /// <returns>The option's results; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or a process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size or the damping steps is negative.</exception>
    /// <exception cref="ArgumentException">
    /// The schedule is the default array or holds <see langword="null"/>, a grid is too small, or the rollback's
    /// arguments are refused.
    /// </exception>
    /// <exception cref="NanoQLException">The payoff isn't striked, or a step or the spline fails.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        CoxIngersollRossProcess cirProcess,
        GeneralizedBlackScholesProcess bsProcess,
        ImmutableArray<Dividend> dividends,
        int tGrid,
        int xGrid,
        int rGrid,
        int dampingSteps,
        double rho,
        FdmSchemeDesc? schemeDesc = null,
        FdmQuantoHelper? quantoHelper = null)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(cirProcess);
        ArgumentNullException.ThrowIfNull(bsProcess);
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
        ArgumentOutOfRangeException.ThrowIfNegative(rGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(dampingSteps);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();

        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        var solver = new FdmCIRSolver(
            cirProcess,
            bsProcess,
            GetSolverDesc(option, context, cirProcess, bsProcess, dividends, tGrid, xGrid, rGrid, dampingSteps, quantoHelper),
            schemeDesc ?? FdmSchemeDesc.Hundsdorfer(),
            rho,
            payoff.Strike,
            quantoHelper);

        double r0 = cirProcess.X0;
        double spot = bsProcess.X0;

        return option.FetchResults(new OneAssetOptionResults
        {
            NPV = solver.ValueAt(spot, r0),
            Delta = solver.DeltaAt(spot, r0),
            Gamma = solver.GammaAt(spot, r0),
            Theta = solver.ThetaAt(spot, r0),
        });
    }

    // QuantLib's getSolverDesc, public there and called only by calculate; its scale factor argument, which it doesn't
    // read, left out
    private static FdmSolverDesc GetSolverDesc(
        VanillaOption option,
        EvaluationContext context,
        CoxIngersollRossProcess cirProcess,
        GeneralizedBlackScholesProcess bsProcess,
        ImmutableArray<Dividend> dividends,
        int tGrid,
        int xGrid,
        int rGrid,
        int dampingSteps,
        FdmQuantoHelper? quantoHelper)
    {
        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        // the times the engine's clock's (FdmBlackScholesOp.Clock), the process's where no structure is dated before
        // the risk-free curve
        TermStructure clock = FdmBlackScholesOp.Clock(bsProcess, quantoHelper);
        double maturity = clock.TimeFromReference(option.Exercise.LastDate());

        // The short rate mesher: uniform from 0 to past QuantLib's upper end, r0, theta and E[r_T] (VR4 (xv), VR8)
        var processMesher = new FdmSimpleProcess1dMesher(rGrid, cirProcess, maturity, tGrid);
        double upper = processMesher.Location(rGrid - 1);
        double expected = cirProcess.Expectation(0.0, cirProcess.X0, maturity);
        double rMax = Math.Max(Math.Max(upper, cirProcess.X0), Math.Max(cirProcess.Level, expected));
        var shortRateMesher = new Uniform1dMesher(0.0, rMax, rGrid);

        // The equity mesher
        var equityMesher = new FdmBlackScholesMesher(
            xGrid,
            bsProcess,
            maturity,
            payoff.Strike,
            null,
            null,
            0.0001,
            1.5,
            (payoff.Strike, 0.1),
            dividends,
            quantoHelper,
            0.0,
            context);

        var mesher = new FdmMesherComposite(equityMesher, shortRateMesher);

        // Calculator
        var calculator = new FdmLogInnerValue(option.Payoff, mesher, 0);

        // Step conditions
        FdmStepConditionComposite conditions = FdmStepConditionComposite.VanillaComposite(
            dividends, option.Exercise, mesher, calculator, clock.ReferenceDate, clock.DayCounter, context);

        // Boundary conditions
        ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries = [];

        // Solver
        return new FdmSolverDesc(mesher, boundaries, conditions, calculator, maturity, tGrid, dampingSteps);
    }
}
