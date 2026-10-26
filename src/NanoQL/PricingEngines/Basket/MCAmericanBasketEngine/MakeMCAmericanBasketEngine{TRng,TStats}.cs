// SPDX-FileCopyrightText: Copyright (C) 2004 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/mcamericanbasketengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>A builder of the Longstaff-Schwartz basket engine, with QuantLib's defaults but the seed.</summary>
/// <typeparam name="TRng">The random-number traits.</typeparam>
/// <typeparam name="TStats">The statistics accumulator.</typeparam>
/// <remarks>
/// <para>
/// An immutable builder (SEQR2): each method returns a modified copy. <see cref="ToEngine"/> returns the engine as a
/// function of the option and the context, its settings bound, in place of QuantLib's conversion to a pricing engine,
/// with QuantLib's checks on the steps. A builder has no value equality.
/// </para>
/// <para>The seed is a constructor argument (S2R4): QuantLib's default, 0, a seed from the clock, is refused.</para>
/// </remarks>
public sealed record MakeMCAmericanBasketEngine<TRng, TStats>
    where TRng : IRngTraits
    where TStats : IStatistics, new()
{
    /// <summary>Initializes a new instance of the <see cref="MakeMCAmericanBasketEngine{TRng, TStats}"/>
    /// record.</summary>
    /// <param name="process">The assets' correlated processes.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside its range.</exception>
    public MakeMCAmericanBasketEngine(StochasticProcessArray process, long seed)
    {
        ArgumentNullException.ThrowIfNull(process);
        Seeds.Check(seed);
        Process = process;
        Seed = seed;
    }

    private StochasticProcessArray Process { get; init; }

    private bool BrownianBridge { get; init; }

    private bool Antithetic { get; init; }

    private int? Steps { get; init; }

    private int? StepsPerYear { get; init; }

    private int? Samples { get; init; }

    private int? MaxSamples { get; init; }

    private double? Tolerance { get; init; }

    private long Seed { get; init; }

    private int? CalibrationSamples { get; init; }

    private int PolynomialOrder { get; init; } = 2;

    private LsmBasisSystemPolynomialType PolynomialType { get; init; } = LsmBasisSystemPolynomialType.Monomial;

    /// <summary>Sets the number of time steps.</summary>
    /// <param name="steps">The number of time steps.</param>
    /// <returns>A modified copy.</returns>
    public MakeMCAmericanBasketEngine<TRng, TStats> WithSteps(int steps) => this with { Steps = steps };

    /// <summary>Sets the number of time steps per year.</summary>
    /// <param name="steps">The number of time steps per year.</param>
    /// <returns>A modified copy.</returns>
    public MakeMCAmericanBasketEngine<TRng, TStats> WithStepsPerYear(int steps) => this with { StepsPerYear = steps };

    /// <summary>Sets whether to build the paths by a Brownian bridge.</summary>
    /// <param name="b">Whether to.</param>
    /// <returns>A modified copy.</returns>
    public MakeMCAmericanBasketEngine<TRng, TStats> WithBrownianBridge(bool b = true) => this with { BrownianBridge = b };

    /// <summary>Sets whether to use antithetic paths.</summary>
    /// <param name="b">Whether to.</param>
    /// <returns>A modified copy.</returns>
    public MakeMCAmericanBasketEngine<TRng, TStats> WithAntitheticVariate(bool b = true) => this with { Antithetic = b };

    /// <summary>Sets the number of samples.</summary>
    /// <param name="samples">The number of samples.</param>
    /// <returns>A modified copy.</returns>
    /// <exception cref="NanoQLException">A tolerance is already set.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="samples"/> is negative.</exception>
    public MakeMCAmericanBasketEngine<TRng, TStats> WithSamples(int samples)
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
    public MakeMCAmericanBasketEngine<TRng, TStats> WithAbsoluteTolerance(double tolerance)
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
    public MakeMCAmericanBasketEngine<TRng, TStats> WithMaxSamples(int samples)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(samples);
        return this with { MaxSamples = samples };
    }

    /// <summary>Sets the seed.</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>A modified copy.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside its range.</exception>
    public MakeMCAmericanBasketEngine<TRng, TStats> WithSeed(long seed)
    {
        Seeds.Check(seed);
        return this with { Seed = seed };
    }

    /// <summary>Sets the number of calibration samples.</summary>
    /// <param name="samples">The number of calibration samples.</param>
    /// <returns>A modified copy.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="samples"/> is negative.</exception>
    public MakeMCAmericanBasketEngine<TRng, TStats> WithCalibrationSamples(int samples)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(samples);
        return this with { CalibrationSamples = samples };
    }

    /// <summary>Sets the regression basis' highest order.</summary>
    /// <param name="polynomialOrder">The order.</param>
    /// <returns>A modified copy.</returns>
    public MakeMCAmericanBasketEngine<TRng, TStats> WithPolynomialOrder(int polynomialOrder) =>
        this with { PolynomialOrder = polynomialOrder };

    /// <summary>Sets the regression basis' family.</summary>
    /// <param name="polynomialType">The family.</param>
    /// <returns>A modified copy.</returns>
    public MakeMCAmericanBasketEngine<TRng, TStats> WithBasisSystem(LsmBasisSystemPolynomialType polynomialType) =>
        this with { PolynomialType = polynomialType };

    /// <summary>The engine, its settings bound: QuantLib's conversion to a pricing engine.</summary>
    /// <returns>The engine as a function of the option and the context.</returns>
    /// <exception cref="NanoQLException">The number of steps is neither or both given.</exception>
    public Func<BasketOption, EvaluationContext, MultiAssetOptionResults> ToEngine()
    {
        if (!(Steps is not null || StepsPerYear is not null))
        {
            throw new NanoQLException("number of steps not given");
        }

        if (!(Steps is null || StepsPerYear is null))
        {
            throw new NanoQLException("number of steps overspecified");
        }

        StochasticProcessArray process = Process;
        int? steps = Steps, stepsPerYear = StepsPerYear, samples = Samples, maxSamples = MaxSamples;
        bool brownianBridge = BrownianBridge, antithetic = Antithetic;
        double? tolerance = Tolerance;
        long seed = Seed;
        int? calibrationSamples = CalibrationSamples;
        int polynomialOrder = PolynomialOrder;
        LsmBasisSystemPolynomialType polynomialType = PolynomialType;
        return (option, context) => MCAmericanBasketEngine.Calculate<TRng, TStats>(
            option,
            context,
            process,
            steps,
            stepsPerYear,
            brownianBridge,
            antithetic,
            samples,
            tolerance,
            maxSamples,
            seed,
            calibrationSamples,
            polynomialOrder,
            polynomialType);
    }
}
