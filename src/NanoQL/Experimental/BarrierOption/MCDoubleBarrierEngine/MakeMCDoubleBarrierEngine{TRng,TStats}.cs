// SPDX-FileCopyrightText: Copyright (C) 2020 Lew Wei Hao
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/barrieroption/mcdoublebarrierengine.hpp ql/experimental/barrieroption/mcdoublebarrierengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.Experimental;

/// <summary>A builder of the Monte Carlo double-barrier engine, with QuantLib's defaults but the seed.</summary>
/// <typeparam name="TRng">The random-number traits.</typeparam>
/// <typeparam name="TStats">The statistics accumulator.</typeparam>
/// <remarks>
/// <para>
/// An immutable builder (SEQR2): each method returns a modified copy. <see cref="ToEngine"/> returns the engine as a
/// function of the option and the context, its settings bound, in place of QuantLib's conversion to a pricing engine,
/// with QuantLib's checks on the steps. A builder has no value equality.
/// </para>
/// <para>
/// The seed is a constructor argument (S2R4, the S-xeq plan's XED3): QuantLib's default, 0, a seed from the clock, is
/// refused.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed record MakeMCDoubleBarrierEngine<TRng, TStats>
    where TRng : IRngTraits
    where TStats : IStatistics, new()
{
    /// <summary>Initializes a new instance of the <see cref="MakeMCDoubleBarrierEngine{TRng, TStats}"/> record.</summary>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside its range.</exception>
    public MakeMCDoubleBarrierEngine(GeneralizedBlackScholesProcess process, long seed)
    {
        ArgumentNullException.ThrowIfNull(process);
        Seeds.Check(seed);
        Process = process;
        Seed = seed;
    }

    private GeneralizedBlackScholesProcess Process { get; init; }

    private bool BrownianBridge { get; init; }

    private bool Antithetic { get; init; }

    private int? Steps { get; init; }

    private int? StepsPerYear { get; init; }

    private int? Samples { get; init; }

    private int? MaxSamples { get; init; }

    private double? Tolerance { get; init; }

    private long Seed { get; init; }

    /// <summary>Sets the number of time steps.</summary>
    /// <param name="steps">The number of steps.</param>
    /// <returns>The modified builder.</returns>
    public MakeMCDoubleBarrierEngine<TRng, TStats> WithSteps(int steps) => this with { Steps = steps };

    /// <summary>Sets the number of time steps per year.</summary>
    /// <param name="steps">The number of steps per year.</param>
    /// <returns>The modified builder.</returns>
    public MakeMCDoubleBarrierEngine<TRng, TStats> WithStepsPerYear(int steps) => this with { StepsPerYear = steps };

    /// <summary>Sets whether the draws are ordered by a Brownian bridge.</summary>
    /// <param name="b">Whether to use a Brownian bridge.</param>
    /// <returns>The modified builder.</returns>
    public MakeMCDoubleBarrierEngine<TRng, TStats> WithBrownianBridge(bool b = true) => this with { BrownianBridge = b };

    /// <summary>Sets whether antithetic variates are used.</summary>
    /// <param name="b">Whether to use antithetic variates.</param>
    /// <returns>The modified builder.</returns>
    public MakeMCDoubleBarrierEngine<TRng, TStats> WithAntitheticVariate(bool b = true) => this with { Antithetic = b };

    /// <summary>Sets the number of samples.</summary>
    /// <param name="samples">The number of samples.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="samples"/> is negative.</exception>
    /// <exception cref="NanoQLException">A tolerance is already set.</exception>
    public MakeMCDoubleBarrierEngine<TRng, TStats> WithSamples(int samples)
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
    public MakeMCDoubleBarrierEngine<TRng, TStats> WithAbsoluteTolerance(double tolerance)
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
    public MakeMCDoubleBarrierEngine<TRng, TStats> WithMaxSamples(int samples)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(samples);
        return this with { MaxSamples = samples };
    }

    /// <summary>Sets the seed.</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside its range.</exception>
    public MakeMCDoubleBarrierEngine<TRng, TStats> WithSeed(long seed)
    {
        Seeds.Check(seed);
        return this with { Seed = seed };
    }

    /// <summary>The engine with the builder's settings: QuantLib's conversion to a pricing engine.</summary>
    /// <returns>The engine, a function of the option and the context.</returns>
    /// <exception cref="NanoQLException">The number of steps is given neither or both ways.</exception>
    public Func<DoubleBarrierOption, EvaluationContext, OneAssetOptionResults> ToEngine()
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
        return (option, context) => MCDoubleBarrierEngine.Calculate<TRng, TStats>(
            option, context, process, steps, stepsPerYear, brownianBridge, antithetic, samples, tolerance, maxSamples, seed);
    }
}
