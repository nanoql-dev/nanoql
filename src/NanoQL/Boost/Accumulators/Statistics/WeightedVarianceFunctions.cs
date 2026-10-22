// SPDX-FileCopyrightText: Copyright 2005 Daniel Egloff, Eric Niebler. Distributed under the Boost
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/accumulators/statistics/weighted_variance.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Accumulators' weighted variance of <c>double</c> samples (<c>weighted_variance.hpp</c>): the iterative
/// <c>weighted_variance_impl</c>, which a set's <c>weighted_variance</c> feature is. The lazy form isn't ported: no set
/// QuantLib builds requests it.
/// </summary>
internal static class WeightedVarianceFunctions
{
    /// <summary>
    /// <c>weighted_variance_impl::operator()</c>, the iterative calculation of the variance of weighted samples:
    /// σ²ₙ = (w̄ₙ − wₙ)/w̄ₙ · σ²ₙ₋₁ + wₙ/(w̄ₙ − wₙ) · (Xₙ − μₙ)², n ≥ 2, σ²₀ = 0, where w̄ₙ is the sum of the n weights and
    /// μₙ the weighted mean of the n samples. The count, the sum of the weights and the mean are the set's after the
    /// sample, since Boost visits a feature's dependencies first.
    /// </summary>
    /// <remarks>
    /// Where the earlier samples' weights vanish in the sum (w̄ₙ − wₙ = 0: they weigh 0, or less than the sum's
    /// rounding) the variance is reset to 0, Boost's first term, σ²ₙ₋₁·0/w̄ₙ, and its second term's limit as their share
    /// of the weight goes to 0: a divergence, Boost's second term being 0/0 or x/0 there, NaN or infinite through every
    /// later sample, where its <c>cnt &gt; 1</c> guard is the case of one earlier sample. Elsewhere it is Boost's
    /// recurrence bit for bit (<c>Divergences/IncrementalStatisticsLeadingZeroWeightTests.cs</c>).
    /// </remarks>
    /// <param name="weightedVariance">The variance before the sample.</param>
    /// <param name="count">The number of samples, the new one included.</param>
    /// <param name="sumOfWeights">The sum of the weights, the new one included.</param>
    /// <param name="weight">The new sample's weight.</param>
    /// <param name="sample">The new sample.</param>
    /// <param name="mean">The weighted mean, the new sample included.</param>
    /// <returns>The variance after the sample; the one before it for the first sample.</returns>
    internal static double Add(
        double weightedVariance, int count, double sumOfWeights, double weight, double sample, double mean)
    {
        if (count > 1)
        {
            if (sumOfWeights - weight == 0.0)
            {
                // the earlier weights vanish in the sum: the first term is 0, the second's limit 0
                weightedVariance = 0.0;
            }
            else
            {
                double tmp = sample - mean;
                weightedVariance = (weightedVariance * (sumOfWeights - weight) / sumOfWeights)
                    + (tmp * tmp * weight / (sumOfWeights - weight));
            }
        }

        return weightedVariance;
    }
}
