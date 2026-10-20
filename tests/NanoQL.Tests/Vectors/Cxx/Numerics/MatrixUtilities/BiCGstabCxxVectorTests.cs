// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's BiCGstab vectors, <c>tests/vectors/cxx/math/matrixutilities/bicgstab.json</c>:
/// <see cref="BiCGStabResult"/> whole, its iteration count and error with the solution, for the wheel file's systems.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BiCGstabCxxVectorTests
{
    [Fact]
    public void BiCGstabCxxVector()
    {
        var file = VectorFile.Load("cxx/math/matrixutilities/bicgstab");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Matrix m = KrylovInputs.Matrix(c);
            check.Output(
                c,
                "result",
                (Func<object?>)(() =>
                {
                    var solver = new BiCGstab(
                        KrylovInputs.Product(m), c.Int("maxIter"), c.Double("relTol"), KrylovInputs.Preconditioner(c, m));
                    BiCGStabResult r = solver.Solve(KrylovInputs.B(c), KrylovInputs.X0(c));
                    return new object[] { r.Iterations, r.Error, r.X.ToArray() };
                }));
        }

        check.Complete();
    }
}
