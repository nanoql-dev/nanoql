// SPDX-FileCopyrightText: Copyright 2006, Eric Niebler, Olivier Gygi. Distributed under the Boost
// SPDX-FileCopyrightText: Copyright 2005 Eric Niebler. Distributed under the Boost
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/accumulators/statistics/weighted_moment.hpp boost/accumulators/statistics/moment.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Accumulators' weighted moments of <c>double</c> samples (<c>weighted_moment.hpp</c>), with the integer power
/// they raise a sample to (<c>numeric::pow</c>, <c>moment.hpp</c>), which squares rather than calls the C runtime's
/// <c>pow</c>: x³ is (x·x)·x and x⁴ is (x·x)·(x·x).
/// </summary>
internal static class WeightedMomentFunctions
{
    /// <summary><c>weighted_moment_impl::operator()</c>: the sum of the weighted powers, with one sample added.</summary>
    /// <param name="sum">The sum of the weighted powers before the sample, 0 for none.</param>
    /// <param name="weight">The sample's weight.</param>
    /// <param name="sample">The sample.</param>
    /// <param name="n">The moment's order, at least 1.</param>
    /// <returns>The sum after the sample.</returns>
    internal static double Add(double sum, double weight, double sample, int n) => sum + (weight * Pow(sample, n));

    /// <summary><c>weighted_moment_impl::result</c>: the sum of the weighted powers over the sum of the weights.</summary>
    /// <param name="sum">The sum of the weighted powers.</param>
    /// <param name="sumOfWeights">The sum of the weights.</param>
    /// <returns>The weighted moment.</returns>
    internal static double Result(double sum, double sumOfWeights) => sum / sumOfWeights;

    /// <summary><c>numeric::pow(x, mpl::int_&lt;N&gt;())</c>: x to an integer power, by squaring.</summary>
    /// <param name="x">The base.</param>
    /// <param name="n">The power, at least 1.</param>
    /// <returns>xⁿ.</returns>
    internal static double Pow(double x, int n)
    {
        if (n == 1)
        {
            return x;
        }

        double y = Pow(x, n / 2);
        double z = y * y;
        return (n % 2) != 0 ? (z * x) : z;
    }
}
