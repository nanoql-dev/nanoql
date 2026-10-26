// SPDX-FileCopyrightText: Copyright (C) 2010 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/fd2dblackscholesvanillaengine.hpp ql/pricingengines/basket/fd2dblackscholesvanillaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// The two-dimensional finite-difference Black-Scholes engine for basket options on two assets: European, American
/// and Bermudan exercises.
/// </summary>
/// <remarks>
/// <para>
/// Its times run on the clock spanning both processes' structures, from their earliest reference date on the first
/// process's risk-free day counter, each structure read at the instants those times denote and accruing nothing
/// before its own date, where QuantLib runs on the first process's risk-free times and reads the second's structures
/// at them (<c>Divergences/Fd2dMismatchedTimesTests.cs</c>); a market on one clock keeps QuantLib's arithmetic.
/// </para>
/// <para>
/// The price discounts on the first process's risk-free curve, as the analytic two-asset engines do, where QuantLib's
/// discounts at the second's (<see cref="Fdm2dBlackScholesOp"/>; <c>Divergences/Fd2dTwoRiskFreeCurvesTests.cs</c>).
/// </para>
/// <para>
/// The correctness of the returned value is tested by reproducing results available in literature and comparison
/// with Kirk's approximation.
/// </para>
/// </remarks>
public static class Fd2dBlackScholesVanillaEngine
{
    /// <summary>Prices a basket option on two assets.</summary>
    /// <param name="option">The basket option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="p1">The first asset's process.</param>
    /// <param name="p2">The second asset's process.</param>
    /// <param name="correlation">The assets' correlation.</param>
    /// <param name="xGrid">The first asset's grid size.</param>
    /// <param name="yGrid">The second asset's grid size.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="dampingSteps">The number of damping steps.</param>
    /// <param name="schemeDesc">The scheme; Hundsdorfer's by default.</param>
    /// <param name="localVol">Whether to use the processes' local volatilities.</param>
    /// <param name="illegalLocalVolOverwrite">
    /// The volatility to use where a local volatility can't be computed; <see langword="null"/> (QuantLib's
    /// <c>-Null&lt;Real&gt;()</c>) or a negative value to let the failure through.
    /// </param>
    /// <returns>The value, delta (the sum of the two assets'), gamma (theirs and twice the cross gamma) and
    /// theta.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A grid size or the step count is refused.</exception>
    /// <exception cref="NanoQLException">A structure can't be read, or the rollback fails.</exception>
    public static MultiAssetOptionResults Calculate(
        BasketOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess p1,
        GeneralizedBlackScholesProcess p2,
        double correlation,
        int xGrid = 100,
        int yGrid = 100,
        int tGrid = 50,
        int dampingSteps = 0,
        FdmSchemeDesc? schemeDesc = null,
        bool localVol = false,
        double? illegalLocalVolOverwrite = null)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(p1);
        ArgumentNullException.ThrowIfNull(p2);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(Body(
            option, context, p1, p2, correlation, xGrid, yGrid, tGrid, dampingSteps,
            schemeDesc ?? FdmSchemeDesc.Hundsdorfer(), localVol, illegalLocalVolOverwrite));
    }

    // QuantLib's calculate().
    private static MultiAssetOptionResults Body(
        BasketOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess p1,
        GeneralizedBlackScholesProcess p2,
        double correlation,
        int xGrid,
        int yGrid,
        int tGrid,
        int dampingSteps,
        FdmSchemeDesc schemeDesc,
        bool localVol,
        double? illegalLocalVolOverwrite)
    {
        var payoff = (BasketPayoff)option.Payoff!;

        // the times on the clock across both processes, where QuantLib's are p1's
        TermStructure clock = FdmBlackScholesOp.ClockAcross([p1, p2]);
        double maturity = clock.TimeFromReference(option.Exercise.LastDate());
        var em1 = new FdmBlackScholesMesher(
            xGrid, p1, maturity, p1.X0, null, null, 0.0001, 1.5, (p1.X0, 0.1), [], null, 0.0, null, clock);
        var em2 = new FdmBlackScholesMesher(
            yGrid, p2, maturity, p2.X0, null, null, 0.0001, 1.5, (p2.X0, 0.1), [], null, 0.0, null, clock);

        var mesher = new FdmMesherComposite(em1, em2);

        var calculator = new FdmLogBasketInnerValue(payoff, mesher);

        FdmStepConditionComposite conditions = FdmStepConditionComposite.VanillaComposite(
            [], option.Exercise, mesher, calculator, clock.ReferenceDate, clock.DayCounter, context);

        ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries = [];

        var solverDesc = new FdmSolverDesc(mesher, boundaries, conditions, calculator, maturity, tGrid, dampingSteps);

        var solver = new Fdm2dBlackScholesSolver(
            p1, p2, correlation, solverDesc, schemeDesc, localVol, illegalLocalVolOverwrite);

        double x = p1.X0;
        double y = p2.X0;

        return new MultiAssetOptionResults
        {
            NPV = solver.ValueAt(x, y),
            Delta = solver.DeltaXat(x, y) + solver.DeltaYat(x, y),
            Gamma = solver.GammaXat(x, y) + solver.GammaYat(x, y) + (2 * solver.GammaXYat(x, y)),
            Theta = solver.ThetaAt(x, y),
        };
    }
}
