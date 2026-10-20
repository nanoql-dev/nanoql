// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="GMRES"/>'s vectors, <c>tests/vectors/math/matrixutilities/gmres.json</c>: the solutions of seeded random
/// systems under three preconditioners and two tolerances, by <see cref="GMRES.Solve"/> and
/// <see cref="GMRES.SolveWithRestart"/>, a zero right-hand side, and refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GMRESVectorTests
{
    [Fact]
    public void GMRESVector()
    {
        var file = VectorFile.Load("math/matrixutilities/gmres");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Matrix m = KrylovInputs.Matrix(c);
            int? restart = c.Inputs.TryGetProperty("restart", out JsonElement r) ? r.GetInt32() : null;
            check.Output(
                c,
                "x",
                (Func<object?>)(() =>
                {
                    var solver = new GMRES(
                        KrylovInputs.Product(m), c.Int("maxIter"), c.Double("relTol"), KrylovInputs.Preconditioner(c, m));
                    GMRESResult result = restart is int count
                        ? solver.SolveWithRestart(count, KrylovInputs.B(c), KrylovInputs.X0(c))
                        : solver.Solve(KrylovInputs.B(c), KrylovInputs.X0(c));
                    return result.X.ToArray();
                }));
        }

        check.Complete();
    }
}
