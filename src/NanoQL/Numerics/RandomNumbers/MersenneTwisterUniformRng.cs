// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2010 Kakhkhor Abdijalilov
// SPDX-FileCopyrightText: Copyright (C) 1997 - 2002, Makoto Matsumoto and Takuji Nishimura,
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/mt19937uniformrng.hpp ql/math/randomnumbers/mt19937uniformrng.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// NOTE: The following copyright notice applies to
// the original C implementation that has been used for this class
//
// A C-program for MT19937, with initialization improved 2002/1/26.
// Coded by Takuji Nishimura and Makoto Matsumoto.
//
// Before using, initialize the state by using init_genrand(seed)
// or init_by_array(init_key, key_length).
//
// Copyright (C) 1997 - 2002, Makoto Matsumoto and Takuji Nishimura,
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
//
//   1. Redistributions of source code must retain the above copyright
//      notice, this list of conditions and the following disclaimer.
//
//   2. Redistributions in binary form must reproduce the above copyright
//      notice, this list of conditions and the following disclaimer in the
//      documentation and/or other materials provided with the distribution.
//
//   3. The names of its contributors may not be used to endorse or promote
//      products derived from this software without specific prior written
//      permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
// OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
//
// Any feedback is very welcome.
// http://www.math.sci.hiroshima-u.ac.jp/~m-mat/MT/emt.html
// email: matumoto@math.keio.ac.jp

using NanoQL.Methods;

namespace NanoQL.Numerics;

/// <summary>
/// The Mersenne Twister uniform random-number generator, MT19937, of period 2^19937 − 1.
/// </summary>
/// <remarks>
/// <para>
/// A mutable working type: each draw changes the generator, and it is not thread-safe. Its seed is explicit, in
/// [1, 4294967295]: QuantLib's seed 0, which draws a seed from the clock, is refused. For more details see
/// http://www.math.keio.ac.jp/matumoto/emt.html.
/// </para>
/// <para>
/// The correctness of the returned values is tested by checking them against known good results.
/// </para>
/// </remarks>
[WorkingType]
public sealed class MersenneTwisterUniformRng : ISeedableRng<MersenneTwisterUniformRng>, IInt32Rng
{
    // state size
    private const int N = 624;

    // shift size
    private const int M = 397;

    // constant vector a
    private const uint MATRIX_A = 0x9908b0dfU;

    // most significant w-r bits
    private const uint UPPER_MASK = 0x80000000U;

    // least significant r bits
    private const uint LOWER_MASK = 0x7fffffffU;

    private readonly uint[] mt;
    private int mti;

    /// <summary>Initializes a new instance of the <see cref="MersenneTwisterUniformRng"/> class from a seed.</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public MersenneTwisterUniformRng(long seed)
    {
        uint s = Seeds.Check(seed);
        mt = new uint[N];
        SeedInitialization(s);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MersenneTwisterUniformRng"/> class from a key, MT19937's
    /// <c>init_by_array</c>.
    /// </summary>
    /// <param name="seeds">The key's words, each in [0, 4294967295]; at least one.</param>
    /// <exception cref="ArgumentException"><paramref name="seeds"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A word of <paramref name="seeds"/> is outside [0, 4294967295].</exception>
    public MersenneTwisterUniformRng(ReadOnlySpan<long> seeds)
    {
        // QuantLib reads seeds[0] unchecked; a key needs a word.
        if (seeds.IsEmpty)
        {
            throw new ArgumentException("the key holds no word: at least one is needed", nameof(seeds));
        }

        var key = new uint[seeds.Length];
        for (int w = 0; w < seeds.Length; w++)
        {
            if (seeds[w] is < 0 or > uint.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(seeds), seeds[w], $"the key's word {w} ({seeds[w]}) must be in [0, 4294967295]");
            }

            key[w] = (uint)seeds[w];
        }

        mt = new uint[N];
        SeedInitialization(19650218U);
        int i = 1, j = 0, k = N > key.Length ? N : key.Length;
        for (; k != 0; k--)
        {
            // non linear
            mt[i] = unchecked((mt[i] ^ ((mt[i - 1] ^ (mt[i - 1] >> 30)) * 1664525U)) + key[j] + (uint)j);
            i++;
            j++;
            if (i >= N)
            {
                mt[0] = mt[N - 1];
                i = 1;
            }

            if (j >= key.Length)
            {
                j = 0;
            }
        }

        for (k = N - 1; k != 0; k--)
        {
            // non linear
            mt[i] = unchecked((mt[i] ^ ((mt[i - 1] ^ (mt[i - 1] >> 30)) * 1566083941U)) - (uint)i);
            i++;
            if (i >= N)
            {
                mt[0] = mt[N - 1];
                i = 1;
            }
        }

        // MSB is 1; assuring non-zero initial array
        mt[0] = UPPER_MASK;
    }

