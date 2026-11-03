// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/asian/mc_discr_arith_av_price_heston.hpp ql/pricingengines/asian/mc_discr_arith_av_price_heston.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// QuantLib's builder of <see cref="MCDiscreteArithmeticAPHestonEngine"/>: named settings, QuantLib's defaults (no
/// antithetic variate, no control variate, the grid the fixing times alone), then <see cref="ToEngine"/>.
/// </summary>
/// <typeparam name="TRng">The random-number traits.</typeparam>
/// <typeparam name="TStats">The statistics accumulator.</typeparam>
/// <remarks>
/// An immutable record: each setting returns a new builder. The seed is required, where QuantLib's defaults to 0, a
/// clock-derived seed.
/// </remarks>
public sealed record MakeMCDiscreteArithmeticAPHestonEngine<TRng, TStats>
    where TRng : IRngTraits
    where TStats : IStatistics, new()
{
    /// <summary>Initializes a new instance of the <see cref="MakeMCDiscreteArithmeticAPHestonEngine{TRng, TStats}"/> class.</summary>
    /// <param name="process">The Heston process.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside its range.</exception>
    public MakeMCDiscreteArithmeticAPHestonEngine(HestonProcess process, long seed)
    {
        ArgumentNullException.ThrowIfNull(process);
        Seeds.Check(seed);
        Process = process;
        Seed = seed;
    }

    private HestonProcess Process { get; init; }

    private bool Antithetic { get; init; }

    private bool ControlVariate { get; init; }

    private int? Samples { get; init; }

    private int? MaxSamples { get; init; }

    private int? Steps { get; init; }

    private int? StepsPerYear { get; init; }

    private double? Tolerance { get; init; }

    private long Seed { get; init; }

    /// <summary>Sets the number of samples.</summary>
    /// <param name="samples">The number of samples.</param>
    /// <returns>The new builder.</returns>
    /// <exception cref="NanoQLException">A tolerance is already set.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="samples"/> is negative.</exception>
    public MakeMCDiscreteArithmeticAPHestonEngine<TRng, TStats> WithSamples(int samples)
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
    public MakeMCDiscreteArithmeticAPHestonEngine<TRng, TStats> WithAbsoluteTolerance(double tolerance)
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
    public MakeMCDiscreteArithmeticAPHestonEngine<TRng, TStats> WithMaxSamples(int samples)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(samples);
        return this with { MaxSamples = samples };
    }

    /// <summary>Sets the seed.</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The new builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside its range.</exception>
    public MakeMCDiscreteArithmeticAPHestonEngine<TRng, TStats> WithSeed(long seed)
    {
        Seeds.Check(seed);
        return this with { Seed = seed };
    }

    /// <summary>Sets whether antithetic variates are used.</summary>
    /// <param name="b">Whether they are.</param>
    /// <returns>The new builder.</returns>
    public MakeMCDiscreteArithmeticAPHestonEngine<TRng, TStats> WithAntitheticVariate(bool b = true) => this with { Antithetic = b };

    /// <summary>Sets whether the geometric average is a control variate; QuantLib's default argument is
    /// <see langword="false"/>.</summary>
    /// <param name="b">Whether it is.</param>
    /// <returns>The new builder.</returns>
    public MakeMCDiscreteArithmeticAPHestonEngine<TRng, TStats> WithControlVariate(bool b = false) => this with { ControlVariate = b };

    /// <summary>Sets the number of time steps.</summary>
    /// <param name="steps">The number of steps.</param>
    /// <returns>The new builder.</returns>
    /// <exception cref="NanoQLException">A number of steps per year is already set.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    public MakeMCDiscreteArithmeticAPHestonEngine<TRng, TStats> WithSteps(int steps)
    {
        if (StepsPerYear is not null)
        {
            throw new NanoQLException("number of steps per year already set");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(steps);
        return this with { Steps = steps };
    }

    /// <summary>Sets the number of time steps per year.</summary>
    /// <param name="steps">The number of steps per year.</param>
    /// <returns>The new builder.</returns>
    /// <exception cref="NanoQLException">A number of steps is already set.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    public MakeMCDiscreteArithmeticAPHestonEngine<TRng, TStats> WithStepsPerYear(int steps)
    {
        if (Steps is not null)
        {
            throw new NanoQLException("number of steps already set");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(steps);
        return this with { StepsPerYear = steps };
    }

    /// <summary>The engine, QuantLib's conversion to a pricing engine: a delegate pricing an option.</summary>
    /// <returns>The engine.</returns>
    public Func<DiscreteAveragingAsianOption, EvaluationContext, OneAssetOptionResults> ToEngine()
    {
        HestonProcess process = Process;
        int? samples = Samples, maxSamples = MaxSamples, steps = Steps, stepsPerYear = StepsPerYear;
        bool antithetic = Antithetic, controlVariate = ControlVariate;
        double? tolerance = Tolerance;
        long seed = Seed;
        return (option, context) => MCDiscreteArithmeticAPHestonEngine.Calculate<TRng, TStats>(
            option, context, process, antithetic, samples, tolerance, maxSamples, seed, steps, stepsPerYear, controlVariate);
    }
}
