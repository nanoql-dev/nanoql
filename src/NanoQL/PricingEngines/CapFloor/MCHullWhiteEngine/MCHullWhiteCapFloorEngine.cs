// SPDX-FileCopyrightText: Copyright (C) 2006 Banca Profilo S.p.A.
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/capfloor/mchullwhiteengine.hpp ql/pricingengines/capfloor/mchullwhiteengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Monte Carlo Hull-White cap/floor engine: the short rate simulated in the forward measure of the last payment,
/// each path priced by <see cref="HullWhiteCapFloorPricer"/>.
/// </summary>
/// <remarks>
/// <para>
/// A static class: <c>Calculate</c> runs QuantLib's four steps (playbook §2.1) on the model, an argument where
/// QuantLib's engine holds it, through <see cref="McSimulation{TPath, TStats}"/>: a
/// <see cref="HullWhiteForwardProcess"/> on the model's curve and parameters at the last payment's time, on a grid of
/// the fixings after the reference date and the last payment; the NPV is the samples' mean, and the error estimate is
/// theirs where the random-number traits allow one. The type parameters, the traits and the accumulator, are QuantLib's
/// template parameters, whose defaults are <see cref="PseudoRandom"/> and <see cref="GeneralStatistics"/>.
/// </para>
/// <para>
/// The seed is explicit: QuantLib's 0, a seed from the clock, is refused.
/// </para>
/// </remarks>
public static class MCHullWhiteCapFloorEngine
{
    /// <summary>Prices a cap, floor or collar by Monte Carlo on a Hull-White model.</summary>
    /// <typeparam name="TRng">The random-number traits.</typeparam>
    /// <typeparam name="TStats">The statistics accumulator.</typeparam>
    /// <param name="capFloor">The cap, floor or collar.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Hull-White model.</param>
    /// <param name="brownianBridge">Whether the paths are drawn through a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>, QuantLib's null.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>; it takes precedence over the number of samples.</param>
    /// <param name="maxSamples">The most samples for a tolerance, or <see langword="null"/> for no limit.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The instrument's results, the NPV and, where the traits allow one, the error estimate: the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The cap, the context or the model is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="requiredSamples"/> or <paramref name="maxSamples"/> is negative, or <paramref name="seed"/> is
    /// outside its range.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The cap's terms are inconsistent, a cash flow isn't a floating-rate coupon, a forward can't be computed, neither a
    /// tolerance nor a number of samples is given, or the samples can't reach the tolerance.
    /// </exception>
    public static InstrumentResults Calculate<TRng, TStats>(
        CapFloor capFloor,
        EvaluationContext context,
        HullWhite model,
        bool brownianBridge,
        bool antitheticVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed)
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        ArgumentNullException.ThrowIfNull(capFloor);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);
        if (requiredSamples is int samples)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(samples, nameof(requiredSamples));
        }

        if (maxSamples is int max)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(max, nameof(maxSamples));
        }

        Seeds.Check(seed);
        if (capFloor.IsExpired(context))
        {
            return capFloor.SetupExpired();
        }

        CapFloorArguments arguments = capFloor.SetupArguments(context);
        arguments.Validate();
        var engine = new Simulation<TRng, TStats>(arguments, model, brownianBridge, antitheticVariate, seed);
        InstrumentResults results = engine.Run(requiredTolerance, requiredSamples, maxSamples ?? int.MaxValue);
        return capFloor.FetchResults(results);
    }

    // QuantLib's engine object: its hooks over one calculation's arguments.
    private sealed class Simulation<TRng, TStats> : McSimulation<MonteCarloPath, TStats>
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        private readonly CapFloorArguments arguments;
        private readonly HullWhite model;
        private readonly bool brownianBridge;
        private readonly long seed;

        public Simulation(CapFloorArguments arguments, HullWhite model, bool brownianBridge, bool antitheticVariate, long seed)
            : base(antitheticVariate, false)
        {
            this.arguments = arguments;
            this.model = model;
            this.brownianBridge = brownianBridge;
            this.seed = seed;
        }

        // QuantLib's calculate().
        public InstrumentResults Run(double? requiredTolerance, int? requiredSamples, int maxSamples)
        {
            MonteCarloModel<MonteCarloPath, TStats> mcModel = Calculate(requiredTolerance, requiredSamples, maxSamples);
            return new InstrumentResults
            {
                NPV = mcModel.SampleAccumulator.Mean(),
                ErrorEstimate = TRng.AllowsErrorEstimate ? mcModel.SampleAccumulator.ErrorEstimate() : null,
            };
        }

        protected override PathPricer<MonteCarloPath> PathPricer()
        {
            Date referenceDate = model.TermStructure.ReferenceDate;
            DayCounter dayCounter = model.TermStructure.DayCounter;
            double forwardMeasureTime = dayCounter.YearFraction(referenceDate, arguments.EndDates[^1]);
            return new HullWhiteCapFloorPricer(arguments, model, forwardMeasureTime);
        }

        protected override NanoQL.TimeGrid TimeGrid()
        {
            Date referenceDate = model.TermStructure.ReferenceDate;
            DayCounter dayCounter = model.TermStructure.DayCounter;

            // only add future fixing times...
            var times = new List<double>();
            for (int i = 0; i < arguments.FixingDates.Length; i++)
            {
                if (arguments.FixingDates[i] > referenceDate)
                {
                    times.Add(dayCounter.YearFraction(referenceDate, arguments.FixingDates[i]));
                }
            }

            // ...and maturity.
            times.Add(dayCounter.YearFraction(referenceDate, arguments.EndDates[^1]));
            return new NanoQL.TimeGrid(times.ToArray());
        }

        protected override IPathGenerator<MonteCarloPath> PathGenerator()
        {
            YieldTermStructure curve = model.TermStructure;
            Date referenceDate = curve.ReferenceDate;
            DayCounter dayCounter = curve.DayCounter;
            double forwardMeasureTime = dayCounter.YearFraction(referenceDate, arguments.EndDates[^1]);
            Vector parameters = model.Params();
            double a = parameters[0], sigma = parameters[1];
            HullWhiteForwardProcess process = new HullWhiteForwardProcess(curve, a, sigma).WithForwardMeasureTime(forwardMeasureTime);
            NanoQL.TimeGrid grid = TimeGrid();
            IRsg generator = SingleVariate<TRng>.MakeSequenceGenerator(grid.Size - 1, seed);
            return SingleVariate<TRng>.MakePathGenerator(process, grid, generator, brownianBridge);
        }
    }
}
