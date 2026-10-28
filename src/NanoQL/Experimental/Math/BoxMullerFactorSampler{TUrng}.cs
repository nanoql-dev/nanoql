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
/// A Gaussian latent model's factor sampler by Box-Muller: a sequence of Box-Muller normal draws, bypassing the copula.
/// QuantLib's <c>LatentModel::FactorSampler</c> specialized on <c>RandomSequenceGenerator&lt;BoxMullerGaussianRng&gt;</c>.
/// </summary>
/// <remarks>
/// A mutable working type (§2.7), not for a multithreaded simulation: QuantLib's Box-Muller is the rejection variant.
/// Its seed constructor is <see cref="BoxMullerFactorSampler.Create{TUrng}"/>, a seed in [1, 4294967295] with no
/// default, where QuantLib's defaults to 0, the clock (the S-xcr plan's XR4 (a)); a generator given instead is copied.
/// </remarks>
/// <typeparam name="TUrng">The uniform generator.</typeparam>
[Experimental("NQL0001")]
public sealed class BoxMullerFactorSampler<TUrng> : IRsg
    where TUrng : class, IRng
{
    private readonly RandomSequenceGenerator<BoxMullerGaussianRng<TUrng>> boxMullRng;

    /// <summary>Initializes a new instance of the <see cref="BoxMullerFactorSampler{TUrng}"/> class.</summary>
    /// <param name="copula">The Gaussian copula policy, whose number of factors the sequences have.</param>
    /// <param name="urng">The uniform generator, which the sampler copies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="urng"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="copula"/> is the zero value.</exception>
    public BoxMullerFactorSampler(GaussianCopulaPolicy copula, TUrng urng)
    {
        ArgumentNullException.ThrowIfNull(urng);
        boxMullRng = new RandomSequenceGenerator<BoxMullerGaussianRng<TUrng>>(
            copula.NumFactors, new BoxMullerGaussianRng<TUrng>(urng));
    }

    private BoxMullerFactorSampler(BoxMullerFactorSampler<TUrng> source)
    {
        boxMullRng = source.boxMullRng.Clone();
    }

    /// <inheritdoc/>
    public int Dimension => boxMullRng.Dimension;

    /// <summary>Draws a sample of every factor, standard normal.</summary>
    /// <returns>The sample, a view of the sampler's buffer valid until its next draw, with its weight.</returns>
    public Sample<ReadOnlyMemory<double>> NextSequence() => boxMullRng.NextSequence();

    /// <inheritdoc/>
    public Sample<ReadOnlyMemory<double>> LastSequence() => boxMullRng.LastSequence();

    /// <summary>Copies the sampler deeply, its generator included.</summary>
    /// <returns>The copy.</returns>
    public BoxMullerFactorSampler<TUrng> Clone() => new(this);

    /// <inheritdoc/>
    IRsg IRsg.Clone() => Clone();
}
