// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's matrix exponential vectors, <c>tests/vectors/cxx/math/matrixutilities/expm.json</c>:
/// <see cref="ExpmFunctions.Expm"/> of four matrices at five times, negative among them, and two tolerances. The wheel
/// doesn't expose it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ExpmCxxVectorTests
{
    [Fact]
    public void ExpmCxxVector()
    {
        var file = VectorFile.Load("cxx/math/matrixutilities/expm");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Matrix m = MatrixVectorTests.FromRows(c.Input("matrix"));
            check.Output(c, "expm", MatrixVectorTests.Rows(ExpmFunctions.Expm(m, c.Double("t"), c.Double("tolerance"))));
        }

        check.Complete();
    }
}
