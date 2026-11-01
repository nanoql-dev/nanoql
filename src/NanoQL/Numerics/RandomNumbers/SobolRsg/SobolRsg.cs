// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Richard Gould
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2013 Cheng Li
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause AND LicenseRef-JoeKuo
// Upstream: ql/math/randomnumbers/sobolrsg.hpp ql/math/randomnumbers/sobolrsg.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// The Joe-Kuo numbers are available under a BSD-style license, at http://web.maths.unsw.edu.au/~fkuo/sobol/:
//
// Copyright (c) 2008, Frances Y. Kuo and Stephen Joe
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//
//     * Redistributions of source code must retain the above copyright
//       notice, this list of conditions and the following disclaimer.
//
//     * Redistributions in binary form must reproduce the above copyright
//       notice, this list of conditions and the following disclaimer in the
//       documentation and/or other materials provided with the distribution.
//
//     * Neither the names of the copyright holders nor the names of the
//       University of New South Wales and the University of Waikato
//       and its contributors may be used to endorse or promote products derived
//       from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ``AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using NanoQL.Methods;

namespace NanoQL.Numerics;

/// <summary>
/// The Sobol low-discrepancy sequence generator. A Gray code counter and bitwise operations are used for very fast
/// sequence generation.
/// </summary>
/// <remarks>
/// <para>
/// The implementation relies on primitive polynomials modulo two from the book "Monte Carlo Methods in Finance" by
/// Peter Jäckel; 21 200 of them are provided, so the dimensionality is at most 21,200. The choice of initialization
/// numbers (also known as free direction integers) is crucial for the homogeneity properties of the sequence
/// (<see cref="SobolRsgDirectionIntegers"/>). For more info on Sobol' sequences see also "Monte Carlo Methods in
/// Financial Engineering," by P. Glasserman, 2004, Springer, section 5.2.3.
/// </para>
/// <para>
/// A mutable working type: each draw changes the generator, and it is not thread-safe. A draw returns a view of the
/// generator's buffer, valid until its next draw. The seed, in [1, 4294967295], only affects the direction integers
/// beyond those tabulated for the chosen set, so lower dimensionalities give the same sequence for any seed; QuantLib's
/// seed 0, which there draws a seed from the clock, is refused.
/// </para>
/// <para>
/// With the Gray code, the counter stops one short of QuantLib's period: <see cref="SkipTo"/> refuses 4294967295, and
/// the draw that would take the counter there throws "period exceeded", where QuantLib's behaviour is undefined.
/// </para>
/// </remarks>
[WorkingType]
public sealed class SobolRsg : ISeedableRsg<SobolRsg>
{
    // QuantLib's M_LN2.
    private const double M_LN2 = 0.693147180559945309417;

    private readonly int dimensionality;
    private readonly double[] sequence;
    private readonly long[] integerSequence;

    // [32][dimensionality], never changed after construction, so a copy shares it.
    private readonly uint[][] directionIntegers;
    private readonly bool useGrayCode;
    private uint sequenceCounter;
    private bool firstDraw = true;

    /// <summary>Initializes a new instance of the <see cref="SobolRsg"/> class.</summary>
    /// <remarks>
    /// The so called generating integer is chosen to be γ(n) = n if <paramref name="useGrayCode"/> is false and
    /// γ(n) = G(n), where G(n) is the Gray code of n, otherwise. The Sobol integers are then constructed using formula
    /// 8.20 resp. 8.23, see "Monte Carlo Methods in Finance" by Peter Jäckel. The default is to use the Gray code since
    /// this allows a faster sequence generation.
    /// </remarks>
    /// <param name="dimensionality">The dimension of each sequence, from 1 to 21,200.</param>
    /// <param name="seed">The seed, in [1, 4294967295], of the direction integers beyond the tabulated ones.</param>
    /// <param name="directionIntegers">The initialization numbers.</param>
    /// <param name="useGrayCode">Whether to use the Gray code.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dimensionality"/> is outside [1, 21200], <paramref name="seed"/> outside [1, 4294967295], or
    /// <paramref name="directionIntegers"/> isn't one of the sets.
    /// </exception>
    public SobolRsg(
        int dimensionality,
        long seed,
        SobolRsgDirectionIntegers directionIntegers = SobolRsgDirectionIntegers.Jaeckel,
        bool useGrayCode = true)
    {
        CheckDimensionality(dimensionality, nameof(dimensionality));
        if (directionIntegers is < SobolRsgDirectionIntegers.Unit or > SobolRsgDirectionIntegers.Kuo3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(directionIntegers), directionIntegers, $"unknown direction integers ({(int)directionIntegers})");
        }

