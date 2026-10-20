// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The systems the Krylov solvers' vectors write (<c>tools/vectors/gen/math/matrixutilities/bicgstab.py</c>): the
/// matrix's product, the preconditioner's, the right-hand side and the initial guess.
/// </summary>
internal static class KrylovInputs
{
    /// <summary>The case's matrix.</summary>
    /// <param name="c">The case.</param>
    /// <returns>The matrix.</returns>
    internal static Matrix Matrix(VectorCase c)
    {
        double[][] rows = [.. c.Input("A").EnumerateArray().Select(r => r.EnumerateArray().Select(e => e.GetDouble()).ToArray())];
        int n = rows.Length;
        var m = new Matrix(n, n);
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                m[i, j] = rows[i][j];
            }
        }

        return m;
    }

    /// <summary>The matrix's product with a vector.</summary>
    /// <param name="m">The matrix.</param>
    /// <returns>The product.</returns>
    internal static Func<Vector, Vector> Product(Matrix m) => x => m * x;

    /// <summary>The case's preconditioner: none, Jacobi's (the diagonal's inverse) or the matrix's inverse.</summary>
    /// <param name="c">The case.</param>
    /// <param name="m">The matrix.</param>
    /// <returns>The preconditioner's product, or <see langword="null"/>.</returns>
    internal static Func<Vector, Vector>? Preconditioner(VectorCase c, Matrix m)
    {
        switch (c.String("preconditioner"))
        {
            case "none":
                return null;
            case "jacobi":
            {
                double[] d = [.. Enumerable.Range(0, m.Rows).Select(i => m[i, i])];
                return x =>
                {
                    var y = new Vector(x.Size);
                    for (int i = 0; i < x.Size; i++)
                    {
                        y[i] = x[i] / d[i];
                    }

                    return y;
                };
            }

            default:
            {
                Matrix inverse = MatrixFunctions.Inverse(m);
                return x => inverse * x;
            }
        }
    }

    /// <summary>The right-hand side.</summary>
    /// <param name="c">The case.</param>
    /// <returns>The vector.</returns>
    internal static Vector B(VectorCase c) => new(c.Doubles("b"));

    /// <summary>The initial guess, or <see langword="null"/>.</summary>
    /// <param name="c">The case.</param>
    /// <returns>The vector, or <see langword="null"/>.</returns>
    internal static Vector? X0(VectorCase c) =>
        c.Input("x0").ValueKind == JsonValueKind.Null ? null : new Vector(c.Doubles("x0"));
}
