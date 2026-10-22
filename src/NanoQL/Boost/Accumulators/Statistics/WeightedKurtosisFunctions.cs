// SPDX-FileCopyrightText: Copyright 2006 Olivier Gygi, Daniel Egloff. Distributed under the Boost
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/accumulators/statistics/weighted_kurtosis.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>Boost.Accumulators' kurtosis of weighted <c>double</c> samples (<c>weighted_kurtosis.hpp</c>).</summary>
internal static class WeightedKurtosisFunctions
{
    /// <summary>
    /// <c>weighted_kurtosis_impl::result</c>: the ratio of the fourth central moment to the square of the second, less 3,
    /// expressed by the simple weighted moments: (m⁽⁴⁾ − 4m⁽³⁾μ + 6m⁽²⁾μ² − 3μ⁴) / (m⁽²⁾ − μ²)² − 3.
    /// </summary>
    /// <param name="moment2">The second weighted moment, m⁽²⁾.</param>
    /// <param name="moment3">The third weighted moment, m⁽³⁾.</param>
    /// <param name="moment4">The fourth weighted moment, m⁽⁴⁾.</param>
    /// <param name="mean">The weighted mean, μ.</param>
    /// <returns>The excess kurtosis.</returns>
    internal static double WeightedKurtosis(double moment2, double moment3, double moment4, double mean) =>
        ((moment4 - (4.0 * moment3 * mean) + (6.0 * moment2 * mean * mean) - (3.0 * mean * mean * mean * mean))
         / ((moment2 - (mean * mean)) * (moment2 - (mean * mean))))
        - 3.0;
}