        Seeds.Check(seed);
        this.dimensionality = dimensionality;
        this.useGrayCode = useGrayCode;
        sequence = new double[dimensionality];
        integerSequence = new long[dimensionality];
        this.directionIntegers = new uint[32][];
        for (int row = 0; row < 32; row++)
        {
            this.directionIntegers[row] = new uint[dimensionality];
        }

        uint[][] d = this.directionIntegers;

        // initializes coefficient array of the k-th primitive polynomial
        // and degree of the k-th primitive polynomial
        var degree = new int[dimensionality];
        var ppmt = new int[dimensionality];

        bool useAltPolynomials = directionIntegers is SobolRsgDirectionIntegers.Kuo or SobolRsgDirectionIntegers.Kuo2
            or SobolRsgDirectionIntegers.Kuo3 or SobolRsgDirectionIntegers.SobolLevitan
            or SobolRsgDirectionIntegers.SobolLevitanLemieux;

        // degree 0 is not used
        ppmt[0] = 0;
        degree[0] = 0;
        int k, index;
        int currentDegree = 1;
        k = 1;
        index = 0;

        int altDegree = useAltPolynomials ? SobolRsgTables.MaxAltDegree : 0;

        if (useAltPolynomials)
        {
            int[][] altPrimitivePolynomials = SobolRsgTables.AltPrimitivePolynomials;
            for (; k < Math.Min(dimensionality, altDegree); k++, index++)
            {
                ppmt[k] = altPrimitivePolynomials[currentDegree - 1][index];
                if (ppmt[k] == -1)
                {
                    ++currentDegree;
                    index = 0;
                    ppmt[k] = altPrimitivePolynomials[currentDegree - 1][index];
                }

                degree[k] = currentDegree;
            }
        }

        for (; k < dimensionality; k++, index++)
        {
            ppmt[k] = PrimitivePolynomials.Get(currentDegree - 1, index);
            if (ppmt[k] == -1)
            {
                ++currentDegree;
                index = 0;
                ppmt[k] = PrimitivePolynomials.Get(currentDegree - 1, index);
            }

            degree[k] = currentDegree;
        }

        // initializes 32 direction integers for each dimension
        // and store them into directionIntegers_[32][dimensionality_]
        //
        // In each dimension k with its associated primitive polynomial,
        // the first degree_[k] direction integers can be chosen freely
        // provided that only the l leftmost bits can be non-zero, and
        // that the l-th leftmost bit must be set

        // degenerate (no free direction integers) first dimension
        int j;
        for (j = 0; j < 32; j++)
        {
            d[j][0] = 1U << (32 - j - 1);
        }

        int maxTabulated;

        // dimensions from 2 (k=1) to maxTabulated (k=maxTabulated-1) included
        // are initialized from tabulated coefficients
        if (directionIntegers == SobolRsgDirectionIntegers.Unit)
        {
            maxTabulated = dimensionality;
            for (k = 1; k < maxTabulated; k++)
            {
                for (int l = 1; l <= degree[k]; l++)
                {
                    d[l - 1][k] = 1U;
                    d[l - 1][k] <<= 32 - l;
                }
            }
        }
        else
        {
            uint[][] initializers = SobolRsgTables.Coefficients(directionIntegers);
            maxTabulated = initializers.Length + 1;
            for (k = 1; k < Math.Min(dimensionality, maxTabulated); k++)
            {
                j = 0;

                // 0UL marks coefficients' end for a given dimension
                while (initializers[k - 1][j] != 0U)
                {
                    d[j][k] = initializers[k - 1][j];
                    d[j][k] <<= 32 - j - 1;
                    j++;
                }
            }
        }

        // random initialization for higher dimensions
        if (dimensionality > maxTabulated)
        {
            var uniformRng = new MersenneTwisterUniformRng(seed);
            for (k = maxTabulated; k < dimensionality; k++)
            {
                for (int l = 1; l <= degree[k]; l++)
                {
                    do
                    {
                        // u is in (0,1)
                        double u = uniformRng.Next().Value;

                        // the direction integer has at most the
                        // rightmost l bits non-zero
                        d[l - 1][k] = (uint)(u * (1UL << l));
                    }
                    while ((d[l - 1][k] & 1U) == 0U);

                    // iterate until the direction integer is odd
                    // that is it has the rightmost bit set

                    // shifting 32-l bits to the left
                    // we are guaranteed that the l-th leftmost bit
                    // is set, and only the first l leftmost bit
                    // can be non-zero
                    d[l - 1][k] <<= 32 - l;
                }
            }
        }

