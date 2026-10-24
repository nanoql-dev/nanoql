// SPDX-FileCopyrightText: (C) Copyright John Maddock 2006.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/lanczos.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's Lanczos approximation for <c>double</c>, <c>lanczos13m53</c> (<c>lanczos.hpp</c>): N = 13, g =
/// 6.024680040776729583740234375, the approximation Boost's precision policy selects for a 53-bit type. Under MSVC on
/// x64 Boost's <c>long double</c> sums forward to <c>lanczos_sse2.hpp</c>'s SSE2 code
/// (<see cref="LanczosSse2Functions"/>).
/// </summary>
internal static class Lanczos13m53
{
    /// <summary>The approximation's g.</summary>
    internal const double G = 6.024680040776729583740234375;

    /// <summary><c>lanczos_sum</c>: the rational sum S(z) with Γ(z) = S(z)·((z + g − ½)/e)^(z − ½).</summary>
    /// <param name="x">The point.</param>
    /// <returns>The sum.</returns>
    internal static double LanczosSum(double x) => LanczosSse2Functions.LanczosSum(x);

    /// <summary><c>lanczos_sum_expG_scaled</c>: <see cref="LanczosSum"/> times e^(−g).</summary>
    /// <param name="x">The point.</param>
    /// <returns>The scaled sum.</returns>
    internal static double LanczosSumExpGScaled(double x) => LanczosSse2Functions.LanczosSumExpGScaled(x);
}