    private MersenneTwisterUniformRng(MersenneTwisterUniformRng source)
    {
        mt = (uint[])source.mt.Clone();
        mti = source.mti;
    }

    // mag01[x] = x * MATRIX_A  for x=0,1
    private static ReadOnlySpan<uint> Mag01 => [0x0U, MATRIX_A];

    /// <summary>Builds the generator from a seed, for generic code (<see cref="ISeedableRng{TSelf}"/>).</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The generator.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public static MersenneTwisterUniformRng Create(long seed) => new(seed);

    /// <summary>Draws a sample with weight 1.0 containing a random number in the (0.0, 1.0) interval.</summary>
    /// <returns>The sample.</returns>
    public Sample<double> Next() => new(NextReal(), 1.0);

    /// <summary>Draws a random number in the (0.0, 1.0) interval.</summary>
    /// <returns>The number.</returns>
    public double NextReal() => ((double)NextUInt32() + 0.5) / 4294967296.0;

    /// <summary>Draws a random integer in the [0, 0xffffffff] interval.</summary>
    /// <returns>The integer, QuantLib's 32-bit unsigned value.</returns>
    public long NextInt32() => NextUInt32();

    /// <summary>
    /// Copies the generator deeply: the copy's next draw equals this generator's, and the two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public MersenneTwisterUniformRng Clone() => new(this);

    /// <inheritdoc/>
    IRng IRng.Clone() => Clone();

    private uint NextUInt32()
    {
        if (mti == N)
        {
            // generate N words at a time
            Twist();
        }

        uint y = mt[mti++];

        // Tempering
        y ^= y >> 11;
        y ^= (y << 7) & 0x9d2c5680U;
        y ^= (y << 15) & 0xefc60000U;
        y ^= y >> 18;
        return y;
    }

    // initializes mt with a seed
    private void SeedInitialization(uint seed)
    {
        mt[0] = seed;
        for (mti = 1; mti < N; mti++)
        {
            // See Knuth TAOCP Vol2. 3rd Ed. P.106 for multiplier.
            // In the previous versions, MSBs of the seed affect
            // only MSBs of the array mt[].
            // 2002/01/09 modified by Makoto Matsumoto
            mt[mti] = unchecked((1812433253U * (mt[mti - 1] ^ (mt[mti - 1] >> 30))) + (uint)mti);
        }
    }

    private void Twist()
    {
        int kk;
        uint y;

        for (kk = 0; kk < N - M; kk++)
        {
            y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
            mt[kk] = mt[kk + M] ^ (y >> 1) ^ Mag01[(int)(y & 0x1U)];
        }

        for (; kk < N - 1; kk++)
        {
            y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
            mt[kk] = mt[kk + M - N] ^ (y >> 1) ^ Mag01[(int)(y & 0x1U)];
        }

        y = (mt[N - 1] & UPPER_MASK) | (mt[0] & LOWER_MASK);
        mt[N - 1] = mt[M - 1] ^ (y >> 1) ^ Mag01[(int)(y & 0x1U)];

        mti = 0;
    }
}
