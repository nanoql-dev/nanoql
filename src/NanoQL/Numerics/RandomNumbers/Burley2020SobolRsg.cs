// SPDX-FileCopyrightText: Copyright (C) 2023 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/burley2020sobolrsg.hpp ql/math/randomnumbers/burley2020sobolrsg.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;

namespace NanoQL.Numerics;

/// <summary>
/// The Sobol sequence scrambled after Brent Burley, "Practical Hash-based Owen Scrambling", Journal of Computer
/// Graphics Techniques, Vol. 9, No. 4, 2020: each sample's index and each dimension's integers through a nested uniform
/// scramble seeded per group of four dimensions.
/// </summary>
/// <remarks>
/// A working type (the S-mm plan's MR1 (ii), SD6): each draw changes it, it isn't thread-safe, and its draws are views
/// of its own buffers, valid until its next draw, allocating nothing. Both seeds are explicit, in [1, 4294967295]
/// (S2R4: QuantLib's defaults are 42 and 43); the scrambling seed feeds the Mersenne twister, where 0 would draw from
/// the clock. Unlike <see cref="SobolRsg"/>, it takes two seeds, so it offers no <c>Create(dimension, seed)</c>.
/// </remarks>
[WorkingType]
public sealed class Burley2020SobolRsg : IRsg
{
    private readonly int dimensionality;
    private readonly long seed;
    private readonly SobolRsgDirectionIntegers directionIntegers;
    private readonly long[] integerSequence;
    private readonly double[] sequence;
    private readonly uint[] group4Seeds;
    private SobolRsg sobolRsg;
    private uint nextSequenceCounter;

    /// <summary>Initializes a new instance of the <see cref="Burley2020SobolRsg"/> class.</summary>
    /// <param name="dimensionality">The dimension of each sequence, from 1 to the direction integers'
    /// dimensions.</param>
    /// <param name="seed">The Sobol sequence's seed, in [1, 4294967295].</param>
    /// <param name="directionIntegers">The Sobol sequence's direction integers.</param>
    /// <param name="scrambleSeed">The scrambling's seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The dimension, a seed or the direction integers are outside their ranges.
    /// </exception>
    public Burley2020SobolRsg(int dimensionality, long seed, SobolRsgDirectionIntegers directionIntegers, long scrambleSeed)
    {
        this.dimensionality = dimensionality;
        this.seed = seed;
        this.directionIntegers = directionIntegers;
        sobolRsg = Reset();
        Seeds.Check(scrambleSeed, nameof(scrambleSeed));
        integerSequence = new long[dimensionality];
        sequence = new double[dimensionality];
        group4Seeds = new uint[((dimensionality - 1) / 4) + 1];
        var mt = new MersenneTwisterUniformRng(scrambleSeed);
        for (int i = 0; i < group4Seeds.Length; ++i)
        {
            group4Seeds[i] = (uint)mt.NextInt32();
        }
    }

    private Burley2020SobolRsg(Burley2020SobolRsg other)
    {
        dimensionality = other.dimensionality;
        seed = other.seed;
        directionIntegers = other.directionIntegers;
        integerSequence = (long[])other.integerSequence.Clone();
        sequence = (double[])other.sequence.Clone();
        group4Seeds = (uint[])other.group4Seeds.Clone();
        sobolRsg = other.sobolRsg.Clone();
        nextSequenceCounter = other.nextSequenceCounter;
    }

    /// <inheritdoc/>
    public int Dimension => dimensionality;

    /// <summary>
    /// Skips to the <paramref name="n"/>-th sample: the next draw is sample <paramref name="n"/>, and the integers
    /// returned are its own, as QuantLib's skip returns them.
    /// </summary>
    /// <param name="n">The sample's index, in [0, 4294967295].</param>
    /// <returns>Sample <paramref name="n"/>'s scrambled integers, a view of the generator's buffer valid until its next
    /// draw.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is outside its range.</exception>
    /// <exception cref="NanoQLException">The skip reaches the last sample, past which the period is
    /// exceeded.</exception>
    public ReadOnlyMemory<long> SkipTo(long n)
    {
        if (n is < 0 or > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(n), n, $"the sample's index ({n}) must be in [0, 4294967295]");
        }

