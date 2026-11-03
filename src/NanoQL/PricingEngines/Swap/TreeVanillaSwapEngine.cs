// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swap/treeswapengine.hpp ql/pricingengines/swap/treeswapengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>The tree swap engine: a <see cref="DiscretizedSwap"/> rolled back to 0 on a short-rate model's lattice.</summary>
/// <remarks>
/// <para>
/// A static class: <c>Calculate</c> runs QuantLib's four steps (playbook §2.1) on the model, an argument where
/// QuantLib's engine holds it, on a lattice built through the swap's mandatory times with the given number of steps,
/// or on a time grid, or on a lattice already built. The swap is initialized at its last mandatory time and rolled back
/// to 0; only its NPV is computed. A floating coupon fixed on or before the reference date is its amount
/// (<see cref="DiscretizedSwap"/>'s public constructor).
/// </para>
/// <para>
/// The term structure is needed only when the model can't provide one: a model that is an
/// <see cref="ITermStructureConsistentModel"/> reads its own curve's reference date and day counter.
/// </para>
/// </remarks>
public static class TreeVanillaSwapEngine
{
    /// <summary>Prices a swap on the model's lattice with a number of time steps through its mandatory times.</summary>
    /// <param name="swap">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The short-rate model.</param>
    /// <param name="timeSteps">The number of time steps, positive.</param>
    /// <param name="termStructure">
    /// The curve that gives the reference date and day counter of the times, for a model that has no curve of its own;
    /// <see langword="null"/>, QuantLib's empty handle, by default.
    /// </param>
    /// <returns>The swap's results, the NPV alone: the expired results when the swap has expired.</returns>
    /// <exception cref="ArgumentNullException">The swap, the context or the model is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="timeSteps"/> isn't positive.</exception>
    /// <exception cref="NanoQLException">
    /// The swap's terms are inconsistent, its nominal isn't constant, a known floating coupon's amount can't be
    /// computed, the model has no curve and none is given, or the model can't build its lattice.
    /// </exception>
    public static FixedVsFloatingSwapResults Calculate(
        VanillaSwap swap, EvaluationContext context, ShortRateModel model, int timeSteps, YieldTermStructure? termStructure = null)
    {
        CheckArguments(swap, context, model);
        LatticeShortRateModelEngine.CheckTimeSteps(timeSteps);
        return Run(swap, context, model, null, timeSteps, termStructure);
    }

    /// <summary>Prices a swap on the model's lattice on a time grid.</summary>
    /// <param name="swap">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The short-rate model.</param>
    /// <param name="timeGrid">The time grid, which must hold the swap's mandatory times.</param>
    /// <param name="termStructure">
    /// The curve that gives the reference date and day counter of the times, for a model that has no curve of its own;
    /// <see langword="null"/>, QuantLib's empty handle, by default.
    /// </param>
    /// <returns>The swap's results, the NPV alone: the expired results when the swap has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument but the term structure is null.</exception>
    /// <exception cref="NanoQLException">
    /// The model can't build its lattice on the grid, the swap's terms are inconsistent, its nominal isn't constant, a
    /// known floating coupon's amount can't be computed, the model has no curve and none is given, or the grid lacks one
    /// of the swap's times.
    /// </exception>
    /// <remarks>The lattice is built first, as QuantLib's engine builds it at construction.</remarks>
    public static FixedVsFloatingSwapResults Calculate(
        VanillaSwap swap, EvaluationContext context, ShortRateModel model, TimeGrid timeGrid, YieldTermStructure? termStructure = null)
    {
        CheckArguments(swap, context, model);
        ArgumentNullException.ThrowIfNull(timeGrid);
        Lattice lattice = model.Tree(timeGrid);
        return Run(swap, context, model, lattice, 0, termStructure);
    }

    /// <summary>Prices a swap on a lattice already built, which the caller reuses across calculations.</summary>
    /// <param name="swap">The swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The short-rate model, whose curve gives the reference date and day counter of the times.</param>
    /// <param name="lattice">The lattice, which must hold the swap's mandatory times.</param>
    /// <param name="termStructure">
    /// The curve that gives the reference date and day counter of the times, for a model that has no curve of its own;
    /// <see langword="null"/>, QuantLib's empty handle, by default.
    /// </param>
    /// <returns>The swap's results, the NPV alone: the expired results when the swap has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument but the term structure is null.</exception>
    /// <exception cref="NanoQLException">
    /// The swap's terms are inconsistent, its nominal isn't constant, a known floating coupon's amount can't be
    /// computed, the model has no curve and none is given, the lattice's grid lacks one of the swap's times, or the
    /// swap has no time on or after the curve's reference date.
    /// </exception>
    public static FixedVsFloatingSwapResults Calculate(
        VanillaSwap swap, EvaluationContext context, ShortRateModel model, Lattice lattice, YieldTermStructure? termStructure = null)
    {
        CheckArguments(swap, context, model);
        ArgumentNullException.ThrowIfNull(lattice);
        return Run(swap, context, model, lattice, 0, termStructure);
    }

    private static void CheckArguments(VanillaSwap swap, EvaluationContext context, ShortRateModel model)
    {
        ArgumentNullException.ThrowIfNull(swap);
        ArgumentNullException.ThrowIfNull(context);

        // QuantLib's "no model specified", in its calculate, is the argument check here.
        ArgumentNullException.ThrowIfNull(model);
    }

    private static FixedVsFloatingSwapResults Run(
        VanillaSwap swap, EvaluationContext context, ShortRateModel model, Lattice? lattice, int timeSteps, YieldTermStructure? termStructure)
    {
        if (swap.IsExpired(context))
        {
            return swap.SetupExpired();
        }

        swap.Validate();
        SwapResults results = Body(swap, context, model, lattice, timeSteps, termStructure);
        return swap.FetchResults(results);
    }

    // QuantLib's calculate().
    private static SwapResults Body(
        VanillaSwap swap, EvaluationContext context, ShortRateModel model, Lattice? givenLattice, int timeSteps, YieldTermStructure? termStructure)
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

        var asset = new DiscretizedSwap(swap, referenceDate, dayCounter, context);
        ImmutableArray<double> times = asset.MandatoryTimes();

        Lattice lattice = givenLattice ?? LatticeShortRateModelEngine.Lattice(model, times.AsSpan(), timeSteps);

        // std::max_element, which QuantLib reads at the end of an empty vector
        if (times.IsEmpty)
        {
            throw new NanoQLException("the swap has no time on or after the curve's reference date");
        }

        double maxTime = times[0];
        for (int i = 1; i < times.Length; i++)
        {
            if (maxTime < times[i])
            {
                maxTime = times[i];
            }
        }

        asset.Initialize(lattice, maxTime);
        asset.Rollback(0.0);

        return new SwapResults { NPV = asset.PresentValue() };
    }
}
