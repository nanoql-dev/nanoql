// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="BiCGstab"/>'s vectors, <c>tests/vectors/math/matrixutilities/bicgstab.json</c>: the solutions of seeded
/// random systems under three preconditioners and two tolerances, a zero right-hand side, and refusals with too few
/// iterations.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BiCGstabVectorTests
{
    [Fact]
    public void BiCGstabVector()
    {
        var file = VectorFile.Load("math/matrixutilities/bicgstab");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Matrix m = KrylovInputs.Matrix(c);
            var solver = new BiCGstab(
                KrylovInputs.Product(m), c.Int("maxIter"), c.Double("relTol"), KrylovInputs.Preconditioner(c, m));
            check.Output(c, "x", (Func<object?>)(() => solver.Solve(KrylovInputs.B(c), KrylovInputs.X0(c)).X.ToArray()));
        }

        check.Complete();
    }
}
