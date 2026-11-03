// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/latentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A latent model's factor sampler by copula inversion: each draw of a uniform sequence generator, every factor's
/// cumulative distribution inverted by the policy. QuantLib's default <c>LatentModel::FactorSampler&lt;USNG&gt;</c>.
/// </summary>
/// <remarks>
/// A mutable working type (§2.7): each draw changes it, and it isn't thread-safe. A draw returns a view of its buffer,
/// valid until its next draw, of weight 1, as QuantLib's. Its seed constructor is
/// <see cref="LatentModelFactorSampler.Create{TCopula, TInitTraits, TUsng}"/>, a seed in [1, 4294967295] with no
/// default, where QuantLib's defaults to 0, the clock; a generator given instead is copied. QuantLib's two
/// specializations by generator type are <see cref="BoxMullerFactorSampler{TUrng}"/> and
/// <see cref="PolarStudentTFactorSampler{TUrng}"/>, chosen by the caller.
/// </remarks>
/// <typeparam name="TCopula">The copula policy.</typeparam>
/// <typeparam name="TInitTraits">The policy's initialization parameters.</typeparam>
/// <typeparam name="TUsng">The uniform sequence generator.</typeparam>
[Experimental("NQL0001")]
[WorkingType]
public sealed class LatentModelFactorSampler<TCopula, TInitTraits, TUsng> : IRsg
    where TCopula : struct, ICopulaPolicy<TCopula, TInitTraits>
    where TInitTraits : struct
    where TUsng : class, IRsg
{
    private readonly TUsng sequenceGen;
    private readonly TCopula copula;
    private readonly double[] x;

    /// <summary>Initializes a new instance of the <see cref="LatentModelFactorSampler{TCopula, TInitTraits, TUsng}"/>
    /// class.</summary>
    /// <param name="copula">The copula policy.</param>
    /// <param name="generator">The uniform sequence generator, of the policy's number of factors, which the sampler
    /// copies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="generator"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The generator's dimension isn't the policy's number of factors, which QuantLib
    /// leaves to the caller to check.</exception>
    public LatentModelFactorSampler(TCopula copula, TUsng generator)
        : this(copula, (TUsng)(generator ?? throw new ArgumentNullException(nameof(generator))).Clone(), adopt: true)
    {
    }

    internal LatentModelFactorSampler(TCopula copula, TUsng generator, bool adopt)
    {
        _ = adopt;
        if (generator.Dimension != copula.NumFactors)
        {
            throw new ArgumentException("the generator's dimension isn't the copula's number of factors", nameof(generator));
        }

        sequenceGen = generator;
        this.copula = copula;
        x = new double[copula.NumFactors];
    }

    /// <inheritdoc/>
    public int Dimension => x.Length;

    /// <summary>Draws a sample of every factor: the systemic ones and then the idiosyncratic ones.</summary>
    /// <returns>The sample, a view of the sampler's buffer valid until its next draw, of weight 1.</returns>
    public Sample<ReadOnlyMemory<double>> NextSequence()
    {
        Sample<ReadOnlyMemory<double>> sample = sequenceGen.NextSequence();
        copula.AllFactorCumulInverter(sample.Value.Span).CopyTo(x, 0);
        return new(x, 1.0);
    }

    /// <inheritdoc/>
    public Sample<ReadOnlyMemory<double>> LastSequence() => new(x, 1.0);

    /// <summary>Copies the sampler deeply, its generator included.</summary>
    /// <returns>The copy.</returns>
    public LatentModelFactorSampler<TCopula, TInitTraits, TUsng> Clone()
    {
        var copy = new LatentModelFactorSampler<TCopula, TInitTraits, TUsng>(copula, (TUsng)sequenceGen.Clone(), adopt: true);
        x.CopyTo(copy.x, 0);
        return copy;
    }

    /// <inheritdoc/>
    IRsg IRsg.Clone() => Clone();
}
