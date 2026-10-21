// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="SparseMatrix"/>'s in-place sum of a matrix that stores a nonzero element outside the target is refused,
/// with <see cref="ArgumentException"/> "element (i, j) outside the m x n matrix". uBLAS checks the sizes only in its
/// debug builds (<c>BOOST_UBLAS_CHECK</c>, <c>detail/matrix_assign.hpp:1057-1058</c>); under <c>NDEBUG</c>, which
/// QuantLib's builds use, <c>make_conformant</c> stores such an element in the target (<c>:147-148</c>). In a row
/// outside, its insertion writes row starts past the target's rows (<c>matrix_sparse.hpp:3070-3073</c>), a result C++
/// leaves undefined; in a column outside, it stores an element past the target's columns, which a product then reads
/// past its vector. An addend of other sizes whose elements all lie inside the target, a zero outside among them, is
/// uBLAS's defined path, kept.
/// </summary>
public class SparseMatrixPlusAssignTests
{
    [Fact]
    public void ANonzeroElementOutsideTheMatrixIsRefused()
    {
        var a = new SparseMatrix(2, 2);
        var b = new SparseMatrix(3, 2);
        b[2, 1] = 1.0;
        Assert.Throws<ArgumentException>(() => a += b);
        Assert.Equal(1, a.Filled1);
    }

    [Fact]
    public void AnAddendInsideTheMatrixIsAdded()
    {
        var a = new SparseMatrix(2, 2);
        a[0, 0] = 1.0;
        var b = new SparseMatrix(3, 3);
        b[1, 1] = 2.0;
        b[2, 2] = 0.0;
        a += b;
        Assert.Equal(1.0, a[0, 0]);
        Assert.Equal(2.0, a[1, 1]);
        Assert.Equal([0, 1, 2], a.Index1Data.ToArray());
    }
}
