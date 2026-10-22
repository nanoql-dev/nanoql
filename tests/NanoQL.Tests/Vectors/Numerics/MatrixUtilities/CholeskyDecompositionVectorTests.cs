// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The CholeskyDecomposition vectors, <c>tests/vectors/math/matrixutilities/choleskydecomposition.json</c>:
/// <see cref="CholeskyDecompositionFunctions.CholeskyDecomposition"/>, strict and flexible, of
/// symmetricschurdecomposition.py's matrices, with <see cref="CholeskyDecompositionFunctions.CholeskySolveFor"/> on the
/// flexible one; and the refusals of a matrix that isn't square and of a right-hand side of another size.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CholeskyDecompositionVectorTests
{
    [Fact]
    public void CholeskyDecompositionVector()
    {
        var file = VectorFile.Load("math/matrixutilities/choleskydecomposition");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Matrix m = MatrixVectorTests.FromRows(c.Input("matrix"));
            var b = new Vector(Numbers(c.Input("b")));
            if (c.Expected.TryGetProperty("strict", out _))
            {
                check.Output(
                    c, "strict", () => MatrixVectorTests.Rows(CholeskyDecompositionFunctions.CholeskyDecomposition(m)));
            }

            if (c.Expected.TryGetProperty("flexible", out _))
            {
                check.Output(
                    c,
                    "flexible",
                    () => MatrixVectorTests.Rows(CholeskyDecompositionFunctions.CholeskyDecomposition(m, true)));
            }

            if (c.Expected.TryGetProperty("solve", out _))
            {
                check.Output(c, "solve", () => CholeskyDecompositionFunctions.CholeskySolveFor(
                    CholeskyDecompositionFunctions.CholeskyDecomposition(m, true), b).AsSpan().ToArray());
            }
        }

        check.Complete();
    }
}
