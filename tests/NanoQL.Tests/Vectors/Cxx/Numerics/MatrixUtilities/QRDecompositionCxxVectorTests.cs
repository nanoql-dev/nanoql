// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's QR vectors, <c>tests/vectors/cxx/math/matrixutilities/qrdecomposition.json</c>:
/// <see cref="QRDecompositionFunctions.QRDecomposition"/> of square, tall, wide and rank-deficient matrices with and
/// without pivoting (Q, R and the permutation), and <see cref="QRDecompositionFunctions.QRSolve"/> for two right-hand
/// sides each, with and without a diagonal. The wheel exposes neither.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class QRDecompositionCxxVectorTests
{
    [Fact]
    public void QRDecompositionCxxVector()
    {
        var file = VectorFile.Load("cxx/math/matrixutilities/qrdecomposition");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Matrix m = MatrixVectorTests.FromRows(c.Input("matrix"));
            bool pivot = c.Bool("pivot");
            QRDecompositionResult r = QRDecompositionFunctions.QRDecomposition(m, pivot);
            check.Output(c, "q", MatrixVectorTests.Rows(r.Q));
            check.Output(c, "r", MatrixVectorTests.Rows(r.R));
            check.Output(c, "ipvt", r.Ipvt.ToArray());
            check.Output(c, "x", c.Input("solves").EnumerateArray().Select(s => (object?)Solve(m, s, pivot)).ToArray());
        }

        check.Complete();
    }

    // the solve for a {b, d} spec, an empty d QuantLib's default
    private static double[] Solve(Matrix m, JsonElement solve, bool pivot)
    {
        double[] d = EquityInputs.Doubles(solve.GetProperty("d"));
        return QRDecompositionFunctions.QRSolve(
            m, new Vector(EquityInputs.Doubles(solve.GetProperty("b"))), pivot, d.Length == 0 ? null : new Vector(d))
            .ToArray();
    }
}
