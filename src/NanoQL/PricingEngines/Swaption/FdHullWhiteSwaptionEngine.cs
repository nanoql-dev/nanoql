// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swaption/fdhullwhiteswaptionengine.hpp ql/pricingengines/swaption/fdhullwhiteswaptionengine.cpp @ v1.44
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
/// The finite-difference Hull-White swaption engine: the swaption rolled back on a mesh of the model's factor, its
/// exercise value the swap's inner value on the model's curves (<see cref="FdmAffineModelSwapInnerValue{TModel}"/>).
/// </summary>
/// <remarks>
/// <para>
/// A static class: <c>Calculate</c> runs QuantLib's four steps (playbook §2.1) on the model, an argument where QuantLib's
/// engine holds it. The mesher is the factor's Ornstein-Uhlenbeck quantiles up to the last exercise; the swap's
/// coupons forecast on a Hull-White with the model's parameters on the index's curve, which must share the discount
/// curve's day counter and reference date, as QuantLib refuses otherwise (the S-sr plan's SSD19); the value is read
/// at the factor's origin.
/// </para>
/// <para>An American exercise reaches the inner value at times off its exercise dates, which it refuses (SSR6 (x)).</para>
/// </remarks>
public static class FdHullWhiteSwaptionEngine
{
    /// <summary>Prices a swaption on a Hull-White model by finite differences.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Hull-White model.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of points of the factor's mesher.</param>
    /// <param name="dampingSteps">The number of damping steps.</param>
    /// <param name="invEps">The probability of the mesher's ends' quantiles.</param>
    /// <param name="schemeDesc">The scheme; <see langword="null"/> for QuantLib's default, <see cref="FdmSchemeDesc.Douglas"/>.</param>
    /// <returns>The swaption's results, the NPV alone: the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The swaption, the context or the model is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size or the number of damping steps is negative.</exception>
    /// <exception cref="ArgumentException">The mesher has no point.</exception>
    /// <exception cref="NanoQLException">
    /// The swaption's terms are inconsistent, an exercise date is past, the index has no curve or one on another day
    /// counter or reference date, the exercise is American, or a step fails.
    /// </exception>
    public static InstrumentResults Calculate(
        Swaption swaption,
        EvaluationContext context,
        HullWhite model,
        int tGrid = 100,
        int xGrid = 100,
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

        var process = new OrnsteinUhlenbeckProcess(model.A(), model.Sigma);
        var shortRateMesher = new FdmSimpleProcess1dMesher(xGrid, process, maturity, 1, invEps);
        var mesher = new FdmMesherComposite(shortRateMesher);

        // 3. Inner Value Calculator
        Dictionary<double, Date> t2d = ExerciseTimes(swaption, referenceDate, dc);

        YieldTermStructure disTs = model.TermStructure;
        YieldTermStructure fwdTs = ForwardingCurve(swaption, disTs);

        var fwdModel = new HullWhite(fwdTs, model.A(), model.Sigma);
        var calculator = new FdmAffineModelSwapInnerValue<HullWhite>(model, fwdModel, swaption.Underlying, t2d, mesher, 0, context);

        // 4. Step conditions
        FdmStepConditionComposite conditions = FdmStepConditionComposite.VanillaComposite(
            [], swaption.Exercise, mesher, calculator, referenceDate, dc, context);

        // 5. Boundary conditions
        ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries = [];

        // 6. Solver
        var solverDesc = new FdmSolverDesc(mesher, boundaries, conditions, calculator, maturity, tGrid, dampingSteps);

        var solver = new FdmHullWhiteSolver(model, solverDesc, schemeDesc ?? FdmSchemeDesc.Douglas());

        return swaption.FetchResults(new InstrumentResults { NPV = solver.ValueAt(0.0) });
    }

    // The exercise dates by their times, a later date at an equal time replacing an earlier, as QuantLib's map assigns.
    internal static Dictionary<double, Date> ExerciseTimes(Swaption swaption, Date referenceDate, DayCounter dc)
    {
        var t2d = new Dictionary<double, Date>();
        foreach (Date exerciseDate in swaption.Exercise.Dates)
        {
            double t = dc.YearFraction(referenceDate, exerciseDate);
            if (!(t >= 0))
            {
                throw new NanoQLException("exercise dates must not contain past date");
            }

            t2d[t] = exerciseDate;
        }

        return t2d;
    }

    // The index's forwarding curve, on the discount curve's day counter and reference date (SSD19).
    internal static YieldTermStructure ForwardingCurve(Swaption swaption, YieldTermStructure disTs)
    {
        YieldTermStructure fwdTs = swaption.Underlying.IborIndex.ForwardingTermStructure
            ?? throw new NanoQLException("empty Handle cannot be dereferenced");

        if (!fwdTs.DayCounter.Equals(disTs.DayCounter))
        {
            throw new NanoQLException("day counter of forward and discount curve must match");
        }

        if (!(fwdTs.ReferenceDate == disTs.ReferenceDate))
        {
            throw new NanoQLException("reference date of forward and discount curve must match");
        }

        return fwdTs;
    }
}
