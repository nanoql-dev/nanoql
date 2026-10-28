// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/latentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A Student t latent model's factor sampler by polar Student t draws, bypassing the copula: one generator per
/// systemic factor, and one for every idiosyncratic factor. QuantLib's <c>LatentModel::FactorSampler</c> specialized
/// on <c>RandomSequenceGenerator&lt;PolarStudentTRng&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// A mutable working type (§2.7), not for a multithreaded simulation: the polar method is a rejection method. Its seed
/// constructor is <see cref="PolarStudentTFactorSampler.Create{TUrng}"/>, a seed in [1, 4294967295] with no default,
/// where QuantLib's defaults to 0, the clock (the S-xcr plan's XR4 (a)); a generator given instead is copied.
/// </para>
/// <para>
/// One uniform stream feeds the t generators in turn, and each draw is scaled by its factor's variance factor
/// √((ν − 2)/ν), so that the factors are independent and of unit variance, as the policy's are. QuantLib's gives each t
/// generator its own copy of one uniform generator, so the systemic draw k equals the idiosyncratic stream's draw k,
/// and leaves its draws raw, of variance ν/(ν − 2) (<c>latentmodel.hpp:779-794</c>, <c>polarstudenttrng.hpp:86</c>):
/// at ν 5 and a weight √0.3 a 1% default threshold simulates 2.65%. A documented divergence (the S-xcr plan's XR8
/// (xix); <c>Divergences/PolarStudentTSamplerTests.cs</c>).
/// </para>
/// </remarks>
/// <typeparam name="TUrng">The uniform generator.</typeparam>
[Experimental("NQL0001")]
public sealed class PolarStudentTFactorSampler<TUrng> : IRsg
    where TUrng : class, IRng
{
    private readonly TUrng urng;
    private readonly ImmutableArray<double> varianceFactors;
    private readonly PolarStudentTRng<TUrng>[] trng;
    private readonly double[] sequence;

    /// <summary>Initializes a new instance of the <see cref="PolarStudentTFactorSampler{TUrng}"/> class.</summary>
    /// <param name="copula">The Student t copula policy.</param>
    /// <param name="urng">The uniform generator, which the sampler copies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="urng"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="copula"/> is the zero value.</exception>
    /// <exception cref="ArgumentException"><paramref name="copula"/> was built without its variables, so its factors are
    /// fewer than its t generators, past whose sequence QuantLib's writes.</exception>
    public PolarStudentTFactorSampler(TCopulaPolicy copula, TUrng urng)
        : this(WithVariables(copula).VarianceFactors, copula.NumFactors, (TUrng)(urng ?? throw new ArgumentNullException(nameof(urng))).Clone())
    {
    }

    private PolarStudentTFactorSampler(ImmutableArray<double> varianceFactors, int numFactors, TUrng urng)
    {
        this.urng = urng;
        this.varianceFactors = varianceFactors;
        trng = new PolarStudentTRng<TUrng>[varianceFactors.Length];
        for (int i = 0; i < trng.Length; i++)
        {
            double vf = varianceFactors[i];
            trng[i] = new PolarStudentTRng<TUrng>(2.0 / (1.0 - (vf * vf)), urng, adopt: true);
        }

        sequence = new double[numFactors];
    }

    /// <inheritdoc/>
    public int Dimension => sequence.Length;

    /// <summary>Draws a sample of every factor, each of unit variance: the systemic ones, then the idiosyncratic
    /// ones.</summary>
    /// <returns>The sample, a view of the sampler's buffer valid until its next draw, of weight 1.</returns>
    public Sample<ReadOnlyMemory<double>> NextSequence()
    {
        int i = 0;

        // systemic samples plus one idiosyncratic
        for (; i < trng.Length; i++)
        {
            sequence[i] = trng[i].Next().Value * varianceFactors[i];
        }

        // rest of idiosyncratic samples
        for (; i < sequence.Length; i++)
        {
            sequence[i] = trng[^1].Next().Value * varianceFactors[^1];
        }

        return new(sequence, 1.0);
    }

    /// <inheritdoc/>
    public Sample<ReadOnlyMemory<double>> LastSequence() => new(sequence, 1.0);

    /// <summary>Copies the sampler deeply, its one uniform stream included.</summary>
    /// <returns>The copy.</returns>
    public PolarStudentTFactorSampler<TUrng> Clone()
    {
        var copy = new PolarStudentTFactorSampler<TUrng>(varianceFactors, sequence.Length, (TUrng)urng.Clone());
        sequence.CopyTo(copy.sequence, 0);
        return copy;
    }

    /// <inheritdoc/>
    IRsg IRsg.Clone() => Clone();

    private static TCopulaPolicy WithVariables(TCopulaPolicy copula) =>
        copula.NumFactors < copula.VarianceFactors.Length
            ? throw new ArgumentException("the polar sampler needs a policy built with its variables: one without them has fewer factors than t generators", nameof(copula))
            : copula;
}
