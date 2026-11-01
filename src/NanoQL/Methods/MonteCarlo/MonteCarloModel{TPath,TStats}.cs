// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/montecarlomodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>A general-purpose Monte Carlo model for path samples.</summary>
/// <typeparam name="TPath">The path type: QuantLib's single- or multi-variate traits' <c>path_type</c>.</typeparam>
/// <typeparam name="TStats">The statistics accumulator.</typeparam>
/// <remarks>
/// <para>
/// The model holds a path generator and a path pricer, and in case of the control variate technique the control
/// option's path pricer and value, with optionally the control's own path generator. Each sample is the pricer's value
/// on a new path, corrected by the control variate, and averaged with its antithetic path's where antithetic variates
/// are used, in QuantLib's order; it is added to the accumulator with the path's weight.
/// </para>
/// <para>
/// A working type (S2R2): <see cref="AddSamples"/> draws from its generators and adds to its accumulator, which it
/// owns. A <see cref="NanoQL.PricingEngines.McSimulation{TPath, TStats}"/> builds one per calculation and returns it.
/// Each batch reserves the accumulator for its samples first, so a one-dimensional batch allocates nothing per sample
/// (the S-sr plan's SSR1 (v)).
/// </para>
/// </remarks>
[WorkingType]
public sealed class MonteCarloModel<TPath, TStats>
    where TStats : IStatistics
{
    private readonly IPathGenerator<TPath> pathGenerator;
    private readonly PathPricer<TPath> pathPricer;
    private readonly bool isAntitheticVariate;
    private readonly PathPricer<TPath>? cvPathPricer;
    private readonly double cvOptionValue;
    private readonly bool isControlVariate;
    private readonly IPathGenerator<TPath>? cvPathGenerator;

    /// <summary>Initializes a new instance of the <see cref="MonteCarloModel{TPath, TStats}"/> class.</summary>
    /// <param name="pathGenerator">The path generator, which the model draws from.</param>
    /// <param name="pathPricer">The path pricer.</param>
    /// <param name="sampleAccumulator">The accumulator, which the model owns and adds to.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="cvPathPricer">The control option's path pricer; <see langword="null"/> for no control variate.</param>
    /// <param name="cvOptionValue">The control option's value.</param>
    /// <param name="cvPathGenerator">
    /// The control's path generator; <see langword="null"/> for the control priced on the model's paths.
    /// </param>
    public MonteCarloModel(
        IPathGenerator<TPath> pathGenerator,
        PathPricer<TPath> pathPricer,
        TStats sampleAccumulator,
        bool antitheticVariate,
        PathPricer<TPath>? cvPathPricer = null,
        double cvOptionValue = 0.0,
        IPathGenerator<TPath>? cvPathGenerator = null)
    {
        ArgumentNullException.ThrowIfNull(pathGenerator);
        ArgumentNullException.ThrowIfNull(pathPricer);
        ArgumentNullException.ThrowIfNull(sampleAccumulator);
        this.pathGenerator = pathGenerator;
        this.pathPricer = pathPricer;
        SampleAccumulator = sampleAccumulator;
        isAntitheticVariate = antitheticVariate;
        this.cvPathPricer = cvPathPricer;
        this.cvOptionValue = cvOptionValue;
        this.cvPathGenerator = cvPathGenerator;
        isControlVariate = cvPathPricer is not null;
    }

    /// <summary>Gets the sample accumulator, which the model adds to.</summary>
    public TStats SampleAccumulator { get; }

    /// <summary>Adds samples to the accumulator.</summary>
    /// <param name="samples">The number of samples.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="samples"/> is negative.</exception>
    public void AddSamples(int samples)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(samples);

        // the batch's storage reserved once, so that the loop allocates nothing per sample
        SampleAccumulator.Reserve((int)Math.Min((long)SampleAccumulator.Samples + samples, int.MaxValue));
        for (int j = 1; j <= samples; j++)
        {
            Sample<TPath> path = pathGenerator.Next();
            double price = pathPricer.Evaluate(path.Value);

            if (isControlVariate)
            {
                if (cvPathGenerator is null)
                {
                    price += cvOptionValue - cvPathPricer!.Evaluate(path.Value);
                }
                else
                {
                    Sample<TPath> cvPath = cvPathGenerator.Next();
                    price += cvOptionValue - cvPathPricer!.Evaluate(cvPath.Value);
                }
            }

            if (isAntitheticVariate)
            {
                Sample<TPath> atPath = pathGenerator.Antithetic();
                double price2 = pathPricer.Evaluate(atPath.Value);
                if (isControlVariate)
                {
                    if (cvPathGenerator is null)
                    {
                        price2 += cvOptionValue - cvPathPricer!.Evaluate(atPath.Value);
                    }
                    else
                    {
                        Sample<TPath> cvPath = cvPathGenerator.Antithetic();
                        price2 += cvOptionValue - cvPathPricer!.Evaluate(cvPath.Value);
                    }
                }

                SampleAccumulator.Add((price + price2) / 2.0, path.Weight);
            }
            else
            {
                SampleAccumulator.Add(price, path.Weight);
            }
        }
    }
}
