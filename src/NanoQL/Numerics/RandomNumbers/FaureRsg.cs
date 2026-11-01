// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004 Gianni Piolanti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/faurersg.hpp ql/math/randomnumbers/faurersg.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;

namespace NanoQL.Numerics;

/// <summary>
/// The Faure low-discrepancy sequence generator. It is based on existing Fortran and C algorithms to calculate the
/// Pascal matrix and gray transforms: E. Thiemard, Economic generation of low-discrepancy sequences with a b-ary gray
/// code; Algorithms 659, 647, http://www.netlib.org/toms/647, http://www.netlib.org/toms/659.
/// </summary>
/// <remarks>
/// <para>
/// A mutable working type: each draw changes it, and it is not thread-safe. A draw returns a view of its buffer, valid
/// until its next draw. The correctness of the returned values is tested by reproducing known good values.
/// </para>
/// <para>
/// Its integers are QuantLib's <c>long</c> under MSVC, 32 bits wide (the S-core plan's CD6), which also sets the number
/// of base-b digits the sequence carries, ⌊ln(2^31 − 1)/ln b⌋: 30 in two dimensions (base 2), 4 in a hundred
/// (base 101). Once its counter carries past them the generator refuses ("Error processing Faure sequence."), after
/// 2^30 − 1 draws in base 2 and 101^4 − 1 in base 101, where QuantLib built with a 64-bit <c>long</c> (Linux) draws
/// other numbers and refuses after about 10^18. These are the Python wheel's numbers.
/// </para>
/// </remarks>
[WorkingType]
public sealed class FaureRsg : IRsg
{
    private readonly int dimensionality;
    private readonly double[] sequence;
    private readonly int[] integerSequence;
    private readonly int[] bary;
    private readonly int[][] gray;
    private readonly int @base;
    private readonly int mbit;
    private readonly int[][] powBase;
    private readonly int[] addOne;
    private readonly int[][][] pascal3D;
    private readonly double normalizationFactor;

    /// <summary>Initializes a new instance of the <see cref="FaureRsg"/> class.</summary>
    /// <param name="dimensionality">The dimension of each sequence, at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dimensionality"/> is below 1.</exception>
    public FaureRsg(int dimensionality)
    {
        if (!(dimensionality > 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(dimensionality), dimensionality, "dimensionality must be greater than 0");
        }

        this.dimensionality = dimensionality;
        sequence = new double[dimensionality];
        integerSequence = new int[dimensionality];

        // base is the lowest prime number >= dimensionality_
        int i, j, k = 1;
        @base = 2;
        while (@base < dimensionality)
        {
            @base = (int)PrimeNumbers.Get(k);
            k++;
        }

        mbit = (int)(Math.Log((double)int.MaxValue) / Math.Log((double)@base));
        gray = new int[dimensionality][];
        for (i = 0; i < dimensionality; i++)
        {
            gray[i] = new int[mbit + 1];
        }

        bary = new int[mbit + 1];

        // setMatrixValues();
        powBase = new int[mbit][];
        for (i = 0; i < mbit; i++)
        {
            powBase[i] = new int[(2 * @base) - 1];
        }

        powBase[mbit - 1][@base] = 1;
        for (int i2 = mbit - 2; i2 >= 0; --i2)
        {
            powBase[i2][@base] = powBase[i2 + 1][@base] * @base;
        }

        for (int ii = 0; ii < mbit; ii++)
        {
            for (int j1 = @base + 1; j1 < (2 * @base) - 1; j1++)
            {
                powBase[ii][j1] = powBase[ii][j1 - 1] + powBase[ii][@base];
            }

            for (int j2 = @base - 1; j2 >= 0; --j2)
            {
                powBase[ii][j2] = powBase[ii][j2 + 1] - powBase[ii][@base];
            }
        }

        addOne = new int[@base];
        for (j = 0; j < @base; j++)
        {
            addOne[j] = (j + 1) % @base;
        }

        // setPascalMatrix();
        pascal3D = new int[mbit][][];
        for (k = 0; k < mbit; k++)
        {
            var mm = new int[dimensionality + 1][];
            for (i = 0; i <= dimensionality; i++)
            {
                mm[i] = new int[k + 1];
            }

            pascal3D[k] = mm;
            pascal3D[k][0][k] = 1;
            pascal3D[k][1][0] = 1;
            pascal3D[k][1][k] = 1;
        }

        int p1, p2;
        for (k = 2; k < mbit; k++)
        {
            for (i = 1; i < k; i++)
            {
                p1 = pascal3D[k - 1][1][i - 1];
                p2 = pascal3D[k - 1][1][i];
                pascal3D[k][1][i] = (p1 + p2) % @base;
            }
        }

        int fact = 1, diag;
        for (j = 2; j < dimensionality; j++)
        {
            for (int kk = mbit - 1; kk >= 0; --kk)
            {
                diag = mbit - kk - 1;
                if (diag == 0)
                {
                    fact = 1;
                }
                else
                {
                    fact = fact * j % @base;
                }

                for (int ii = 0; ii <= kk; ii++)
                {
                    pascal3D[diag + ii][j][ii] = fact * pascal3D[diag + ii][1][ii] % @base;
                }
            }
        }

        normalizationFactor = (double)@base * (double)powBase[0][@base];
    }

