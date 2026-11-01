// SPDX-FileCopyrightText: Copyright (C) 2023 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause AND CC0-1.0
// Upstream: ql/math/randomnumbers/xoshiro256starstaruniformrng.hpp ql/math/randomnumbers/xoshiro256starstaruniformrng.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// NOTE: The following copyright notice applies to the
// original C implementation https://prng.di.unimi.it/xoshiro256starstar.c
// that has been used for this class.
//
// Written in 2018 by David Blackman and Sebastiano Vigna (vigna@acm.org)
//
// To the extent possible under law, the author has dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// See <http://creativecommons.org/publicdomain/zero/1.0/>.
//
// NOTE: The following copyright notice applies to the
// original C implementation https://prng.di.unimi.it/splitmix64.c
// that has been used for this class.
//
// Written in 2015 by Sebastiano Vigna (vigna@acm.org)
//
// To the extent possible under law, the author has dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// See <http://creativecommons.org/publicdomain/zero/1.0/>.

using NanoQL.Methods;

namespace NanoQL.Numerics;

/// <summary>
/// The xoshiro256** uniform random-number generator, of period 2^256 − 1. For more details see
/// https://prng.di.unimi.it/ and its reference implementation https://prng.di.unimi.it/xoshiro256starstar.c.
/// </summary>
/// <remarks>
/// <para>
/// A mutable working type: each draw changes the generator, and it is not thread-safe. The correctness of the returned
/// values is tested by checking them against the reference implementation in C.
/// </para>
/// <para>
/// Its seed is QuantLib's 64-bit <c>std::uint64_t</c>, taken as a <see cref="long"/>'s 64 bits (the S-core plan's
/// CR2 (a)): <c>unchecked((long)10108360646465513120UL)</c> draws QuantLib's sequence for that seed. It is the one
/// generator whose seed isn't confined to [1, 4294967295]; seed 0, which QuantLib reads as a seed drawn from the clock,
/// is refused, and so is the all-zero state, from which the generator draws 0 forever.
/// </para>
/// </remarks>
[WorkingType]
public sealed class Xoshiro256StarStarUniformRng : ISeedableRng<Xoshiro256StarStarUniformRng>, IInt64Rng
{
    private ulong s0;
    private ulong s1;
    private ulong s2;
    private ulong s3;

    /// <summary>
    /// Initializes a new instance of the <see cref="Xoshiro256StarStarUniformRng"/> class from a seed, its state the
    /// first four draws of a SplitMix64 generator seeded with it.
    /// </summary>
    /// <param name="seed">The seed's 64 bits, any but 0.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is 0.</exception>
    public Xoshiro256StarStarUniformRng(long seed)
    {
        if (seed == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(seed),
                seed,
                "seed 0, which QuantLib reads as a seed drawn from the clock, is refused: a seed is explicit, any 64 bits but 0");
        }

        // QuantLib's SplitMix64, seeded with the seed
        ulong x = unchecked((ulong)seed);
        s0 = SplitMix64Next(ref x);
        s1 = SplitMix64Next(ref x);
        s2 = SplitMix64Next(ref x);
        s3 = SplitMix64Next(ref x);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Xoshiro256StarStarUniformRng"/> class from its state. Make sure
    /// that the four words are chosen randomly: otherwise the first random numbers might not be well distributed.
    /// </summary>
    /// <param name="s0">The state's first word's 64 bits.</param>
    /// <param name="s1">The state's second word's 64 bits.</param>
    /// <param name="s2">The state's third word's 64 bits.</param>
    /// <param name="s3">The state's fourth word's 64 bits.</param>
    /// <exception cref="ArgumentException">Every word is 0, a state from which the generator draws 0
    /// forever.</exception>
    public Xoshiro256StarStarUniformRng(long s0, long s1, long s2, long s3)
    {
        if (s0 == 0 && s1 == 0 && s2 == 0 && s3 == 0)
        {
            throw new ArgumentException(
                "the all-zero state is refused: the generator would return 0 forever", nameof(s0));
        }

        this.s0 = unchecked((ulong)s0);
        this.s1 = unchecked((ulong)s1);
        this.s2 = unchecked((ulong)s2);
        this.s3 = unchecked((ulong)s3);
    }

    private Xoshiro256StarStarUniformRng(Xoshiro256StarStarUniformRng source)
    {
        s0 = source.s0;
        s1 = source.s1;
        s2 = source.s2;
        s3 = source.s3;
    }

    /// <summary>Builds the generator from a seed, for generic code (<see cref="ISeedableRng{TSelf}"/>).</summary>
    /// <param name="seed">The seed's 64 bits, any but 0.</param>
    /// <returns>The generator.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is 0.</exception>
    public static Xoshiro256StarStarUniformRng Create(long seed) => new(seed);

    /// <summary>Draws a sample with weight 1.0 containing a random number in the (0.0, 1.0) interval.</summary>
    /// <returns>The sample.</returns>
    public Sample<double> Next() => new(NextReal(), 1.0);

    /// <summary>Draws a random number in the (0.0, 1.0) interval.</summary>
    /// <returns>The number.</returns>
    public double NextReal() => ((double)(NextUInt64() >> 11) + 0.5) * (1.0 / (double)(1UL << 53));

    /// <summary>Draws a random integer in the [0, 0xffffffffffffffff] interval.</summary>
    /// <returns>The integer's 64 bits, as a <see cref="long"/>.</returns>
    public long NextInt64() => unchecked((long)NextUInt64());

    /// <summary>
    /// Copies the generator deeply: the copy's next draw equals this generator's, and the two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public Xoshiro256StarStarUniformRng Clone() => new(this);

    /// <inheritdoc/>
    IRng IRng.Clone() => Clone();

    private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

    private ulong NextUInt64()
    {
        ulong result = unchecked(Rotl(s1 * 5, 7) * 9);
        ulong t = s1 << 17;
        s2 ^= s0;
        s3 ^= s1;
        s1 ^= s2;
        s0 ^= s3;
        s2 ^= t;
        s3 = Rotl(s3, 45);
        return result;
    }

    // SplitMix64's next(), its state x
    private static ulong SplitMix64Next(ref ulong x)
    {
        ulong z = x = unchecked(x + 0x9e3779b97f4a7c15UL);
        z = unchecked((z ^ (z >> 30)) * 0xbf58476d1ce4e5b9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94d049bb133111ebUL);
        return z ^ (z >> 31);
    }
}
