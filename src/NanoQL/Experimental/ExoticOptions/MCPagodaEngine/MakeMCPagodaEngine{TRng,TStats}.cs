// SPDX-FileCopyrightText: Copyright (C) 2008 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/exoticoptions/mcpagodaengine.hpp ql/experimental/exoticoptions/mcpagodaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.Experimental;

/// <summary>A builder of the Monte Carlo Pagoda engine, with QuantLib's defaults but the seed.</summary>
/// <typeparam name="TRng">The random-number traits.</typeparam>
/// <typeparam name="TStats">The statistics accumulator.</typeparam>
/// <remarks>
/// <para>
/// An immutable builder (SEQR2): each method returns a modified copy. <see cref="ToEngine"/> returns the engine as a
/// function of the option and the context, its settings bound, in place of QuantLib's conversion to a pricing engine.
/// A builder has no value equality.
/// </para>
/// <para>The seed is a constructor argument (S2R4; the S-xeq plan's XED3): QuantLib's default, 0, a seed from the clock,
/// is refused.</para>
/// </remarks>
[Experimental("NQL0001")]
public sealed record MakeMCPagodaEngine<TRng, TStats>
    where TRng : IRngTraits
    where TStats : IStatistics, new()
{
    /// <summary>Initializes a new instance of the <see cref="MakeMCPagodaEngine{TRng, TStats}"/>
    /// record.</summary>
    /// <param name="process">The assets' correlated processes.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside its range.</exception>
    public MakeMCPagodaEngine(StochasticProcessArray process, long seed)
    {
        ArgumentNullException.ThrowIfNull(process);
        Seeds.Check(seed);
        Process = process;
        Seed = seed;
    }

    private StochasticProcessArray Process { get; init; }

    private bool BrownianBridge { get; init; }

    private bool Antithetic { get; init; }

    private int? Samples { get; init; }

    private int? MaxSamples { get; init; }

    private double? Tolerance { get; init; }

    private long Seed { get; init; }

    /// <summary>Sets whether to build the paths by a Brownian bridge.</summary>
    /// <param name="b">Whether to.</param>
    /// <returns>A modified copy.</returns>
    public MakeMCPagodaEngine<TRng, TStats> WithBrownianBridge(bool b = true) => this with { BrownianBridge = b };

    /// <summary>Sets whether to use antithetic paths.</summary>
    /// <param name="b">Whether to.</param>
    /// <returns>A modified copy.</returns>
    public MakeMCPagodaEngine<TRng, TStats> WithAntitheticVariate(bool b = true) => this with { Antithetic = b };

    /// <summary>Sets the number of samples.</summary>
    /// <param name="samples">The number of samples.</param>
    /// <returns>A modified copy.</returns>
    /// <exception cref="NanoQLException">A tolerance is already set.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="samples"/> is negative.</exception>
    public MakeMCPagodaEngine<TRng, TStats> WithSamples(int samples)
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
    /// <returns>A modified copy.</returns>
    /// <exception cref="NanoQLException">
    /// A number of samples is already set, or the traits allow no error estimate.
    /// </exception>
    public MakeMCPagodaEngine<TRng, TStats> WithAbsoluteTolerance(double tolerance)
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

    /// <summary>Sets the largest number of samples.</summary>
    /// <param name="samples">The largest number of samples.</param>
    /// <returns>A modified copy.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="samples"/> is negative.</exception>
    public MakeMCPagodaEngine<TRng, TStats> WithMaxSamples(int samples)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(samples);
        return this with { MaxSamples = samples };
    }

    /// <summary>Sets the seed.</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>A modified copy.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside its range.</exception>
    public MakeMCPagodaEngine<TRng, TStats> WithSeed(long seed)
    {
        Seeds.Check(seed);
        return this with { Seed = seed };
    }

    /// <summary>The engine, its settings bound: QuantLib's conversion to a pricing engine.</summary>
    /// <returns>The engine as a function of the option and the context.</returns>
    public Func<PagodaOption, EvaluationContext, MultiAssetOptionResults> ToEngine()
    {
        StochasticProcessArray process = Process;
        int? samples = Samples, maxSamples = MaxSamples;
        bool brownianBridge = BrownianBridge, antithetic = Antithetic;
        double? tolerance = Tolerance;
        long seed = Seed;
        return (option, context) => MCPagodaEngine.Calculate<TRng, TStats>(
            option, context, process, brownianBridge, antithetic, samples, tolerance, maxSamples, seed);
    }
}