        // computation of directionIntegers_[l][k] for l>=degree_[k]
        // by recurrence relation
        for (k = 1; k < dimensionality; k++)
        {
            int gk = degree[k];
            for (int l = gk; l < 32; l++)
            {
                // eq. 8.19 "Monte Carlo Methods in Finance" by P. Jäckel
                uint n = d[l - gk][k] >> gk;

                // a[k][j] are the coefficients of the monomials in ppmt[k]
                // The highest order coefficient a[k][0] is not actually
                // used in the recurrence relation, and the lowest order
                // coefficient a[k][gk] is always set: this is the reason
                // why the highest and lowest coefficient of
                // the polynomial ppmt[k] are not included in its encoding,
                // provided that its degree is known.
                // That is: a[k][j] = ppmt[k] >> (gk-j-1)
                for (int m = 1; m < gk; m++)
                {
                    // XORed with a selection of (unshifted) direction
                    // integers controlled by which of the a[k][j] are set
                    if (((ppmt[k] >> (gk - m - 1)) & 1) != 0)
                    {
                        n ^= d[l - m][k];
                    }
                }

                // a[k][gk] is always set, so directionIntegers_[l-gk][k]
                // will always enter
                n ^= d[l - gk][k];
                d[l][k] = n;
            }
        }

        // initialize the Sobol integer/double vectors
        // first draw, this is only needed if Gray code is used
        if (useGrayCode)
        {
            for (k = 0; k < dimensionality; k++)
            {
                integerSequence[k] = d[0][k];
            }
        }
    }

    private SobolRsg(SobolRsg source)
    {
        dimensionality = source.dimensionality;
        sequence = (double[])source.sequence.Clone();
        integerSequence = (long[])source.integerSequence.Clone();
        directionIntegers = source.directionIntegers;
        useGrayCode = source.useGrayCode;
        sequenceCounter = source.sequenceCounter;
        firstDraw = source.firstDraw;
    }

    /// <summary>Gets the dimension of each sequence.</summary>
    public int Dimension => dimensionality;

    /// <summary>
    /// Builds a generator from a dimension and a seed, with QuantLib's default direction integers
    /// (<see cref="SobolRsgDirectionIntegers.Jaeckel"/>) and the Gray code, for generic code
    /// (<see cref="ISeedableRsg{TSelf}"/>).
    /// </summary>
    /// <param name="dimension">The dimension of each sequence, from 1 to 21,200.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The generator.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An argument is out of its range.</exception>
    public static SobolRsg Create(int dimension, long seed)
    {
        CheckDimensionality(dimension, nameof(dimension));
        return new(dimension, seed);
    }

    /// <summary>Skips to the n-th sample in the low-discrepancy sequence, and returns its integers.</summary>
    /// <remarks>
    /// With the Gray code, QuantLib's skip to 4294967295 converts a logarithm of 0 to an integer, which C++ leaves
    /// undefined: it is refused.
    /// </remarks>
    /// <param name="n">The sample's index, in [0, 4294967295]; below 4294967295 with the Gray code.</param>
    /// <returns>The integer sequence, a view of the generator's buffer valid until its next draw.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is out of its range.</exception>
    public ReadOnlyMemory<long> SkipTo(long n)
    {
        if (n is < 0 or > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(n), n, $"the sample's index ({n}) must be in [0, 4294967295]");
        }

        if (useGrayCode && n == uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(n), n, "with the Gray code, the sample's index must be below 4294967295, where QuantLib's is undefined");
        }

        return SkipToCore((uint)n);
    }

    /// <summary>Draws the next integer sequence, the Sobol integers, each in [0, 4294967295].</summary>
    /// <remarks>
    /// Without the Gray code, a fresh generator's first two draws are the same point, as in QuantLib: each draw
    /// recomputes the point at its counter before advancing it.
    /// </remarks>
    /// <returns>The integer sequence, a view of the generator's buffer valid until its next draw.</returns>
    /// <exception cref="NanoQLException">The draw would exceed the period ("period exceeded").</exception>
    public ReadOnlyMemory<long> NextInt32Sequence()
    {
        if (!useGrayCode)
        {
            SkipToCore(sequenceCounter);
            if (firstDraw)
            {
                firstDraw = false;
            }
            else
            {
                ++sequenceCounter;
                if (sequenceCounter == 0U)
                {
                    throw new NanoQLException("period exceeded");
                }
            }

            return integerSequence;
        }

        if (firstDraw)
        {
            // it was precomputed in the constructor
            firstDraw = false;
            return integerSequence;
        }

        // QuantLib's draw that takes the counter to 4294967295 reads past its 32 rows of direction integers; this one
        // stops there, one draw before QuantLib's own check.
        if (sequenceCounter == uint.MaxValue - 1)
        {
            throw new NanoQLException("period exceeded");
        }

        // increment the counter
        sequenceCounter++;

        // instead of using the counter n as new unique generating integer
        // for the n-th draw use the Gray code G(n) as proposed
        // by Antonov and Saleev
        uint m = sequenceCounter;

        // Find rightmost zero bit of n
        int j = 0;
        while ((m & 1U) != 0U)
        {
            m >>= 1;
            j++;
        }

        uint[] row = directionIntegers[j];
        for (int k = 0; k < dimensionality; k++)
        {
            // XOR the appropriate direction number into each component of
            // the integer sequence to obtain a new Sobol integer for that
            // component
            integerSequence[k] ^= row[k];
        }

        return integerSequence;
    }

    /// <summary>Draws the next sequence, normalized to doubles in (0, 1), with weight 1.0.</summary>
    /// <returns>The sequence, a view of the generator's buffer valid until its next draw, and its weight.</returns>
    /// <exception cref="NanoQLException">The draw would exceed the period ("period exceeded").</exception>
    public Sample<ReadOnlyMemory<double>> NextSequence()
    {
        ReadOnlySpan<long> v = NextInt32Sequence().Span;

        // normalize to get a double in (0,1)
        for (int k = 0; k < dimensionality; k++)
        {
            sequence[k] = v[k] * (0.5 / (1UL << 31));
        }

        return new(sequence, 1.0);
    }

    /// <summary>Gets the sequence the last <see cref="NextSequence"/> gave; an integer draw doesn't change it.</summary>
    /// <returns>The sequence, a view of the generator's buffer valid until its next draw, and its weight.</returns>
    public Sample<ReadOnlyMemory<double>> LastSequence() => new(sequence, 1.0);

    /// <summary>
    /// Copies the generator deeply: the copy's next draw equals this generator's, into its own buffer, and the two
    /// change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public SobolRsg Clone() => new(this);

    /// <inheritdoc/>
    IRsg IRsg.Clone() => Clone();

    // QuantLib's two checks of the dimensionality, under the caller's parameter name.
    private static void CheckDimensionality(int dimensionality, string paramName)
    {
        if (dimensionality <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, dimensionality, "dimensionality must be greater than 0");
        }

        if (dimensionality > PrimitivePolynomials.PPMT_MAX_DIM)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                dimensionality,
                $"dimensionality {dimensionality} exceeds the number of available primitive polynomials modulo two ({PrimitivePolynomials.PPMT_MAX_DIM})");
        }
    }

    private long[] SkipToCore(uint skip)
    {
        uint N = unchecked(skip + 1U);

        if (useGrayCode)
        {
            uint ops = (uint)(Math.Log((double)N) / M_LN2) + 1;

            // Convert to Gray code
            uint G = N ^ (N >> 1);
            for (int k = 0; k < dimensionality; k++)
            {
                integerSequence[k] = 0;
                for (int index = 0; index < ops; index++)
                {
                    if (((G >> index) & 1U) != 0U)
                    {
                        integerSequence[k] ^= directionIntegers[index][k];
                    }
                }
            }
        }
        else
        {
            Array.Clear(integerSequence);
            uint mask = 1;
            for (int index = 0; index < 32; index++)
            {
                if ((N & mask) != 0U)
                {
                    uint[] row = directionIntegers[index];
                    for (int k = 0; k < dimensionality; k++)
                    {
                        integerSequence[k] ^= row[k];
                    }
                }

                mask <<= 1;
            }
        }

        sequenceCounter = skip;
        return integerSequence;
    }
}
