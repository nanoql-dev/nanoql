// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swaption/treeswaptionengine.hpp ql/pricingengines/swaption/treeswaptionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>The numerical lattice engine for swaptions: a <see cref="DiscretizedSwaption"/> rolled back on a short-rate model's lattice.</summary>
/// <remarks>
/// <para>
/// A static class: each <c>Calculate</c> overload runs QuantLib's four steps (playbook §2.1), the model an argument
/// where QuantLib's engine holds it. QuantLib's constructors are its overloads: on a number of time steps, the lattice
/// built per call on a grid through the swaption's mandatory times; on a time grid, the model's lattice on it, which
/// QuantLib's engine builds once and nanoql builds per call; and on a built lattice, which a caller reuses by passing
/// it in. The swaption is rolled back to its first exercise time on or after the reference date, and its value is
/// its present value there.
/// </para>
/// <para>
/// The engine is not guaranteed to work if the underlying swap has a start date in the past, before the reference
/// date: prune the swap's initial part so that it starts at t ≥ 0. The term structure is needed only when the model
/// can't provide one: a model that is an <see cref="ITermStructureConsistentModel"/> gives the reference date and day
/// counter of the times from its own curve, as QuantLib's engine does, whatever curve is given.
/// </para>
/// </remarks>
public static class TreeSwaptionEngine
{
    /// <summary>Prices a swaption on the model's lattice with a number of time steps through its mandatory times.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The short-rate model.</param>
    /// <param name="timeSteps">The number of time steps, positive.</param>
    /// <param name="termStructure">
    /// The curve that gives the reference date and day counter of the times, for a model that has no curve of its own;
    /// <see langword="null"/>, QuantLib's empty handle, by default.
    /// </param>
    /// <returns>The swaption's results: the expired results when the swaption has expired.</returns>
    /// <exception cref="ArgumentNullException">The swaption, the context or the model is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="timeSteps"/> isn't positive.</exception>
    /// <exception cref="NanoQLException">
    /// The swaption's terms are inconsistent, it is cash settled at the par yield curve, the model has no curve and
    /// none is given, or the swaption can't be discretized.
    /// </exception>
    public static InstrumentResults Calculate(
        Swaption swaption,
        EvaluationContext context,
        ShortRateModel model,
        int timeSteps,
        YieldTermStructure? termStructure = null)
    {
        CheckArguments(swaption, context, model);
        LatticeShortRateModelEngine.CheckTimeSteps(timeSteps);
        return Run(swaption, context, model, null, timeSteps, termStructure);
    }

    /// <summary>Prices a swaption on the model's lattice on a time grid.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The short-rate model.</param>
    /// <param name="timeGrid">The time grid, which must hold the swaption's mandatory times.</param>
    /// <param name="termStructure">
    /// The curve that gives the reference date and day counter of the times, for a model that has no curve of its own;
    /// <see langword="null"/>, QuantLib's empty handle, by default.
    /// </param>
    /// <returns>The swaption's results: the expired results when the swaption has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument but the term structure is null.</exception>
    /// <exception cref="NanoQLException">
    /// The model can't build its lattice on the grid, the swaption's terms are inconsistent, it is cash settled at the
    /// par yield curve, the model has no curve and none is given, the grid lacks one of the swaption's times, or the
    /// swaption can't be discretized.
    /// </exception>
    /// <remarks>The lattice is built first, as QuantLib's engine builds it at construction.</remarks>
    public static InstrumentResults Calculate(
        Swaption swaption,
        EvaluationContext context,
        ShortRateModel model,
        TimeGrid timeGrid,
        YieldTermStructure? termStructure = null)
    {
        CheckArguments(swaption, context, model);
        ArgumentNullException.ThrowIfNull(timeGrid);
        Lattice lattice = model.Tree(timeGrid);
        return Run(swaption, context, model, lattice, 0, termStructure);
    }

