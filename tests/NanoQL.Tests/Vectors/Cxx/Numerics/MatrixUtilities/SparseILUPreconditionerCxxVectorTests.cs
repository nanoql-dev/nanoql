// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's incomplete LU vectors, <c>tests/vectors/cxx/math/matrixutilities/sparseilupreconditioner.json</c>:
/// <see cref="SparseILUPreconditioner"/>'s factors at fill levels 0 to 3 on banded and random sparse matrices and on
/// fdmlinearop.cpp's test matrix, and the preconditioner applied to two vectors each. The wheel doesn't expose it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SparseILUPreconditionerCxxVectorTests
{
    [Fact]
    public void SparseILUPreconditionerCxxVector()
    {
        var file = VectorFile.Load("cxx/math/matrixutilities/sparseilupreconditioner");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var ilu = new SparseILUPreconditioner(SparseOf(c.Input("matrix")), c.Int("lfil"));
            check.Output(c, "l", Rows(ilu.L()));
            check.Output(c, "u", Rows(ilu.U()));
            check.Output(
                c,
                "apply",
                c.Input("b").EnumerateArray().Select(b => (object?)ilu.Apply(new Vector(EquityInputs.Doubles(b))).ToArray()).ToArray());
        }

        check.Complete();
    }

    // a sparse matrix of the given rows, their zeros not stored, as the harness builds it
    private static SparseMatrix SparseOf(JsonElement rows)
    {
        double[][] r = [.. rows.EnumerateArray().Select(EquityInputs.Doubles)];
        var m = new SparseMatrix(r.Length, r.Length);
        for (int i = 0; i < r.Length; ++i)
        {
            for (int j = 0; j < r[i].Length; ++j)
            {
                if (r[i][j] != 0.0)
                {
                    m[i, j] = r[i][j];
                }
            }
        }

        return m;
    }

    private static object?[] Rows(SparseMatrix m) =>
        [.. Enumerable.Range(0, m.Size1).Select(i => (object?)Enumerable.Range(0, m.Size2).Select(j => m[i, j]).ToArray())];
}
