// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/knuthuniformrng.hpp ql/math/randomnumbers/knuthuniformrng.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;

namespace NanoQL.Numerics;

/// <summary>
/// The uniform random-number generator by Knuth. For more details see Knuth, Seminumerical Algorithms, 3rd edition,
/// Section 3.6.
/// </summary>
/// <remarks>
/// <para>
/// This is not Knuth's original implementation, which is available at
/// http://www-cs-faculty.stanford.edu/~knuth/programs.html, but rather a slightly modified version wrapped in a class.
/// Such modifications did not affect the code but only the data structures used, which were converted to their
/// standard equivalents.
/// </para>
/// <para>
/// A mutable working type: each draw changes the generator, and it is not thread-safe. Its seed is explicit, in
/// [1, 4294967295]: QuantLib's seed 0, which draws a seed from the clock, is refused. Only the seed's low 30 bits reach
/// the state, as in QuantLib (Knuth's mask), so seeds differing above them draw one sequence.
/// </para>
/// </remarks>
[WorkingType]
public sealed class KnuthUniformRng : ISeedableRng<KnuthUniformRng>
{
    private const int KK = 100;
    private const int LL = 37;
    private const int TT = 70;
    private const int QUALITY = 1009;

    private readonly double[] ranfArrBuf;
    private readonly double[] ranU;
    private int ranfArrPtr;
    private int ranfArrSentinel;

    /// <summary>Initializes a new instance of the <see cref="KnuthUniformRng"/> class from a seed.</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public KnuthUniformRng(long seed)
    {
        uint s = Seeds.Check(seed);
        ranfArrBuf = new double[QUALITY];
        ranU = new double[QUALITY];
        ranfArrPtr = ranfArrSentinel = ranfArrBuf.Length;
        RanfStart(s);
    }

    private KnuthUniformRng(KnuthUniformRng source)
    {
        ranfArrBuf = (double[])source.ranfArrBuf.Clone();
        ranU = (double[])source.ranU.Clone();
        ranfArrPtr = source.ranfArrPtr;
        ranfArrSentinel = source.ranfArrSentinel;
    }

    /// <summary>Builds the generator from a seed, for generic code (<see cref="ISeedableRng{TSelf}"/>).</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The generator.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public static KnuthUniformRng Create(long seed) => new(seed);

    /// <summary>Draws a sample with weight 1.0 containing a random number uniformly chosen from (0.0, 1.0).</summary>
    /// <returns>The sample.</returns>
    public Sample<double> Next()
    {
        double result = ranfArrPtr != ranfArrSentinel ? ranfArrBuf[ranfArrPtr++] : RanfArrCycle();
        return new(result, 1.0);
    }

    /// <summary>
    /// Copies the generator deeply: the copy's next draw equals this generator's, and the two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public KnuthUniformRng Clone() => new(this);

    /// <inheritdoc/>
    IRng IRng.Clone() => Clone();

    private static double ModSum(double x, double y) => (x + y) - (int)(x + y);

    private static bool IsOdd(int s) => (s & 1) != 0;

    private void RanfStart(uint seed)
    {
        int t, s, j;
        var u = new double[KK + KK - 1];
        var ul = new double[KK + KK - 1];

        // 2 to the -52
        double ulp = (1.0 / (1L << 30)) / (1L << 22);
        double ss = 2.0 * ulp * ((seed & 0x3fffffff) + 2);

        for (j = 0; j < KK; j++)
        {
            // bootstrap the buffer
            u[j] = ss;
            ul[j] = 0.0;

            // cyclic shift of 51 bits
            ss += ss;
            if (ss >= 1.0)
            {
                ss -= 1.0 - (2 * ulp);
            }
        }

        for (; j < KK + KK - 1; j++)
        {
            u[j] = ul[j] = 0.0;
        }

        // make u[1] (and only u[1]) "odd"
        u[1] += ulp;
        ul[1] = ulp;
        s = (int)(seed & 0x3fffffff);
        t = TT - 1;
        while (t != 0)
        {
            // "square"
            for (j = KK - 1; j > 0; --j)
            {
                ul[j + j] = ul[j];
                u[j + j] = u[j];
            }

            for (j = KK + KK - 2; j > KK - LL; j -= 2)
            {
                ul[KK + KK - 1 - j] = 0.0;
                u[KK + KK - 1 - j] = u[j] - ul[j];
            }

            for (j = KK + KK - 2; j >= KK; --j)
            {
                if (ul[j] != 0.0)
                {
                    ul[j - (KK - LL)] = ulp - ul[j - (KK - LL)];
                    u[j - (KK - LL)] = ModSum(u[j - (KK - LL)], u[j]);
                    ul[j - KK] = ulp - ul[j - KK];
                    u[j - KK] = ModSum(u[j - KK], u[j]);
                }
            }

            if (IsOdd(s))
            {
                // "multiply by z"
                for (j = KK; j > 0; --j)
                {
                    ul[j] = ul[j - 1];
                    u[j] = u[j - 1];
                }

                // shift the buffer cyclically
                ul[0] = ul[KK];
                u[0] = u[KK];
                if (ul[KK] != 0.0)
                {
                    ul[LL] = ulp - ul[LL];
                    u[LL] = ModSum(u[LL], u[KK]);
                }
            }

            if (s != 0)
            {
                s >>= 1;
            }
            else
            {
                t--;
            }
        }

        for (j = 0; j < LL; j++)
        {
            ranU[j + KK - LL] = u[j];
        }

        for (; j < KK; j++)
        {
            ranU[j - LL] = u[j];
        }
    }

    private void RanfArray(double[] aa, int n)
    {
        int i, j;
        for (j = 0; j < KK; j++)
        {
            aa[j] = ranU[j];
        }

        for (; j < n; j++)
        {
            aa[j] = ModSum(aa[j - KK], aa[j - LL]);
        }

        for (i = 0; i < LL; i++, j++)
        {
            ranU[i] = ModSum(aa[j - KK], aa[j - LL]);
        }

        for (; i < KK; i++, j++)
        {
            ranU[i] = ModSum(aa[j - KK], ranU[i - LL]);
        }
    }

    private double RanfArrCycle()
    {
        RanfArray(ranfArrBuf, QUALITY);
        ranfArrPtr = 1;
        ranfArrSentinel = 100;
        return ranfArrBuf[0];
    }
}
