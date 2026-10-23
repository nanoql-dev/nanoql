// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swaption/fdg2swaptionengine.hpp ql/pricingengines/swaption/fdg2swaptionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The finite-difference G2 swaption engine: the swaption rolled back on a two-dimensional mesh of the model's factors,
/// its exercise value the swap's inner value on the model's curves (<see cref="FdmAffineModelSwapInnerValue{TModel}"/>).
/// </summary>
/// <remarks>
/// <para>
/// A static class: <c>Calculate</c> runs QuantLib's four steps (playbook §2.1) on the model, an argument where QuantLib's
/// engine holds it. The mesher is each factor's Ornstein-Uhlenbeck quantiles up to the last exercise; the swap's
/// coupons forecast on a G2 with the model's parameters on the index's curve, which must share the discount curve's
/// day counter and reference date, as QuantLib refuses otherwise (the S-sr plan's SSD19); the value is read at the
/// factors' origin.
/// </para>
/// <para>An American exercise reaches the inner value at times off its exercise dates, which it refuses (SSR6 (x)).</para>
/// </remarks>
public static class FdG2SwaptionEngine
{
    /// <summary>Prices a swaption on a G2 model by finite differences.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The G2 model.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of points of the first factor's mesher.</param>
    /// <param name="yGrid">The number of points of the second factor's mesher.</param>
    /// <param name="dampingSteps">The number of damping steps.</param>
    /// <param name="invEps">The probability of the meshers' ends' quantiles.</param>
    /// <param name="schemeDesc">The scheme; <see langword="null"/> for QuantLib's default, <see cref="FdmSchemeDesc.Hundsdorfer"/>.</param>
    /// <returns>The swaption's results, the NPV alone: the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The swaption, the context or the model is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size or the number of damping steps is negative.</exception>
    /// <exception cref="ArgumentException">A mesher has no point.</exception>
    /// <exception cref="NanoQLException">
    /// The swaption's terms are inconsistent, an exercise date is past, the index has no curve or one on another day
    /// counter or reference date, the exercise is American, or a step fails.
    /// </exception>
    public static InstrumentResults Calculate(
        Swaption swaption,
        EvaluationContext context,
        G2 model,
        int tGrid = 100,
        int xGrid = 50,
        int yGrid = 50,
        int dampingSteps = 0,
        double invEps = 1e-5,
        FdmSchemeDesc? schemeDesc = null)
    {
        ArgumentNullException.ThrowIfNull(swaption);
        ArgumentNullException.ThrowIfNull(context);

        // QuantLib's "no model specified", in its calculate, is the argument check here.
        ArgumentNullException.ThrowIfNull(model);
        ArgumentOutOfRangeException.ThrowIfNegative(tGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(xGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(yGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(dampingSteps);
        if (swaption.IsExpired(context))
        {
            return swaption.SetupExpired();
        }

        swaption.Validate();

        // 1. Term structure
        YieldTermStructure ts = model.TermStructure;

        // 2. Mesher
        DayCounter dc = ts.DayCounter;
        Date referenceDate = ts.ReferenceDate;
        double maturity = dc.YearFraction(referenceDate, swaption.Exercise.LastDate());

        var process1 = new OrnsteinUhlenbeckProcess(model.A(), model.Sigma);

        var process2 = new OrnsteinUhlenbeckProcess(model.B(), model.Eta);

        var xMesher = new FdmSimpleProcess1dMesher(xGrid, process1, maturity, 1, invEps);

        var yMesher = new FdmSimpleProcess1dMesher(yGrid, process2, maturity, 1, invEps);

        var mesher = new FdmMesherComposite(xMesher, yMesher);

        // 3. Inner Value Calculator
        Dictionary<double, Date> t2d = FdHullWhiteSwaptionEngine.ExerciseTimes(swaption, referenceDate, dc);

        YieldTermStructure disTs = model.TermStructure;
        YieldTermStructure fwdTs = FdHullWhiteSwaptionEngine.ForwardingCurve(swaption, disTs);

        var fwdModel = new G2(fwdTs, model.A(), model.Sigma, model.B(), model.Eta, model.Rho);

        var calculator = new FdmAffineModelSwapInnerValue<G2>(model, fwdModel, swaption.Underlying, t2d, mesher, 0, context);

        // 4. Step conditions
        FdmStepConditionComposite conditions = FdmStepConditionComposite.VanillaComposite(
            [], swaption.Exercise, mesher, calculator, referenceDate, dc, context);

        // 5. Boundary conditions
        ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries = [];

        // 6. Solver
        var solverDesc = new FdmSolverDesc(mesher, boundaries, conditions, calculator, maturity, tGrid, dampingSteps);

        var solver = new FdmG2Solver(model, solverDesc, schemeDesc ?? FdmSchemeDesc.Hundsdorfer());

        return swaption.FetchResults(new InstrumentResults { NPV = solver.ValueAt(0.0, 0.0) });
    }
}
