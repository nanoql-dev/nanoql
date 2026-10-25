// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/mceuropeanhestonengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>A builder of the Monte Carlo Heston European engine, with QuantLib's defaults but the seed.</summary>
/// <typeparam name="TRng">The random-number traits.</typeparam>
/// <typeparam name="TStats">The statistics accumulator.</typeparam>
/// <remarks>
/// <para>
/// An immutable builder (SEQR2): each method returns a modified copy. <see cref="ToEngine"/> returns the engine as a
/// function of the option and the context, its settings bound, in place of QuantLib's conversion to a pricing engine.
/// QuantLib's checks stand: the steps given one way at most, the samples beside no tolerance, a tolerance only where
/// the traits estimate an error, and the steps given before the conversion. A builder has no value equality.
/// </para>
/// <para>
/// The seed is a constructor argument (S2R4; the S-hes plan's HR2 (v)): QuantLib's default, 0, a seed from the clock,
/// is refused.
/// </para>
/// </remarks>
public sealed record MakeMCEuropeanHestonEngine<TRng, TStats>
    where TRng : IRngTraits
    where TStats : IStatistics, new()
{
    /// <summary>Initializes a new instance of the <see cref="MakeMCEuropeanHestonEngine{TRng, TStats}"/> record.</summary>
    /// <param name="process">The Heston process, or a Bates process.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside its range.</exception>
    public MakeMCEuropeanHestonEngine(HestonProcess process, long seed)
    {
        ArgumentNullException.ThrowIfNull(process);
        Seeds.Check(seed);
        Process = process;
        Seed = seed;
    }

    private HestonProcess Process { get; init; }

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
    /// <exception cref="NanoQLException">The number of steps per year is already set.</exception>
    public MakeMCEuropeanHestonEngine<TRng, TStats> WithSteps(int steps)
    {
        if (StepsPerYear is not null)
        {
            throw new NanoQLException("number of steps per year already set");
        }

        return this with { Steps = steps };
    }

    /// <summary>Sets the number of time steps per year.</summary>
    /// <param name="steps">The number of steps per year.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="NanoQLException">The number of steps is already set.</exception>
    public MakeMCEuropeanHestonEngine<TRng, TStats> WithStepsPerYear(int steps)
    {
        if (Steps is not null)
        {
            throw new NanoQLException("number of steps already set");
        }

        return this with { StepsPerYear = steps };
    }

    /// <summary>Sets the number of samples.</summary>
    /// <param name="samples">The number of samples.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="samples"/> is negative.</exception>
    /// <exception cref="NanoQLException">A tolerance is already set.</exception>
    public MakeMCEuropeanHestonEngine<TRng, TStats> WithSamples(int samples)
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
    public MakeMCEuropeanHestonEngine<TRng, TStats> WithAbsoluteTolerance(double tolerance)
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
    public MakeMCEuropeanHestonEngine<TRng, TStats> WithMaxSamples(int samples)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(samples);
        return this with { MaxSamples = samples };
    }

    /// <summary>Sets the seed.</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside its range.</exception>
    public MakeMCEuropeanHestonEngine<TRng, TStats> WithSeed(long seed)
    {
        Seeds.Check(seed);
        return this with { Seed = seed };
    }

    /// <summary>Sets whether antithetic variates are used.</summary>
    /// <param name="b">Whether to use antithetic variates.</param>
    /// <returns>The modified builder.</returns>
    public MakeMCEuropeanHestonEngine<TRng, TStats> WithAntitheticVariate(bool b = true) => this with { Antithetic = b };

    /// <summary>The engine with the builder's settings: QuantLib's conversion to a pricing engine.</summary>
    /// <returns>The engine, a function of the option and the context.</returns>
    /// <exception cref="NanoQLException">No number of steps is given.</exception>
    public Func<VanillaOption, EvaluationContext, OneAssetOptionResults> ToEngine()
    {
        if (!(Steps is not null || StepsPerYear is not null))
        {
            throw new NanoQLException("number of steps not given");
        }

        HestonProcess process = Process;
        int? steps = Steps, stepsPerYear = StepsPerYear, samples = Samples, maxSamples = MaxSamples;
        bool antithetic = Antithetic;
        double? tolerance = Tolerance;
        long seed = Seed;
        return (option, context) => MCEuropeanHestonEngine.Calculate<TRng, TStats>(
            option, context, process, steps, stepsPerYear, antithetic, samples, tolerance, maxSamples, seed);
    }
}
