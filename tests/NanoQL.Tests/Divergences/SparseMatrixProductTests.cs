// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="SparseMatrixFunctions.Prod"/> on a matrix with more rows than columns. QuantLib's <c>prod</c> sizes its
/// result by the columns and writes it by the rows (<c>sparsematrix.hpp:51-69</c>), so such a matrix writes past the
/// result, and one with fewer rows pads it. nanoql sizes the result by the rows. QuantLib's
/// <c>testSparseMatrixMemory</c> asserts the column-sized result, <c>Array({0, 18, 0, 84})</c> for an 8×4 matrix
/// (<c>matrices.cpp:753-755</c>); the check moved here from <c>MatricesTests.cs</c>.
/// </summary>
public class SparseMatrixProductTests
{
    [Fact]
    public void TheProductHasARowsLength()
    {
        var m = new SparseMatrix(8, 4);
        m[3, 1] = 42;
        m[1, 2] = 6;

        Vector y = SparseMatrixFunctions.Prod(m, new Vector([1.0, 2.0, 3.0, 4.0]));

        // QuantLib's check: its own values in every row it computes, the first four (QuantLib: {0, 18, 0, 84})
        Assert.Equal([0.0, 18.0, 0.0, 84.0, 0.0, 0.0, 0.0, 0.0], y.ToArray());
    }
}
