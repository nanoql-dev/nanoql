// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's sparse matrix vectors, <c>tests/vectors/cxx/math/matrixutilities/sparsematrix.json</c>:
/// <see cref="SparseMatrix"/>'s in-place sum, the first matrix's storage after it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SparseMatrixCxxVectorTests
{
    [Fact]
    public void SparseMatrixCxxVector()
    {
        var file = VectorFile.Load("cxx/math/matrixutilities/sparsematrix");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            int size1 = c.Int("size1"), size2 = c.Int("size2");
            SparseMatrix m = Built(size1, size2, c.Input("a"));
            m += Built(size1, size2, c.Input("b"));
            check.Output(c, "filled1", m.Filled1);
            check.Output(c, "index1", m.Index1Data.ToArray());
            check.Output(c, "index2", m.Index2Data.ToArray());
            check.Output(c, "values", m.ValueData.ToArray());
        }

        check.Complete();
    }

    // A matrix written element by element, in the given order.
    private static SparseMatrix Built(int size1, int size2, JsonElement elements)
    {
        var m = new SparseMatrix(size1, size2);
        foreach (JsonElement e in elements.EnumerateArray())
        {
            m[e[0].GetInt32(), e[1].GetInt32()] = e[2].GetDouble();
        }

        return m;
    }
}
