// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's SVD vectors, <c>tests/vectors/cxx/math/matrixutilities/svd.json</c>: <see cref="SVD"/>'s 2-norm,
/// condition number, numerical rank and least-squares solutions, which the wheel doesn't expose, on the wheel file's
/// matrices.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SVDCxxVectorTests
{
    [Fact]
    public void SVDCxxVector()
    {
        var file = VectorFile.Load("cxx/math/matrixutilities/svd");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var svd = new SVD(MatrixVectorTests.FromRows(c.Input("matrix")));
            check.Output(c, "norm2", svd.Norm2());
            check.Output(c, "cond", svd.Cond());
            check.Output(c, "rank", svd.Rank());
            check.Output(
                c,
                "solveFor",
                c.Input("b").EnumerateArray().Select(b => (object?)svd.SolveFor(new Vector(EquityInputs.Doubles(b))).AsSpan().ToArray()).ToArray());
        }

        check.Complete();
    }
}
