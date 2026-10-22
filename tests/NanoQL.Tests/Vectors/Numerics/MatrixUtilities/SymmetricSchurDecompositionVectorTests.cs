// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The SymmetricSchurDecomposition vectors, <c>tests/vectors/math/matrixutilities/symmetricschurdecomposition.json</c>:
/// <see cref="SymmetricSchurDecomposition"/>'s eigenvalues and eigenvectors of symmetric matrices of 1 to 8 rows,
/// repeated eigenvalues and extreme elements among them; and the refusals of no rows and of a matrix that isn't
/// square.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SymmetricSchurDecompositionVectorTests
{
    [Fact]
    public void SymmetricSchurDecompositionVector()
    {
        var file = VectorFile.Load("math/matrixutilities/symmetricschurdecomposition");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Matrix m = MatrixVectorTests.FromRows(c.Input("matrix"));
            if (!c.Expected.TryGetProperty("eigenvectors", out _))
            {
                check.Output(c, "eigenvalues", () => new SymmetricSchurDecomposition(m).Eigenvalues().AsSpan().ToArray());
                continue;
            }

            var d = new SymmetricSchurDecomposition(m);
            check.Output(c, "eigenvalues", d.Eigenvalues().AsSpan().ToArray());
            check.Output(c, "eigenvectors", MatrixVectorTests.Rows(d.Eigenvectors()));
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"math/matrixutilities/symmetricschurdecomposition: bit-identical {check.BitIdentical} of {check.Reals} real outputs");
        check.Complete();
    }
}
