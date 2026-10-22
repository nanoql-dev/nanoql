// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A NaN eigenvalue is refused before the eigenvalues are sorted (playbook §2's undefined-path row): QuantLib sorts
/// the (eigenvalue, eigenvector) pairs with <c>std::sort</c> and <c>std::greater</c> (symmetricschurdecomposition.cpp:116-124),
/// whose ordering a NaN breaks. A NaN on a diagonal whose other elements are 0 ends the Jacobi loop at once and reaches
/// the sort; a matrix of one row sorts nothing, and keeps QuantLib's NaN.
/// </summary>
public class SymmetricSchurNaNSortTests
{
    [Fact]
    public void ANaNEigenvalueCantBeSorted()
    {
        var m = new Matrix(new[,] { { double.NaN, 0.0 }, { 0.0, 1.0 } });
        NanoQLException e = Assert.Throws<NanoQLException>(() => new SymmetricSchurDecomposition(m));
        Assert.Equal("NaN eigenvalue not allowed in a sort", e.Message);

        Assert.True(double.IsNaN(new SymmetricSchurDecomposition(new Matrix(1, 1, double.NaN)).Eigenvalues()[0]));
    }
}
