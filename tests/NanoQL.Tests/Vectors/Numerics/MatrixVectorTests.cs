// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Matrix vectors, <c>tests/vectors/math/matrix.json</c>: <see cref="Matrix"/>'s sum, difference and products,
/// the transpose, the outer product, the inverse, the vector products, the dimensions, the text and the fill
/// constructor, with the refusals of operands of the wrong size, on 20 cases of 1 to 6 rows and columns; and 40
/// square matrices through <see cref="MatrixFunctions.Inverse"/>, whose bit-identical outputs it prints.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MatrixVectorTests
{
    [Fact]
    public void MatrixVector()
    {
        var file = VectorFile.Load("math/matrix");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Name.StartsWith("inverse ", StringComparison.Ordinal))
            {
                Matrix m = FromRows(c.Input("m"));
                check.Output(c, "inverse", () => Rows(MatrixFunctions.Inverse(m)));
                continue;
            }

            Matrix a = FromRows(c.Input("a")), b = FromRows(c.Input("b")), product = FromRows(c.Input("c"));
            Matrix q = FromRows(c.Input("q")), wrong = FromRows(c.Input("wrong"));
            Matrix wrongProduct = FromRows(c.Input("wrongProduct"));
            Vector v = new(Numbers(c.Input("v"))), w = new(Numbers(c.Input("w")));
            double s = c.Double("s");
            check.Output(c, "sum", Rows(a + b));
            check.Output(c, "difference", Rows(a - b));
            check.Output(c, "product", Rows(a * product));
            check.Output(c, "scaled", Rows(a * s));
            check.Output(c, "scaledLeft", Rows(s * a));
            check.Output(c, "transpose", Rows(MatrixFunctions.Transpose(a)));
            check.Output(c, "outerProduct", Rows(MatrixFunctions.OuterProduct(v, w)));
            check.Output(c, "inverse", () => Rows(MatrixFunctions.Inverse(q)));
            check.Output(c, "vectorTimesMatrix", v * a);
            check.Output(c, "matrixTimesVector", a * w);
            check.Output(c, "rows", a.Rows);
            check.Output(c, "columns", a.Columns);
            check.Output(c, "text", a.ToString());
            check.Output(c, "filled", Rows(new Matrix(a.Rows, a.Columns, s)));
            check.Output(c, "sumWrongSize", () => a + wrong);
            check.Output(c, "differenceWrongSize", () => a - wrong);
            check.Output(c, "productWrongSize", () => a * wrongProduct);

            // The wrong-size vectors: Array(rows + 1, 1.0) and Array(columns + 1, 1.0).
            check.Output(c, "vectorTimesMatrixWrongSize", () => new Vector(a.Rows + 1, 1.0) * a);
            check.Output(c, "matrixTimesVectorWrongSize", () => a * new Vector(a.Columns + 1, 1.0));
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"math/matrix: bit-identical {check.BitIdentical} of {check.Reals} real outputs");
        check.Complete();
    }

    // A matrix from the vectors' list of rows, through the rectangular-array constructor.
    internal static Matrix FromRows(JsonElement rows)
    {
        double[][] r = [.. rows.EnumerateArray().Select(Numbers)];
        int columns = r.Length == 0 ? 0 : r[0].Length;
        var data = new double[r.Length, columns];
        for (int i = 0; i < r.Length; ++i)
        {
            for (int j = 0; j < columns; ++j)
            {
                data[i, j] = r[i][j];
            }
        }

        return new Matrix(data);
    }

    // A matrix from the spec {"rows", "columns", "data"}, its elements by rows, through the range constructor.
    internal static Matrix FromSpec(JsonElement spec) =>
        new(spec.GetProperty("rows").GetInt32(), spec.GetProperty("columns").GetInt32(), Numbers(spec.GetProperty("data")));

    // A matrix as the vectors write one: its list of rows.
    internal static object?[] Rows(Matrix m) => [.. Enumerable.Range(0, m.Rows).Select(i => (object?)m.Row(i).ToArray())];
}
