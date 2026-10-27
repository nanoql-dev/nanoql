// SPDX-FileCopyrightText: Copyright (C) 2008 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/asian/mc_discr_arith_av_strike.hpp ql/pricingengines/asian/mc_discr_arith_av_strike.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// QuantLib's builder of <see cref="MCDiscreteArithmeticASEngine"/>: named settings, QuantLib's defaults (a Brownian bridge, no antithetic
/// variate), then <see cref="ToEngine"/>.
/// </summary>
/// <typeparam name="TRng">The random-number traits.</typeparam>
/// <typeparam name="TStats">The statistics accumulator.</typeparam>
/// <remarks>
/// An immutable record (SEQR2): each setting returns a new builder. The seed is required (S2R4), where QuantLib's
/// defaults to 0, a clock-derived seed.
/// </remarks>
public sealed record MakeMCDiscreteArithmeticASEngine<TRng, TStats>
    where TRng : IRngTraits
    where TStats : IStatistics, new()
{
    /// <summary>Initializes a new instance of the <see cref="MakeMCDiscreteArithmeticASEngine{TRng, TStats}"/> class.</summary>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside its range.</exception>
    public MakeMCDiscreteArithmeticASEngine(GeneralizedBlackScholesProcess process, long seed)
    {
        ArgumentNullException.ThrowIfNull(process);
        Seeds.Check(seed);
        Process = process;
        Seed = seed;
    }

    private GeneralizedBlackScholesProcess Process { get; init; }

    private bool Antithetic { get; init; }

    private int? Samples { get; init; }

    private int? MaxSamples { get; init; }

    private double? Tolerance { get; init; }

    private bool BrownianBridge { get; init; } = true;

    private long Seed { get; init; }

    /// <summary>Sets whether the draws are ordered by a Brownian bridge.</summary>
    /// <param name="b">Whether they are.</param>
    /// <returns>The new builder.</returns>
    public MakeMCDiscreteArithmeticASEngine<TRng, TStats> WithBrownianBridge(bool b = true) => this with { BrownianBridge = b };

    /// <summary>Sets the number of samples.</summary>
    /// <param name="samples">The number of samples.</param>
    /// <returns>The new builder.</returns>
    /// <exception cref="NanoQLException">A tolerance is already set.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="samples"/> is negative.</exception>
    public MakeMCDiscreteArithmeticASEngine<TRng, TStats> WithSamples(int samples)
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
    /// <returns>The new builder.</returns>
    /// <exception cref="NanoQLException">A number of samples is already set, or the generator allows no error
    /// estimate.</exception>
    public MakeMCDiscreteArithmeticASEngine<TRng, TStats> WithAbsoluteTolerance(double tolerance)
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
    /// <param name="samples">The number of samples.</param>
    /// <returns>The new builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="samples"/> is negative.</exception>
    public MakeMCDiscreteArithmeticASEngine<TRng, TStats> WithMaxSamples(int samples)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(samples);
        return this with { MaxSamples = samples };
    }

    /// <summary>Sets the seed.</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The new builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside its range.</exception>
    public MakeMCDiscreteArithmeticASEngine<TRng, TStats> WithSeed(long seed)
    {
        Seeds.Check(seed);
        return this with { Seed = seed };
    }

    /// <summary>Sets whether antithetic variates are used.</summary>
    /// <param name="b">Whether they are.</param>
    /// <returns>The new builder.</returns>
    public MakeMCDiscreteArithmeticASEngine<TRng, TStats> WithAntitheticVariate(bool b = true) => this with { Antithetic = b };

    /// <summary>The engine, QuantLib's conversion to a pricing engine: a delegate pricing an option.</summary>
    /// <returns>The engine.</returns>
    public Func<DiscreteAveragingAsianOption, EvaluationContext, OneAssetOptionResults> ToEngine()
    {
        GeneralizedBlackScholesProcess process = Process;
        int? samples = Samples, maxSamples = MaxSamples;
        bool brownianBridge = BrownianBridge, antithetic = Antithetic;
        double? tolerance = Tolerance;
        long seed = Seed;
        return (option, context) => MCDiscreteArithmeticASEngine.Calculate<TRng, TStats>(
            option, context, process, brownianBridge, antithetic, samples, tolerance, maxSamples, seed);
    }
}
