// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/hybridsimulatedannealing.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The hybrid simulated annealing's sampler, which draws a new point from a density on the current one: QuantLib's
/// <c>Sampler</c> template parameter.
/// </summary>
/// <remarks>
/// An extension point, as in QuantLib's concept, <c>void operator()(Array&amp; newPoint, const Array&amp; currentPoint,
/// const Array&amp; temp)</c>. A sampler may keep state between draws, its generators'; the annealing copies it
/// (<see cref="Clone"/>) for each minimization.
/// </remarks>
/// <typeparam name="TSelf">The sampler's own type.</typeparam>
[Experimental("NQL0001")]
public interface IAnnealingSampler<TSelf>
    where TSelf : IAnnealingSampler<TSelf>
{
    /// <summary>Draws a new point (QuantLib's <c>operator()</c>).</summary>
    /// <param name="newPoint">The new point, which it sets.</param>
    /// <param name="currentPoint">The current point.</param>
    /// <param name="temp">Each dimension's temperature.</param>
    void Sample(Vector newPoint, Vector currentPoint, Vector temp);

    /// <summary>A copy, its state included: the copy's next draw equals this sampler's.</summary>
    /// <returns>The copy.</returns>
    TSelf Clone();
}
