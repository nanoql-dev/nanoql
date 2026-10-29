// SPDX-FileCopyrightText: Copyright (C) 2016 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdornsteinuhlenbeckvanillaengine.hpp ql/experimental/finitedifferences/fdornsteinuhlenbeckvanillaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The one-dimensional finite-difference engine of a vanilla option on an Ornstein-Uhlenbeck process: the payoff on the
/// process's own value, discounted on a curve, with dividends and early exercise by the vanilla step conditions.
/// </summary>
/// <remarks>
/// The maturity and the step conditions' times are read on the discount curve's day counter from its reference date,
/// as QuantLib's. A payoff that isn't striked, which QuantLib casts to a null payoff and reads, is refused.
/// </remarks>
[Experimental("NQL0001")]
public static class FdOrnsteinUhlenbeckVanillaEngine
{
    /// <summary>Prices an option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Ornstein-Uhlenbeck process.</param>
    /// <param name="rTS">The discount curve.</param>
    /// <param name="dividends">The dividends, or <c>default</c> for none.</param>
    /// <param name="tGrid">The time steps.</param>
    /// <param name="xGrid">The mesher's points.</param>
    /// <param name="dampingSteps">The damping steps.</param>
    /// <param name="epsilon">The mesher's probability cut.</param>
    /// <param name="schemeDesc">The scheme, or <see langword="null"/> for Douglas's.</param>
    /// <returns>The value, delta, gamma and theta at the process's value today.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A dividend is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size is negative.</exception>
    /// <exception cref="NanoQLException">The option is refused, or its payoff isn't striked.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        OrnsteinUhlenbeckProcess process,
        YieldTermStructure rTS,
        ImmutableArray<Dividend> dividends = default,
        int tGrid = 100,
        int xGrid = 100,
        int dampingSteps = 0,
        double epsilon = 0.0001,
        FdmSchemeDesc? schemeDesc = null)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(rTS);
        ImmutableArray<Dividend> schedule = FdBlackScholesBarrierEngine.Dividends(dividends, nameof(dividends));
        ArgumentOutOfRangeException.ThrowIfNegative(tGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(xGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(dampingSteps);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();

        // 1. Mesher
        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        DayCounter dc = rTS.DayCounter;
        Date referenceDate = rTS.ReferenceDate;
        double maturity = dc.YearFraction(referenceDate, option.Exercise.LastDate());
        var equityMesher = new FdmSimpleProcess1dMesher(xGrid, process, maturity, 1, epsilon);
        var mesher = new FdmMesherComposite(equityMesher);

        // 2. Calculator
        var calculator = new FdmOUInnerValue(payoff, mesher, 0);

        // 3. Step conditions
        FdmStepConditionComposite conditions = FdmStepConditionComposite.VanillaComposite(
            schedule, option.Exercise, mesher, calculator, referenceDate, dc, context);

        // 4. Boundary conditions
        ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries = [];

        // 5. Solver
        var solverDesc = new FdmSolverDesc(mesher, boundaries, conditions, calculator, maturity, tGrid, dampingSteps);
        var op = new FdmOrnsteinUhlenbeckOp(mesher, process, rTS, 0);
        var solver = new Fdm1DimSolver(solverDesc, schemeDesc ?? FdmSchemeDesc.Douglas(), op);
        double spot = process.X0;
        return option.FetchResults(new OneAssetOptionResults
        {
            NPV = solver.InterpolateAt(spot),
            Delta = solver.DerivativeX(spot),
            Gamma = solver.DerivativeXX(spot),
            Theta = solver.ThetaAt(spot),
        });
    }

    // the payoff on the process's own value at a mesher's location
    private sealed class FdmOUInnerValue(Payoff payoff, FdmMesher mesher, int direction) : FdmInnerValueCalculator
    {
        public override double InnerValue(FdmLinearOpIterator iter, double t) => payoff.Evaluate(mesher.Location(iter, direction));

        public override double AvgInnerValue(FdmLinearOpIterator iter, double t) => InnerValue(iter, t);
    }
}
