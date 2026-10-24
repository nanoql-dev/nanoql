// SPDX-FileCopyrightText: Copyright (C) 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2022 Jonghee Lee
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/mcamericanengine.hpp ql/pricingengines/vanilla/mcamericanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>A builder of the Monte Carlo American engine, with QuantLib's defaults but the seed.</summary>
/// <typeparam name="TRng">The pricing random-number traits.</typeparam>
/// <typeparam name="TStats">The statistics accumulator.</typeparam>
/// <typeparam name="TRngCalibration">The calibration random-number traits.</typeparam>
/// <remarks>
/// <para>
/// An immutable builder (SEQR2): each method returns a modified copy, and <see cref="ToEngine"/> the engine as a
/// function of the option and the context, with QuantLib's checks on the steps. QuantLib's defaults: 2,048 calibration
/// samples, a polynomial order of 2, the monomial basis, no antithetic variates and no control variate.
/// </para>
/// <para>
/// The seed is a constructor argument (S2R4); the calibration seed is optional, its default QuantLib's documented one,
/// the seed plus 1,768,237,423 modulo 2³² (the S-van plan's VR4 (v)), where QuantLib's builder passes its narrowed null.
/// </para>
/// </remarks>
public sealed record MakeMCAmericanEngine<TRng, TStats, TRngCalibration>
    where TRng : IRngTraits
    where TStats : IStatistics, new()
    where TRngCalibration : IRngTraits
{
    /// <summary>Initializes a new instance of the <see cref="MakeMCAmericanEngine{TRng, TStats, TRngCalibration}"/> record.</summary>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside its range.</exception>
    public MakeMCAmericanEngine(GeneralizedBlackScholesProcess process, long seed)
    {
        ArgumentNullException.ThrowIfNull(process);
        Seeds.Check(seed);
        Process = process;
        Seed = seed;
    }

    private GeneralizedBlackScholesProcess Process { get; init; }

    private bool Antithetic { get; init; }

    private bool ControlVariate { get; init; }

    private int? Steps { get; init; }

    private int? StepsPerYear { get; init; }

    private int? Samples { get; init; }

    private int? MaxSamples { get; init; }

    private int CalibrationSamples { get; init; } = 2048;

    private double? Tolerance { get; init; }

    private long Seed { get; init; }

    private int PolynomialOrder { get; init; } = 2;

    private LsmBasisSystemPolynomialType PolynomialType { get; init; } = LsmBasisSystemPolynomialType.Monomial;

    private bool? AntitheticCalibration { get; init; }

    private long? SeedCalibration { get; init; }

    /// <summary>Sets the regression basis' highest order.</summary>
    /// <param name="polynomialOrder">The order.</param>
    /// <returns>The modified builder.</returns>
    public MakeMCAmericanEngine<TRng, TStats, TRngCalibration> WithPolynomialOrder(int polynomialOrder) =>
        this with { PolynomialOrder = polynomialOrder };

    /// <summary>Sets the regression basis' family.</summary>
    /// <param name="polynomialType">The family.</param>
    /// <returns>The modified builder.</returns>
    public MakeMCAmericanEngine<TRng, TStats, TRngCalibration> WithBasisSystem(LsmBasisSystemPolynomialType polynomialType) =>
        this with { PolynomialType = polynomialType };

    /// <summary>Sets the number of time steps.</summary>
    /// <param name="steps">The number of steps.</param>
    /// <returns>The modified builder.</returns>
    public MakeMCAmericanEngine<TRng, TStats, TRngCalibration> WithSteps(int steps) => this with { Steps = steps };

    /// <summary>Sets the number of time steps per year.</summary>
    /// <param name="steps">The number of steps per year.</param>
    /// <returns>The modified builder.</returns>
    public MakeMCAmericanEngine<TRng, TStats, TRngCalibration> WithStepsPerYear(int steps) => this with { StepsPerYear = steps };

    /// <summary>Sets the number of samples.</summary>
    /// <param name="samples">The number of samples.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="samples"/> is negative.</exception>
    /// <exception cref="NanoQLException">A tolerance is already set.</exception>
    public MakeMCAmericanEngine<TRng, TStats, TRngCalibration> WithSamples(int samples)
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
    public MakeMCAmericanEngine<TRng, TStats, TRngCalibration> WithAbsoluteTolerance(double tolerance)
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
    public MakeMCAmericanEngine<TRng, TStats, TRngCalibration> WithMaxSamples(int samples)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(samples);
        return this with { MaxSamples = samples };
    }

    /// <summary>Sets the number of calibration samples.</summary>
    /// <param name="calibrationSamples">The number of calibration samples.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="calibrationSamples"/> is negative.</exception>
    public MakeMCAmericanEngine<TRng, TStats, TRngCalibration> WithCalibrationSamples(int calibrationSamples)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(calibrationSamples);
        return this with { CalibrationSamples = calibrationSamples };
    }

    /// <summary>Sets the seed.</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside its range.</exception>
    public MakeMCAmericanEngine<TRng, TStats, TRngCalibration> WithSeed(long seed)
    {
        Seeds.Check(seed);
        return this with { Seed = seed };
    }

    /// <summary>Sets whether the pricing uses antithetic variates.</summary>
    /// <param name="b">Whether to use antithetic variates.</param>
    /// <returns>The modified builder.</returns>
    public MakeMCAmericanEngine<TRng, TStats, TRngCalibration> WithAntitheticVariate(bool b = true) => this with { Antithetic = b };

    /// <summary>Sets whether the European option is a control variate.</summary>
    /// <param name="b">Whether to use the control variate.</param>
    /// <returns>The modified builder.</returns>
    public MakeMCAmericanEngine<TRng, TStats, TRngCalibration> WithControlVariate(bool b = true) => this with { ControlVariate = b };

    /// <summary>Sets whether the calibration uses antithetic variates.</summary>
    /// <param name="b">Whether to use antithetic variates in the calibration.</param>
    /// <returns>The modified builder.</returns>
    public MakeMCAmericanEngine<TRng, TStats, TRngCalibration> WithAntitheticVariateCalibration(bool b = true) =>
        this with { AntitheticCalibration = b };

    /// <summary>Sets the calibration seed.</summary>
    /// <param name="seed">The calibration seed, in [1, 4294967295].</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside its range.</exception>
    public MakeMCAmericanEngine<TRng, TStats, TRngCalibration> WithSeedCalibration(long seed)
    {
        Seeds.Check(seed);
        return this with { SeedCalibration = seed };
    }

    /// <summary>The engine with the builder's settings: QuantLib's conversion to a pricing engine.</summary>
    /// <returns>The engine, a function of the option and the context.</returns>
    /// <exception cref="NanoQLException">The number of steps is given neither or both ways.</exception>
    public Func<VanillaOption, EvaluationContext, OneAssetOptionResults> ToEngine()
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
        bool antithetic = Antithetic, controlVariate = ControlVariate;
        double? tolerance = Tolerance;
        long seed = Seed;
        int polynomialOrder = PolynomialOrder, calibrationSamples = CalibrationSamples;
        LsmBasisSystemPolynomialType polynomialType = PolynomialType;
        bool? antitheticCalibration = AntitheticCalibration;
        long? seedCalibration = SeedCalibration;
        return (option, context) => MCAmericanEngine.Calculate<TRng, TStats, TRngCalibration>(
            option,
            context,
            process,
            steps,
            stepsPerYear,
            antithetic,
            controlVariate,
            samples,
            tolerance,
            maxSamples,
            seed,
            polynomialOrder,
            polynomialType,
            calibrationSamples,
            antitheticCalibration,
            seedCalibration);
    }
}
