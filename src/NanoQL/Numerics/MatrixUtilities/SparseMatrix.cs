// SPDX-FileCopyrightText: Copyright (C) 2012 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/sparsematrix.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A sparse matrix in compressed row storage: QuantLib's <c>SparseMatrix</c>, uBLAS's
/// <c>compressed_matrix&lt;Real&gt;</c>. A mutable working type, as <see cref="Matrix"/> is.
/// </summary>
/// <remarks>
/// <para>
/// Reading an element returns zero where none is stored and stores nothing; writing one stores it, zero included, as
/// uBLAS's element reference does. The storage is uBLAS's, which QuantLib's tests read: <see cref="Index1Data"/> holds
/// each filled row's start in <see cref="Index2Data"/> (the stored elements' columns, sorted within a row) and
/// <see cref="ValueData"/>, and <see cref="Filled1"/> is one more than the last row written to.
/// </para>
/// <para>
/// As uBLAS's <c>NDEBUG</c> build, which QuantLib's builds use, indices aren't checked against the sizes, and the sum
/// of matrices of different sizes has the smaller of each; an in-place sum refuses only a nonzero element outside the
/// matrix: uBLAS writes a row outside past the matrix's row starts, and stores a column outside past its columns, which
/// a product then reads past its vector.
/// </para>
/// </remarks>
public sealed class SparseMatrix
{
    private readonly CompressedMatrix m;

    /// <summary>Initializes a new instance of the <see cref="SparseMatrix"/> class, with no element stored.</summary>
    /// <param name="size1">The number of rows.</param>
    /// <param name="size2">The number of columns.</param>
    /// <param name="nonZeros">The number of elements to make room for.</param>
    /// <exception cref="ArgumentOutOfRangeException">An argument is negative.</exception>
    public SparseMatrix(int size1, int size2, int nonZeros = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size1);
        ArgumentOutOfRangeException.ThrowIfNegative(size2);
        ArgumentOutOfRangeException.ThrowIfNegative(nonZeros);
        m = new CompressedMatrix(size1, size2, nonZeros);
    }

    private SparseMatrix(CompressedMatrix m)
    {
        this.m = m;
    }

    /// <summary>Gets the number of rows.</summary>
    public int Size1 => m.Size1;

    /// <summary>Gets the number of columns.</summary>
    public int Size2 => m.Size2;

    /// <summary>Gets the number of filled entries of <see cref="Index1Data"/>, one more than the last row written
    /// to.</summary>
    public int Filled1 => m.Filled1;

    /// <summary>Gets the rows' starts in <see cref="Index2Data"/> and <see cref="ValueData"/>, the filled
    /// entries.</summary>
    public ReadOnlySpan<int> Index1Data => m.Index1Data;

    /// <summary>Gets the stored elements' columns, row by row.</summary>
    public ReadOnlySpan<int> Index2Data => m.Index2Data;

    /// <summary>Gets the stored elements' values, row by row.</summary>
    public ReadOnlySpan<double> ValueData => m.ValueData;

    /// <summary>
    /// Gets or sets the element in row <paramref name="i"/> and column <paramref name="j"/>: zero where none is
    /// stored; a set stores the element, zero included.
    /// </summary>
    /// <param name="i">The row.</param>
    /// <param name="j">The column.</param>
    /// <returns>The element.</returns>
    public double this[int i, int j]
    {
        get => m.Get(i, j);
        set => m.At(i, j) = value;
    }

    /// <summary>The element-by-element sum, holding only its nonzero elements, as uBLAS assigns it.</summary>
    /// <param name="a">The first matrix.</param>
    /// <param name="b">The second matrix.</param>
    /// <returns>A new matrix.</returns>
    /// <exception cref="ArgumentNullException">A matrix is <see langword="null"/>.</exception>
    public static SparseMatrix operator +(SparseMatrix a, SparseMatrix b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return new SparseMatrix(CompressedMatrix.Sum(a.m, b.m, 0));
    }

    /// <summary>
    /// Adds a matrix in place, as uBLAS's <c>+=</c> of a matrix does: each element <paramref name="other"/> stores is
    /// added to this matrix's, which stores a nonzero one it lacked, so an element that cancels stays stored and a zero
    /// of <paramref name="other"/> this matrix lacks isn't stored.
    /// </summary>
    /// <param name="other">The matrix added.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="other"/> stores a nonzero element outside this matrix: in a row outside, which uBLAS writes past
    /// this matrix's row starts, or in a column outside, which it stores past this matrix's columns.
    /// </exception>
    public void operator +=(SparseMatrix other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // uBLAS's size check is off under NDEBUG: an element in a row outside this matrix writes past its row starts,
        // and one in a column outside is stored past its columns
        ReadOnlySpan<int> index1 = other.m.Index1Data;
        ReadOnlySpan<int> index2 = other.m.Index2Data;
        ReadOnlySpan<double> values = other.m.ValueData;
        for (int i = 0; i + 1 < index1.Length; ++i)
        {
            for (int k = index1[i]; k < index1[i + 1]; ++k)
            {
                if (values[k] != 0.0 && (i >= Size1 || index2[k] >= Size2))
                {
                    throw new ArgumentException(
                        $"element ({i}, {index2[k]}) outside the {Size1} x {Size2} matrix", nameof(other));
                }
            }
        }

        m.PlusAssign(other.m);
    }

    /// <summary>A copy, storage included: explicit zeros stay stored.</summary>
    /// <returns>The copy.</returns>
    public SparseMatrix Clone() => new(new CompressedMatrix(m));
}
