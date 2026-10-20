// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's GMRES vectors, <c>tests/vectors/cxx/math/matrixutilities/gmres.json</c>: <see cref="GMRESResult"/>
/// whole, its relative residuals with the solution, by <see cref="GMRES.Solve"/> and
/// <see cref="GMRES.SolveWithRestart"/> (a case's <c>restart</c> null for the first), for the wheel file's systems.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GMRESCxxVectorTests
{
    [Fact]
    public void GMRESCxxVector()
    {
        var file = VectorFile.Load("cxx/math/matrixutilities/gmres");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Matrix m = KrylovInputs.Matrix(c);
            int? restart = c.Input("restart").ValueKind == JsonValueKind.Null ? null : c.Int("restart");
            check.Output(
                c,
                "result",
                (Func<object?>)(() =>
                {
                    var solver = new GMRES(
                        KrylovInputs.Product(m), c.Int("maxIter"), c.Double("relTol"), KrylovInputs.Preconditioner(c, m));
                    GMRESResult r = restart is int count
                        ? solver.SolveWithRestart(count, KrylovInputs.B(c), KrylovInputs.X0(c))
                        : solver.Solve(KrylovInputs.B(c), KrylovInputs.X0(c));
                    return new object[] { r.Errors.ToArray(), r.X.ToArray() };
                }));
        }

        check.Complete();
    }
}
