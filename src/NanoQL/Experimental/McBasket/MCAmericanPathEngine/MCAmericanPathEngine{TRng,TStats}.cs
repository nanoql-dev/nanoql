// SPDX-FileCopyrightText: Copyright (C) 2009 Andrea Odetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/mcbasket/mcamericanpathengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>The Longstaff-Schwartz Monte Carlo engine for path options with early exercise on correlated
/// processes.</summary>
/// <typeparam name="TRng">The random-number traits.</typeparam>
/// <typeparam name="TStats">The statistics.</typeparam>
/// <remarks>
/// The regression's basis is QuantLib's: monomials of order 2 in the payoff's state. The paths run on the clock
/// spanning every Black-Scholes process's structures, as <see cref="MCPathBasketEngine"/>'s, each payment discounted on
/// the first process's risk-free curve at its instant and each fixing's curve the risk-free curve implied from its date
/// (the S-xeq plan's XER5 (xxxiii), (xxxiv)); a market on one clock keeps QuantLib's draws and arithmetic.
/// </remarks>
[Experimental("NQL0001")]
public sealed class MCAmericanPathEngine<TRng, TStats> : MCLongstaffSchwartzPathEngine<TRng, TStats>
    where TRng : IRngTraits
    where TStats : IStatistics, new()
{
    private readonly StochasticProcessArray processes;
    private readonly TermStructure? clock;

    private MCAmericanPathEngine(
        PathMultiAssetOption option,
        EvaluationContext context,
        StochasticProcessArray processes,
        TermStructure? clock,
        int? timeSteps,
        int? timeStepsPerYear,
        bool brownianBridge,
        bool antitheticVariate,
        bool controlVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed,
        int? nCalibrationSamples)
        : base(
            option,
            context,
            clock is null ? processes : processes.OnClock(clock),
            timeSteps,
            timeStepsPerYear,
            brownianBridge,
            antitheticVariate,
            controlVariate,
            requiredSamples,
            requiredTolerance,
            maxSamples,
            seed,
            nCalibrationSamples)
    {
        this.processes = processes;
        this.clock = clock;
    }

    /// <summary>Prices a path option with early exercise.</summary>
    /// <param name="option">The path option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="processes">The assets' correlated processes, the first a Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/>.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/>.</param>
    /// <param name="brownianBridge">Whether to build the paths by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether to use antithetic paths.</param>
    /// <param name="controlVariate">Whether to use a control variate, which this engine doesn't provide.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The tolerance, or <see langword="null"/>.</param>
    /// <param name="maxSamples">The largest number of samples, or <see langword="null"/>.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <param name="nCalibrationSamples">The calibration samples, positive, or <see langword="null"/> for 2,048.</param>
    /// <returns>The value and, where the traits allow one, its error estimate.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The time steps are neither or both given, or not positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The seed or a sample count is out of range, or
    /// <paramref name="nCalibrationSamples"/> isn't positive.</exception>
    /// <exception cref="NanoQLException">
    /// The option has no payoff or no fixing date, the fixing dates aren't strictly increasing, the array is empty or its
    /// first process isn't a Black-Scholes process, neither samples nor a tolerance is given, or the simulation fails.
    /// </exception>
    public static InstrumentResults Calculate(
        PathMultiAssetOption option,
        EvaluationContext context,
        StochasticProcessArray processes,
        int? timeSteps,
        int? timeStepsPerYear,
        bool brownianBridge,
        bool antitheticVariate,
        bool controlVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed,
        int? nCalibrationSamples = null)
    {
        ArgumentNullException.ThrowIfNull(processes);
        var engine = new MCAmericanPathEngine<TRng, TStats>(
            option,
            context,
            processes,
            MCEuropeanBasketEngine.Clock(processes),
            timeSteps,
            timeStepsPerYear,
            brownianBridge,
            antitheticVariate,
            controlVariate,
            requiredSamples,
            requiredTolerance,
            maxSamples,
            seed,
            nCalibrationSamples);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        MonteCarloModel<MultiPath, TStats> model = engine.Run();
        return option.FetchResults(new InstrumentResults
        {
            NPV = model.SampleAccumulator.Mean(),
            ErrorEstimate = TRng.AllowsErrorEstimate ? model.SampleAccumulator.ErrorEstimate() : null,
        });
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The array is empty ("Stochastic process array required") or its first process
    /// isn't a Black-Scholes process, or the fixing dates and the grid's times differ ("Invalid dates/times").</exception>
    protected override LongstaffSchwartzMultiPathPricer LsmPathPricer()
    {
        if (!(processes.Size > 0))
        {
            throw new NanoQLException("Stochastic process array required");
        }

        if (processes.Process(0) is not GeneralizedBlackScholesProcess process)
        {
            throw new NanoQLException("generalized Black-Scholes process required");
        }

        (int[] positions, Vector discounts, ImmutableArray<YieldTermStructure> curves) =
            MCPathBasketEngine.FixingData(TimeGrid(), Option.FixingDates, process.RiskFreeRate, clock!);

        const int polynomialOrder = 2;
        const LsmBasisSystemPolynomialType polynomialType = LsmBasisSystemPolynomialType.Monomial;
        return new LongstaffSchwartzMultiPathPricer(Option.PathPayoff, positions, curves, discounts, polynomialOrder, polynomialType);
    }
}
