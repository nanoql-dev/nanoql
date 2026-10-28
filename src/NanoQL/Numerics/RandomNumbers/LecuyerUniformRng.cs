// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/lecuyeruniformrng.hpp ql/math/randomnumbers/lecuyeruniformrng.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;

namespace NanoQL.Numerics;

/// <summary>
/// The uniform random-number generator of L'Ecuyer with added Bays-Durham shuffle (known as ran2 in Numerical Recipes).
/// For more details see Section 7.1 of Numerical Recipes in C, 2nd Edition, Cambridge University Press (available at
/// http://www.nr.com/).
/// </summary>
/// <remarks>
/// <para>
/// A mutable working type: each draw changes the generator, and it is not thread-safe. Its seed is explicit, in
/// [1, 4294967295]: QuantLib's seed 0, which draws a seed from the clock, is refused.
/// </para>
/// <para>
/// It computes in 64 bits, as QuantLib's <c>long</c> does on Linux. On Windows, where QuantLib's <c>long</c> and the
/// Python wheel's seed are 32 bits wide, a seed of 2^31 or above wraps to a negative one, which draws other numbers;
/// the two builds agree on every seed below 2^31.
/// </para>
/// </remarks>
public sealed class LecuyerUniformRng : ISeedableRng<LecuyerUniformRng>
{
    private const long m1 = 2147483563L;
    private const long a1 = 40014L;
    private const long q1 = 53668L;
    private const long r1 = 12211L;

    private const long m2 = 2147483399L;
    private const long a2 = 40692L;
    private const long q2 = 52774L;
    private const long r2 = 3791L;

    private const int bufferSize = 32;

    // int(1+m1/bufferSize) = int(1+(m1-1)/bufferSize)
    private const long bufferNormalizer = 67108862L;

    private const double maxRandom = 1.0 - NumericLimits.QL_EPSILON;

    private readonly long[] buffer;
    private long temp1;
    private long temp2;
    private long y;

    /// <summary>Initializes a new instance of the <see cref="LecuyerUniformRng"/> class from a seed.</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public LecuyerUniformRng(long seed)
    {
        Seeds.Check(seed);
        buffer = new long[bufferSize];
        temp2 = temp1 = seed;

        // Load the shuffle table (after 8 warm-ups)
        for (int j = bufferSize + 7; j >= 0; j--)
        {
            long k = temp1 / q1;
            temp1 = (a1 * (temp1 - (k * q1))) - (k * r1);
            if (temp1 < 0)
            {
                temp1 += m1;
            }

            if (j < bufferSize)
            {
                buffer[j] = temp1;
            }
        }

        y = buffer[0];
    }

    private LecuyerUniformRng(LecuyerUniformRng source)
    {
        buffer = (long[])source.buffer.Clone();
        temp1 = source.temp1;
        temp2 = source.temp2;
        y = source.y;
    }

    /// <summary>Builds the generator from a seed, for generic code (<see cref="ISeedableRng{TSelf}"/>).</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The generator.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public static LecuyerUniformRng Create(long seed) => new(seed);

    /// <summary>Draws a sample with weight 1.0 containing a random number uniformly chosen from (0.0, 1.0).</summary>
    /// <returns>The sample.</returns>
    public Sample<double> Next()
    {
        long k = temp1 / q1;

        // Compute temp1=(a1*temp1) % m1
        // without overflows (Schrage's method)
        temp1 = (a1 * (temp1 - (k * q1))) - (k * r1);
        if (temp1 < 0)
        {
            temp1 += m1;
        }

        k = temp2 / q2;

        // Compute temp2=(a2*temp2) % m2
        // without overflows (Schrage's method)
        temp2 = (a2 * (temp2 - (k * q2))) - (k * r2);
        if (temp2 < 0)
        {
            temp2 += m2;
        }

        // Will be in the range 0..bufferSize-1
        int j = (int)(y / bufferNormalizer);

        // Here temp1 is shuffled, temp1 and temp2 are
        // combined to generate output
        y = buffer[j] - temp2;
        buffer[j] = temp1;
        if (y < 1)
        {
            y += m1 - 1;
        }

        double result = y / (double)m1;

        // users don't expect endpoint values
        if (result > maxRandom)
        {
            result = maxRandom;
        }

        return new(result, 1.0);
    }

    /// <summary>
    /// Copies the generator deeply: the copy's next draw equals this generator's, and the two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public LecuyerUniformRng Clone() => new(this);

    /// <inheritdoc/>
    IRng IRng.Clone() => Clone();
}
