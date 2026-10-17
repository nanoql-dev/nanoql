// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/seedgenerator.hpp ql/math/randomnumbers/seedgenerator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The check every generator applies to its seed, in place of QuantLib's <c>SeedGenerator</c>: seeds are explicit
/// inputs, QuantLib's 32-bit unsigned values taken as a <see cref="long"/> in [1, 4294967295].
/// </summary>
/// <remarks>
/// QuantLib reads a seed of 0 as "no seed" and draws one from its <c>SeedGenerator</c> singleton, whose own seed comes
/// from the clock. nanoql reads no clock, so 0 is refused, a documented divergence.
/// </remarks>
internal static class Seeds
{
    /// <summary>The seed as QuantLib's 32-bit value, once checked.</summary>
    /// <param name="seed">The seed.</param>
    /// <param name="paramName">The parameter's name, for the exception.</param>
    /// <returns>The seed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The seed is outside [1, 4294967295].</exception>
    public static uint Check(long seed, string paramName = "seed")
    {
        if (seed == 0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                seed,
                "seed 0, which QuantLib reads as a seed drawn from the clock, is refused: a seed is explicit, in [1, 4294967295]");
        }

        if (seed is < 1 or > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(paramName, seed, $"seed ({seed}) must be in [1, 4294967295]");
        }

        return (uint)seed;
    }
}