    private FaureRsg(FaureRsg source)
    {
        dimensionality = source.dimensionality;
        sequence = (double[])source.sequence.Clone();
        integerSequence = (int[])source.integerSequence.Clone();
        bary = (int[])source.bary.Clone();
        gray = new int[source.gray.Length][];
        for (int i = 0; i < gray.Length; i++)
        {
            gray[i] = (int[])source.gray[i].Clone();
        }

        // the tables below are never written after construction
        @base = source.@base;
        mbit = source.mbit;
        powBase = source.powBase;
        addOne = source.addOne;
        pascal3D = source.pascal3D;
        normalizationFactor = source.normalizationFactor;
    }

    /// <summary>Gets the dimension of each sequence.</summary>
    public int Dimension => dimensionality;

    /// <summary>Draws the next integer sequence: the sequence's next point before its normalisation.</summary>
    /// <returns>The integers, a view of the generator's buffer valid until its next draw.</returns>
    /// <exception cref="NanoQLException">The counter carries past the base-b digits the sequence carries.</exception>
    public ReadOnlyMemory<int> NextIntSequence()
    {
        GenerateNextIntSequence();
        return integerSequence;
    }

    /// <summary>Gets the integer sequence the last draw gave.</summary>
    /// <returns>The integers, a view of the generator's buffer valid until its next draw.</returns>
    public ReadOnlyMemory<int> LastIntSequence() => integerSequence;

    /// <summary>Draws the next sequence, in [0, 1) in each dimension.</summary>
    /// <returns>The sequence, a view of the generator's buffer valid until its next draw, and its weight, 1.</returns>
    /// <exception cref="NanoQLException">The counter carries past the base-b digits the sequence carries.</exception>
    public Sample<ReadOnlyMemory<double>> NextSequence()
    {
        GenerateNextIntSequence();
        for (int i = 0; i < dimensionality; i++)
        {
            sequence[i] = integerSequence[i] / normalizationFactor;
        }

        return new(sequence, 1.0);
    }

    /// <summary>Gets the sequence the last draw gave.</summary>
    /// <returns>The sequence, a view of the generator's buffer valid until its next draw, and its weight, 1.</returns>
    public Sample<ReadOnlyMemory<double>> LastSequence() => new(sequence, 1.0);

    /// <summary>
    /// Copies the generator deeply: the copy's next draw equals this one's, into its own buffer, and the two change
    /// apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public FaureRsg Clone() => new(this);

    /// <inheritdoc/>
    IRsg IRsg.Clone() => Clone();

    private void GenerateNextIntSequence()
    {
        int bit = 0;
        bary[bit] = addOne[bary[bit]];
        while (bary[bit] == 0)
        {
            bit++;
            bary[bit] = addOne[bary[bit]];
        }

        if (bit == mbit)
        {
            throw new NanoQLException("Error processing Faure sequence.");
        }

        int tmp, g1, g2;
        for (int i = 0; i < dimensionality; i++)
        {
            for (int j = 0; j <= bit; j++)
            {
                tmp = gray[i][j];
                gray[i][j] = (pascal3D[bit][i][j] + tmp) % @base;
                g1 = gray[i][j];
                g2 = @base - 1 + g1 - tmp;
                integerSequence[i] += powBase[j][g2];
            }
        }
    }
}
