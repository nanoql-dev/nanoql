// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/capfloor/treecapfloorengine.hpp ql/pricingengines/capfloor/treecapfloorengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>The tree cap/floor engine: a <see cref="DiscretizedCapFloor"/> rolled back on a short-rate model's lattice.</summary>
/// <remarks>
/// <para>
/// A static class: <c>Calculate</c> runs QuantLib's four steps (playbook §2.1) on the model, an argument where
/// QuantLib's engine holds it, on a lattice built through the asset's mandatory times with the given number of steps, or
/// on a time grid, or on a lattice already built. The asset is initialized at its last payment and rolled back to its
/// first start, or to the reference date where that start is before it (a seasoned cap, the S-sr plan's SSR6 (iv)),
/// where QuantLib's grid refuses the negative time, or to the first caplet's fixing where the asset values that caplet
/// there, before its start (the S-sr plan's B1-1, <c>Divergences/CapletFixingExpiryTests.cs</c>). A caplet paid on the
/// reference date counts only when the context includes today's cash flows, as the discretized swap's known coupon does.
/// </para>
/// <para>
/// The term structure is needed only when the model can't provide one: a model that is an
/// <see cref="ITermStructureConsistentModel"/> reads its own curve's reference date and day counter.
/// </para>
/// </remarks>
public static class TreeCapFloorEngine
{
    /// <summary>Prices a cap, floor or collar on the model's lattice with a number of time steps through its mandatory times.</summary>
    /// <param name="capFloor">The cap, floor or collar.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The short-rate model.</param>
    /// <param name="timeSteps">The number of time steps, positive.</param>
    /// <param name="termStructure">
    /// The curve that gives the reference date and day counter of the times, for a model that has no curve of its own;
    /// <see langword="null"/>, QuantLib's empty handle, by default.
    /// </param>
    /// <returns>The instrument's results: the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The cap, the context or the model is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="timeSteps"/> isn't positive.</exception>
    /// <exception cref="NanoQLException">
    /// The cap's terms are inconsistent, a cash flow isn't a floating-rate coupon, a forward can't be computed, the
    /// model has no curve and none is given, or the model can't build its lattice.
    /// </exception>
    public static InstrumentResults Calculate(
        CapFloor capFloor, EvaluationContext context, ShortRateModel model, int timeSteps, YieldTermStructure? termStructure = null)
    {
        CheckArguments(capFloor, context, model);
        LatticeShortRateModelEngine.CheckTimeSteps(timeSteps);
        return Run(capFloor, context, model, null, timeSteps, termStructure);
    }

    /// <summary>Prices a cap, floor or collar on the model's lattice on a time grid.</summary>
    /// <param name="capFloor">The cap, floor or collar.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The short-rate model.</param>
    /// <param name="timeGrid">The time grid, which must hold the cap's mandatory times.</param>
    /// <param name="termStructure">
    /// The curve that gives the reference date and day counter of the times, for a model that has no curve of its own;
    /// <see langword="null"/>, QuantLib's empty handle, by default.
    /// </param>
    /// <returns>The instrument's results: the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument but the term structure is null.</exception>
    /// <exception cref="NanoQLException">
    /// The model can't build its lattice on the grid, the cap's terms are inconsistent, a forward can't be computed,
    /// the model has no curve and none is given, or the grid lacks one of the cap's times.
    /// </exception>
    /// <remarks>The lattice is built first, as QuantLib's engine builds it at construction.</remarks>
    public static InstrumentResults Calculate(
        CapFloor capFloor, EvaluationContext context, ShortRateModel model, TimeGrid timeGrid, YieldTermStructure? termStructure = null)
    {
        CheckArguments(capFloor, context, model);
        ArgumentNullException.ThrowIfNull(timeGrid);
        Lattice lattice = model.Tree(timeGrid);
        return Run(capFloor, context, model, lattice, 0, termStructure);
    }

