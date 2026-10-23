// SPDX-FileCopyrightText: Copyright (C) 2006 Banca Profilo S.p.A.
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/capfloor/mchullwhiteengine.hpp ql/pricingengines/capfloor/mchullwhiteengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>A builder of the Monte Carlo Hull-White cap/floor engine, with QuantLib's defaults but the seed.</summary>
/// <typeparam name="TRng">The random-number traits.</typeparam>
/// <typeparam name="TStats">The statistics accumulator.</typeparam>
/// <remarks>
/// <para>
/// An immutable builder: each method returns a modified copy, so a builder kept and reused never changes under its
/// holder. <see cref="ToEngine"/> returns the engine as a function of the cap and the context, its settings bound, in
/// place of QuantLib's conversion to a pricing engine. A builder has no value equality.
/// </para>
/// <para>
/// The seed is a constructor argument (S2R4; the S-sr plan's SSR1 (iv)): QuantLib's default, 0, a seed from the clock,
/// is refused.
/// </para>
/// </remarks>
public sealed record MakeMCHullWhiteCapFloorEngine<TRng, TStats>
    where TRng : IRngTraits
    where TStats : IStatistics, new()
{
    /// <summary>Initializes a new instance of the <see cref="MakeMCHullWhiteCapFloorEngine{TRng, TStats}"/> record.</summary>
    /// <param name="model">The Hull-White model.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside its range.</exception>
    public MakeMCHullWhiteCapFloorEngine(HullWhite model, long seed)
    {
        ArgumentNullException.ThrowIfNull(model);
        Seeds.Check(seed);
        Model = model;
        Seed = seed;
    }

    private HullWhite Model { get; init; }

    private bool Antithetic { get; init; }

    private int? Samples { get; init; }

    private int? MaxSamples { get; init; }

    private double? Tolerance { get; init; }

    private bool BrownianBridge { get; init; }

    private long Seed { get; init; }

    /// <summary>Sets whether the paths are drawn through a Brownian bridge.</summary>
    /// <param name="b">Whether to use a Brownian bridge.</param>
    /// <returns>The modified builder.</returns>
    public MakeMCHullWhiteCapFloorEngine<TRng, TStats> WithBrownianBridge(bool b = true) => this with { BrownianBridge = b };

    /// <summary>Sets the number of samples.</summary>
    /// <param name="samples">The number of samples.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="samples"/> is negative.</exception>
    /// <exception cref="NanoQLException">A tolerance is already set.</exception>
    public MakeMCHullWhiteCapFloorEngine<TRng, TStats> WithSamples(int samples)
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
    public MakeMCHullWhiteCapFloorEngine<TRng, TStats> WithAbsoluteTolerance(double tolerance)
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
    public MakeMCHullWhiteCapFloorEngine<TRng, TStats> WithMaxSamples(int samples)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(samples);
        return this with { MaxSamples = samples };
    }

    /// <summary>Sets the seed.</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside its range.</exception>
    public MakeMCHullWhiteCapFloorEngine<TRng, TStats> WithSeed(long seed)
    {
        Seeds.Check(seed);
        return this with { Seed = seed };
    }

    /// <summary>Sets whether antithetic variates are used.</summary>
    /// <param name="b">Whether to use antithetic variates.</param>
    /// <returns>The modified builder.</returns>
    public MakeMCHullWhiteCapFloorEngine<TRng, TStats> WithAntitheticVariate(bool b = true) => this with { Antithetic = b };

    /// <summary>The engine with the builder's settings: QuantLib's conversion to a pricing engine.</summary>
    /// <returns>The engine, a function of the cap and the context.</returns>
    public Func<CapFloor, EvaluationContext, InstrumentResults> ToEngine()
    {
        HullWhite model = Model;
        bool brownianBridge = BrownianBridge, antithetic = Antithetic;
        int? samples = Samples, maxSamples = MaxSamples;
        double? tolerance = Tolerance;
        long seed = Seed;
        return (capFloor, context) => MCHullWhiteCapFloorEngine.Calculate<TRng, TStats>(
            capFloor, context, model, brownianBridge, antithetic, samples, tolerance, maxSamples, seed);
    }
}
