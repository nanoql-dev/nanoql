// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's TQR vectors, <c>tests/vectors/cxx/math/matrixutilities/tqreigendecomposition.json</c>:
/// <see cref="TqrEigenDecomposition"/>'s eigenvalues, eigenvectors (by rows) and iteration count under each eigenvector
/// calculation and shift strategy, and its refusal of mismatched sizes.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TqrEigenDecompositionCxxVectorTests
{
    [Fact]
    public void TqrEigenDecompositionCxxVector()
    {
        var file = VectorFile.Load("cxx/math/matrixutilities/tqreigendecomposition");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] diag = c.Doubles("diag"), sub = c.Doubles("sub");
            int[] calc = c.Ints("calc"), strategy = c.Ints("strategy");
            object?[] results = [.. Enumerable.Range(0, calc.Length).Select(i => (object?)(Func<object?>)(() =>
            {
                var tqr = new TqrEigenDecomposition(
                    new Vector(diag),
                    new Vector(sub),
                    (TqrEigenDecompositionEigenVectorCalculation)calc[i],
                    (TqrEigenDecompositionShiftStrategy)strategy[i]);
                Matrix ev = tqr.Eigenvectors();
                double[][] rows = [.. Enumerable.Range(0, ev.Rows).Select(r =>
                    Enumerable.Range(0, ev.Columns).Select(j => ev[r, j]).ToArray())];
                return new object[] { tqr.Eigenvalues().ToArray(), rows, tqr.Iterations };
            }))];
            check.Output(c, "result", results);
        }

        check.Complete();
    }
}
