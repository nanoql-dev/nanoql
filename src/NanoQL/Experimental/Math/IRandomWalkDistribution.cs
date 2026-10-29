// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/isotropicrandomwalk.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A distribution an isotropic random walk draws its step's radius from, over a 32-bit Mersenne Twister: QuantLib's
/// <c>Distribution</c> template parameter, "compatible with <c>std::mt19937</c>".
/// </summary>
/// <remarks>
/// An extension point, as in QuantLib. A distribution may keep state between draws, as <c>std::normal_distribution</c>
/// keeps its second value; <see cref="Clone"/> copies it.
/// </remarks>
[Experimental("NQL0001")]
public interface IRandomWalkDistribution
{
    /// <summary>The next draw (QuantLib's <c>operator()(Engine&amp;)</c>).</summary>
    /// <param name="engine">The engine, <c>std::mt19937</c>'s twin, which the draw advances.</param>
    /// <returns>The draw.</returns>
    double Next(MersenneTwisterUniformRng engine);

    /// <summary>A copy, its state included: the copy's next draw on an equal engine equals this one's.</summary>
    /// <returns>The copy.</returns>
    IRandomWalkDistribution Clone();
}
