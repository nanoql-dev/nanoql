// SPDX-FileCopyrightText: (C) Copyright John Maddock 2006.
// SPDX-FileCopyrightText: (C) Copyright Johan Rade 2006.
// SPDX-FileCopyrightText: (C) Copyright Paul A. Bristow 2011 (added changesign).
// SPDX-FileCopyrightText: (C) Copyright Matt Borland 2024
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/sign.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>Boost.Math's <c>sign</c> (<c>sign.hpp</c>) for <c>double</c>.</summary>
internal static class SignFunctions
{
    /// <summary>
    /// <c>sign(z)</c>: 0 for a zero of either sign, otherwise −1 or 1 by <c>signbit</c>, the sign bit, which Boost reads
    /// through <c>std::signbit</c> under MSVC.
    /// </summary>
    /// <param name="z">The value.</param>
    /// <returns>−1, 0 or 1.</returns>
    /// <remarks>
    /// A NaN has no order but has a sign bit, so its sign is −1 or 1 by that bit, where <see cref="Math.Sign(double)"/>
    /// throws: −1 for <see cref="double.NaN"/>, whose sign bit is set, and for the NaN an x64 processor's invalid
    /// operation produces (∞ − ∞).
    /// </remarks>
    internal static int Sign(double z) => (z == 0) ? 0 : double.IsNegative(z) ? -1 : 1;
}
