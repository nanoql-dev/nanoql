// SPDX-FileCopyrightText: Copyright (c) 2000-2007 Joerg Walter, Mathias Koch, Gunter Winkler
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/numeric/ublas/matrix_sparse.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// uBLAS's <c>compressed_matrix&lt;double&gt;</c> (<c>matrix_sparse.hpp</c>), row-major with zero-based indices, the
/// members QuantLib's <c>SparseMatrix</c> and its tests use: the compressed row storage with its filled counts, element
/// access with insertion, the capacity's growth, and the sum's sparse assignment, which keeps only nonzero elements.
/// </summary>
/// <remarks>
/// <para>
/// <c>index1Data[i]</c> is the start of row i's elements in <c>index2Data</c> (their columns, sorted) and
/// <c>valueData</c>, for i below <c>filled1</c>; <c>filled2</c> elements are stored. The arrays beyond those counts
/// hold no defined value, as Boost's don't. As Boost's <c>NDEBUG</c> build, which QuantLib's builds use, indices aren't
/// checked against the sizes.
/// </para>
/// </remarks>
internal sealed class CompressedMatrix
{
    private int size1_;
    private int size2_;
    private int capacity_;
    private int filled1_;
    private int filled2_;
    private int[] index1Data_;
    private int[] index2Data_;
    private double[] valueData_;

    /// <summary>Initializes a new instance of the <see cref="CompressedMatrix"/> class, empty.</summary>
    /// <param name="size1">The number of rows.</param>
    /// <param name="size2">The number of columns.</param>
    /// <param name="nonZeros">The capacity asked for, at least min(size1, size2) once restricted.</param>
    internal CompressedMatrix(int size1, int size2, int nonZeros = 0)
    {
        size1_ = size1;
        size2_ = size2;
        capacity_ = RestrictCapacity(nonZeros);
        filled1_ = 1;
        filled2_ = 0;
        index1Data_ = new int[size1_ + 1];
        index2Data_ = new int[capacity_];
        valueData_ = new double[capacity_];
        index1Data_[filled1_ - 1] = filled2_;
    }

    /// <summary>Initializes a new instance of the <see cref="CompressedMatrix"/> class, a copy of the
    /// storage.</summary>
    /// <param name="m">The matrix copied.</param>
    internal CompressedMatrix(CompressedMatrix m)
    {
        size1_ = m.size1_;
        size2_ = m.size2_;
        capacity_ = m.capacity_;
        filled1_ = m.filled1_;
        filled2_ = m.filled2_;
        index1Data_ = (int[])m.index1Data_.Clone();
        index2Data_ = (int[])m.index2Data_.Clone();
        valueData_ = (double[])m.valueData_.Clone();
    }

    /// <summary>Gets the number of rows.</summary>
    internal int Size1 => size1_;

    /// <summary>Gets the number of columns.</summary>
    internal int Size2 => size2_;

    /// <summary>Gets the number of elements the storage holds without growing.</summary>
    internal int NnzCapacity => capacity_;

    /// <summary>Gets the number of filled entries of <see cref="Index1Data"/>: one more than the last row written
    /// to.</summary>
    internal int Filled1 => filled1_;

    /// <summary>Gets the number of stored elements.</summary>
    internal int Filled2 => filled2_;

    /// <summary>Gets the rows' starts, the filled entries.</summary>
    internal ReadOnlySpan<int> Index1Data => index1Data_.AsSpan(0, filled1_);

    /// <summary>Gets the stored elements' columns.</summary>
    internal ReadOnlySpan<int> Index2Data => index2Data_.AsSpan(0, filled2_);

    /// <summary>Gets the stored elements' values.</summary>
    internal ReadOnlySpan<double> ValueData => valueData_.AsSpan(0, filled2_);

    /// <summary>The sum of two matrices of one size, assigned as uBLAS assigns a sparse expression.</summary>
    /// <param name="a">The first matrix.</param>
    /// <param name="b">The second matrix.</param>
    /// <param name="nonZeros">The result's capacity asked for.</param>
    /// <returns>The sum, of the smaller of the two sizes, holding only its nonzero elements.</returns>
    /// <remarks>
    /// uBLAS's <c>matrix_assign</c> for a sparse row-major target (<c>detail/matrix_assign.hpp</c>) clears the target
    /// and inserts each nonzero element of the expression, row by row; the expression's iterator visits the union of
    /// the two patterns in column order, an element of one matrix alone summed with zero.
    /// </remarks>
    internal static CompressedMatrix Sum(CompressedMatrix a, CompressedMatrix b, int nonZeros)
    {
        // BOOST_UBLAS_SAME under NDEBUG: the smaller size, unchecked
        var m = new CompressedMatrix(Min(a.size1_, b.size1_), Min(a.size2_, b.size2_), nonZeros);
        for (int i = 0; i < m.size1_; ++i)
        {
            (int ia, int iaEnd) = a.RowRange(i);
            (int ib, int ibEnd) = b.RowRange(i);
            while (ia < iaEnd || ib < ibEnd)
            {
                int j;
                double t;
                if (ib == ibEnd || (ia < iaEnd && a.index2Data_[ia] < b.index2Data_[ib]))
                {
                    j = a.index2Data_[ia];
                    t = a.valueData_[ia] + 0.0;
                    ++ia;
                }
                else if (ia == iaEnd || b.index2Data_[ib] < a.index2Data_[ia])
                {
                    j = b.index2Data_[ib];
                    t = 0.0 + b.valueData_[ib];
                    ++ib;
                }
                else
                {
                    j = a.index2Data_[ia];
                    t = a.valueData_[ia] + b.valueData_[ib];
                    ++ia;
                    ++ib;
                }

                if (t != 0.0)
                {
                    m.InsertElement(i, j, t);
                }
            }
        }

        return m;
    }

