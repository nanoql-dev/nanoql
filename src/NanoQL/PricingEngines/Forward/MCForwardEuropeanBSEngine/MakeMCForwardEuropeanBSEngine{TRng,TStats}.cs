// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/forward/mcforwardeuropeanbsengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>A builder of the Monte Carlo forward-start Black-Scholes engine, with QuantLib's defaults but the seed.</summary>
/// <typeparam name="TRng">The random-number traits.</typeparam>
/// <typeparam name="TStats">The statistics accumulator.</typeparam>
/// <remarks>
/// An immutable builder (SEQR2): each method returns a modified copy. <see cref="ToEngine"/> returns the engine as a
/// function of the option and the context, its settings bound, in place of QuantLib's conversion to a pricing engine,
/// with QuantLib's checks on the steps. The seed is a constructor argument (S2R4). QuantLib's
/// <c>withBrownianBridge</c> defaults its argument to <see langword="false"/>, kept. A builder has no value equality.
/// </remarks>
public sealed record MakeMCForwardEuropeanBSEngine<TRng, TStats>
    where TRng : IRngTraits
    where TStats : IStatistics, new()
{
    /// <summary>Initializes a new instance of the <see cref="MakeMCForwardEuropeanBSEngine{TRng, TStats}"/> record.</summary>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside its range.</exception>
    public MakeMCForwardEuropeanBSEngine(GeneralizedBlackScholesProcess process, long seed)
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
    /// <param name="steps">The number of steps.</param>
    /// <returns>The modified builder.</returns>
    public MakeMCForwardEuropeanBSEngine<TRng, TStats> WithSteps(int steps) => this with { Steps = steps };

    /// <summary>Sets the number of time steps per year.</summary>
    /// <param name="steps">The number of steps per year.</param>
    /// <returns>The modified builder.</returns>
    public MakeMCForwardEuropeanBSEngine<TRng, TStats> WithStepsPerYear(int steps) => this with { StepsPerYear = steps };

    /// <summary>Sets whether the draws are ordered by a Brownian bridge.</summary>
    /// <param name="b">Whether to use a Brownian bridge; QuantLib's default is <see langword="false"/>.</param>
    /// <returns>The modified builder.</returns>
    public MakeMCForwardEuropeanBSEngine<TRng, TStats> WithBrownianBridge(bool b = false) => this with { BrownianBridge = b };

    /// <summary>Sets the number of samples.</summary>
    /// <param name="samples">The number of samples.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="samples"/> is negative.</exception>
    /// <exception cref="NanoQLException">A tolerance is already set.</exception>
    public MakeMCForwardEuropeanBSEngine<TRng, TStats> WithSamples(int samples)
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
    /// <returns>The modified builder.</returns>
    /// <exception cref="NanoQLException">
    /// A number of samples is already set, or the random-number traits allow no error estimate.
    /// </exception>
    public MakeMCForwardEuropeanBSEngine<TRng, TStats> WithAbsoluteTolerance(double tolerance)
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
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="samples"/> is negative.</exception>
    public MakeMCForwardEuropeanBSEngine<TRng, TStats> WithMaxSamples(int samples)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(samples);
        return this with { MaxSamples = samples };
    }

    /// <summary>Sets the seed.</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside its range.</exception>
    public MakeMCForwardEuropeanBSEngine<TRng, TStats> WithSeed(long seed)
    {
        Seeds.Check(seed);
        return this with { Seed = seed };
    }

    /// <summary>Sets whether antithetic variates are used.</summary>
    /// <param name="b">Whether to use antithetic variates.</param>
    /// <returns>The modified builder.</returns>
    public MakeMCForwardEuropeanBSEngine<TRng, TStats> WithAntitheticVariate(bool b = true) => this with { Antithetic = b };

    /// <summary>The engine with the builder's settings: QuantLib's conversion to a pricing engine.</summary>
    /// <returns>The engine, a function of the option and the context.</returns>
    /// <exception cref="NanoQLException">The number of steps is given neither or both ways.</exception>
    public Func<ForwardVanillaOption, EvaluationContext, OneAssetOptionResults> ToEngine()
    {
        if (!(Steps is not null || StepsPerYear is not null))
        {
            throw new NanoQLException("number of steps not given");
        }

        if (!(Steps is null || StepsPerYear is null))
        {
            throw new NanoQLException("number of steps overspecified - set EITHER steps OR stepsPerYear");
        }

        GeneralizedBlackScholesProcess process = Process;
        int? steps = Steps, stepsPerYear = StepsPerYear, samples = Samples, maxSamples = MaxSamples;
        bool brownianBridge = BrownianBridge, antithetic = Antithetic;
        double? tolerance = Tolerance;
        long seed = Seed;
        return (option, context) => MCForwardEuropeanBSEngine.Calculate<TRng, TStats>(
            option, context, process, steps, stepsPerYear, brownianBridge, antithetic, samples, tolerance, maxSamples, seed);
    }
}
