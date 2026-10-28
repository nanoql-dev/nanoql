// SPDX-FileCopyrightText: Copyright (C) 2024 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/zigguratgaussianrng.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A random-number generator that also draws 64-bit integers and uniform reals: the interface QuantLib's
/// <c>ZigguratGaussianRng</c> needs of its <c>RNG</c>, <c>nextInt64()</c> and <c>nextReal()</c>.
/// </summary>
/// <remarks>
/// <see cref="Xoshiro256StarStarUniformRng"/> implements it, the only QuantLib generator that does.
/// </remarks>
public interface IInt64Rng : IRng
{
    /// <summary>Draws the next integer, in [0, 0xffffffffffffffff], changing the generator.</summary>
    /// <returns>The integer's 64 bits, as a <see cref="long"/>: QuantLib's unsigned value is its bits read
    /// unsigned.</returns>
    long NextInt64();

    /// <summary>Draws the next random number in the (0.0, 1.0) interval, changing the generator.</summary>
    /// <returns>The number.</returns>
    double NextReal();
}