    /// <summary>The stored element's position in the data arrays, or −1.</summary>
    /// <param name="i">The row.</param>
    /// <param name="j">The column.</param>
    /// <returns>The position, or −1 where the element isn't stored.</returns>
    internal int FindElement(int i, int j)
    {
        int element1 = i;
        int element2 = j;
        if (filled1_ <= element1 + 1)
        {
            return -1;
        }

        int itBegin = index1Data_[element1];
        int itEnd = index1Data_[element1 + 1];
        int it = LowerBound(itBegin, itEnd, element2);
        if (it == itEnd || index2Data_[it] != element2)
        {
            return -1;
        }

        return it;
    }

    /// <summary>The element's value, zero where it isn't stored: the const <c>operator()</c>.</summary>
    /// <param name="i">The row.</param>
    /// <param name="j">The column.</param>
    /// <returns>The value.</returns>
    internal double Get(int i, int j)
    {
        int p = FindElement(i, j);
        return p >= 0 ? valueData_[p] : 0.0;
    }

    /// <summary>The element, inserted as zero where it isn't stored: the non-const <c>operator()</c>.</summary>
    /// <param name="i">The row.</param>
    /// <param name="j">The column.</param>
    /// <returns>A reference to the element's value, valid until the next insertion.</returns>
    internal ref double At(int i, int j)
    {
        // the insertion can grow the arrays, so it runs before the array is read
        int element1 = i;
        int p;
        if (filled1_ <= element1 + 1)
        {
            p = InsertElement(i, j, 0.0);
            return ref valueData_[p];
        }

        p = FindElement(i, j);
        if (p < 0)
        {
            p = InsertElement(i, j, 0.0);
        }

        return ref valueData_[p];
    }

    /// <summary>Takes the other matrix's sizes and storage, uBLAS's <c>assign_temporary</c> (a swap).</summary>
    /// <param name="m">The matrix whose storage is taken.</param>
    internal void AssignTemporary(CompressedMatrix m)
    {
        size1_ = m.size1_;
        size2_ = m.size2_;
        capacity_ = m.capacity_;
        filled1_ = m.filled1_;
        filled2_ = m.filled2_;
        index1Data_ = m.index1Data_;
        index2Data_ = m.index2Data_;
        valueData_ = m.valueData_;
    }

    private int RestrictCapacity(int nonZeros)
    {
        nonZeros = Max(nonZeros, Min(size1_, size2_));

        // Guarding against overflow - Thanks to Alexei Novakov for the hint.
        // non_zeros = (std::min) (non_zeros, size1_ * size2_);
        if (size1_ > 0 && nonZeros / size1_ >= size2_)
        {
            nonZeros = size1_ * size2_;
        }

        return nonZeros;
    }

    private void Reserve(int nonZeros)
    {
        capacity_ = RestrictCapacity(nonZeros);
        Array.Resize(ref index2Data_, capacity_);
        Array.Resize(ref valueData_, capacity_);
        filled2_ = Min(capacity_, filled2_);
    }

    private int InsertElement(int i, int j, double t)
    {
        if (filled2_ >= capacity_)
        {
            Reserve(2 * filled2_);
        }

        int element1 = i;
        int element2 = j;
        while (filled1_ <= element1 + 1)
        {
            index1Data_[filled1_] = filled2_;
            ++filled1_;
        }

        int itBegin = index1Data_[element1];
        int itEnd = index1Data_[element1 + 1];
        int n = LowerBound(itBegin, itEnd, element2);
        ++filled2_;
        Array.Copy(index2Data_, n, index2Data_, n + 1, filled2_ - 1 - n);
        index2Data_[n] = element2;
        Array.Copy(valueData_, n, valueData_, n + 1, filled2_ - 1 - n);
        valueData_[n] = t;
        while (element1 + 1 < filled1_)
        {
            ++index1Data_[element1 + 1];
            ++element1;
        }

        return n;
    }

    // The positions of row i's elements in the data arrays; none beyond the filled rows.
    private (int Begin, int End) RowRange(int i) =>
        i + 1 < filled1_ ? (index1Data_[i], index1Data_[i + 1]) : (0, 0);

    private int LowerBound(int first, int last, int value)
    {
        while (first < last)
        {
            int mid = first + ((last - first) / 2);
            if (index2Data_[mid] < value)
            {
                first = mid + 1;
            }
            else
            {
                last = mid;
            }
        }

        return first;
    }

    private static int Max(int a, int b) => a < b ? b : a;

    private static int Min(int a, int b) => b < a ? b : a;
}
