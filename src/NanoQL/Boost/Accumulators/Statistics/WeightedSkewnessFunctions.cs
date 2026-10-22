// SPDX-FileCopyrightText: Copyright 2006 Olivier Gygi, Daniel Egloff. Distributed under the Boost
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/accumulators/statistics/weighted_skewness.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>Boost.Accumulators' skewness of weighted <c>double</c> samples (<c>weighted_skewness.hpp</c>).</summary>
internal static class WeightedSkewnessFunctions
{
    /// <summary>
    /// <c>weighted_skewness_impl::result</c>: the ratio of the third central moment to the 3/2-th power of the second,
    /// expressed by the simple weighted moments: (m⁽³⁾ − 3m⁽²⁾μ + 2μ³) / ((m⁽²⁾ − μ²)·√(m⁽²⁾ − μ²)).
    /// </summary>
    /// <param name="moment2">The second weighted moment, m⁽²⁾.</param>
    /// <param name="moment3">The third weighted moment, m⁽³⁾.</param>
    /// <param name="mean">The weighted mean, μ.</param>
    /// <returns>The skewness.</returns>
    internal static double WeightedSkewness(double moment2, double moment3, double mean) =>
        (moment3 - (3.0 * moment2 * mean) + (2.0 * mean * mean * mean))
        / ((moment2 - (mean * mean)) * Math.Sqrt(moment2 - (mean * mean)));
}
