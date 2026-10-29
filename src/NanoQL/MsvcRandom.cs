// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL;

/// <summary>
/// The C++ standard library's distributions over <c>std::mt19937</c> as MSVC 14.44 draws them, so a port of QuantLib
/// code that draws from them reproduces the streams of QuantLib's vectors, generated on that platform: written from
/// the published algorithms in MSVC's order of draws.
/// </summary>
/// <remarks>
/// <see cref="MersenneTwisterUniformRng"/>'s 32-bit integers are <c>std::mt19937</c>'s on the same seed in
/// [1, 4294967295].
/// </remarks>
internal static class MsvcRandom
{
    /// <summary>
    /// <c>std::generate_canonical&lt;double, 53&gt;</c>: the first integer's top 21 bits and the second's 32, times
    /// 2⁻⁵³, in [0, 1).
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <returns>The draw.</returns>
    internal static double Canonical(MersenneTwisterUniformRng engine)
    {
        ulong g0 = (ulong)engine.NextInt32();
        ulong g1 = (ulong)engine.NextInt32();
        return ((g0 >> 11) + (g1 << 21)) * (1.0 / 9007199254740992.0);
    }

    /// <summary><c>std::uniform_real_distribution&lt;double&gt;(a, b)</c>: the canonical draw times b − a, plus a.</summary>
    /// <param name="engine">The engine.</param>
    /// <param name="a">The lower end.</param>
    /// <param name="b">The upper end.</param>
    /// <returns>The draw.</returns>
    internal static double UniformReal(MersenneTwisterUniformRng engine, double a, double b) =>
        (Canonical(engine) * (b - a)) + a;

    /// <summary>
    /// <c>std::cauchy_distribution&lt;double&gt;(a, b)</c>: a + b·tan(π(u − ½)), u the canonical draw.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="a">The location.</param>
    /// <param name="b">The scale.</param>
    /// <returns>The draw.</returns>
    internal static double Cauchy(MersenneTwisterUniformRng engine, double a, double b)
    {
        double u = Canonical(engine);
        return a + (b * Math.Tan(Math.PI * (u - 0.5)));
    }

    /// <summary>
    /// <c>std::uniform_int_distribution&lt;std::size_t&gt;(min, max)</c>: Lemire's multiply-and-reject on one 32-bit
    /// integer for a range of at most 2³² values (D. Lemire, "Fast Random Integer Generation in an Interval", ACM Trans.
    /// Model. Comput. Simul. 29 (1), 2019): the integer times the range's size, redrawn while its low 32 bits fall
    /// below 2³² mod the size, the high bits the draw.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="min">The lowest value.</param>
    /// <param name="max">
    /// The highest value, below min + 2³², or min − 1, whose range MSVC reads as the full 64 bits.
    /// </param>
    /// <returns>The draw, in [min, max].</returns>
    internal static long UniformInt(MersenneTwisterUniformRng engine, long min, long max)
    {
        if ((ulong)(max - min) == ulong.MaxValue)
        {
            // the full 64-bit range, as an empty range (max one below min) also reads it: two integers, the first high
            ulong first = (ulong)engine.NextInt32();
            ulong second = (ulong)engine.NextInt32();
            return (long)(((first << 32) | second) + (ulong)min);
        }

        // Lemire's test: the low half below the size first, the exact threshold 2^32 mod size only then
        ulong size = (ulong)(max - min) + 1UL;
        ulong product = (ulong)engine.NextInt32() * size;
        ulong low = product & 0xFFFFFFFFUL;
        if (low < size)
        {
            ulong threshold = (0x100000000UL - size) % size;
            while (low < threshold)
            {
                product = (ulong)engine.NextInt32() * size;
                low = product & 0xFFFFFFFFUL;
            }
        }

        return min + (long)(product >> 32);
    }
}
