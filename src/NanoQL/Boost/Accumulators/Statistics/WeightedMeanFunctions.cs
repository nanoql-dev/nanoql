// SPDX-FileCopyrightText: Copyright 2006 Eric Niebler, Olivier Gygi. Distributed under the Boost
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/accumulators/statistics/weighted_mean.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Accumulators' weighted mean of <c>double</c> samples (<c>weighted_mean.hpp</c>): the lazy
/// <c>weighted_mean_impl</c>, which a set's <c>weighted_mean</c> feature is, and which the iterative weighted variance
/// reads where it depends on <c>immediate_weighted_mean</c>, the same feature. The immediate form isn't ported: no set
/// QuantLib builds requests it.
/// </summary>
internal static class WeightedMeanFunctions
{
    /// <summary><c>weighted_mean_impl::result</c>: the weighted sum over the sum of the weights.</summary>
    /// <param name="weightedSum">The weighted sum, Σ xᵢwᵢ.</param>
    /// <param name="sumOfWeights">The sum of the weights.</param>
    /// <returns>The weighted mean.</returns>
    internal static double WeightedMean(double weightedSum, double sumOfWeights) => weightedSum / sumOfWeights;
}