    /// <summary>Prices a swaption on a lattice already built, which the caller reuses across calculations.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The short-rate model, whose curve gives the reference date and day counter of the times.</param>
    /// <param name="lattice">The lattice, which must hold the swaption's mandatory times: the model's, as its <see cref="ShortRateModel.Tree"/> builds it.</param>
    /// <param name="termStructure">
    /// The curve that gives the reference date and day counter of the times, for a model that has no curve of its own;
    /// <see langword="null"/>, QuantLib's empty handle, by default.
    /// </param>
    /// <returns>The swaption's results: the expired results when the swaption has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument but the term structure is null.</exception>
    /// <exception cref="NanoQLException">
    /// The swaption's terms are inconsistent, it is cash settled at the par yield curve, the model has no curve and
    /// none is given, the lattice's grid lacks one of the swaption's times, no exercise time is on or after the
    /// reference date, or the swaption can't be discretized.
    /// </exception>
    public static InstrumentResults Calculate(
        Swaption swaption,
        EvaluationContext context,
        ShortRateModel model,
        Lattice lattice,
        YieldTermStructure? termStructure = null)
    {
        CheckArguments(swaption, context, model);
        ArgumentNullException.ThrowIfNull(lattice);
        return Run(swaption, context, model, lattice, 0, termStructure);
    }

    private static void CheckArguments(Swaption swaption, EvaluationContext context, ShortRateModel model)
    {
        ArgumentNullException.ThrowIfNull(swaption);
        ArgumentNullException.ThrowIfNull(context);

        // QuantLib's "no model specified", in its calculate, is the argument check here.
        ArgumentNullException.ThrowIfNull(model);
    }

    private static InstrumentResults Run(
        Swaption swaption,
        EvaluationContext context,
        ShortRateModel model,
        Lattice? lattice,
        int timeSteps,
        YieldTermStructure? termStructure)
    {
        if (swaption.IsExpired(context))
        {
            return swaption.SetupExpired();
        }

        swaption.Validate();
        InstrumentResults results = Body(swaption, context, model, lattice, timeSteps, termStructure);
        return swaption.FetchResults(results);
    }

    // QuantLib's calculate().
    private static InstrumentResults Body(
        Swaption swaption,
        EvaluationContext context,
        ShortRateModel model,
        Lattice? givenLattice,
        int timeSteps,
        YieldTermStructure? termStructure)
    {
        if (!(swaption.SettlementMethod != SettlementMethod.ParYieldCurve))
        {
            throw new NanoQLException("cash settled (ParYieldCurve) swaptions not priced with TreeSwaptionEngine");
        }

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

        var asset = new DiscretizedSwaption(swaption, referenceDate, dayCounter, context);
        Lattice lattice;

        if (givenLattice is not null)
        {
            lattice = givenLattice;
        }
        else
        {
            ImmutableArray<double> times = asset.MandatoryTimes();
            lattice = LatticeShortRateModelEngine.Lattice(model, times.AsSpan(), timeSteps);
        }

        ImmutableArray<NanoQL.Time.Date> exerciseDates = swaption.Exercise.Dates;
        double[] stoppingTimes = new double[exerciseDates.Length];
        for (int i = 0; i < stoppingTimes.Length; ++i)
        {
            stoppingTimes[i] = dayCounter.YearFraction(referenceDate, swaption.Exercise.Date(i));
        }

        asset.Initialize(lattice, stoppingTimes[^1]);

        double nextExercise = FirstNonNegative(stoppingTimes);
        asset.Rollback(nextExercise);

        return new InstrumentResults { NPV = asset.PresentValue() };
    }

    // QuantLib's *std::find_if(..., t >= 0), which reads past the times when none is: on a lattice of QuantLib's, the
    // asset's initialization at the last of them refuses a negative time first, but a lattice of one's own may not.
    private static double FirstNonNegative(double[] stoppingTimes)
    {
        foreach (double t in stoppingTimes)
        {
            if (t >= 0.0)
            {
                return t;
            }
        }

        throw new NanoQLException("no exercise time on or after the reference date");
    }
}
