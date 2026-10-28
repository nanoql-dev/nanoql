// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/gaussiancopulapolicy.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The Gaussian latent model's copula policy: standard normal systemic and idiosyncratic factors, so that the latent
/// variables are standard normal too, by the Gaussian's stability under convolution.
/// </summary>
/// <remarks>
/// A <c>readonly struct</c> (D12) built by <see cref="Create"/>. Its zero value isn't usable: QuantLib's default
/// construction reads the first row of an empty weight matrix (<c>gaussiancopulapolicy.hpp:40-43</c>), so every member
/// of <c>default(GaussianCopulaPolicy)</c> throws <see cref="InvalidOperationException"/> (S2R9).
/// </remarks>
[Experimental("NQL0001")]
public readonly struct GaussianCopulaPolicy : ICopulaPolicy<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>
{
    private static readonly NormalDistribution DensityFunction = new();
    private static readonly CumulativeNormalDistribution Cumulative = new();

    private readonly int numFactors;

    private GaussianCopulaPolicy(int numFactors)
    {
        this.numFactors = numFactors;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The policy is the zero value.</exception>
    public int NumFactors => Checked().numFactors;

    /// <summary>The policy of a latent model's factor weights.</summary>
    /// <param name="factorWeights">The weights, <c>factorWeights[iVariable][iFactor]</c>, of at least one variable and
    /// one factor.</param>
    /// <param name="initTraits">The parameters, none.</param>
    /// <returns>The policy.</returns>
    /// <exception cref="ArgumentException">There's no variable or no factor; or a variable's weights have a squared norm
    /// of 1 or more ("Non normal random factor combination.").</exception>
    public static GaussianCopulaPolicy Create(
        ImmutableArray<ImmutableArray<double>> factorWeights, GaussianCopulaPolicyInitTraits initTraits = default)
    {
        if (factorWeights.IsDefaultOrEmpty || factorWeights[0].IsDefaultOrEmpty)
        {
            throw new ArgumentException("a latent model needs at least one variable and one factor", nameof(factorWeights));
        }

        // check factors in LM are normalized.
        foreach (ImmutableArray<double> factorWeight in factorWeights)
        {
            double factorsNorm = 0.0;
            foreach (double w in factorWeight)
            {
                factorsNorm += w * w;
            }

            if (!(factorsNorm < 1.0))
            {
                throw new ArgumentException("Non normal random factor combination.", nameof(factorWeights));
            }
        }

        return new GaussianCopulaPolicy(factorWeights.Length + factorWeights[0].Length);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The policy is the zero value.</exception>
    public GaussianCopulaPolicyInitTraits GetInitTraits()
    {
        Checked();
        return default;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The policy is the zero value.</exception>
    public double CumulativeY(double val, int iVariable)
    {
        Checked();
        return Cumulative.Evaluate(val);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The policy is the zero value.</exception>
    public double CumulativeZ(double z)
    {
        Checked();
        return Cumulative.Evaluate(z);
    }

    /// <summary>
    /// The joint density of a realization of the systemic factors: the product of the standard normal's density at
    /// each.
    /// </summary>
    /// <param name="m">The systemic factors' values.</param>
    /// <returns>The density.</returns>
    /// <exception cref="InvalidOperationException">The policy is the zero value.</exception>
    public double Density(ReadOnlySpan<double> m)
    {
        Checked();
        double x = 1.0;
        foreach (double y in m)
        {
            x *= DensityFunction.Evaluate(y);
        }

        return x;
    }

    /// <summary>The inverse of a latent variable's cumulative distribution, the standard normal's.</summary>
    /// <param name="p">The probability.</param>
    /// <param name="iVariable">The variable's index, unused: every variable is standard normal.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidOperationException">The policy is the zero value.</exception>
    public double InverseCumulativeY(double p, int iVariable)
    {
        Checked();
        return InverseCumulativeNormal.StandardValue(p);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The policy is the zero value.</exception>
    public double InverseCumulativeZ(double p)
    {
        Checked();
        return InverseCumulativeNormal.StandardValue(p);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The policy is the zero value.</exception>
    public double InverseCumulativeDensity(double p, int iFactor)
    {
        Checked();
        return InverseCumulativeNormal.StandardValue(p);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The policy is the zero value.</exception>
    public double[] AllFactorCumulInverter(ReadOnlySpan<double> probs)
    {
        Checked();
        var result = new double[probs.Length];
        for (int i = 0; i < probs.Length; i++)
        {
            result[i] = InverseCumulativeNormal.StandardValue(probs[i]);
        }

        return result;
    }

    private GaussianCopulaPolicy Checked() =>
        numFactors == 0
            ? throw new InvalidOperationException("the zero value of GaussianCopulaPolicy isn't a usable policy")
            : this;
}
