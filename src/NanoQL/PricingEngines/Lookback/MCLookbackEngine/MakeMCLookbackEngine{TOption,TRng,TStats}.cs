// SPDX-FileCopyrightText: Copyright (C) 2020 Lew Wei Hao
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/lookback/mclookbackengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Monte Carlo lookback engine's builder, QuantLib's <c>MakeMCLookbackEngine</c>: its members, defaults, checks and
/// texts, the seed required.
/// </summary>
/// <typeparam name="TOption">The lookback instrument the engine prices: one of the four continuous lookback
/// options.</typeparam>
/// <typeparam name="TRng">The random-number traits.</typeparam>
/// <typeparam name="TStats">The statistics accumulator.</typeparam>
/// <remarks>
/// A sealed, immutable record: each <c>With</c> member returns a copy. <see cref="ToEngine"/> returns
/// <see cref="MCLookbackEngine"/>'s overload for the instrument type; another type, which QuantLib's template can't
/// instantiate, is refused there.
/// </remarks>
public sealed record MakeMCLookbackEngine<TOption, TRng, TStats>
    where TOption : OneAssetOption
    where TRng : IRngTraits
    where TStats : IStatistics, new()
{
    /// <summary>Initializes a new instance of the <see cref="MakeMCLookbackEngine{TOption, TRng, TStats}"/> class.</summary>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The seed is outside its range.</exception>
    public MakeMCLookbackEngine(GeneralizedBlackScholesProcess process, long seed)
    {
        ArgumentNullException.ThrowIfNull(process);
        Seeds.Check(seed);
        Process = process;
        Seed = seed;
    }

    private GeneralizedBlackScholesProcess Process { get; init; }

    private bool Antithetic { get; init; }

    private int? Steps { get; init; }

    private int? StepsPerYear { get; init; }

    private int? Samples { get; init; }

    private int? MaxSamples { get; init; }

    private double? Tolerance { get; init; }

    private bool BrownianBridge { get; init; }

    private long Seed { get; init; }

    /// <summary>Sets the number of time steps.</summary>
    /// <param name="steps">The number of time steps.</param>
    /// <returns>A copy with the steps set.</returns>
    public MakeMCLookbackEngine<TOption, TRng, TStats> WithSteps(int steps) => this with { Steps = steps };

    /// <summary>Sets the number of time steps per year.</summary>
    /// <param name="steps">The number of time steps per year.</param>
    /// <returns>A copy with the steps per year set.</returns>
    public MakeMCLookbackEngine<TOption, TRng, TStats> WithStepsPerYear(int steps) => this with { StepsPerYear = steps };

    /// <summary>Sets whether the draws are ordered by a Brownian bridge.</summary>
    /// <param name="b">Whether to use a Brownian bridge.</param>
    /// <returns>A copy with the choice set.</returns>
    public MakeMCLookbackEngine<TOption, TRng, TStats> WithBrownianBridge(bool b = true) => this with { BrownianBridge = b };

    /// <summary>Sets whether antithetic variates are used.</summary>
    /// <param name="b">Whether to use antithetic variates.</param>
    /// <returns>A copy with the choice set.</returns>
    public MakeMCLookbackEngine<TOption, TRng, TStats> WithAntitheticVariate(bool b = true) => this with { Antithetic = b };

    /// <summary>Sets the number of samples.</summary>
    /// <param name="samples">The number of samples.</param>
    /// <returns>A copy with the samples set.</returns>
    /// <exception cref="NanoQLException">A tolerance is already set.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="samples"/> is negative.</exception>
    public MakeMCLookbackEngine<TOption, TRng, TStats> WithSamples(int samples)
    {
        if (Tolerance is not null)
        {
            throw new NanoQLException("tolerance already set");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(samples);
        return this with { Samples = samples };
    }

    /// <summary>Sets the absolute tolerance.</summary>
    /// <param name="tolerance">The tolerance.</param>
    /// <returns>A copy with the tolerance set.</returns>
    /// <exception cref="NanoQLException">A number of samples is already set, or the traits give no error
    /// estimate.</exception>
    public MakeMCLookbackEngine<TOption, TRng, TStats> WithAbsoluteTolerance(double tolerance)
    {
        if (Samples is not null)
        {
            throw new NanoQLException("number of samples already set");
        }

        if (!TRng.AllowsErrorEstimate)
        {
            throw new NanoQLException("chosen random generator policy does not allow an error estimate");
        }

        return this with { Tolerance = tolerance };
    }

    /// <summary>Sets the most samples for a tolerance.</summary>
    /// <param name="samples">The most samples.</param>
    /// <returns>A copy with the limit set.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="samples"/> is negative.</exception>
    public MakeMCLookbackEngine<TOption, TRng, TStats> WithMaxSamples(int samples)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(samples);
        return this with { MaxSamples = samples };
    }

    /// <summary>Sets the seed.</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>A copy with the seed set.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The seed is outside its range.</exception>
    public MakeMCLookbackEngine<TOption, TRng, TStats> WithSeed(long seed)
    {
        Seeds.Check(seed);
        return this with { Seed = seed };
    }

    /// <summary>The engine: QuantLib's conversion to a pricing engine, its checks of the steps.</summary>
    /// <returns>The engine as a delegate on the instrument and the evaluation context.</returns>
    /// <exception cref="NanoQLException">
    /// The steps are given neither or both ways, or the instrument type isn't one of the four continuous lookback
    /// options.
    /// </exception>
    public Func<TOption, EvaluationContext, OneAssetOptionResults> ToEngine()
    {
        if (!(Steps is not null || StepsPerYear is not null))
        {
            throw new NanoQLException("number of steps not given");
        }

        if (!(Steps is null || StepsPerYear is null))
        {
            throw new NanoQLException("number of steps overspecified");
        }

        GeneralizedBlackScholesProcess process = Process;
        int? steps = Steps, stepsPerYear = StepsPerYear, samples = Samples, maxSamples = MaxSamples;
        bool brownianBridge = BrownianBridge, antithetic = Antithetic;
        double? tolerance = Tolerance;
        long seed = Seed;
        Type t = typeof(TOption);
        if (t == typeof(ContinuousPartialFloatingLookbackOption))
        {
            return (option, context) => MCLookbackEngine.Calculate<TRng, TStats>(
                (ContinuousPartialFloatingLookbackOption)(object)option, context, process, steps, stepsPerYear,
                brownianBridge, antithetic, samples, tolerance, maxSamples, seed);
        }

        if (t == typeof(ContinuousPartialFixedLookbackOption))
        {
            return (option, context) => MCLookbackEngine.Calculate<TRng, TStats>(
                (ContinuousPartialFixedLookbackOption)(object)option, context, process, steps, stepsPerYear,
                brownianBridge, antithetic, samples, tolerance, maxSamples, seed);
        }

        if (t == typeof(ContinuousFloatingLookbackOption))
        {
            return (option, context) => MCLookbackEngine.Calculate<TRng, TStats>(
                (ContinuousFloatingLookbackOption)(object)option, context, process, steps, stepsPerYear,
                brownianBridge, antithetic, samples, tolerance, maxSamples, seed);
        }

        if (t == typeof(ContinuousFixedLookbackOption))
        {
            return (option, context) => MCLookbackEngine.Calculate<TRng, TStats>(
                (ContinuousFixedLookbackOption)(object)option, context, process, steps, stepsPerYear,
                brownianBridge, antithetic, samples, tolerance, maxSamples, seed);
        }

        throw new NanoQLException(
            $"no Monte Carlo lookback engine for {t.Name}: the engine prices ContinuousFixedLookbackOption, "
            + "ContinuousFloatingLookbackOption, ContinuousPartialFixedLookbackOption and "
            + "ContinuousPartialFloatingLookbackOption");
    }
}
