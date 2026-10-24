// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The SVD vectors, <c>tests/vectors/math/matrixutilities/svd.json</c>: <see cref="SVD"/>'s U, V, S and singular
/// values of matrices with as many or more rows than columns and with fewer, structured, rank-deficient and of
/// extreme elements among them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SVDVectorTests
{
    [Fact]
    public void SVDVector()
    {
        var file = VectorFile.Load("math/matrixutilities/svd");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var svd = new SVD(MatrixVectorTests.FromRows(c.Input("matrix")));
            check.Output(c, "U", MatrixVectorTests.Rows(svd.U()));
            check.Output(c, "V", MatrixVectorTests.Rows(svd.V()));
            check.Output(c, "S", MatrixVectorTests.Rows(svd.S()));
            check.Output(c, "singularValues", svd.SingularValues().AsSpan().ToArray());
        }

        check.Complete();
    }
}