        nextSequenceCounter = (uint)n;
        NextInt32Sequence();
        --nextSequenceCounter;
        return integerSequence;
    }

    /// <summary>Draws the next scrambled integer sequence, each in [0, 4294967295].</summary>
    /// <returns>The integers, a view of the generator's buffer valid until its next draw.</returns>
    /// <exception cref="NanoQLException">The draw exceeds the period.</exception>
    public ReadOnlyMemory<long> NextInt32Sequence()
    {
        uint n = Burley2020Scrambling.NestedUniformScramble(nextSequenceCounter, group4Seeds[0]);
        ReadOnlySpan<long> seq = sobolRsg.SkipTo(n).Span;
        seq.CopyTo(integerSequence);
        int i = 0, group = 0;
        do
        {
            ulong groupSeed = group4Seeds[group++];
            for (ulong g = 0; g < 4 && i < dimensionality; ++g, ++i)
            {
                groupSeed = Burley2020Scrambling.LocalHashCombine(groupSeed, g);
                integerSequence[i] = Burley2020Scrambling.NestedUniformScramble((uint)integerSequence[i], (uint)groupSeed);
            }
        }
        while (i < dimensionality);

        if (!(++nextSequenceCounter != 0))
        {
            throw new NanoQLException("Burley2020SobolRsg::nextInt32Sequence(): period exceeded");
        }

        return integerSequence;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The draw exceeds the period.</exception>
    public Sample<ReadOnlyMemory<double>> NextSequence()
    {
        ReadOnlySpan<long> v = NextInt32Sequence().Span;

        // normalize to get a double in (0,1)
        for (int k = 0; k < dimensionality; ++k)
        {
            sequence[k] = (v[k] + 0.5) / 4294967296.0;
        }

        return new Sample<ReadOnlyMemory<double>>(sequence, 1.0);
    }

    /// <inheritdoc/>
    public Sample<ReadOnlyMemory<double>> LastSequence() => new(sequence, 1.0);

    /// <summary>Copies the generator deeply: the copy's next draw equals this generator's, into its own
    /// buffers.</summary>
    /// <returns>The copy.</returns>
    public Burley2020SobolRsg Clone() => new(this);

    /// <inheritdoc/>
    IRsg IRsg.Clone() => Clone();

    // QuantLib's reset(): a fresh Sobol sequence without the Gray code, and the counter at 0.
    private SobolRsg Reset()
    {
        nextSequenceCounter = 0;
        return new SobolRsg(dimensionality, seed, directionIntegers, useGrayCode: false);
    }
}

/// <summary>QuantLib's anonymous helpers of the scrambling.</summary>
file static class Burley2020Scrambling
{
    // for ReverseBits() see http://graphics.stanford.edu/~seander/bithacks.html#BitReverseTable
    private static readonly byte[] BitReverseTable =
    [
        0, 128, 64, 192, 32, 160, 96, 224, 16, 144, 80, 208, 48, 176, 112, 240, 8, 136, 72, 200, 40, 168, 104, 232, 24,
        152, 88, 216, 56, 184, 120, 248, 4, 132, 68, 196, 36, 164, 100, 228, 20, 148, 84, 212, 52, 180, 116, 244, 12, 140,
        76, 204, 44, 172, 108, 236, 28, 156, 92, 220, 60, 188, 124, 252, 2, 130, 66, 194, 34, 162, 98, 226, 18, 146, 82,
        210, 50, 178, 114, 242, 10, 138, 74, 202, 42, 170, 106, 234, 26, 154, 90, 218, 58, 186, 122, 250, 6, 134, 70, 198,
        38, 166, 102, 230, 22, 150, 86, 214, 54, 182, 118, 246, 14, 142, 78, 206, 46, 174, 110, 238, 30, 158, 94, 222, 62,
        190, 126, 254, 1, 129, 65, 193, 33, 161, 97, 225, 17, 145, 81, 209, 49, 177, 113, 241, 9, 137, 73, 201, 41, 169,
        105, 233, 25, 153, 89, 217, 57, 185, 121, 249, 5, 133, 69, 197, 37, 165, 101, 229, 21, 149, 85, 213, 53, 181, 117,
        245, 13, 141, 77, 205, 45, 173, 109, 237, 29, 157, 93, 221, 61, 189, 125, 253, 3, 131, 67, 195, 35, 163, 99, 227,
        19, 147, 83, 211, 51, 179, 115, 243, 11, 139, 75, 203, 43, 171, 107, 235, 27, 155, 91, 219, 59, 187, 123, 251, 7,
        135, 71, 199, 39, 167, 103, 231, 23, 151, 87, 215, 55, 183, 119, 247, 15, 143, 79, 207, 47, 175, 111, 239, 31, 159,
        95, 223, 63, 191, 127, 255,
    ];

    internal static uint NestedUniformScramble(uint x, uint seed)
    {
        x = ReverseBits(x);
        x = LaineKarrasPermutation(x, seed);
        x = ReverseBits(x);
        return x;
    }

    // the results depend a lot on the details of the hash_combine() function that is used; QuantLib uses
    // hash_combine() calling hash(), hash_mix() as boost 1.83.0's container_hash implements them
    internal static ulong LocalHashCombine(ulong x, ulong v) => LocalHashMix(x + 0x9e3779b9 + LocalHash(v));

    private static uint ReverseBits(uint x) =>
        ((uint)BitReverseTable[x & 0xff] << 24) | ((uint)BitReverseTable[(x >> 8) & 0xff] << 16)
        | ((uint)BitReverseTable[(x >> 16) & 0xff] << 8) | BitReverseTable[(x >> 24) & 0xff];

    private static uint LaineKarrasPermutation(uint x, uint seed)
    {
        x += seed;
        x ^= x * 0x6c50b47cU;
        x ^= x * 0xb82f1e52U;
        x ^= x * 0xc7afe638U;
        x ^= x * 0x8d22f6e6U;
        return x;
    }

    private static ulong LocalHashMix(ulong x)
    {
        const ulong m = 0xe9846af9b1a615d;
        x ^= x >> 32;
        x *= m;
        x ^= x >> 32;
        x *= m;
        x ^= x >> 28;
        return x;
    }

    private static ulong LocalHash(ulong v)
    {
        ulong seed = 0;
        seed = (v >> 32) + LocalHashMix(seed);
        seed = (v & 0xFFFFFFFF) + LocalHashMix(seed);
        return seed;
    }
}