    /// <summary>Prices a cap, floor or collar on a lattice already built, which the caller reuses across calculations.</summary>
    /// <param name="capFloor">The cap, floor or collar.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The short-rate model, whose curve gives the reference date and day counter of the times.</param>
    /// <param name="lattice">The lattice, which must hold the cap's mandatory times.</param>
    /// <param name="termStructure">
    /// The curve that gives the reference date and day counter of the times, for a model that has no curve of its own;
    /// <see langword="null"/>, QuantLib's empty handle, by default.
    /// </param>
    /// <returns>The instrument's results: the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument but the term structure is null.</exception>
    /// <exception cref="NanoQLException">
    /// The cap's terms are inconsistent, a forward can't be computed, the model has no curve and none is given, or the
    /// lattice's grid lacks one of the cap's times.
    /// </exception>
    public static InstrumentResults Calculate(
        CapFloor capFloor, EvaluationContext context, ShortRateModel model, Lattice lattice, YieldTermStructure? termStructure = null)
    {
        CheckArguments(capFloor, context, model);
        ArgumentNullException.ThrowIfNull(lattice);
        return Run(capFloor, context, model, lattice, 0, termStructure);
    }

    private static void CheckArguments(CapFloor capFloor, EvaluationContext context, ShortRateModel model)
    {
        ArgumentNullException.ThrowIfNull(capFloor);
        ArgumentNullException.ThrowIfNull(context);

        // QuantLib's "no model specified", in its calculate, is the argument check here.
        ArgumentNullException.ThrowIfNull(model);
    }

    private static InstrumentResults Run(
        CapFloor capFloor, EvaluationContext context, ShortRateModel model, Lattice? lattice, int timeSteps, YieldTermStructure? termStructure)
    {
        if (capFloor.IsExpired(context))
        {
            return capFloor.SetupExpired();
        }

        CapFloorArguments arguments = capFloor.SetupArguments(context);
        arguments.Validate();
        InstrumentResults results = Body(arguments, context, model, lattice, timeSteps, termStructure);
        return capFloor.FetchResults(results);
    }

    // QuantLib's calculate().
    private static InstrumentResults Body(
        CapFloorArguments arguments,
        EvaluationContext context,
        ShortRateModel model,
        Lattice? givenLattice,
        int timeSteps,
        YieldTermStructure? termStructure)
    {
        Date referenceDate;
        DayCounter dayCounter;

        if (model is ITermStructureConsistentModel tsmodel)
        {
            referenceDate = tsmodel.TermStructure.ReferenceDate;
            dayCounter = tsmodel.TermStructure.DayCounter;
        }
        else
        {
            YieldTermStructure curve = termStructure ?? throw new NanoQLException("empty Handle cannot be dereferenced");
            referenceDate = curve.ReferenceDate;
            dayCounter = curve.DayCounter;
        }

        var capfloor = new DiscretizedCapFloor(arguments, referenceDate, dayCounter, context.IncludeTodaysCashFlows == true);
        Lattice lattice;

        if (givenLattice is not null)
        {
            lattice = givenLattice;
        }
        else
        {
            ImmutableArray<double> times = capfloor.MandatoryTimes();
            lattice = LatticeShortRateModelEngine.Lattice(model, times.AsSpan(), timeSteps);
        }

        double firstTime = dayCounter.YearFraction(referenceDate, arguments.StartDates[0]);

        // B1-1: a caplet valued at its fixing, before the first start, is reached there
        double earliestFixing = capfloor.EarliestFixingTime;
        if (earliestFixing < firstTime)
        {
            firstTime = earliestFixing;
        }

        double lastTime = dayCounter.YearFraction(referenceDate, arguments.EndDates[^1]);
        capfloor.Initialize(lattice, lastTime);

        // a seasoned cap's first start is before the reference date, where the grid starts
        capfloor.Rollback(firstTime < 0.0 ? 0.0 : firstTime);

        return new InstrumentResults { NPV = capfloor.PresentValue() };
    }
}
